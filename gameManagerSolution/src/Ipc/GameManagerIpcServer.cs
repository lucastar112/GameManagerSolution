using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using gameManager.src.Api;
using GameManager.Shared.Ipc;

namespace gameManager.src.Ipc
{
    public sealed class GameManagerIpcServer
    {
        private const string PipeName = "GameManager";

        private int _clientId;

        private readonly GameManagerApi _api;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        public GameManagerIpcServer(GameManagerApi api)
        {
            _api = api;
        }

        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine(
                $"IPC server listening on '{PipeName}'.");

            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken);

                    Console.WriteLine(
                        $"IPC client connected (ID {_clientId}).");

                    _ = HandleClientSafelyAsync(
                        pipe,
                        _clientId,
                        cancellationToken);
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
            }
        }

        private async Task HandleClientSafelyAsync(NamedPipeServerStream pipe, int clientId, CancellationToken cancellationToken)
        {
            try
            {
                await HandleClientAsync(
                    pipe,
                    clientId,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"IPC client {clientId} handler failed:");
                Console.WriteLine(exception);
            }
        }

        private async Task HandleClientAsync(
            NamedPipeServerStream pipe,
            int clientId,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"IPC client {clientId}: handler started.");

            await using (pipe)
            {
                try
                {
                    using StreamReader reader =
                        new(
                            pipe,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: 1024,
                            leaveOpen: true);

                    await using StreamWriter writer =
                        new(
                            pipe,
                            new UTF8Encoding(false),
                            bufferSize: 1024,
                            leaveOpen: true)
                        {
                            AutoFlush = true
                        };

                    Console.WriteLine(
                        $"IPC client {clientId} ready.");

                    while (
                        pipe.IsConnected &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine(
                            $"IPC {clientId}: " +
                            "waiting for request...");

                        string? requestJson =
                            await reader.ReadLineAsync(
                                cancellationToken);

                        if (requestJson is null)
                        {
                            break;
                        }

                        Console.WriteLine(
                            $"IPC {clientId}: received request: " +
                            requestJson);

                        IpcRequest? request =
                            JsonSerializer.Deserialize<IpcRequest>(
                                requestJson,
                                JsonOptions);

                        if (request is null)
                        {
                            Console.WriteLine(
                                $"IPC {clientId}: invalid request.");

                            continue;
                        }

                        IpcResponse response =
                            await HandleRequestAsync(
                                request,
                                cancellationToken);

                        string responseJson =
                            JsonSerializer.Serialize(
                                response,
                                JsonOptions);

                        Console.WriteLine(
                            $"IPC {clientId}: sending response: " +
                            responseJson);

                        await writer.WriteLineAsync(
                            responseJson);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException)
                {
                    Console.WriteLine(
                        $"IPC client {clientId} disconnected.");
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"IPC client {clientId} error: " +
                        exception);
                }
            }
        }

        private async Task<IpcResponse> HandleGetAsync(
    IpcRequest request,
    CancellationToken cancellationToken)
        {
            Console.WriteLine(
                $"Calling GET API for '{request.Resource}'.");

            string? value =
                await _api.GetAsync<string>(
                    request.Resource,
                    cancellationToken);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Value = value
            };
        }

        private async Task<IpcResponse> HandleSetAsync(
    IpcRequest request,
    CancellationToken cancellationToken)
        {
            Console.WriteLine(
                $"Calling SET API for '{request.Resource}'.");

            if (request.Value is null)
            {
                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = new IpcError
                    {
                        Code = "INVALID_VALUE",
                        Message = "No value was provided."
                    }
                };
            }

            await _api.SetAsync(
                request.Resource,
                request.Value,
                cancellationToken);

            Console.WriteLine(
                $"SET API succeeded for '{request.Resource}'.");

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true
            };
        }

        private async Task<IpcResponse> HandleRequestAsync(
    IpcRequest request,
    CancellationToken cancellationToken)
        {
            Console.WriteLine($"Dispatching operation: {request.Operation}");

            switch (request.Operation)
            {
                case IpcOperation.GetResource:
                    return await HandleGetAsync(
                        request,
                        cancellationToken);

                case IpcOperation.SetResource:
                    return await HandleSetAsync(
                        request,
                        cancellationToken);

                case IpcOperation.Ping:
                    return new IpcResponse
                    {
                        RequestId = request.RequestId,
                        Success = true
                    };

                default:
                    return new IpcResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Error = new IpcError
                        {
                            Code = "UNKNOWN_OPERATION",
                            Message =
                                $"Unknown operation '{request.Operation}'."
                        }
                    };
            }
        }
    }
}