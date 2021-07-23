using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;

namespace DurableFunctionsEpicFail
{
    public class DurableEpicFail
    {
        static Random random = new Random();
        static readonly HttpClient http = new HttpClient();
        readonly ILogger<DurableEpicFail> logger;

        public DurableEpicFail(ILogger<DurableEpicFail> logger) => this.logger = logger;

        [FunctionName(nameof(Start))]
        public async Task Start(
            [TimerTrigger("* * * * * *", RunOnStartup = true)] TimerInfo timer, 
            [EventGrid(TopicEndpointUri = "EventGridUrl", TopicKeySetting = "EventGridKey")] IAsyncCollector<EventGridEvent> events)
        {
            logger.LogWarning("Queuing 1k orchestrations");

            await Enumerable.Range(0, 1000).ParallelForEachAsync(200, i =>
                // Orchestrations are started via an event grid event
                events.AddAsync(new EventGridEvent(
                    Guid.NewGuid().ToString(), i.ToString(),
                    "Data", "EpicEvent", DateTime.UtcNow, "1.0", "Durable")));
        }

        [FunctionName(nameof(StartOrchestration))]
        public async Task StartOrchestration(
            [EventGridTrigger] EventGridEvent e,
            [DurableClient] IDurableOrchestrationClient client)
            // Event grid handler starts the orchestration
            => await client.StartNewAsync(nameof(RunOrchestration));

        [FunctionName(nameof(RunOrchestration))]
        public async Task RunOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            logger.LogInformation("{0} Started", context.InstanceId);
            
            // Runs HTTP GET and queues a message
            await context.CallActivityAsync(nameof(RunHttpActivity), context.InstanceId);

            // Events are triggered by the queue trigger.
            logger.LogDebug("{0} Waiting for Begin", context.InstanceId);
            await context.WaitForExternalEvent("Begin");
            logger.LogDebug("{0} Waiting for End", context.InstanceId);
            await context.WaitForExternalEvent("End");
            logger.LogWarning("{0} Done", context.InstanceId);
        }

        [FunctionName(nameof(RunHttpActivity))]
        [return: Queue("Activity")]
        public static Task<string> RunHttpActivity([ActivityTrigger] IDurableActivityContext context) 
            => http.GetStringAsync("https://docs.microsoft.com/");

        [FunctionName(nameof(DoWork))]
        public static async Task DoWork([QueueTrigger("Activity")] string instanceId, [DurableClient] IDurableOrchestrationClient client)
        {
            // Random 0..5sec delay
            await Task.Delay((int)Math.Round(random.NextDouble() * 5000));

            await client.RaiseEventAsync(instanceId, "Begin");

            // Random 0..5sec delay
            await Task.Delay((int)Math.Round(random.NextDouble() * 5000));

            await client.RaiseEventAsync(instanceId, "End");
        }
    }
}
