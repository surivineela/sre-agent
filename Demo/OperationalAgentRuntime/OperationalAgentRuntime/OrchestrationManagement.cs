using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime
{
    public class OrchestrationManagement
    {
        private readonly ILogger<OrchestrationManagement> _logger;

        public OrchestrationManagement(ILogger<OrchestrationManagement> logger)
        {
            _logger = logger;
        }

        [Function("TerminateAll")]
        public async Task<IActionResult> TerminateAll(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
            [DurableClient] DurableTaskClient client
            )
        {
            await foreach(var i in client.GetAllInstancesAsync())
            {
                if(i.IsRunning)
                {
                    _logger.LogInformation($"Terminating instance {i.Name}, {i.InstanceId}");
                    await client.TerminateInstanceAsync(i.InstanceId);
                }
            }


            // this doesn't seem to work?
            //await client.Entities.CleanEntityStorageAsync();            

            return new OkResult();
        }
    }
}
