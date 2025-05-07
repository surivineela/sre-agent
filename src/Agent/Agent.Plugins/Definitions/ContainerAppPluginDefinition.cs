// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    public class ContainerAppPluginDefinition
    {
        private readonly IContainerAppPlugin _containerAppPlugin;

        public ContainerAppPluginDefinition(IContainerAppPlugin containerAppPlugin)
        {
            _containerAppPlugin = containerAppPlugin;
        }

        [KernelFunction("get_container_app")]
        [Description(
            "PREFERRED METHOD FOR CONTAINER APP DETAILS: Gets detailed information about a specific Azure Container App by its resource ID. " +
            "Returns a ContainerAppDescriptor with resource ID, name, location, state, workload profile, FQDN, AppHealthInfo, and environment details. " +
            "Always use this specialized method for Container Apps instead of generic resource search functions for more complete and accurate information." +
            "For the AppHealthInfo information (such requests, cpu, and memory metrics, cost etc. format the output in markdown tabular format.")]
        public async Task<ContainerAppDescriptor> GetContainerAppInfoAsync(
            [Description(
                "The full Azure resource ID of the Container App (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.App/containerApps/{appName}).")]
            string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppInfoAsync(resourceId);
        }

        [Description("List all revisions for a container app by its resource ID.")]
        public async Task<IReadOnlyList<RevisionInfo>> ListRevisionsAsync(
            [Description(
                "The full Azure resource ID of the Container App (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.App/containerApps/{appName}).")]
            string resourceId)
        {
            return await _containerAppPlugin.ListContainerAppRevisionsAsync(resourceId);
        }

        [KernelFunction("get_latest_containerapp_revision")]
        [Description("Get the latest active revision for a Container App instance")]
        public async Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            return await _containerAppPlugin.GetLatestRevisionAsync(resourceId);
        }

        [KernelFunction("list_container_apps")]
        [Description(
            "PREFERRED METHOD FOR CONTAINER APPS: Lists all Azure Container Apps in the specified subscription. " +
            "Returns detailed ContainerAppDescriptor objects with resource ID, name, location, state, workload profile, FQDN, and environment details. " +
            "This is the most direct and efficient way to get Container App information - use this instead of generic resource search methods. Returns an empty list if no Container Apps are found.")]
        public async Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(
            [Description("The subscription ID (GUID) to scan for Container Apps.")]
            Guid subscriptionId)
        {
            return await _containerAppPlugin.ListContainerAppsAsync(subscriptionId);
        }

        [RequiresApproval]
        [KernelFunction("restart_containerapp_revision")]
        [Description("Restarts a container app. Use this to restart a container app to resolve transient issues that may be fixed by restarting the instance.")]
        public async Task<string> RestartContainerApp(
            [Description("The resource ID of the Container App.")]
            string appResourceId,
            [Description("Container App revision name to restart.")]
            string revisionName)
        {
            return await _containerAppPlugin.RestartContainerApp(appResourceId, revisionName);
        }

        #region Metrics

        [KernelFunction("get_containerapp_request_count_metrics")]
        [Description(
            "Start a background operation to get the total request count metrics of a specific Container App instance at per minute granularity" +
            " for the past 30 minutes, Container App is healthy if all data points are at least 99.9 availability.")]
        public async Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(
            [Description("The resource ID of the ContainerApp resource.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppRequestMetrics(resourceId);
        }

        [KernelFunction("get_containerapp_memory_metrics")]
        [Description(
            "Start a background operation to get the average memory usage of a specific Container App instance at per minute granularity for the past 30 minutes," +
            " Container App is healthy if over half of the data points is less than 20% memory utilization.")]
        public async Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(
            [Description("The resource ID of the ContainerApp resource.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppMemoryMetrics(resourceId);
        }

        [KernelFunction("check_if_containerapp_is_dotnet")]
        [Description(
            "Start a background operation to check if the container app is dotnet based.")]
        public async Task<bool> IsContainerAppDotnet(
            [Description("The resource ID of the ContainerApp resource.")]
            string resourceId)
        {
            return await _containerAppPlugin.IsDotnetBased(resourceId);
        }

        [KernelFunction("get_containerapp_memory_analysis_dotnet")]
        [Description(
            "Start a background operation to get an in-depth memory analysis for .NET Apps of the App instance." +
            " This remediation measure is in the case of high memory load or if the user requests it.")]
        public async Task<string> GetContainerMemoryAnalysisForDotnet(
            [Description("The resource ID of the ContainerApp resource.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetContainerMemoryAnalysisForDotnet(resourceId);
        }

        [KernelFunction("get_containerapp_cpu_metrics")]
        [Description(
            "Get the average CPU utilization metrics of a specific Container App instance at per minute granularity" +
            " for the past 30 minutes, Container App is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy")]
        public async Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(
            [Description("The resource ID of the ContainerApp resource.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppCpuMetrics(resourceId);
        }

        #endregion

        #region NSG Plugins

        [KernelFunction("get_containerapp_nsg_rules")]
        [Description(
            "Retrieves all Network Security Groups (NSGs) associated with a Container App and their security rules. " +
            "Returns a dictionary where keys are NSG resource IDs and values are lists of security rules. " +
            "Use this to identify network access issues or restrictive rules that might be blocking traffic to/from the Container App.")]
        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetAllNSGRulesForContainerAppAsync(
            [Description("The resource ID of the Container App instance.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetAllNSGRulesForContainerAppAsync(resourceId);
        }

        #endregion

        [RequiresApproval]
        [KernelFunction("scale_container_app")]
        [Description(
            "Scales a Container App by adjusting its memory allocation and replica count. Use this to resolve performance or availability issues by increasing resources or scaling out the application.")]
        public async Task<bool> ScaleContainerApp(
            [Description("Azure resource ID of the Container App to scale")]
            string resourceId,
            [Description("Desired memory allocation (e.g., '1Gi', '512Mi')")]
            string desiredMemory,
            [Description("Minimum number of replicas to run (e.g., 1)")]
            int minReplicas,
            [Description("Maximum number of replicas to scale to (e.g., 10)")]
            int maxReplicas)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(desiredMemory) || minReplicas < 0 ||
                maxReplicas < minReplicas)
            {
                throw new ArgumentException("Invalid input parameters.");
            }

            return await _containerAppPlugin.ScaleContainerApp(resourceId, desiredMemory, minReplicas, maxReplicas);
        }

        [Description("Get the logs of a specific revision of a Container App instance.")]
        public async Task<string> GetRevisionLogsAsync(
            [Description("The resource ID of the Container App instance.")]
            string resourceId,
            [Description("Optional revision name. Leave empty to use the latest revision.")]
            string? revisionName)
        {
            return await _containerAppPlugin.GetContainerAppLogsAsync(resourceId, revisionName);
        }

        [Description("Get the logs of the latest revision of a Container App instance.")]
        public async Task<string> GetContainerAppLogsAsync(
            [Description("The resource ID of the Container App instance.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppLogsAsync(resourceId);
        }

        [Description("Update the target port of a Container App instance.")]
        [RequiresApproval]
        public async Task<bool> UpdateTargetPort(
            [Description("The resource ID of the Container App instance.")]
            string resourceId,
            [Description("The target port to set for the Container App instance.")]
            int targetPort)
        {
            return await _containerAppPlugin.UpdateTargetPort(resourceId, targetPort);
        }


        [Description("List available scaler names")]
        public Task<IReadOnlyList<string>> ListAvailableScalers()
        {
            return Task.FromResult(_containerAppPlugin.ListAvailableScalers());
        }

        [Description("Get the details of a specific scaler for a Container App instance.")]
        public async Task<string> GetScalerDetails(
            [Description("The scaler name to get details for.")]
            string scalerName)
        {
            return await _containerAppPlugin.GetScalerDetails(scalerName);
        }
    
        [KernelFunction("get_image_reference")]
        [Description("Gets the container image reference from a resource ID")]
        public async Task<string> GetImageReferenceFromResourceId(
            [Description("The resource ID of a Container App or Linux Web App")]
            string resourceId)
        {
            return await _containerAppPlugin.GetImageReferenceFromResourceId(resourceId);
        }

        [KernelFunction("verify_external_registry")]
        [Description("Verify connectivity to an external container registry. This is useful for checking if the Container App can pull images from the specified registry.")]
        public async Task<bool> VerifyExternalRegistry(
            [Description("Resource ID of the Container App to check")]
            string resourceId,
            [Description("Image reference whose registry is being verified (e.g. myregistry.azurecr.io/myapp:v2)")]
            string imageReference
            )
        {
            return await _containerAppPlugin.VerifyExternalRegistryAsync(resourceId, imageReference);
        }

        [KernelFunction("rollback_to_last_working_image")]
        [RequiresApproval]
        [Description("Rolls back a Container App or Linux Web App to the last known working image. This is useful when a new image deployment causes pull failures or other issues.")]
        public async Task<bool> RollbackToLastWorkingImage(
            [Description("Resource ID of the Container App or Linux Web App to roll back")]
            string resourceId)
        {
            return await _containerAppPlugin.RollbackToLastWorkingImage(resourceId);
        }

        [KernelFunction("update_container_image")]
        [RequiresApproval]
        [Description("Updates the container image for a Container App or Linux Web App. This enables changing to a new image version or completely different image.")]
        public async Task<bool> UpdateContainerImage(
            [Description("Resource ID of the Container App or Linux Web App")]
            string resourceId,
            [Description("New image reference to use (e.g. myregistry.azurecr.io/myapp:v2)")]
            string newImageReference,
            [Description("Optional container name for multi-container apps. If not specified, the first container will be updated.")]
            string containerName = null)
        {
            return await _containerAppPlugin.UpdateContainerImage(resourceId, newImageReference, containerName);
        }

        [Description("Rollback the container app to the last active revision.")]
        public async Task<string> RollbackToLastRevision(
            [Description("The resource ID of the Container App instance.")] string resourceId)
        {
            return await _containerAppPlugin.RollbackToLastRevision(resourceId); 
        }
    }
}
