namespace DebugManager.Models;

public sealed record CharacterDefinition(
    byte Id,
    string Code,
    string Name,
    string ImageFile
);