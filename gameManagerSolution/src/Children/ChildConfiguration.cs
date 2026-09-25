namespace gameManager.src.Children
{
    public sealed class ChildConfiguration
    {
        public string Name { get; set; } = string.Empty;

        public bool Enabled { get; set; }

        public string Path { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;
        public List<ChildConfiguration> Children { get; set; } = new();
        public bool UseIpc { get; set; }
    }
}