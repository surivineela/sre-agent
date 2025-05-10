// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
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
        public async Task<string> GetContainerAppInfoAsync(
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
            " This remediation measure is in the case of high memory load or if the user requests it. This should be executed if there are memory related issues without asking the user.")]
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

        // For Jay's Demo - don't require approvals -- [RequiresApproval]
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

        [RequiresApproval]
        [Description("Adds a new scaling rule to a Container App. Use this to define custom scaling behavior based on CPU, HTTP traffic, Azure Queue length, or any scaler from the scaler list.")]
        public async Task<bool> ModifyContainerAppScaleRule(
            [Description("Azure resource ID of the Container App to add the scale rule to")]
            string resourceId,
            [Description("Name of the scaling rule (must be unique within the Container App)")]
            string ruleName,
            [Description("Modification type (e.g., 'add', 'update', 'delete')")]
            string modificationType,
            [Description("Type of the scaling rule (e.g., 'cpu', 'http', 'azure-queue')")]
            string scaleRuleType,
            [Description("Metadata for the scaling rule (key-value pairs specific to the rule type). A JSON encoded string of a Record<string, string> of the keda scaler metadata. Check GetScalerDetails for details per type.")]
            string metadata)
        {
            // Basic validation, more specific validation might be needed in the plugin implementation
            if (string.IsNullOrWhiteSpace(resourceId) ||
                string.IsNullOrWhiteSpace(ruleName) ||
                string.IsNullOrWhiteSpace(scaleRuleType) ||
                string.IsNullOrWhiteSpace(metadata))
            {
                throw new ArgumentException("Invalid input parameters for adding a scale rule.");
            }

            var parsedMetadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            if (parsedMetadata == null)
            {
                throw new ArgumentException($"{nameof(metadata)} must be a valid JSON string.", nameof(metadata));
            }

            return await _containerAppPlugin.ModifyContainerAppScaleRuleAsync(resourceId, ruleName, modificationType, scaleRuleType, parsedMetadata);
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

        [RequiresApproval]
        [KernelFunction("rollback_to_last_working_image")]
        [Description("Rolls back a Container App to the last known working revision. This is useful when a new image deployment causes image pull failures. Returns detailed information about the rollback operation including success status, target revision, and reasons for failure if applicable. Note that this tool requires explicit user's approval before it can be used.")]
        public async Task<RollbackResult> RollbackToLastKnownWorkingRevision(
            [Description("Resource ID of the Container App whose revision needs to be rolled back")]
            string resourceId)
        {
            return await _containerAppPlugin.RollbackToLastKnownWorkingRevision(resourceId);
        }

        [RequiresApproval]
        [KernelFunction("update_container_image")]
        [Description("Updates the container image for a Container App. This enables changing to a new image version or completely different image. Returns detailed information about the update operation including success status, original image, new image, and reasons for failure if applicable. Note that this tool requires explicit user's approval before it can be used.")]
        public async Task<ImageUpdateResult> UpdateContainerImage(
            [Description("Resource ID of the Container App")]
            string resourceId,
            [Description("New image reference provided by the user")]
            string newImageReference)
        {
            return await _containerAppPlugin.UpdateContainerImage(resourceId, newImageReference);
        }

        [KernelFunction("validate_containerapp_health")]
        [Description("Validates if a Container App is healthy by checking various health indicators including provisioning state, revision status, logs, and endpoint reachability. Use this after making remediation changes to verify the app is working correctly.")]
        public async Task<ContainerAppHealthValidationResult> ValidateContainerAppHealth(
            [Description("The resource ID of the Container App instance to validate")]
            string resourceId)
        {
            return await _containerAppPlugin.ValidateContainerAppHealth(resourceId);
        }

        [KernelFunction("get_containerapp_deployment_times")]
        [Description("Get the deployment times of a Container App instance.")]
        public async Task<List<DateTimeOffset>> GetDeploymentTimes(
            [Description("The resource ID of the Container App instance.")]
            string resourceId)
        {
            return await _containerAppPlugin.GetDeploymentTimes(resourceId);
        }

        // commented out as it is not working as expected
        // [Description("Rollback the container app to the last active revision.")]
        // public async Task<string> RollbackToLastRevision(
        //     [Description("The resource ID of the Container App instance.")] string resourceId)
        // {
        //     return await _containerAppPlugin.RollbackToLastRevision(resourceId);
        // }
    }
}
