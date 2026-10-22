namespace gameManager.src.Memory;

public sealed class ResourceAvailabilityManager
{
    private readonly GameStateManager _gameState;

    public ResourceAvailabilityManager(GameStateManager gameState)
    {
        _gameState = gameState;
    }

    public bool IsAvailable(MemoryResource resource)
    {
        switch (resource.Availability)
        {
            case ResourceAvailability.Always:
                return true;

            case ResourceAvailability.Race:
                return _gameState.IsRaceActive();

            default:
                return false;
        }
    }
}