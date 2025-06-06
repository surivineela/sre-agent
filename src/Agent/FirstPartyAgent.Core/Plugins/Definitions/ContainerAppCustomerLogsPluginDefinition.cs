// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppCustomerLogsPluginDefinition
    {
        private readonly IContainerAppCustomerLogsPlugin _plugin;
        private readonly IManagedClusterPlugin _managedClusterPlugin;

        public ContainerAppCustomerLogsPluginDefinition(IContainerAppCustomerLogsPlugin plugin, IManagedClusterPlugin ManagedClusterPlugin)
        {
            _plugin = plugin;
            _managedClusterPlugin = ManagedClusterPlugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetLogConfiguration)]
        [Description(
            @"Get list of Log Configuration for the container app environment at start and end of time window. It also checks if Log Configuration are configured or not. Outputs obtained are:
            - ChageStatus for logDestination (whether log destination has changed or not)
            - logDestination (value of log destination after change)
            - PreviousLogDestination (value of log destination before change)
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
            If no data is returned then it may mean no warnings are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetEventProcessorErrors(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app or job. Use empty string if container app or job name is not available")] string containerAppOrJobName)
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
            If no data is returned then it may mean no leader election event happened during the interval or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
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

        [KernelFunction(KernelFunctionNames.ACA.GetAppsAndjobsVolumeForEnv)]
        [Description(
            @"Get list of Apps and Jobs Volume for the container app environment at start and end of time window.
            If no data is returned then it may mean no apps and jobs volume data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetAppsAndjobsVolumeForEnv(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetAppsAndjobsVolumeForEnv(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEventProcessorPods)]
        [Description(
            @"Get list of Event Processor Pods for the container app environment at start and end of time window.
            If no data is returned then it may mean no event processor pods are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetEventProcessorPods(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetEventProcessorPods(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetLogProcessorPods)]
        [Description(
            @"Get list of Log Processor Pods for the container app environment at start and end of time window.
            
            Returns list of log processor pods along with their start and end time and the node on which they are running. If many pods on the same node have short duration, it may indicate issue with log processor on the node.

            If no data is returned then it may mean no log processor pods are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetLogProcessorPods(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetLogProcessorPods(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEventProcessorPodStatus)]
        [Description(
            @"Get list of Event Processor Pod Status along with their restart counts for the container app environment at start and end of time window.
            If no data is returned then it may mean no event processor pod status data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetEventProcessorPodStatus(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetEventProcessorPodStatus(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetLogProcessorPodStatus)]
        [Description(
            @"Get list of Log Processor Pod Status along with their restart counts for the container app environment at start and end of time window.
            If no data is returned then it may mean no log processor pod status data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetLogProcessorPodStatus(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetLogProcessorPodStatus(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppWorkloadProfile)]
        [Description(
            @"Get type of Container App Workload Profile for the container app environment at start and end of time window.
            If no data is returned then it may mean no container app workload profile data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetContainerAppWorkloadProfile(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app or job name.")] string containerAppOrJobName)
        {
            return _plugin.GetContainerAppWorkloadProfile(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                containerAppOrJobName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetInputPressureOnLogProcessor)]
        [Description(
            @"Get Input Pressure on Log Processor for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.

            What this metric measures: The query calculates the total records input to log-processor.

            When it is applicable: Anomaly in this indicates high resource pressure on the log-processor.
        ")]
        public Task<string> GetInputPressureOnLogProcessor(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _managedClusterPlugin.GetGenericMetricCountData(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName,
                "fluentbit_input_records_total",
                0);
        }


        [KernelFunction(KernelFunctionNames.ACA.GetMemoryPressureOnFluentbit)]
        [Description(
            @"Get Memory Pressure on Fluentbit for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.

            What this metric measures: The query calculates the total input storage memory used by fluentbit in bytes.

            When it is applicable: Anomaly in this indicates high memory resource pressure on the fluentbit
        ")]
        public Task<string> GetMemoryPressureOnFluentbit(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _managedClusterPlugin.GetGenericMetricCountData(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName,
                "fluentbit_input_storage_memory_bytes",
                0);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetFluentbitOutputCount)]
        [Description(
            @"Get count of output processed by Fluentbit for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.

            What this metric measures: The query calculates the total output records processed by fluentbit.

            When it is applicable: Significant drop in the value indicates flunetbit having issues. Manunal investigation is required.
        ")]
        public Task<string> GetFluentbitOutputCount(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _managedClusterPlugin.GetGenericMetricCountData(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName,
                "fluentbit_output_proc_records_total",
                0);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetFluentbitBufferPressure)]
        [Description(
            @"Get buffer pressure experienced by Fluentbit for the managed Kubernetes cluster, segmented by node or VMSS over a specified time range.

            What this metric measures: The query calculates input storage buffer overflow for fluentbit.

            When it is applicable: Existence of this metric indicates that input storage has exceeded its configured limit. No records indicate healthy.
        ")]
        public Task<string> GetFluentbitBufferPressure(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _managedClusterPlugin.GetGenericMetricCountData(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName,
                "fluentbit_input_storage_overlimit",
                0);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetFluentbitOutputErrors)]
        [Description(
            @"Get any output errors faced by Fluentbit for the customer's container app or job in the managed Kubernetes cluster.
            What this metric measures: The query calculates the total output errors for the customer's container app or job experienced by fluentbit.
            When it is applicable: Existence of this metric indicates that fluentbit is having issues in processing the output. Manual investigation is required."
        )]
        public Task<string> GetFluentbitOutputErrors(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetFluentbitOutputErrors(
                region.NormalizeLocation(),
                fromDate,
                toDate,
                managedClusterName);
        }
    }
}
