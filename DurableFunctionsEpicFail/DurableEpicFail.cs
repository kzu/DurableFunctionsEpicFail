using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableFunctionsEpicFail
{
    public class DurableEpicFail
    {
        static Random random = new Random();
        readonly ILogger<DurableEpicFail> logger;

        public DurableEpicFail(ILogger<DurableEpicFail> logger) => this.logger = logger;

        [FunctionName(nameof(Start))]
        public async Task Start([TimerTrigger("* * * * * *", RunOnStartup = true)] TimerInfo timer, [DurableClient] IDurableOrchestrationClient client)
        {
            logger.LogWarning("Queuing 1k orchestrations");

            for (var i = 0; i < 1000; i++)
                await client.StartNewAsync(nameof(RunOrchestration));
        }

        [FunctionName(nameof(RunOrchestration))]
        public async Task RunOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            logger.LogDebug("{0} Started", context.InstanceId);
            await context.CallActivityAsync(nameof(RunActivity), context.InstanceId);
            logger.LogDebug("{0} Waiting for Begin", context.InstanceId);
            await context.WaitForExternalEvent("Begin");
            logger.LogDebug("{0} Waiting for End", context.InstanceId);
            await context.WaitForExternalEvent("End");
            logger.LogInformation("{0} Done", context.InstanceId);
        }

        [FunctionName(nameof(RunActivity))]
        [return: Queue("Activity")]
        public static string RunActivity([ActivityTrigger] IDurableActivityContext context) => context.GetInput<string>();

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
