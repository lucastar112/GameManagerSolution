using System.Diagnostics;
using gameManager.src.Dolphin;
using gameManager.src.Arguments;

namespace gameManager.src
{
    public sealed class GameManager
    {
        private readonly DolphinConnection _dolphin;

        public GameManager(Process process)
        {
            _dolphin = new DolphinConnection(process);
        }

        public void Run()
        {
            Console.WriteLine("GameManager started.");

            while (true)
            {
                if (!_dolphin.IsGameRunning())
                {
                    Console.WriteLine("Waiting for game initialization...");
                    Thread.Sleep(1000);
                    continue;
                }

                Console.WriteLine("Game initialized.");

                RunGameLoop();

                Console.WriteLine("Game is no longer running.");

                Thread.Sleep(1000);
            }
        }

        private void RunGameLoop()
        {
            while (_dolphin.IsGameRunning())
            {
                // GameManager logic will eventually live here.
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

                // Launch Dolphin with the game
                DolphinLauncher dolphinLauncher = new();
                Process dolphinProcess = dolphinLauncher.Start(arguments.DolphinPath, arguments.GamePath);

                // Start gameManager
                GameManager gameManager = new(dolphinProcess);
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