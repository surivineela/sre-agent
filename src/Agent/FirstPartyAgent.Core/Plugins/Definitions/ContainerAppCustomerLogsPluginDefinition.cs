// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppCustomerLogsPluginDefinition
    {
        private readonly IContainerAppCustomerLogsPlugin _plugin;

        public ContainerAppCustomerLogsPluginDefinition(IContainerAppCustomerLogsPlugin plugin)
        {
            _plugin = plugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetLogConfiguration)]
        [Description(
            @"Get list of Log Configuration for the container app environment at start and end of time window. It also checks if Log Configuration are configured or not.
            If no data is returned then ask to validate inputs again as it should never be the case.")]
        public Task<string> GetLogConfiguration(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Customer subscription ID of the managed environment.")] Guid customerSubscriptionId,
            [Description("Name of the customer managed environment.")] string managedEnvironmentName,
            [Description("Name of the managed cluster.")] string managedClusterName
        )
        {
            return _plugin.GetLogConfiguration(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                customerSubscriptionId,
                managedEnvironmentName,
                managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEventProcessorErrors)]
        [Description(
            @"Get list of Event Processor Errors for the container app environment at start and end of time window.
            If no data is returned then it may mean no warnings are present or there is an issue with the query.")]
        public Task<string> GetEventProcessorErrors(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app or job.")] string containerAppOrJobName)
        {
            return _plugin.GetEventProcessorErrors(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName,
                containerAppOrJobName);
        }
    }
}
