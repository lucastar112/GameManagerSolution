namespace gameManager.src.Memory;

/// <summary>
/// Values written by the game to describe the race-cache lifecycle.
/// </summary>
public enum GameState : byte
{
    /// <summary>No race is loaded and no race cache is available.</summary>
    RaceNotLoaded = 0,

    /// <summary>The game has requested that MTWManager build the race cache.</summary>
    CacheBuildRequested = 1,

    /// <summary>MTWManager has built and loaded the race cache.</summary>
    CacheReady = 2
}
