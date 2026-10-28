using System.Text.Json;

namespace gameManager.src.Memory
{
    public sealed class MemoryResourceManager
    {
        private readonly Dictionary<string, MemoryResource> _resources;


        public MemoryResourceManager(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Memory resource file was not found.", filePath);

            string json = File.ReadAllText(filePath);
            _resources = LoadResources(json);
        }

        public MemoryResource Get(string name)
        {
            if (!_resources.TryGetValue(name, out MemoryResource? resource))
                throw new KeyNotFoundException($"Memory resource '{name}' was not found.");

            return resource;
        }

        private static Dictionary<string, MemoryResource> LoadResources(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("resources", out JsonElement resourcesElement))
                throw new InvalidDataException("Memory resource file does not contain a 'resources' object.");

            Dictionary<string, MemoryResource> resources = new();

            foreach (JsonProperty property in resourcesElement.EnumerateObject())
            {
                string name = property.Name;
                JsonElement definition = property.Value;

                // ------------------------------------------------------------
                // Dynamic
                // ------------------------------------------------------------

                bool isDynamic = false;

                if (definition.TryGetProperty("dynamic", out JsonElement dynamicElement))
                    isDynamic = dynamicElement.GetBoolean();

                // ------------------------------------------------------------
                // Address
                // ------------------------------------------------------------

                IntPtr address = IntPtr.Zero;

                if (definition.TryGetProperty("address", out JsonElement addressElement))
                {
                    string addressString = addressElement.GetString() ?? throw new InvalidDataException($"Resource '{name}' has an invalid address.");
                    address = ParseAddress(addressString);
                }

                // Static resources must have an address.
                // Dynamic resources get their address from cache.json.
                if (!isDynamic && address == IntPtr.Zero)
                    throw new InvalidDataException($"Resource '{name}' does not contain an address.");



                // ------------------------------------------------------------
                // Type
                // ------------------------------------------------------------

                string type = definition.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;

                // ------------------------------------------------------------
                // Length
                // ------------------------------------------------------------

                int length = definition.TryGetProperty("length", out JsonElement lengthElement) ? lengthElement.GetInt32() : 0;

                // ------------------------------------------------------------
                // Offsets
                // ------------------------------------------------------------

                List<IntPtr> offsets = new();

                if (definition.TryGetProperty("offsets", out JsonElement offsetsElement))
                {
                    foreach (JsonElement offsetElement in offsetsElement.EnumerateArray())
                    {
                        string offsetString = offsetElement.GetString() ?? throw new InvalidDataException($"Resource '{name}' contains an invalid offset.");
                        offsets.Add(ParseAddress(offsetString));
                    }
                }

                // ------------------------------------------------------------
                // Dynamic base
                // ------------------------------------------------------------

                string? dynamicBase = null;

                if (definition.TryGetProperty("dynamicBase", out JsonElement dynamicBaseElement))
                    dynamicBase = dynamicBaseElement.GetString();

                if (isDynamic && string.IsNullOrWhiteSpace(dynamicBase))
                    throw new InvalidDataException($"Dynamic resource '{name}' does not contain a 'dynamicBase'.");

                // ------------------------------------------------------------
                // Availability
                // ------------------------------------------------------------

                ResourceAvailability availability = ResourceAvailability.Always;

                if (definition.TryGetProperty("availability", out JsonElement availabilityElement))
                {
                    byte availabilityValue = availabilityElement.GetByte();

                    if (!Enum.IsDefined(typeof(ResourceAvailability), availabilityValue))
                        throw new InvalidDataException($"Resource '{name}' has an invalid availability value: {availabilityValue}.");

                    availability = (ResourceAvailability)availabilityValue;
                }

                // ------------------------------------------------------------
                // Create resource
                // ------------------------------------------------------------

                MemoryResource resource = new()
                {
                    Name = name,
                    Address = address,
                    Type = type,
                    Length = length,
                    Offsets = offsets,
                    IsDynamic = isDynamic,
                    DynamicBase = dynamicBase,
                    Availability = availability
                };

                resources.Add(name, resource);
            }

            return resources;
        }

        private static IntPtr ParseAddress(string value)
        {
            value = value.Trim();

            bool negative = false;

            if (value.StartsWith("-"))
            {
                negative = true;
                value = value[1..];
            }
            else if (value.StartsWith("+"))
                value = value[1..];

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value[2..];

            if (!long.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long result))
                throw new InvalidDataException($"Invalid hexadecimal address or offset: '{(negative ? "-" : "")}{value}'.");

            if (negative)
                result = -result;

            return new IntPtr(result);
        }
    }
}