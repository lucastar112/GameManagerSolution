using System.Text.Json;

namespace gameManager.src.Memory
{
    public sealed class MemoryResourceManager
    {
        private readonly Dictionary<string, MemoryResource> _resources;

        public MemoryResourceManager(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Memory resource file was not found.",
                    filePath);
            }

            string json = File.ReadAllText(filePath);

            _resources = LoadResources(json);
        }

        public MemoryResource Get(string name)
        {
            if (!_resources.TryGetValue(name, out MemoryResource? resource))
            {
                throw new KeyNotFoundException(
                    $"Memory resource '{name}' was not found.");
            }

            return resource;
        }

        public bool TryGet(
            string name,
            out MemoryResource? resource)
        {
            return _resources.TryGetValue(name, out resource);
        }

        private static Dictionary<string, MemoryResource> LoadResources(
            string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                "resources",
                out JsonElement resourcesElement))
            {
                throw new InvalidDataException(
                    "Memory resource file does not contain a 'resources' object.");
            }

            Dictionary<string, MemoryResource> resources = new();

            foreach (JsonProperty property in resourcesElement.EnumerateObject())
            {
                string name = property.Name;
                JsonElement definition = property.Value;

                if (!definition.TryGetProperty(
                    "address",
                    out JsonElement addressElement))
                {
                    throw new InvalidDataException(
                        $"Resource '{name}' does not contain an address.");
                }

                string addressString =
                    addressElement.GetString()
                    ?? throw new InvalidDataException(
                        $"Resource '{name}' has an invalid address.");

                IntPtr address = ParseAddress(addressString);

                string type =
                    definition.TryGetProperty(
                        "type",
                        out JsonElement typeElement)
                        ? typeElement.GetString() ?? string.Empty
                        : string.Empty;

                int length =
                    definition.TryGetProperty(
                        "length",
                        out JsonElement lengthElement)
                        ? lengthElement.GetInt32()
                        : 0;

                List<IntPtr> offsets = new();

                if (definition.TryGetProperty(
                    "offsets",
                    out JsonElement offsetsElement))
                {
                    foreach (JsonElement offsetElement
                        in offsetsElement.EnumerateArray())
                    {
                        string offsetString =
                            offsetElement.GetString()
                            ?? throw new InvalidDataException(
                                $"Resource '{name}' contains an invalid offset.");

                        offsets.Add(ParseAddress(offsetString));
                    }
                }

                resources.Add(
                    name,
                    new MemoryResource
                    {
                        Name = name,
                        Address = address,
                        Type = type,
                        Length = length,
                        Offsets = offsets
                    });
            }

            return resources;
        }

        private static IntPtr ParseAddress(string value)
        {
            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value[2..];
            }

            if (!long.TryParse(
                value,
                System.Globalization.NumberStyles.HexNumber,
                null,
                out long result))
            {
                throw new InvalidDataException(
                    $"Invalid hexadecimal address: '{value}'.");
            }

            return new IntPtr(result);
        }
    }
}