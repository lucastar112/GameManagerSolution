using gameManager.src.Api;
using System.IO.Pipes;

namespace gameManager.src.Ipc
{
    public sealed class GameManagerIpcServer
    {
        private int _clientId;
        private const string PipeName = "GameManager";

        private readonly IGameManager _api;

        public GameManagerIpcServer(IGameManager api)
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
                NamedPipeServerStream pipe =
                    new(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                try
                {
                    await pipe.WaitForConnectionAsync(
                        cancellationToken);

                    Console.WriteLine(
                        "IPC client connected.");

                    int clientId = Interlocked.Increment(ref _clientId);

                    Console.WriteLine($"IPC client connected (ID {clientId}).");

                    _ = HandleClientAsync(pipe, clientId, cancellationToken).ContinueWith(task =>
                    {
                        if (task.Exception is not null)
                            {
                                Console.WriteLine($"IPC client handler failed: {task.Exception}");
                            }
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipe, int clientId, CancellationToken cancellationToken)
        {
            await using (pipe)
            {
                try
                {
                    Console.WriteLine(
                        $"IPC client {clientId} ready.");

                    while (
                        pipe.IsConnected &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine(
                            $"IPC {clientId}: waiting for request...");

                        IpcRequest? request =
                            await IpcStream.ReceiveAsync<IpcRequest>(
                                pipe,
                                cancellationToken);

                        if (request is null)
                        {
                            break;
                        }

                        Console.WriteLine(
                            $"IPC {clientId}: received request " +
                            $"{request.RequestId} " +
                            $"'{request.Operation}' " +
                            $"'{request.Resource}'.");

                        IpcResponse response =
                            await HandleRequestAsync(
                                request,
                                cancellationToken);

                        Console.WriteLine(
                            $"IPC {clientId}: sending response.");

                        await IpcStream.SendAsync(
                            pipe,
                            response,
                            cancellationToken);
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
                        $"{exception}");
                }
            }
        }

        private async Task<IpcResponse> HandleRequestAsync(
    IpcRequest request,
    CancellationToken cancellationToken)
        {
            try
            {
                if (request.Operation != "get")
                {
                    return new IpcResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Error =
                            $"Unknown operation '{request.Operation}'."
                    };
                }

                Console.WriteLine(
                    $"Calling API for '{request.Resource}'.");

                string? value =
                    await _api.GetAsync<string>(
                        request.Resource,
                        cancellationToken);

                Console.WriteLine(
                    $"API returned '{value}'.");

                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Value = value
                };
            }
            catch (Exception exception)
            {
                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = exception.Message
                };
            }
        }
    }
}
