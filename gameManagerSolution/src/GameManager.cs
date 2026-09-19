using gameManager.src.Api;
using gameManager.src.Arguments;
using gameManager.src.Dolphin;
using gameManager.src.Ipc;
using gameManager.src.Memory;
using gameManager.src.Children;
using System.Diagnostics;

namespace gameManager.src
{
    public sealed class GameManager
    {
        private readonly GameManagerApi _api;
        private readonly DolphinConnection _dolphin;
        private readonly GameManagerIpcServer _ipcServer;
        private readonly ChildProcessManager _childProcessManager;
        private readonly ChildConfigurationManager _childConfiguration;


        private static readonly TimeSpan ChildStatusInterval = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan ChildStatusTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Starts GameManager's main functionnality
        /// </summary>
        public GameManager(Process process, MemoryResourceManager resources, ChildConfigurationManager childConfiguration, DynamicResourceManager dynamicResources)
        {
            _dolphin = new DolphinConnection(process);
            _api = new GameManagerApi(_dolphin, resources, dynamicResources);
            _ipcServer = new GameManagerIpcServer(_api);
            _childProcessManager = new ChildProcessManager(_ipcServer);
            _childConfiguration = childConfiguration;
        }

        /// <summary>
        /// Initialise the setup
        /// </summary>
        public void Run()
        {
            Console.WriteLine("GameManager started.");

            Task ipcServerTask = _ipcServer.RunAsync();
            _ipcServer.WaitUntilReadyAsync().GetAwaiter().GetResult();

            Console.WriteLine("IPC server is ready.");

            WaitForGameInitialization();
            StartChildrenAsync(CancellationToken.None).GetAwaiter().GetResult();
            _ = RunChildStatusMonitorAsync(CancellationToken.None);
            _ = RunGameHeartbeatAsync(CancellationToken.None);
            RunGameLoop();
        }

        /// <summary>
        /// Main loop
        /// </summary>
        private void RunGameLoop()
        {
            string? gameId =_api.GetAsync<string>("GameId").GetAwaiter().GetResult();
            Console.WriteLine($"API Game ID: {gameId}");

            while (_dolphin.IsGameRunning())
                Thread.Sleep(100);
            
            Console.WriteLine("Game is no longer running.");
        }

        /// <summary>
        /// Checks the child's "Heartbeat" to ensure child all the necessary processes are running
        /// </summary>
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Sends an "Heartbeat" to the game, used for exception management ingame
        /// </summary>
        private async Task RunGameHeartbeatAsync(CancellationToken cancellationToken)
        {
            bool heartbeat = false;

            while (_dolphin.IsGameRunning() && !cancellationToken.IsCancellationRequested)
            {
                heartbeat = !heartbeat;
                try
                {
                    await _api.SetAsync("GameManagerHeartbeat", heartbeat, cancellationToken);
                    //Console.WriteLine($"GameManager heartbeat: {(heartbeat ? 1 : 0)}");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Heartbeat write failed: {exception.Message}");
                    break;
                }

                try
                {
                    await Task.Delay(HeartbeatInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Request an "Heartbeat" from the child prcesses, used for exception management
        /// </summary>
        private async Task RunChildStatusMonitorAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IReadOnlyCollection<int> clientIds = _ipcServer.GetConnectedClientIds();

                foreach (int clientId in clientIds)
                {
                    try
                    {
                        string? status = await _ipcServer.GetStatusAsync(clientId, cancellationToken);

                        if (_ipcServer.TryGetClientName(clientId, out string? name))
                        {
                            // Useful for debugging, but can be noisy
                            //Console.WriteLine($"Child '{name}' status: {status}");
                        }
                        else
                        {
                            // Useful for debugging, but can be noisy
                            //Console.WriteLine($"Unregistered child {clientId} status: {status}");
                        }
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine($"Child {clientId} did not respond.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Child {clientId} status check failed: " + exception.Message);
                    }
                }
                await Task.Delay(ChildStatusInterval, cancellationToken);
            }
        }

        /// <summary>
        /// Starts all children processes defined in game\{gameID}\{ReleaseType}\config\children\
        /// </summary>
        private async Task StartChildrenAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ChildConfiguration> children = _childConfiguration.GetChildren();

            foreach (ChildConfiguration child in children)
            {
                if (!child.Enabled)
                {
                    Console.WriteLine($"Child '{child.Name}' is disabled.");
                    continue;
                }

                try
                {
                    await _childProcessManager.StartChildAsync(child, cancellationToken);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Failed to start child '{child.Name}':");
                    Console.WriteLine(exception);
                }
            }
        }

        /// <summary>
        /// Wait when the game's memory is loaded in Dolphin
        /// </summary>
        private void WaitForGameInitialization()
        {
            Console.WriteLine("Waiting for game initialization...");

            while (!_dolphin.IsGameRunning()) 
                Thread.Sleep(500);
            
            Console.WriteLine("Game initialized.");
            Thread.Sleep(500);
        }

        public static void Main(string[] args)
        {
            try
            {
                // Parse all launch arguments
                GameManagerArguments arguments = GameManagerArgumentParser.Parse(args);
                Console.WriteLine($"Game: {arguments.GameId}");
                Console.WriteLine($"Configuration: {arguments.Configuration}");
                Console.WriteLine($"Dolphin: {arguments.DolphinPath}");
                Console.WriteLine($"Game: {arguments.GamePath}");

                //Loads the memory.json file
                string memoryFile = Path.Combine("games", arguments.GameId, arguments.Configuration, "config", "memory.json");
                MemoryResourceManager resources = new(memoryFile);

                string childrenFile = Path.Combine("games", arguments.GameId, arguments.Configuration, "config", "children.json");
                ChildConfigurationManager childConfiguration = new(childrenFile);

                // Launch Dolphin with the game
                DolphinLauncher dolphinLauncher = new();
                Process dolphinProcess = dolphinLauncher.Start(arguments.DolphinPath, arguments.GamePath);

                // Load the dynamic cache.json file
                string cacheFile = Path.Combine("games", arguments.GameId, arguments.Configuration, "config", "cache.json");
                DynamicResourceManager dynamicResources = new(cacheFile);
                dynamicResources.Load();

                // Start gameManager
                GameManager gameManager = new(dolphinProcess, resources, childConfiguration, dynamicResources);
                gameManager.Run();
            }

            catch (Exception exception)
            {
                Console.WriteLine($"Fatal GameManager error: {exception.Message}");
                Console.WriteLine(exception);
            }
        }
    }
}