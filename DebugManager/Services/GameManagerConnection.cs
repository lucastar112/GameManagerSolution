using GameManager.Shared.Client;

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

    public Task<byte[]> GetPlayersIdAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<byte[]>("PlayersId", cancellationToken: cancellationToken);
    }

    public Task<byte[]> GetVehiclesIdAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<byte[]>("VehiclesId", cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}