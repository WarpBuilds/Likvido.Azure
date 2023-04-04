using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Likvido.Azure.Queue
{
    public interface IQueueService
    {
        Task SendAsync(
            string queueName,
            object message,
            TimeSpan? initialVisibilityDelay = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default);

        Task SendAsync(string queueName,
            IEnumerable<object> messages,
            TimeSpan? initialVisibilityDelay = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default);
    }
}
