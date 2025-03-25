using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;

namespace Agent.Plugins.Mocks
{
    public class MockContainerAppPlugin : IContainerAppPlugin
    {
        public Task<bool> CreateOrUpdateNSGRuleAsync(string nsgResourceId, SecurityRuleData rule)
        {
            throw new NotImplementedException();
        }

        public Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetAllNSGRulesForContainerAppAsync(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<ContainerAppDescriptor> GetContainerAppInfoAsync(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveNSGRuleAsync(string nsgResourceId, string ruleName)
        {
            throw new NotImplementedException();
        }

        public Task<string> RestartContainerApp(string appResourceId, string revisionName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ScaleContainerApp(string resourceId, string desiredMemory, int minReplicas, int maxReplicas)
        {
            throw new NotImplementedException();
        }
    }
}
