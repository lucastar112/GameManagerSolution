using gameManager.src.Arguments;
using gameManager.src.Dolphin;
using System.Diagnostics;
using System.Text;

namespace gameManager.src
{
    public sealed class GameManager
    {
        private bool _memoryTestCompleted;
        private const long GameIdAddress = 0x80000000;
        private const int GameIdLength = 6;
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

            if (!_memoryTestCompleted)
            {
                TestGameIdMemory();
                _memoryTestCompleted = true;
            }

            while (_dolphin.IsGameRunning())
            {
                Thread.Sleep(100);
            }
        }
        private void TestGameIdMemory()
        {
            byte[] buffer = new byte[GameIdLength];

            Thread.Sleep(500);

            if (!_dolphin.TryRead(
                new IntPtr(GameIdAddress),
                buffer,
                out int bytesRead))
            {
                Console.WriteLine("Failed to read game ID.");
                return;
            }

            string gameId = Encoding.ASCII.GetString(buffer);

            Console.WriteLine($"Game ID: '{gameId}'");
            Console.WriteLine($"Bytes read: {bytesRead}");

            byte[] testGameId = Encoding.ASCII.GetBytes("TEST01");

            if (!_dolphin.TryWrite(
                new IntPtr(GameIdAddress),
                testGameId,
                out int bytesWritten))
            {
                Console.WriteLine("Failed to write game ID.");
                return;
            }

            Console.WriteLine($"Bytes written: {bytesWritten}");

            Array.Clear(buffer);

            if (_dolphin.TryRead(
                new IntPtr(GameIdAddress),
                buffer,
                out bytesRead))
            {
                string newGameId = Encoding.ASCII.GetString(buffer);

                Console.WriteLine($"Game ID after write: '{newGameId}'");
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