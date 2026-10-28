using GameManager.Shared.Ipc;

namespace gameManager.src.Memory;

/// <summary>
/// Validates whether a configured resource can be accessed in the current game state.
/// A resource that is not ready produces a specific IPC error instead of being
/// represented as a valid zero or null value.
/// </summary>
public sealed class ResourceAvailabilityManager
{
    private readonly GameStateManager _gameState;

    public ResourceAvailabilityManager(GameStateManager gameState)
    {
        _gameState = gameState;
    }

    public void EnsureAvailable(MemoryResource resource)
    {
        switch (resource.Availability)
        {
            case ResourceAvailability.Always:
                return;

            case ResourceAvailability.Race:
                EnsureRaceCacheReady(resource);
                return;

            default:
                throw new GameManagerIpcException(
                    IpcErrorCodes.InvalidValue,
                    $"Resource '{resource.Name}' has an unsupported availability setting '{resource.Availability}'.");
        }
    }

    private void EnsureRaceCacheReady(MemoryResource resource)
    {
        if (!_gameState.TryGetState(out GameState state))
        {
            throw new GameManagerIpcException(
                IpcErrorCodes.ReadFailed,
                $"Could not read the game/cache state at 0x80000CF6 while checking resource '{resource.Name}'.");
        }

        if (state == GameState.CacheReady)
            return;

        string explanation = state switch
        {
            GameState.RaceNotLoaded =>
                "no race is loaded and the race cache is not available",
            GameState.CacheBuildRequested =>
                "the game has requested a cache build, but the cache is not ready yet",
            _ =>
                $"the game reported an unknown state value {(byte)state}"
        };

        string code = state is GameState.RaceNotLoaded or GameState.CacheBuildRequested
            ? IpcErrorCodes.ResourceNotReady
            : IpcErrorCodes.InvalidGameState;

        throw new GameManagerIpcException(
            code,
            $"Resource '{resource.Name}' is not ready: {explanation} (state={(byte)state}).");
    }
}
