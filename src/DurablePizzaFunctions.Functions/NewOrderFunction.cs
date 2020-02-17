using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurablePizzaFunctions.Functions.Pizzas;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DurablePizzaFunctions.Functions
{
    public static class NewOrderFunction
    {
        [FunctionName("NewOrderFunction")]
        public static async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            Order order = context.GetInput<Order>() ?? throw new ArgumentNullException(nameof(order), "An order is required");

            await context.CallActivityAsync("NewOrderFunction_StoreOrder", order);

            using (var timeoutCts = new CancellationTokenSource())
            {
                DateTime expiration = context.CurrentUtcDateTime.AddSeconds(30);
                Task timeoutTask = context.CreateTimer(expiration, timeoutCts.Token);

                bool orderPaid = false;
                for (int retryCount = 0; retryCount <= 3; retryCount++)
                {
                    Task<Guid> paymentResponseTask = context.WaitForExternalEvent<Guid>("PaymentReceived");

                    Task paidOrderTask = await Task.WhenAny(paymentResponseTask, timeoutTask);
                    if (paidOrderTask == paymentResponseTask)
                    {
                        if (paymentResponseTask.Result == order.Id)
                        {
                            orderPaid = true;
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

                return orderPaid;
            }
        }

        [FunctionName("NewOrderFunction_StoreOrder")]
        public static void SendFakeMessage([ActivityTrigger] Order order, ILogger log) =>
            log.LogInformation($"Storing order {order.Id}.");

        [FunctionName("NewOrderFunction_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {

            string json = await req.Content.ReadAsStringAsync();
            var order = JsonConvert.DeserializeObject<Order>(json);

            string instanceId = await starter.StartNewAsync("NewOrderFunction", order);
            
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}