using gameManager.src.Dolphin;
using gameManager.src.Memory;
using GameManager.Shared.Ipc;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace gameManager.src.Api
{
    public sealed class GameManagerApi
    {
        private readonly DolphinConnection _dolphin;
        private readonly DynamicResourceManager _dynamicResources;
        private readonly MemoryResourceManager _resources;

        /// <summary>
        /// Stores dolphin data
        /// </summary>
        public GameManagerApi(DolphinConnection dolphin, MemoryResourceManager resources, DynamicResourceManager dynamicResources)
        {
            _dolphin = dolphin;
            _resources = resources;
            _dynamicResources = dynamicResources;
        }

        /// <summary>
        /// Get a ressource from the game
        /// </summary>
        public Task<T?> GetAsync<T>(string resource, int? playerId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MemoryResource definition = _resources.Get(resource);
            object? value = ReadResource<T>(definition, playerId);
            return Task.FromResult((T?)value);
        }


        /// <summary>
        /// Read the game's memory
        /// </summary>
        private T ReadResource<T>(MemoryResource resource, int? playerId)
        {
            if (resource.Length <= 0)
                throw new InvalidOperationException($"Resource '{resource.Name}' has an invalid length.");

            byte[] buffer = new byte[resource.Length];
            int bytesRead;

            if (resource.IsDynamic)
            {
                bytesRead = ReadDynamicResource(resource, playerId, buffer);
            }
            else if (resource.IsPointerChain)
            {
                if (!_dolphin.TryReadPointerChain(resource.Address, buffer, resource.Offsets, out bytesRead))
                    throw new InvalidOperationException($"Failed to read resource '{resource.Name}'.");
            }
            else
            {
                if (!_dolphin.TryRead(resource.Address, buffer, out bytesRead))
                    throw new InvalidOperationException($"Failed to read resource '{resource.Name}'.");
            }

            if (bytesRead != buffer.Length)
                throw new InvalidOperationException($"Resource '{resource.Name}' returned {bytesRead} bytes, expected {buffer.Length}.");

            if (typeof(T) == typeof(string))
            {
                string value = Encoding.ASCII.GetString(buffer);
                return (T)(object)value;
            }

            throw new NotSupportedException($"Type '{typeof(T).Name}' is not supported yet.");
        }

        private int ReadDynamicResource(MemoryResource resource, int? playerId, byte[] buffer)
        {
            if (playerId is null)
                throw new GameManagerIpcException(IpcErrorCodes.InvalidRequest, $"Resource '{resource.Name}' requires a player ID.");

            if (string.IsNullOrWhiteSpace(resource.DynamicBase))
                throw new InvalidOperationException($"Dynamic resource '{resource.Name}' does not define a dynamic base.");

            if (!_dynamicResources.TryResolve(resource.DynamicBase, playerId.Value, out IntPtr baseAddress))
                throw new GameManagerIpcException(IpcErrorCodes.ResourceUnavailable, $"Resource '{resource.Name}' is not available for player {playerId.Value}.");

            if (!_dolphin.TryReadPointerChainFromBase(baseAddress, buffer, resource.Offsets, out int bytesRead))
                throw new InvalidOperationException($"Failed to read dynamic resource '{resource.Name}'.");

            return bytesRead;
        }

        /// <summary>
        /// Set a value to the game's memory
        /// </summary>
        public Task SetAsync(string resource, object value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MemoryResource definition = _resources.Get(resource);
            byte[] buffer = ConvertValue(value);

            if (buffer.Length != definition.Length) 
                throw new InvalidOperationException($"Resource '{definition.Name}' requires exactly " + $"{definition.Length} bytes, but received {buffer.Length}.");
            
            if (!_dolphin.TryWrite(definition.Address, buffer, out int bytesWritten)) 
                throw new InvalidOperationException($"Failed to write resource '{definition.Name}'.");
            
            if (bytesWritten != buffer.Length) 
                throw new InvalidOperationException($"Resource '{definition.Name}' wrote {bytesWritten} bytes, " + $"expected {buffer.Length}.");
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Convert the value from the json file
        /// </summary>
        private static byte[] ConvertValue(object value)
        {
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String) value = jsonElement.GetString() 
                    ?? throw new InvalidOperationException("String value cannot be null.");

                else if (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False) 
                    value = jsonElement.GetBoolean();

                else 
                    throw new NotSupportedException($"JSON value type '{jsonElement.ValueKind}' " + "is not supported yet.");
            }
            if (value is string stringValue) 
                return Encoding.ASCII.GetBytes(stringValue);

            if (value is bool boolValue) 
                return new byte[]{boolValue ? (byte)1 : (byte)0};
            
            throw new NotSupportedException($"Type '{value.GetType().Name}' is not supported yet.");
        }

        /*private IntPtr ResolveResourceAddress(MemoryResource resource, int? playerId)
        {
            if (!resource.RequiresContext)
                return resource.Address;

            if (playerId is null)
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' requires a player ID.");

            return _dynamicResources.Resolve(
                resource.Name,
                playerId.Value);
        }*/
    }
}