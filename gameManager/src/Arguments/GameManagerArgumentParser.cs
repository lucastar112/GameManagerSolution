namespace gameManager.src.Arguments
{
    public sealed record GameManagerArguments(
        // Needed parser information
        string GameId,
        string Configuration,
        string DolphinPath,
        string GamePath
    );

    public static class GameManagerArgumentParser
    {
        public static GameManagerArguments Parse(string[] args)
        {
            // Do the necessary testing with the arguments
            if (args.Length != 4)
            {
                throw new ArgumentException("Usage: GameManager.exe " + "<GAME_ID> <CONFIG> <DOLPHIN_PATH> <GAME_PATH>");
            }

            string gameId = args[0].ToUpperInvariant();
            string configuration = args[1].ToUpperInvariant();
            string dolphinPath = Path.GetFullPath(args[2]);
            string gamePath = Path.GetFullPath(args[3]);

            if (configuration is not ("DEV" or "REL"))
            {
                throw new ArgumentException($"Unknown configuration '{configuration}'. " + "Expected DEV or REL.");
            }

            if (!File.Exists(dolphinPath))
            {
                throw new FileNotFoundException("Dolphin executable was not found.", dolphinPath);
            }

            if (!File.Exists(Path.GetFullPath(gamePath)))
            {
                throw new FileNotFoundException("Game file was not found.", gamePath);
            }

            return new GameManagerArguments(
                gameId,
                configuration,
                dolphinPath,
                gamePath
            );
        }
    }
}
