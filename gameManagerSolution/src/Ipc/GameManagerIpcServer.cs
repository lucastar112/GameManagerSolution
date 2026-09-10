using gameManager.src.Api;
using GameManager.Shared.Ipc;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

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

                    int clientId = Interlocked.Increment(ref _clientId);

                    Console.WriteLine($"IPC client connected (ID {_clientId}).");

                    _ = HandleClientSafelyAsync(
                        pipe,
                        clientId,
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
            await using (pipe)
            {
                try
                {
                    Console.WriteLine(
                        $"IPC client {clientId}: handler started.");

                    using StreamReader reader = new(
                        pipe,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);

                    using StreamWriter writer = new(
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
                            $"IPC {clientId}: waiting for request...");

                        string? requestJson =
                            await reader.ReadLineAsync(cancellationToken);

                        if (requestJson is null)
                        {
                            Console.WriteLine(
                                $"IPC client {clientId} closed the connection.");

                            break;
                        }

                        if (string.IsNullOrWhiteSpace(requestJson))
                        {
                            Console.WriteLine(
                                $"IPC {clientId}: received an empty request.");

                            continue;
                        }

                        Console.WriteLine(
                            $"IPC {clientId}: received request: {requestJson}");

                        IpcRequest? request;

                        try
                        {
                            request =
                                JsonSerializer.Deserialize<IpcRequest>(
                                    requestJson,
                                    JsonOptions);
                        }
                        catch (JsonException exception)
                        {
                            Console.WriteLine(
                                $"IPC {clientId}: invalid JSON request.");

                            Console.WriteLine(exception);

                            IpcResponse response = new()
                            {
                                RequestId = 0,
                                Success = false,
                                Error = new IpcError
                                {
                                    Code = IpcErrorCodes.InvalidRequest,
                                    Message = "Invalid JSON request."
                                }
                            };

                            await writer.WriteLineAsync(
                                JsonSerializer.Serialize(
                                    response,
                                    JsonOptions));

                            continue;
                        }

                        if (request is null)
                        {
                            IpcResponse response = new()
                            {
                                RequestId = 0,
                                Success = false,
                                Error = new IpcError
                                {
                                    Code = IpcErrorCodes.InvalidRequest,
                                    Message = "Request cannot be null."
                                }
                            };

                            await writer.WriteLineAsync(
                                JsonSerializer.Serialize(
                                    response,
                                    JsonOptions));

                            continue;
                        }

                        IpcResponse ipcResponse =
                            await HandleRequestAsync(
                                request,
                                cancellationToken);

                        string responseJson =
                            JsonSerializer.Serialize(
                                ipcResponse,
                                JsonOptions);

                        Console.WriteLine(
                            $"IPC {clientId}: sending response: {responseJson}");

                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine(
                        $"IPC client {clientId} handler cancelled.");
                }
                catch (IOException)
                {
                    Console.WriteLine(
                        $"IPC client {clientId} disconnected.");
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"IPC client {clientId} handler failed:");

                    Console.WriteLine(exception);
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
            try
            {
                Console.WriteLine(
                    $"Dispatching operation: {request.Operation}");

                return request.Operation switch
                {
                    IpcOperation.GetResource =>
                        await HandleGetAsync(request, cancellationToken),

                    IpcOperation.SetResource =>
                        await HandleSetAsync(request, cancellationToken),

                    IpcOperation.Ping =>
                        HandlePing(request),

                    _ =>
                        CreateErrorResponse(
                            request,
                            IpcErrorCodes.UnknownOperation,
                            $"Unknown operation '{request.Operation}'.")
                };
            }
            catch (KeyNotFoundException exception)
            {
                return CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidResource,
                    exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidValue,
                    exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidRequest,
                    exception.Message);
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"IPC request {request.RequestId} failed:");
                Console.WriteLine(exception);

                return CreateErrorResponse(
                    request,
                    IpcErrorCodes.InternalError,
                    "An internal GameManager error occurred.");
            }
        }

        private static IpcResponse CreateErrorResponse(
    IpcRequest request,
    string code,
    string message)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Value = null,
                Error = new IpcError
                {
                    Code = code,
                    Message = message
                }
            };
        }

        private static IpcResponse HandlePing(IpcRequest request)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Value = "PONG",
                Error = null
            };
        }
    }
}