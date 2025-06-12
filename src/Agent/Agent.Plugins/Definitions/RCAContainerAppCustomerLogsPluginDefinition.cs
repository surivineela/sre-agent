using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.SemanticKernel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppCustomerLogsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppCustomerLogsPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomerLogConfiguration", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "customerSubscriptionId", customerSubscriptionId.ToString() },
                { "managedEnvironmentName", managedEnvironmentName },
                { "managedClusterName", managedClusterName }
            });
        }

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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorErrors", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "containerAppOrJobName", containerAppOrJobName }
            });
        }

        [Description(
            @"Get list of Event Processor Leader Election Events for the container app environment at start and end of time window.
            If no data is returned then it may mean no leader election event happened during the interval or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetEventProcessorLeaderElectionEvents(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorLeaderElectionEvents", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description(
            @"Get list of Apps and Jobs Volume for the container app environment at start and end of time window.
            If no data is returned then it may mean no apps and jobs volume data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetAppsAndjobsVolumeForEnv(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetAppsAndjobsVolumeForEnv", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }


        [Description(
            @"Get list of Event Processor Pods for the container app environment at start and end of time window.
            If no data is returned then it may mean no event processor pods are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetEventProcessorPods(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodsWithPrefix", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-event-processor" }
            });
        }


        [Description(
            @"Get list of Log Processor Pods for the container app environment at start and end of time window.
            If no data is returned then it may mean no log processor pods are present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetLogProcessorPods(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodsWithPrefix", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-log-processor" }
            });
        }


        [Description(
            @"Get list of Event Processor Pod Status for the container app environment at start and end of time window.
            If no data is returned then it may mean no event processor pod status data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetEventProcessorPodStatus(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-event-processor" },
                { "podNamespace", "k8se-system" }
            });
        }

        [Description(
            @"Get list of Log Processor Pod Status for the container app environment at start and end of time window.
            If no data is returned then it may mean no log processor pod status data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetLogProcessorPodStatus(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-log-processor" },
                { "podNamespace", "k8se-system" }
            });
        }

        [Description(
            @"Get type of Container App Workload Profile for the container app environment at start and end of time window.
            If no data is returned then it may mean no container app workload profile data is present or there is an issue with the provided inputs. Please ensure those are correct otherwise re-run the tool.")]
        public Task<string> GetContainerAppWorkloadProfile(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app or job name.")] string containerAppOrJobName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppWorkloadProfile", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "containerAppOrJobName", containerAppOrJobName }
            });
        }

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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_input_records_total" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }


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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_input_storage_memory_bytes" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_output_proc_records_total" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_input_storage_overlimit" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

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
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetFluentbitOutputErrorsForApp", region,
            new Dictionary<string, string> {
                { "region", region.ToString() },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_output_errors_total" }
            });
        }

        private static string GetDuration(DateTime fromDate, DateTime toDate)
        {
            var totalHours = (toDate - fromDate).TotalHours;
            var totalDays = (toDate - fromDate).TotalDays;
            // Use the lowest frequency possible for the given range
            if (totalDays > 5)
            {
                return "1d";
            }
            if (totalHours > 24)
            {
                return "1h";
            }
            return "1m";
        }
    }
}
