// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface IContainerAppPlugin
    {
        Task<string> GetContainerAppInfoAsync(string resourceId);

        Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId);

        Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId);

        Task<IReadOnlyList<RevisionInfo>> ListContainerAppRevisionsAsync(string resourceId);

        Task<string> RestartContainerApp(string appResourceId, string revisionName);

        Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(string resourceId);

        Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(string resourceId);

        Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(string resourceId);

        Task<IDictionary<string, string>> GetAllNSGRulesForContainerAppAsync(string resourceId);

        Task<bool> ScaleContainerApp(string resourceId, string desiredMemory, int minReplicas, int maxReplicas);

        Task<string> GetContainerAppLogsAsync(string resourceId, string? revisionName = null);

        Task<bool> UpdateTargetPort(string resourceId, int targetPort);

        IReadOnlyList<string> ListAvailableScalers();

        Task<string> GetScalerDetails(string scalerName);

        Task<string> GetImageReferenceFromResourceId(string resourceId);

        Task<bool> VerifyExternalRegistryAsync(string resourceId, string imageReference);

        Task<RollbackResult> RollbackToLastKnownWorkingRevision(string resourceId);

        Task<string> GetContainerMemoryAnalysisForDotnet(string resourceId);
        Task<bool> IsDotnetBased(string resourceId);
        // Task<string> RollbackToLastRevision(string resourceId);
        Task<ImageUpdateResult> UpdateContainerImage(string resourceId, string newImageReference);

        Task<ContainerAppHealthValidationResult> ValidateContainerAppHealth(string resourceId);

        Task<bool> ModifyContainerAppScaleRuleAsync(string resourceId, string ruleName, string modificationType, string scaleRuleType, IDictionary<string, string> metadata);
        Task<List<DateTimeOffset>> GetDeploymentTimes(string resourceId);
    }
}
