public sealed class MemoryResource
{
    public string Name { get; set; } = string.Empty;

    public IntPtr Address { get; set; }

    public int Length { get; set; }

    public string Type { get; set; } = string.Empty;

    public List<nint> Offsets { get; set; } = new();

    public bool IsPointerChain { get; set; }

    public bool IsDynamic { get; set; }

    public string? DynamicBase { get; set; }
}
