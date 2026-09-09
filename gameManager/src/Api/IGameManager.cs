using System;
using System.Collections.Generic;
using System.Text;

namespace gameManager.src.Api
{
    public interface IGameManager
    {
        Task<T?> GetAsync<T>(string resource, CancellationToken cancellationToken = default);

        Task SubscribeAsync(string resource, TimeSpan interval, CancellationToken cancellationToken = default);

        Task UnsubscribeAsync(string resource, CancellationToken cancellationToken = default);
    }
}
