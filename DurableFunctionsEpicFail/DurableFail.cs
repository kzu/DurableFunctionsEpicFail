using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableFunctionsEpicFail
{
    public class DurableFail
    {
        static Random random = new Random();
        readonly ILogger<DurableFail> logger;

        public DurableFail(ILogger<DurableFail> logger) => this.logger = logger;

        [FunctionName(nameof(Start))]
        public static async Task Start([TimerTrigger("0 0 * * * *", RunOnStartup = true)] TimerInfo timer, [DurableClient] IDurableOrchestrationClient client)
        {
            for (int i = 0; i < 10000; i++)
                await client.StartNewAsync(nameof(RunOrchestration));
        }

        [FunctionName(nameof(RunOrchestration))]
        public async Task RunOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            logger.LogInformation("Starting Activity from {0}", context.InstanceId);
            await context.CallActivityAsync(nameof(RunActivity), context.InstanceId);
            logger.LogInformation("Waiting for Begin from {0}", context.InstanceId);
            await context.WaitForExternalEvent("Begin");
            logger.LogInformation("Waiting for End from {0}", context.InstanceId);
            await context.WaitForExternalEvent("End");
            logger.LogWarning("Done from {0}", context.InstanceId);
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
