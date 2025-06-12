using Agent.Core.Helpers;
using Azure.ResourceManager.AppService.Models;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Interface
{
    public interface IReliabilityPlugin
    {
        Task<string> UpdateAlwaysOn(string resourceId, bool enabled);

        Task<string> UpdateHealthCheck(string resourceId, string healthCheckPath);

        Task<string> UpdateAutoHeal(string resourceId, bool autoHealEnabled, AutoHealRules autoHealRules);

        Task<string> UpdateHostWorkers(string resourceId, int numberOfWorkers);

        Task<string> GetReliabilityStatus(string resourceId);

        Task<string> GetReliabilityStatusForSubscriptions(CancellationToken cancellationToken = default);

        Task<string> GetAppsToMonitor(CancellationToken cancellationToken = default);

        Task<OrchestrationRuntimeStatus> GetReliabilityOrchestrationStatus(string instanceId);
    }
}
