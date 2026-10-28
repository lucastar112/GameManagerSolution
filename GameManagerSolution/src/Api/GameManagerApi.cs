using GameManager.Dolphin;
using gameManager.src.Memory;
using GameManager.Shared.Ipc;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Buffers.Binary;

namespace gameManager.src.Api
{
    public sealed class GameManagerApi
    {
        private readonly DolphinConnection _dolphin;
        private readonly DynamicResourceManager _dynamicResources;
        private readonly MemoryResourceManager _resources;
        private readonly ResourceAvailabilityManager _availabilityManager;

        /// <summary>
        /// Stores dolphin data
        /// </summary>
        public GameManagerApi(DolphinConnection dolphin, MemoryResourceManager resources, DynamicResourceManager dynamicResources, ResourceAvailabilityManager availabilityManager)
        {
            _dolphin = dolphin;
            _resources = resources;
            _dynamicResources = dynamicResources;
            _availabilityManager = availabilityManager;
        }

        /// <summary>
        /// Get a ressource from the game
        /// </summary>
        public async Task<T?> GetAsync<T>(string resource, int? playerId = null, CancellationToken cancellationToken = default)
        {
            object? value = await GetResourceAsync(resource, playerId, cancellationToken);

            if (value is null)
                return default;

            return (T)value;
        }


        private int ReadDynamicResource(
            MemoryResource resource,
            int? playerId,
            byte[] buffer)
        {
            if (playerId is null)
            {
                throw new GameManagerIpcException(
                    IpcErrorCodes.InvalidRequest,
                    $"Resource '{resource.Name}' requires a player ID.");
            }

            if (string.IsNullOrWhiteSpace(resource.DynamicBase))
            {
                throw new InvalidOperationException(
                    $"Dynamic resource '{resource.Name}' does not define a dynamic base.");
            }

            if (!_dynamicResources.TryResolve(
                resource.DynamicBase,
                playerId.Value,
                out IntPtr baseAddress))
            {
                throw new GameManagerIpcException(
                    IpcErrorCodes.ResourceUnavailable,
                    $"Resource '{resource.Name}' is not available for player {playerId.Value}.");
            }

            Console.WriteLine(
                $"[MemoryDebug] {resource.Name}: " +
                $"Base=0x{baseAddress.ToInt64():X8}, " +
                $"Player={playerId.Value}, " +
                $"Offsets={string.Join(", ", resource.Offsets.Select(o => $"0x{o:X}"))}");

            if (!_dolphin.TryReadPointerChainFromBase(
                baseAddress,
                buffer,
                resource.Offsets,
                out int bytesRead))
            {
                throw new InvalidOperationException(
                    $"Failed to read dynamic resource '{resource.Name}'.");
            }

            Console.WriteLine(
                $"[MemoryDebug] {resource.Name}: " +
                $"BytesRead={bytesRead}, " +
                $"RawBytes={Convert.ToHexString(buffer)}");

            return bytesRead;
        }


        /// <summary>
        /// Set a value to the game's memory
        /// </summary>
        public Task SetAsync(string resource, object value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MemoryResource definition = _resources.Get(resource);
            _availabilityManager.EnsureAvailable(definition);

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

        public Task<object?> GetResourceAsync(string resource, int? playerId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MemoryResource definition = _resources.Get(resource);

            _availabilityManager.EnsureAvailable(definition);

            return Task.FromResult(ReadResourceValue(definition, playerId));
        }


        private object ReadResourceValue(MemoryResource resource, int? playerId)
        {
            if (resource.Length <= 0)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' has an invalid length.");
            }

            byte[] buffer = new byte[resource.Length];
            int bytesRead;

            if (resource.IsDynamic)
            {
                bytesRead = ReadDynamicResource(resource, playerId, buffer);
            }
            else if (resource.IsPointerChain)
            {
                if (!_dolphin.TryReadPointerChain(
                        resource.Address,
                        buffer,
                        resource.Offsets,
                        out bytesRead))
                {
                    throw new InvalidOperationException(
                        $"Failed to read resource '{resource.Name}'.");
                }
            }
            else
            {
                if (!_dolphin.TryRead(resource.Address, buffer, out bytesRead))
                {
                    throw new InvalidOperationException(
                        $"Failed to read resource '{resource.Name}'.");
                }
            }

            if (bytesRead != buffer.Length)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' returned {bytesRead} bytes, " +
                    $"expected {buffer.Length}.");
            }

            Console.WriteLine(
                $"[MemoryDebug] Resource={resource.Name}, " +
                $"Type={resource.Type}, " +
                $"Length={buffer.Length}, " +
                $"Bytes={Convert.ToHexString(buffer)}");


            string type = resource.Type.ToLowerInvariant();

            object value = type switch
            {
                // Unsigned integers
                "uint8" when buffer.Length == 1 =>
                    buffer[0],

                "uint16" when buffer.Length == 2 =>
                    BinaryPrimitives.ReadUInt16BigEndian(buffer),

                "uint32" when buffer.Length == 4 =>
                    BinaryPrimitives.ReadUInt32BigEndian(buffer),

                // Signed integers
                "int8" when buffer.Length == 1 =>
                    unchecked((sbyte)buffer[0]),

                "int16" when buffer.Length == 2 =>
                    BinaryPrimitives.ReadInt16BigEndian(buffer),

                "int32" when buffer.Length == 4 =>
                    BinaryPrimitives.ReadInt32BigEndian(buffer),

                // Floating-point values
                "single" or "float" when buffer.Length == 4 =>
                    BitConverter.Int32BitsToSingle(
                        BinaryPrimitives.ReadInt32BigEndian(buffer)),

                "double" when buffer.Length == 8 =>
                    BitConverter.Int64BitsToDouble(
                        BinaryPrimitives.ReadInt64BigEndian(buffer)),

                // Strings and raw byte arrays
                "string" =>
                    Encoding.ASCII.GetString(buffer),

                "bytes" =>
                    buffer,

                _ => throw new NotSupportedException(
                    $"Resource '{resource.Name}' has unsupported type " +
                    $"'{resource.Type}' or an invalid length of {buffer.Length}.")
            };

            Console.WriteLine(
                $"[MemoryDebug] Resource={resource.Name}, " +
                $"DecodedValue={value}");

            return value;
        }
    }
}