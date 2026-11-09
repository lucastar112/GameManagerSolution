using System.Buffers.Binary;
using GameManager.Tools.TrackFormats;
using Xunit;

namespace GameManager.Tools.Tests;

public sealed class BasicParsingTests
{
    [Fact]
    public void KmpReader_ParsesValidEmptySection()
    {
        byte[] kmp = MakeKmpWithOneEmptySection();
        KmpDocument doc = KmpFileReader.Read(kmp);
        Assert.Equal((uint)kmp.Length, doc.FileLength);
        Assert.Equal((ushort)0x14, doc.HeaderLength);
        Assert.Single(doc.Sections);
        Assert.Equal("KTPT", doc.Sections[0].Magic);
        Assert.Empty(doc.Sections[0].Entries);
        Assert.Empty(KmpCheckpointDecoder.Decode(doc).Checkpoints);
    }

    [Fact]
    public void KmpReader_RejectsWrongMagic()
    {
        byte[] kmp = MakeKmpWithOneEmptySection();
        kmp[0] = 0;
        Assert.Throws<InvalidDataException>(() => KmpFileReader.Read(kmp));
    }

    [Fact]
    public void BrresReader_RejectsInvalidHeaderInAllModes()
    {
        foreach (BrresImportMode mode in Enum.GetValues<BrresImportMode>())
            Assert.Throws<InvalidDataException>(() => BrresModelReader.Decode(new byte[64], mode));
    }

    [Fact]
    public void SzsReader_RejectsInvalidArchive()
    {
        Assert.Throws<InvalidDataException>(() => SzsArchiveReader.Open(new byte[64]));
    }

    [Fact]
    public void OptionalSchoolProjectSample_CanLoadRealTrack()
    {
        // Set this environment variable to an existing, locally owned .szs.
        // The test deliberately does not bundle or distribute Nintendo assets.
        string? sample = Environment.GetEnvironmentVariable("GAMEMANAGER_SAMPLE_SZS");
        if (string.IsNullOrEmpty(sample) || !File.Exists(sample)) return;
        CourseData course = CourseDataLoader.OpenFile(sample, BrresImportMode.GeometryOnly);
        Assert.NotEmpty(course.Kmp.Sections);
        Assert.NotNull(course.Model);
        Assert.True(course.Model!.Geometry.TriangleCount > 0);
        Assert.Empty(course.Model.TexturePixels);
        Assert.Empty(course.Model.TevShaders);
    }

    private static byte[] MakeKmpWithOneEmptySection()
    {
        byte[] data = new byte[0x1C];
        "RKMD"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4, 4), (uint)data.Length);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(8, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(10, 2), 0x14);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12, 4), 0x9D8);
        "KTPT"u8.CopyTo(data.AsSpan(0x14));
        return data;
    }
}
