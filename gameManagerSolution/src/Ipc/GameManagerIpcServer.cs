using gameManager.src.Api;
using GameManager.Shared.Ipc;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gameManager.src.Ipc
{
    /// <summary>
    /// Handles IPC Connection to GameManager
    /// </summary>
    public sealed class GameManagerIpcServer
    {
        private const string PipeName = "GameManager";
        private static readonly TimeSpan ChildStatusTimeout = TimeSpan.FromMilliseconds(500);
        private int _clientId;
        private long _nextRequestId;
        private readonly GameManagerApi _api;
        private readonly SemaphoreSlim _clientConnectedSignal = new(0);
        //private readonly TaskCompletionSource<int> _clientConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<int, string> _clientNames = new();
        private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();

        /// <summary>
        /// Returns the keys to all clients connected
        /// </summary>
        /// <returns></returns>
        public IReadOnlyCollection<int> GetConnectedClientIds()
        {
            return _clients.Keys.ToArray();
        }
        

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters = {new JsonStringEnumConverter()}
        };

        public GameManagerIpcServer(GameManagerApi api)
        {
            _api = api;
        }

        /// <summary>
        /// Run IPC server
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"IPC server listening on '{PipeName}'.");

            _ready.TrySetResult(true);

            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken);
                    int clientId =Interlocked.Increment(ref _clientId);
                    Console.WriteLine($"IPC client connected (ID {clientId}).");
                    _ = HandleClientSafelyAsync(pipe, clientId, cancellationToken);
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// Get client "heartbeat"
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="GameManagerIpcException"></exception>
        public async Task<string?> GetStatusAsync(int clientId, CancellationToken cancellationToken = default)
        {
            if (!_clients.TryGetValue(clientId, out ClientConnection? client))
                throw new InvalidOperationException($"Client {clientId} is not connected.");

            long requestId = Interlocked.Increment(ref _nextRequestId);
            IpcChildRequest request = new()
            {
                MessageType = IpcMessageType.ChildRequest,
                RequestId = requestId,
                Operation = IpcOperation.GetStatus
            };

            TaskCompletionSource<IpcResponse> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            client.PendingRequests[requestId] = completion;

            try
            {
                await client.WriteAsync(JsonSerializer.Serialize(request, JsonOptions), cancellationToken);
                IpcResponse response = await completion.Task.WaitAsync(ChildStatusTimeout, cancellationToken);

                if (!response.Success)
                    throw new GameManagerIpcException(response.Error?.Code ?? "UNKNOWN_ERROR", response.Error?.Message ?? "Unknown error.");
                return response.Value?.ToString();
            }
            finally
            {
                client.PendingRequests.TryRemove(requestId, out _);
            }
        }

        /// <summary>
        /// Handle if an exception occures
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="clientId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task HandleClientSafelyAsync(NamedPipeServerStream pipe, int clientId, CancellationToken cancellationToken)
        {
            try
            {
                await HandleClientAsync(pipe, clientId, cancellationToken);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"IPC client {clientId} handler failed:");
                Console.WriteLine(exception);
            }
        }

        /// <summary>
        /// Handles client connexion
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="clientId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task HandleClientAsync(NamedPipeServerStream pipe, int clientId, CancellationToken cancellationToken)
        {
            await using (pipe)
            {
                ClientConnection client = new(clientId, pipe);

                _clients[clientId] = client;

                _clientConnectedSignal.Release();

                try
                {
                    Console.WriteLine($"IPC client {clientId}: handler started.");

                    using StreamReader reader = new
                        (
                            pipe,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: 1024,
                            leaveOpen: true
                        );

                    Console.WriteLine($"IPC client {clientId} ready.");

                    while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        // Useful for debugging, but may produce a lot of output in production. Uncomment the following line if you want to log waiting for requests.
                        //Console.WriteLine($"IPC {clientId}: waiting for request...");

                        string? json = await reader.ReadLineAsync(cancellationToken);

                        if (json is null)
                        {
                            Console.WriteLine($"IPC client {clientId} closed the connection.");
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(json))
                            continue;

                        // Useful for debugging, but may produce a lot of output in production. Uncomment the following line if you want to log received messages.
                        //Console.WriteLine($"IPC {clientId}: received: {json}");

                        IpcMessageType? messageType;

                        try
                        {
                            using JsonDocument document = JsonDocument.Parse(json);

                            if (!document.RootElement.TryGetProperty(
                                    "MessageType",
                                    out JsonElement messageTypeElement))
                            {
                                Console.WriteLine(
                                    $"IPC client {clientId}: message has no MessageType.");

                                continue;
                            }

                            messageType =
                                messageTypeElement.Deserialize<IpcMessageType>(JsonOptions);
                        }
                        catch (JsonException exception)
                        {
                            Console.WriteLine(
                                $"IPC client {clientId}: invalid IPC message.");

                            Console.WriteLine(exception);

                            continue;
                        }

                        switch (messageType)
                        {
                            case IpcMessageType.Response:
                                HandleChildResponse(client, json);
                                continue;

                            case IpcMessageType.Request:
                                break;

                            case IpcMessageType.ChildRequest:
                                Console.WriteLine(
                                    $"IPC client {clientId}: unexpected ChildRequest from child.");

                                continue;

                            default:
                                Console.WriteLine(
                                    $"IPC client {clientId}: unexpected message type '{messageType}'.");

                                continue;
                        }

                        IpcRequest? request;

                        try
                        {
                            request = JsonSerializer.Deserialize<IpcRequest>(
                                json,
                                JsonOptions);
                        }
                        catch (JsonException exception)
                        {
                            Console.WriteLine($"IPC {clientId}: invalid JSON request.");
                            Console.WriteLine(exception);

                            IpcResponse response = new()
                            {
                                MessageType = IpcMessageType.Response,
                                RequestId = 0,
                                Success = false,
                                Error = new IpcError
                                {
                                    Code = IpcErrorCodes.InvalidRequest,
                                    Message = "Invalid JSON request."
                                }
                            };

                            await client.WriteAsync(
                                JsonSerializer.Serialize(response, JsonOptions),
                                cancellationToken);

                            continue;
                        }

                        if (request is null)
                            continue;

                        IpcResponse ipcResponse = await HandleRequestAsync(request, clientId, cancellationToken);

                        await client.WriteAsync(JsonSerializer.Serialize(ipcResponse, JsonOptions), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"IPC client {clientId} handler cancelled.");
                }
                catch (IOException)
                {
                    Console.WriteLine($"IPC client {clientId} disconnected.");
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"IPC client {clientId} handler failed:");
                    Console.WriteLine(exception);
                }
                finally
                {
                    _clients.TryRemove(clientId, out _);

                    _clientNames.TryRemove(clientId, out _);

                    foreach (TaskCompletionSource<IpcResponse> pending in client.PendingRequests.Values)
                    {
                        pending.TrySetException(new IOException($"IPC client {clientId} disconnected."));
                    }

                    Console.WriteLine($"IPC client {clientId} removed.");
                }
            }
        }

        /// <summary>
        /// Handles to response to the correct client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="json"></param>
        private static void HandleChildResponse(ClientConnection client, string json)
        {
            IpcResponse? response = JsonSerializer.Deserialize<IpcResponse>(json, JsonOptions);

            if (response is null)
            {
                Console.WriteLine($"IPC client {client.ClientId}: " + "invalid response.");
                return;
            }

            // Useful for debugging, but may produce a lot of output in production. Uncomment the following line if you want to log received responses.
            //Console.WriteLine($"IPC client {client.ClientId}: " + $"received response {response.RequestId}.");

            if (client.PendingRequests.TryRemove(response.RequestId, out TaskCompletionSource<IpcResponse>? source))
                source.TrySetResult(response);
            
            else
                Console.WriteLine($"IPC client {client.ClientId}: " + $"unknown response ID {response.RequestId}.");
        }

        /// <summary>
        /// Handles the "Get" requests from the clients
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<IpcResponse> HandleGetAsync(IpcRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Resource is null)
                {
                    return CreateErrorResponse(request, IpcErrorCodes.InvalidResource, "No resource was specified.");
                }

                object? value = await _api.GetResourceAsync(request.Resource, request.PlayerId, cancellationToken);

                // Useful for debugging, but may produce a lot of output in production. Uncomment the following line if you want to log the value type.
                // Console.WriteLine($"IPC Value type: {value?.GetType().FullName ?? "null"}");

                return new IpcResponse
                {
                    MessageType = IpcMessageType.Response,
                    RequestId = request.RequestId,
                    Success = true,
                    Value = value
                };
            }
            catch (GameManagerIpcException exception)
            {
                return CreateErrorResponse(request, exception.Code, exception.Message);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"GET '{request.Resource}' failed:");
                Console.WriteLine(exception);
                return CreateErrorResponse(request, IpcErrorCodes.InternalError, exception.Message);
            }
        }

        /// <summary>
        /// Handles the "Set" requests from the clients
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private async Task<IpcResponse> HandleSetAsync(IpcRequest request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Calling SET API for '{request.Resource}'.");

            if (request.Value is null)
            {
                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = new IpcError
                    {
                        Code = IpcErrorCodes.InvalidValue,
                        Message = "No value was provided."
                    }
                };
            }

            if (request.Resource is null)
                throw new ArgumentNullException(nameof(request));


            await _api.SetAsync(request.Resource, request.Value, cancellationToken);

            Console.WriteLine($"SET API succeeded for '{request.Resource}'.");

            return new IpcResponse
            {
                MessageType = IpcMessageType.Response,
                RequestId = request.RequestId,
                Success = true
            };
        }

        /// <summary>
        /// Handles the different types of request send by the clients
        /// </summary>
        /// <param name="request"></param>
        /// <param name="clientId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<IpcResponse> HandleRequestAsync(IpcRequest request, int clientId, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"Dispatching operation: {request.Operation}");

                return request.Operation switch
                {
                    IpcOperation.GetResource => await HandleGetAsync(request, cancellationToken),
                    IpcOperation.SetResource => await HandleSetAsync(request, cancellationToken),
                    IpcOperation.Ping => HandlePing(request),
                    IpcOperation.Register => HandleRegister(request, clientId),
                    _ => CreateErrorResponse(request, IpcErrorCodes.UnknownOperation, $"Unknown operation '{request.Operation}'.")
                };
            }
            catch (KeyNotFoundException exception)
            {
                return CreateErrorResponse(request, IpcErrorCodes.InvalidResource, exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return CreateErrorResponse(request, IpcErrorCodes.InvalidValue, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return CreateErrorResponse(request, IpcErrorCodes.InvalidRequest, exception.Message);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"IPC request {request.RequestId} failed:");
                Console.WriteLine(exception);
                return CreateErrorResponse(request, IpcErrorCodes.InternalError, "An internal GameManager error occurred.");
            }
        }

        /// <summary>
        /// Create error to send to the child
        /// </summary>
        /// <param name="request"></param>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static IpcResponse CreateErrorResponse(IpcRequest request, string code, string message)
        {
            return new IpcResponse
            {
                MessageType = IpcMessageType.Response,
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

        /// <summary>
        /// Test if child is alive
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static IpcResponse HandlePing(IpcRequest request)
        {
            return new IpcResponse
            {
                MessageType = IpcMessageType.Response,
                RequestId = request.RequestId,
                Success = true,
                Value = "PONG",
                Error = null
            };
        }

        private sealed class ClientConnection
        {
            private readonly SemaphoreSlim _writeLock = new(1, 1);
            private readonly StreamWriter _writer;
            public int ClientId { get; }

            public ConcurrentDictionary<long, TaskCompletionSource<IpcResponse>> PendingRequests { get; } = new();

            /// <summary>
            /// Connects to client
            /// </summary>
            /// <param name="clientId"></param>
            /// <param name="pipe"></param>
            public ClientConnection(int clientId, Stream pipe)
            {
                ClientId = clientId;

                _writer = new StreamWriter(pipe, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
                {
                    AutoFlush = true
                };
            }

            /// <summary>
            /// Handles write lock for IPC messages
            /// </summary>
            /// <param name="message"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            public async Task WriteAsync(string message, CancellationToken cancellationToken)
            {
                if (_writer is null)
                {
                    throw new InvalidOperationException("GameManager client is not connected.");
                }

                Console.WriteLine("[GameManagerClient] Waiting for write lock...");
                await _writeLock.WaitAsync(cancellationToken);
                Console.WriteLine("[GameManagerClient] Write lock acquired.");

                try
                {
                    Console.WriteLine($"[GameManagerClient] Sending: {message}");
                    await _writer.WriteLineAsync(message);
                    Console.WriteLine("[GameManagerClient] Message sent.");
                }
                finally
                {
                    _writeLock.Release();
                    Console.WriteLine("[GameManagerClient] Write lock released.");
                }
            }

        }

        /// <summary>
        /// Wait until client is connected
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<int> WaitForClientAsync(CancellationToken cancellationToken = default)
        {
            await _clientConnectedSignal.WaitAsync(cancellationToken);
            IReadOnlyCollection<int> clientIds = GetConnectedClientIds();

            if (clientIds.Count == 0)
            {
                throw new InvalidOperationException("A client connection was signaled, but no client is connected.");
            }

            return clientIds.Max();
        }

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            return _ready.Task.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Rturns client ID
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool TryGetClientName(int clientId, out string? name)
        {
            return _clientNames.TryGetValue(clientId, out name);
        }

        /// <summary>
        /// Returns a list of registrered clients
        /// </summary>
        /// <returns></returns>
        public IReadOnlyDictionary<int, string> GetRegisteredClients()
        {
            return new Dictionary<int, string>(_clientNames);
        }

        /// <summary>
        /// Handles the child's registration
        /// </summary>
        /// <param name="request"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        private IpcResponse HandleRegister(IpcRequest request, int clientId)
        {
            if (request.Value is null)
            {
                return CreateErrorResponse(request, IpcErrorCodes.InvalidValue, "Registration data is missing.");
            }

            string? name = request.Value.ToString();

            if (string.IsNullOrWhiteSpace(name))
            {
                return CreateErrorResponse(request, IpcErrorCodes.InvalidValue, "A valid child name is required.");
            }

            _clientNames[clientId] = name;

            Console.WriteLine($"Child '{name}' registered as client {clientId}.");

            return new IpcResponse
            {
                MessageType = IpcMessageType.Response,
                RequestId = request.RequestId,
                Success = true,
                Value = name
            };
        }
    }
}