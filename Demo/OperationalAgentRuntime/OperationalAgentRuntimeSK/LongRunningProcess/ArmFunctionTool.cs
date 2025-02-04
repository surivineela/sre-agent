using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OperationalAgentCore;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;
using System.ComponentModel;
using System.Text.Json;

namespace OperationalAgentRuntime.Tools
{
    public class ArmFunctionTool
    {
        private readonly ILogger logger;

        public ArmFunctionTool(ILogger logger)
        {
            this.logger = logger;
        }

        [Description("Disables basic auth on a site resource")]
        public async Task<bool> DisableBasicAuth(
            [Description("The resource ID of the app.")]
            string appResourceId
        )
        {
            // Checking the status here is not strictly necessary, but the arm helper code current takes a status, so this is an easy way to satisfy that
            var status = (await ArmHelper.CheckBasicAuth([appResourceId])).Single();
            bool success = await ArmHelper.DisableBasicAuth(status);

            if (success)
            {
                TrackedActionHelper.UpdateActionStatus(status.ResourceId, ActionStatus.Completed);
            }
            return success;

        }

        [Description("Sets the minimum TLS version on a site resource")]
        public async Task<string> SetMinimumTlsVersion(
            [Description("The resource ID of the app.")]
            string appResourceId,
            [Description("The minimum TLS version to set, e.g. 1.2")]
            string minimumTlsVersion
        )
        {
            var status = (await ArmHelper.GetTlsSettings([appResourceId])).SingleOrDefault();
            bool success = false;

            if(status != null)
            {
                success = await ArmHelper.UpdateMinimumTlsVersion(status, minimumTlsVersion);
            }

            var message = success switch
            {
                true => $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {DateTime.UtcNow:o}",
                false => $"Failed to update resource {appResourceId} at {DateTime.UtcNow:o}",
            };
            
            if (success)
            {
                TrackedActionHelper.UpdateActionStatus(status.ResourceId, ActionStatus.Completed);            
            }

            logger?.LogInformation(message);
            return message;
        }
    }
}
