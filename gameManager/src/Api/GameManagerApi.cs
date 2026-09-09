using gameManager.src.Dolphin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gameManager.src.Api
{
    public sealed class GameManagerApi : IGameManager
    {
        private readonly DolphinConnection _dolphin;

        public GameManagerApi(DolphinConnection dolphin)
        {
            _dolphin = dolphin;
        }

        public async Task<T?> GetAsync<T>(
            string resource,
            CancellationToken cancellationToken = default)
        {
            // Resource lookup will eventually happen here.
            throw new NotImplementedException();
        }

        public Task SubscribeAsync(string resource, TimeSpan interval, CancellationToken cancellationToken = default)
        {
            // Subscription will eventually happen here.
            throw new NotImplementedException();
        }

        public Task UnsubscribeAsync(string resource, CancellationToken cancellationToken = default)
        {
            // Unsubscription will eventually happen here.
            throw new NotImplementedException();
        }
    }
}
