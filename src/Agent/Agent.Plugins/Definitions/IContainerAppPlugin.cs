// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;
using Azure.ResourceManager.Network;

namespace Agent.Plugins.Definitions
{
    public interface IContainerAppPlugin
    {
        Task<ContainerAppDescriptor> GetContainerAppInfoAsync(string resourceId);

        Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId);

        Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId);

        Task<string> RestartContainerApp(string appResourceId, string revisionName);

        Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(string resourceId);

        Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(string resourceId);

        Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(string resourceId);

        Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetAllNSGRulesForContainerAppAsync(string resourceId);
        
        Task<bool> CreateOrUpdateNSGRuleAsync(string nsgResourceId, SecurityRuleData rule);
            
        Task<bool> RemoveNSGRuleAsync(string nsgResourceId, string ruleName);

        Task<bool> ScaleContainerApp(string resourceId, string desiredMemory, int minReplicas, int maxReplicas);
    }
}
