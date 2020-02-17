using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace DurablePizzaFunctions.Functions
{
    public static class NewOrderFunction
    {
        [FunctionName("NewOrderFunction")]
        public static async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            string phoneNumber = context.GetInput<string>();
            if (string.IsNullOrEmpty(phoneNumber))
            {
                throw new ArgumentNullException(
                    nameof(phoneNumber),
                    "A phone number input is required.");
            }

            var challengeCode = await context.CallActivityAsync<int>(
                "NewOrderFunction_SendFakeMessage",
                phoneNumber);

            using (var timeoutCts = new CancellationTokenSource())
            {
                // The user has 90 seconds to respond with the code they received in the SMS message.
                DateTime expiration = context.CurrentUtcDateTime.AddMinutes(5);
                Task timeoutTask = context.CreateTimer(expiration, timeoutCts.Token);

                bool authorized = false;
                for (int retryCount = 0; retryCount <= 3; retryCount++)
                {
                    Task<int> challengeResponseTask =
                        context.WaitForExternalEvent<int>("SmsChallengeResponse");

                    Task winner = await Task.WhenAny(challengeResponseTask, timeoutTask);
                    if (winner == challengeResponseTask)
                    {
                        // We got back a response! Compare it to the challenge code.
                        if (challengeResponseTask.Result == challengeCode)
                        {
                            authorized = true;
                            break;
                        }
                    }
                    else
                    {
                        // Timeout expired
                        break;
                    }
                }

                if (!timeoutTask.IsCompleted)
                {
                    // All pending timers must be complete or canceled before the function exits.
                    timeoutCts.Cancel();
                }

                return authorized;
            }
        }

        [FunctionName("NewOrderFunction_SendFakeMessage")]
        public static int SendFakeMessage([ActivityTrigger] string phoneNumber, ILogger log)
        {
            var rand = new Random(Guid.NewGuid().GetHashCode());
            int challengeCode = rand.Next(10000);

            log.LogInformation($"Sending challengeCode {challengeCode} to number {phoneNumber}.");
            return challengeCode;
        }

        [FunctionName("NewOrderFunction_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            var content = req.Content;
            string jsonContent = await content.ReadAsStringAsync();
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("NewOrderFunction", jsonContent);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}