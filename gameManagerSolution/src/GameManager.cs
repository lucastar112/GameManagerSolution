using gameManager.src.Api;
using gameManager.src.Arguments;
using gameManager.src.Dolphin;
using gameManager.src.Ipc;
using gameManager.src.Memory;
using System.Diagnostics;

namespace gameManager.src
{
    public sealed class GameManager
    {
        private readonly GameManagerApi _api;
        private readonly DolphinConnection _dolphin;
        private readonly GameManagerIpcServer _ipcServer;

        public GameManager(Process process, MemoryResourceManager resources)
        {
            _dolphin = new DolphinConnection(process);

            _api = new GameManagerApi(_dolphin, resources);

            _ipcServer = new GameManagerIpcServer(_api);
        }

        public void Run()
        {
            Console.WriteLine("GameManager started.");

            _ = _ipcServer.RunAsync();

            while (true)
            {
                if (!_dolphin.IsGameRunning())
                {
                    Console.WriteLine("Waiting for game initialization...");
                    Thread.Sleep(1000);
                    continue;
                }

                Console.WriteLine("Game initialized.");

                Thread.Sleep(500);

                RunGameLoop();

                Console.WriteLine("Game is no longer running.");

                Thread.Sleep(1000);
            }
        }


        private void RunGameLoop()
        {
            Thread.Sleep(500);

            string? gameId = _api
                .GetAsync<string>("GameId")
                .GetAwaiter()
                .GetResult();

            Console.WriteLine($"API Game ID: {gameId}");

            while (_dolphin.IsGameRunning())
            {
                Thread.Sleep(100);
            }
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

                //Loads the memory.json file for the game and configuration
                string memoryFile = Path.Combine("Games", arguments.GameId, arguments.Configuration, "memory.json");
                MemoryResourceManager resources = new(memoryFile);

                // Launch Dolphin with the game
                DolphinLauncher dolphinLauncher = new();
                Process dolphinProcess = dolphinLauncher.Start(arguments.DolphinPath, arguments.GamePath);

                // Start gameManager
                GameManager gameManager = new(dolphinProcess, resources);
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