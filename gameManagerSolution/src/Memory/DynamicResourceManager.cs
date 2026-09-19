using System.Globalization;
using System.Text.Json;

namespace gameManager.src.Memory
{
    public sealed class DynamicResourceManager
    {
        private readonly string _cacheFile;

        private DynamicCache? _cache;

        public DynamicResourceManager(string cacheFile)
        {
            _cacheFile = cacheFile;
        }

        public void Load()
        {
            if (!File.Exists(_cacheFile))
            {
                _cache = null;
                return;
            }

            string json = File.ReadAllText(_cacheFile);
            _cache = JsonSerializer.Deserialize<DynamicCache>(json, new JsonSerializerOptions {PropertyNameCaseInsensitive = true}) ?? throw new InvalidOperationException($"Failed to deserialize dynamic cache '{_cacheFile}'.");
        }

        public bool TryResolve(string dynamicBase, int playerId, out IntPtr address)
        {
            address = IntPtr.Zero;

            Console.WriteLine($"Resolving dynamic base '{dynamicBase}' for player {playerId}.");

            if (_cache is null)
            {
                Console.WriteLine("Dynamic cache is not loaded.");
                return false;
            }

            if (!_cache.Resources.TryGetValue(dynamicBase, out DynamicResourceCache? resource))
            {
                Console.WriteLine($"Dynamic resource '{dynamicBase}' was not found in cache.");
                return false;
            }

            if (!resource.Players.TryGetValue(playerId, out string? addressString))
            {
                Console.WriteLine($"Player {playerId} was not found in dynamic resource '{dynamicBase}'.");
                return false;
            }

            Console.WriteLine($"Dynamic base resolved: player {playerId} -> {addressString}");

            if (!TryParseAddress(addressString, out address))
            {
                Console.WriteLine($"Invalid dynamic address '{addressString}'.");
                return false;
            }

            Console.WriteLine($"Dynamic address parsed: 0x{address.ToInt64():X8}");

            return true;
        }

        private static bool TryParseAddress(string value, out IntPtr address)
        {
            address = IntPtr.Zero;
            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value[2..];

            if (!long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long result))
                return false;

            address = new IntPtr(result);
            return true;
        }

        public void SetPlayerAddress(string resourceName, int playerId, IntPtr address)
        {
            if (_cache is null)
                _cache = new DynamicCache();

            if (!_cache.Resources.TryGetValue(resourceName, out DynamicResourceCache? resource))
            {
                resource = new DynamicResourceCache();

                _cache.Resources.Add(resourceName, resource);
            }

            resource.Players[playerId] = $"0x{address.ToInt64():X8}";
        }

        public void Save()
        {
            if (_cache is null)
                throw new InvalidOperationException("Dynamic cache is not initialized.");

            string? directory = Path.GetDirectoryName(_cacheFile);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions{WriteIndented = true});

            File.WriteAllText(_cacheFile, json);

            Console.WriteLine($"Dynamic cache saved: {_cacheFile}");
        }

        public void Clear()
        {
            _cache = new DynamicCache{Version = 1};
        }
    }
}