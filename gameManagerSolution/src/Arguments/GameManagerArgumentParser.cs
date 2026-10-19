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
        /// <summary>
        /// Parse the arguments when launched
        /// </summary>
        public static GameManagerArguments Parse(string[] args)
        {
            if (args.Length != 3) 
                throw new ArgumentException("Usage: GameManager.exe " + "<GAME_ID> <DOLPHIN_PATH> <GAME_PATH>");
            
            string gameId = args[0].ToUpperInvariant();
            string dolphinPath = Path.GetFullPath(args[2]);
            string gamePath = Path.GetFullPath(args[3]);

            #if DEBUG
                string configuration = "DEV";
            #else
                string configuration = "REL";
            #endif

            if (!File.Exists(dolphinPath))
                throw new FileNotFoundException("Dolphin executable was not found.", dolphinPath);
            
            if (!File.Exists(Path.GetFullPath(gamePath)))
                throw new FileNotFoundException("Game file was not found.", gamePath);
            
            return new GameManagerArguments(gameId, configuration, dolphinPath, gamePath);
        }
    }
}
