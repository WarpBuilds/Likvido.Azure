using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Fallback;
using Polly.Retry;

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

            await GetResiliencePipeline()
                .ExecuteAsync(async cancellationToken => await _client.SendEventsAsync(cloudEvents, cancellationToken).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        private ResiliencePipeline<Response> GetResiliencePipeline() =>
            new ResiliencePipelineBuilder<Response>()
                .AddRetry(new RetryStrategyOptions<Response>
                {
                    ShouldHandle = new PredicateBuilder<Response>().Handle<Exception>(),
                    Delay = TimeSpan.FromSeconds(3),
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    OnRetry = args =>
                    {
                        _logger.LogError(args.Outcome.Exception, "Error while publishing events to Event Grid. Retrying in {SleepDuration}. Attempt number {AttemptNumber}", args.RetryDelay.ToString("g"), args.AttemptNumber);
                        return default;
                    }
                })
                .AddFallback(new FallbackStrategyOptions<Response>
                {
                    ShouldHandle = new PredicateBuilder<Response>().Handle<Exception>(),
                    FallbackAction = args => default,
                    OnFallback = args =>
                    {
                        if (args.Outcome.Exception == null)
                        {
                            return default;
                        }

                        _logger.LogCritical(args.Outcome.Exception, "Failed to publish events to Event Grid after multiple retries");
                        throw args.Outcome.Exception;
                    }
                })
                .Build();
    }
}
