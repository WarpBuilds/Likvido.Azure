using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;

namespace Likvido.Azure.EventGrid
{
    public class EventGridService : IEventGridService
    {
        private readonly ILogger<EventGridService> _logger;
        private readonly string _eventGridSource;
        private readonly EventGridPublisherClient _client;

        public EventGridService(EventGridPublisherClient client, ILogger<EventGridService> logger, string eventGridSource)
        {
            _logger = logger;
            _eventGridSource = eventGridSource;
            _client = client;
        }

        public async Task PublishAsync(params IEvent[] events)
        {
            if (events?.Any() != true || _client == null)
            {
                return;
            }

            var cloudEvents = events.Select(x => new CloudEvent(_eventGridSource, x.GetEventType(), x));

            await GetRetryPolicyAsync()
                .ExecuteAsync(async () => await _client.SendEventsAsync(cloudEvents).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        private AsyncPolicyWrap<Response> GetRetryPolicyAsync()
        {
            var retryPolicy = Policy
              .Handle<Exception>()
              .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)),
              onRetry: (e, sleepDuration, attemptNumber, context) =>
              {
                  _logger.LogError(e, "Error while publishing events to Event Grid. Retrying in {SleepDuration}. Attempt number {AttemptNumber}", sleepDuration, attemptNumber);
              });

            var fallbackPolicy = Policy<Response>
              .Handle<Exception>()
              .FallbackAsync(
                fallbackValue: null,
                onFallbackAsync: e =>
                {
                    _logger.LogCritical(e.Exception, "Failed to publish events to Event Grid after multiple retries.");
                    throw e.Exception;
                });

            return fallbackPolicy.WrapAsync(retryPolicy);
        }
    }
}
