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
            @"Get list of Log Configuration for the container app environment at start and end of time window. It also checks if Log Configuration are configured or not. Outputs obtained are:
            - ChageStatus for logDestination (whether log destination has changed or not)
            - logDestination (value of log destination after change)
            - PreviousLogDestination (value of log destination before change)
            - hasWorkloadProfiles (whether managed environment has workload profiles)
            - isLegionEnabled (whether legion is enabled or not)
            - chartVersion (chart version changes of the managed environment)
            - hasDynamicJsonColumns (whether dynamic json columns are present or not)
            If no data is returned then ask to validate inputs again as it should never be the case.")]
        public Task<string> GetLogConfiguration(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Customer Subscription ID of the managed environment.")] Guid customerSubscriptionId,
            [Description("Name of the customer Managed Environment.")] string managedEnvironmentName,
            [Description("Name of the Managed Cluster. Use empty string if managed cluster is not available.")] string managedClusterName
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
            @"Get list of Event Processor Errors for the container app environment at start and end of time window. At least 1 output present means logs are present. No Warnings/Errors means no issues found.
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

        [KernelFunction(KernelFunctionNames.ACA.GetEventProcessorLeaderElectionEvents)]
        [Description(
            @"Get list of Event Processor Leader Election Events for the container app environment at start and end of time window.
            If no data is returned then it may mean no leader election event happened during the interval or there is an issue with the query.")]
        public Task<string> GetEventProcessorLeaderElectionEvents(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetEventProcessorLeaderElectionEvents(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }
    }
}
