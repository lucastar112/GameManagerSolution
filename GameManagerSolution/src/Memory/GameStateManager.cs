using GameManager.Dolphin;

namespace gameManager.src.Memory;

public sealed class GameStateManager
{
    private static readonly IntPtr GameStateAddress = new(0x80000CF6);

    private readonly DolphinConnection _dolphin;

    public GameStateManager(DolphinConnection dolphin)
    {
        _dolphin = dolphin;
    }

    public bool TryGetState(out GameState state)
    {
        state = GameState.NotInRace;

        if (!_dolphin.TryReadByte(GameStateAddress, out byte value))
        {
            Console.WriteLine($"[GameState] Failed to read 0x{GameStateAddress.ToInt64():X8}");
            return false;
        }

        state = (GameState)value;
        Console.WriteLine($"[GameState] Raw=0x{value:X2}, State={state}");
        return true;
    }

    public bool IsRaceActive()
    {
        return TryGetState(out GameState state) && state == GameState.RaceReady;
    }
}