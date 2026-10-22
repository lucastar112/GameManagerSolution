namespace gameManager.src.Memory
{
    public sealed class DynamicCache
    {
        public int Version { get; set; }

        public Dictionary<string, DynamicResourceCache> Resources { get; set; } = new();
    }

    public sealed class DynamicResourceCache
    {
        public Dictionary<int, string> Players { get; set; } = new();
    }
}