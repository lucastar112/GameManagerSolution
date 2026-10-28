using System.Globalization;
using System.Text.Json;

namespace gameManager.src.Memory
{
    public sealed class DynamicResourceManager
    {
        private readonly string _cacheFile;
        private readonly object _cacheLock = new();

        private DynamicCache? _cache;

        public DynamicResourceManager(string cacheFile)
        {
            _cacheFile = cacheFile;
        }

        public void Load()
        {
            Reload();
        }

        public void Reload()
        {
            string fullPath = Path.GetFullPath(_cacheFile);

            Console.WriteLine(
                $"[DynamicResourceManager] Reloading cache: {fullPath}");

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "Dynamic cache file was not found.",
                    fullPath);
            }

            // Deserialize into a temporary object first.
            // The active cache remains untouched if this fails.
            string json = File.ReadAllText(fullPath);

            DynamicCache newCache =
                JsonSerializer.Deserialize<DynamicCache>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize dynamic cache '{fullPath}'.");

            // Replace the active cache atomically.
            lock (_cacheLock)
            {
                _cache = newCache;
            }

            Console.WriteLine(
                "[DynamicResourceManager] Cache reloaded successfully.");
        }

        public bool TryResolve(
            string dynamicBase,
            int playerId,
            out IntPtr address)
        {
            address = IntPtr.Zero;

            Console.WriteLine(
                $"Resolving dynamic base '{dynamicBase}' for player {playerId}.");

            string? addressString;

            // Read from a consistent cache snapshot.
            lock (_cacheLock)
            {
                if (_cache is null)
                {
                    Console.WriteLine("Dynamic cache is not loaded.");
                    return false;
                }

                if (!_cache.Resources.TryGetValue(
                        dynamicBase,
                        out DynamicResourceCache? resource))
                {
                    Console.WriteLine(
                        $"Dynamic resource '{dynamicBase}' was not found in cache.");
                    return false;
                }

                if (!resource.Players.TryGetValue(
                        playerId,
                        out addressString))
                {
                    Console.WriteLine(
                        $"Player {playerId} was not found in dynamic resource '{dynamicBase}'.");
                    return false;
                }
            }

            Console.WriteLine(
                $"Dynamic base resolved: player {playerId} -> {addressString}");

            if (!TryParseAddress(addressString, out address))
            {
                Console.WriteLine(
                    $"Invalid dynamic address '{addressString}'.");
                return false;
            }

            Console.WriteLine(
                $"Dynamic address parsed: 0x{address.ToInt64():X8}");

            return true;
        }

        private static bool TryParseAddress(
            string value,
            out IntPtr address)
        {
            address = IntPtr.Zero;
            value = value.Trim();

            if (value.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                value = value[2..];
            }

            if (!long.TryParse(
                    value,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out long result))
            {
                return false;
            }

            address = new IntPtr(result);
            return true;
        }
    }
}