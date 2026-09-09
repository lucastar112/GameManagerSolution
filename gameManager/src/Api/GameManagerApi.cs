using gameManager.src.Dolphin;
using gameManager.src.Memory;
using System.Text;

namespace gameManager.src.Api
{
    public sealed class GameManagerApi : IGameManager
    {
        private readonly DolphinConnection _dolphin;
        private readonly MemoryResourceManager _resources;

        public GameManagerApi(
            DolphinConnection dolphin,
            MemoryResourceManager resources)
        {
            _dolphin = dolphin;
            _resources = resources;
        }

        public Task<T?> GetAsync<T>(
            string resource,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MemoryResource definition = _resources.Get(resource);

            object? value = ReadResource<T>(definition);

            return Task.FromResult((T?)value);
        }

        private T ReadResource<T>(MemoryResource resource)
        {
            if (resource.IsPointerChain)
            {
                throw new NotSupportedException(
                    $"Pointer-chain resource '{resource.Name}' is not supported yet.");
            }

            if (resource.Length <= 0)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' has an invalid length.");
            }

            byte[] buffer = new byte[resource.Length];

            if (!_dolphin.TryRead(
                resource.Address,
                buffer,
                out int bytesRead))
            {
                throw new InvalidOperationException(
                    $"Failed to read resource '{resource.Name}'.");
            }

            if (bytesRead != buffer.Length)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' returned " +
                    $"{bytesRead} bytes, expected {buffer.Length}.");
            }

            if (typeof(T) == typeof(string))
            {
                string value = Encoding.ASCII.GetString(buffer);

                return (T)(object)value;
            }

            throw new NotSupportedException(
                $"Type '{typeof(T).Name}' is not supported yet.");
        }
    }
}