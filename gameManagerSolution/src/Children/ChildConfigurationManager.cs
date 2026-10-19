using System.Text.Json;

namespace gameManager.src.Children
{
    public sealed class ChildConfigurationManager
    {
        private readonly ChildConfiguration _configuration;

        /// <summary>
        /// Load childConfiguration file
        /// </summary>
        public ChildConfigurationManager(string filePath)
        {
            if (!File.Exists(filePath)) 
                throw new FileNotFoundException("Child configuration file was not found.", filePath);

            string json = File.ReadAllText(filePath);
            _configuration = JsonSerializer.Deserialize<ChildConfiguration>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive = true})
                ?? throw new InvalidOperationException("Failed to deserialize child configuration.");
        }

        /// <summary>
        /// Return's the list of child processes
        /// </summary>
        public IReadOnlyList<ChildConfiguration> GetChildren()
        {
            return _configuration.Children;
        }
    }
}