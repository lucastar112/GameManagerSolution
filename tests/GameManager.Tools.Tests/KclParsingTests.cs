using System.Buffers.Binary;
using Vector3 = System.Numerics.Vector3;
using GameManager.Tools.TrackFormats;
using Xunit;

namespace GameManager.Tools.Tests;

public sealed class KclParsingTests
{
    [Fact]
    public void ParsesHeaderVectorsPrismsFlagsAndOctree()
    {
        KclDocument doc = KclFileReader.Read(MakeMinimalKcl());
        Assert.Single(doc.Positions);
        Assert.Equal(5, doc.Normals.Count);
        Assert.Single(doc.Prisms);
        Assert.Equal(1, doc.Prisms[0].Id);
        Assert.Equal((ushort)0x1c63, doc.Prisms[0].Collision.Raw);
        Assert.Equal(3, doc.Prisms[0].Collision.Type);
        Assert.Equal(3, doc.Prisms[0].Collision.Variant);
        Assert.Equal(4, doc.Prisms[0].Collision.BlightIndex);
        Assert.Equal(3, doc.Prisms[0].Collision.WheelDepth);
        Assert.Equal(0, doc.Prisms[0].Collision.Effects);
        Assert.Equal(1, doc.Octree.RootEntryCount);
        Assert.Equal(new[] { 1 }, doc.Octree.ReferencedPrismIds);
        Assert.Empty(doc.Octree.InvalidReferencedPrismIds);
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void ReconstructsTriangleFromPrismNormals()
    {
        KclDocument doc = KclFileReader.Read(MakeMinimalKcl());
        var triangle = Assert.Single(KclTriangleDecoder.Decode(doc).Triangles);
        AssertVectorNear(new Vector3(0, 0, 0), triangle.A);
        AssertVectorNear(new Vector3(10, 0, 0), triangle.B);
        AssertVectorNear(new Vector3(0, 0, 10), triangle.C);
    }

    [Fact]
    public void SupportsLegacyHeaderWithoutSphereRadius()
    {
        byte[] withRadius = MakeMinimalKcl();
        byte[] withoutRadius = new byte[withRadius.Length - 4];
        Array.Copy(withRadius, 0, withoutRadius, 0, 0x38);
        Array.Copy(withRadius, 0x3c, withoutRadius, 0x38, withRadius.Length - 0x3c);
        BinaryPrimitives.WriteUInt32BigEndian(withoutRadius.AsSpan(0, 4), 0x38);
        BinaryPrimitives.WriteUInt32BigEndian(withoutRadius.AsSpan(4, 4), 0x44);
        BinaryPrimitives.WriteUInt32BigEndian(withoutRadius.AsSpan(8, 4), 0x70);
        BinaryPrimitives.WriteUInt32BigEndian(withoutRadius.AsSpan(12, 4), 0x90);
        KclDocument doc = KclFileReader.Read(withoutRadius);
        Assert.Null(doc.Header.SphereRadius);
        Assert.Single(doc.Prisms);
        Assert.Equal(new[] { 1 }, doc.Octree.ReferencedPrismIds);
    }

    [Fact]
    public void MalformedLeafReferenceIsReportedNotUsedAsAPrism()
    {
        byte[] data = MakeMinimalKcl();
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(0x9a, 2), 0xffff);
        KclDocument doc = KclFileReader.Read(data);
        Assert.Empty(doc.Octree.ReferencedPrismIds);
        Assert.Equal(new[] { 65535 }, doc.Octree.InvalidReferencedPrismIds);
        Assert.NotEmpty(doc.Warnings);
    }

    [Fact]
    public void RejectsTruncatedAndInvalidSectionOffsets()
    {
        Assert.Throws<InvalidDataException>(() => KclFileReader.Read(new byte[0x37]));
        byte[] bytes = MakeMinimalKcl();
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12, 4), 0x7fffffff);
        Assert.Throws<InvalidDataException>(() => KclFileReader.Read(bytes));
    }

    [Fact]
    public void FlagsPreserveEntire16BitValue()
    {
        var flag = new KclCollisionFlag(0xfca3);
        Assert.Equal((ushort)0xfca3, flag.Raw);
        Assert.Equal(3, flag.Type);
        Assert.Equal((0xfca3 >> 5) & 7, flag.Variant);
        Assert.True(flag.SoftWallBit);
    }

    [Fact]
    public void OptionalRealCourse_DecodesKclWhenProvided()
    {
        string? sample = Environment.GetEnvironmentVariable("GAMEMANAGER_SAMPLE_SZS");
        if (string.IsNullOrWhiteSpace(sample) || !File.Exists(sample)) return;
        SzsArchiveReader archive = SzsArchiveReader.Open(File.ReadAllBytes(sample));
        if (!archive.ContainsFile("course.kcl")) return;
        KclDocument kcl = KclFileReader.Read(archive.ReadFile("course.kcl"));
        Assert.NotEmpty(kcl.Prisms);
        Assert.NotEmpty(kcl.Octree.ReferencedPrismIds);
        Assert.NotEmpty(KclTriangleDecoder.Decode(kcl).Triangles);
        CourseData full = CourseDataLoader.OpenFile(sample, BrresImportMode.GeometryOnly);
        Assert.Equal(kcl.PrismCount, full.Kcl?.PrismCount);
    }

    private static void AssertVectorNear(Vector3 expected, GameManager.Tools.TrackFormats.Vector3 actual)
    {
        float dx = expected.X - actual.X;
        float dy = expected.Y - actual.Y;
        float dz = expected.Z - actual.Z;
        Assert.InRange(MathF.Sqrt(dx * dx + dy * dy + dz * dz), 0f, 0.001f);
    }

    private static byte[] MakeMinimalKcl()
    {
        // 0x3c header, one position, five normals, one 0x10-byte prism, tiny octree.
        byte[] bytes = new byte[0x9e];
        Write32(0, 0x3c); Write32(4, 0x48);
        Write32(8, 0x74); // Prism offset is stored 0x10 BEFORE the first prism.
        Write32(12, 0x94);
        WriteFloat(0x10, 30);
        WriteFloat(0x38, 25);
        Write32(0x20, 0xfffffffe); Write32(0x24, 0xfffffffe); Write32(0x28, 0xfffffffe);
        Write32(0x2c, 1);
        WriteVector(0x3c, new Vector3(0, 0, 0));
        WriteVector(0x48, new Vector3(0, -1, 0));
        WriteVector(0x54, new Vector3(-1, 0, 0));
        WriteVector(0x60, new Vector3(0, 0, -1));
        WriteVector(0x6c, Vector3.Normalize(new Vector3(1, 0, 1)));
        WriteVector(0x78, Vector3.Zero); // unused fifth normal
        WriteFloat(0x84, 10 / MathF.Sqrt(2));
        Write16(0x88, 0); Write16(0x8a, 0); Write16(0x8c, 1);
        Write16(0x8e, 2); Write16(0x90, 3); Write16(0x92, 0x1c63);
        Write32(0x94, 0x80000004); // Root leaf pointer, relative to current table.
        Write16(0x98, 0); Write16(0x9a, 1); Write16(0x9c, 0);
        return bytes;
        void Write32(int off, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(off, 4), value);
        void Write16(int off, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(off, 2), value);
        void WriteFloat(int off, float value) => Write32(off, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
        void WriteVector(int off, Vector3 v)
        {
            WriteFloat(off, v.X); WriteFloat(off + 4, v.Y); WriteFloat(off + 8, v.Z);
        }
    }
}
