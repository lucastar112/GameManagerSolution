using System.Text;
using System.Text.Json;

namespace gameManager.src.Ipc
{
    public static class IpcStream
    {
        public static async Task SendAsync<T>(
            Stream stream,
            T message,
            CancellationToken cancellationToken = default)
        {
            byte[] payload = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(message));

            byte[] length = BitConverter.GetBytes(
                payload.Length);

            await stream.WriteAsync(
                length,
                cancellationToken);

            await stream.WriteAsync(
                payload,
                cancellationToken);

            await stream.FlushAsync(
                cancellationToken);
        }

        public static async Task<T?> ReceiveAsync<T>(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            byte[] lengthBuffer = new byte[4];

            await ReadExactlyAsync(
                stream,
                lengthBuffer,
                cancellationToken);

            int length = BitConverter.ToInt32(
                lengthBuffer,
                0);

            if (length <= 0)
            {
                throw new InvalidDataException(
                    "Invalid IPC message length.");
            }

            byte[] payload = new byte[length];

            await ReadExactlyAsync(
                stream,
                payload,
                cancellationToken);

            return JsonSerializer.Deserialize<T>(
                payload);
        }

        private static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            int offset = 0;

            while (offset < buffer.Length)
            {
                int bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(
                        offset,
                        buffer.Length - offset),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    throw new IOException(
                        "IPC connection was closed.");
                }

                offset += bytesRead;
            }
        }
    }
}