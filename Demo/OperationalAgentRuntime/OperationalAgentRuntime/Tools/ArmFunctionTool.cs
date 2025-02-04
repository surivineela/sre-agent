using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using HandlebarsDotNet;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Tools
{
    // Migrated and being used in the new project
    public class ArmFunctionTool
    {
        private DurableTaskClient durableClient;
        private Guid operationId;

        public ArmFunctionTool()
        {

        }

        public ArmFunctionTool(DurableTaskClient client, string operationId)
        {
            this.durableClient = client;
            this.operationId = Guid.Parse(operationId);
        }

        [KernelFunction, Description("Gets all the subscriptions the agent currently has access to")]
        public async Task<List<AzureSubscription>> GetAllSubscriptions()
        {
            List<AzureSubscription> subscriptions = await ArmHelper.GetSubscriptionsAsync();
            return subscriptions;
        }

        [KernelFunction, Description("Gets all resources on a subscription, returning a list of resource IDs")]
        public async Task<List<string>> GetAllResources(
            [Description("The subscription to query resources for")]
            string subscriptionId
)
        {
            List<string> resourceIds = await ArmHelper.GetAllResourceUriAsync(subscriptionId);
            return resourceIds;
        }

        //[Function(nameof(CheckBasicAuthForResourcesV2))]
        //public static async Task<List<BasicAuthStatus>> CheckBasicAuthForResourcesV2([ActivityTrigger] List<string> resourceIds, FunctionContext executionContext)
        //{
        //    var list = await ArmHelper.CheckBasicAuth(resourceIds);
        //    return list;
        //}

        [KernelFunction, Description("Checks whether basic auth is enabled on a list of site resources")]
        public async Task<List<BasicAuthStatus>> CheckBasicAuth(
            [Description("The resource IDs of the apps to check.")]
            List<string> appResourceIds
        )
        {
            List<BasicAuthStatus> status = await ArmHelper.CheckBasicAuth(appResourceIds);
            return status;
        }

        [Function(nameof(CheckBasicAuthSingle))]
        [KernelFunction, Description("Checks whether basic auth is enabled on a site resource")]
        public async Task<List<BasicAuthStatus>> CheckBasicAuthSingle(
            [ActivityTrigger]
            [Description("The resource ID of the app to check.")]
            string appResourceId
)
        {
            List<BasicAuthStatus> status = await ArmHelper.CheckBasicAuth([appResourceId]);
            return status;
        }

        [KernelFunction, Description("Disables basic auth on a site resource")]
        public async Task<bool> DisableBasicAuth(
            [Description("The resource ID of the app.")]
            string appResourceId
        )
        {
            // Checking the status here is not strictly necessary, but the arm helper code current takes a status, so this is an easy way to satisfy that
            var status = (await ArmHelper.CheckBasicAuth([appResourceId])).Single();

            Console.WriteLine(JsonSerializer.Serialize(status));
            bool success = await ArmHelper.DisableBasicAuth(status);

            if (durableClient != null)
            {
                await TrackedAgentOperationActionHelper.AppendAnnotation(durableClient, operationId, $"Disabled basic auth for app {status.Name}");                
            }

            return success;
            
        }
    }
}
