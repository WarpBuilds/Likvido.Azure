using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using Newtonsoft.Json;
using Polly;

namespace Likvido.Azure.Queue
{
    public class QueueService : IQueueService
    {
        private readonly QueueServiceClient _queueServiceClient;
        public QueueService(QueueServiceClient queueServiceClient)
        {
            _queueServiceClient = queueServiceClient;
        }

        public async Task SendAsync(
            string queueName,
            object message,
            TimeSpan? initialVisibilityDelay = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default)
        {
            await SendMessageAsync(queueName, message, initialVisibilityDelay, timeToLive, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendAsync(
            string queueName,
            IEnumerable<object> messages,
            TimeSpan? initialVisibilityDelay = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default)
        {
            foreach (var message in messages)
            {
                await SendMessageAsync(queueName, message, initialVisibilityDelay, timeToLive, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SendMessageAsync(
            string queueName,
            object message,
            TimeSpan? initialVisibilityDelay = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default)
        {
            await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(5 * Math.Pow(2, attempt)))
                .ExecuteAsync(async () =>
                {
                    var queue = _queueServiceClient.GetQueueClient(queueName);
                    try
                    {
                        await queue.SendMessageAsync(
                                JsonConvert.SerializeObject(message),
                                timeToLive: timeToLive ?? TimeSpan.FromSeconds(-1), // Using -1 means that the message does not expire.
                                visibilityTimeout: initialVisibilityDelay,
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.NotFound)
                    {
                        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                })
                .ConfigureAwait(false);
        }
    }
}
