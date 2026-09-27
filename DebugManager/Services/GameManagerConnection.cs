using GameManager.Shared.Client;
using System.Text.Json;

namespace DebugManager.Services;

public sealed class GameManagerConnection : IAsyncDisposable
{
    private readonly GameManagerClient _client = new();

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(cancellationToken);
        await _client.RegisterAsync("DebugManager", cancellationToken);
        Console.WriteLine("DebugManager registered with GameManager.");
    }

    public Task<T?> GetAsync<T>(string resource, int? playerId = null, CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<T>(resource, playerId, cancellationToken);
    }

    public Task<JsonElement> GetPlayersIdAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<JsonElement>("PlayersId", cancellationToken: cancellationToken);
    }

    public Task<JsonElement> GetVehiclesIdAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<JsonElement>("VehiclesId", cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}