using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;

namespace Agent.Plugins.Mocks
{
    public class MockRemediationPlugin : IRemediationPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly MockApprovalPlugin _approvalPlugin;
        private readonly MockArmPlugin _armPlugin;

        public MockRemediationPlugin(TimeProvider timeProvider, IApprovalPlugin approvalPlugin, IArmPlugin armPlugin)
        {
            _timeProvider = timeProvider;
            _approvalPlugin = (MockApprovalPlugin) approvalPlugin;
            _armPlugin = (MockArmPlugin) armPlugin;
        }

        public async Task<RemediationResult> ScaleAppServicePlanVertically(string resourceId)
        {
            if (!_approvalPlugin.ApprovedOperations.Contains("ScaleAppServicePlan"))
            {
                return new RemediationResult(
                    Success: false,
                    Action: "ScaleAppServicePlan", 
                    Details: $"No approval found for scaling App Service Plan for resource {resourceId}.",
                    OperationId: null,
                    FinishedTime: _timeProvider.GetUtcNow().DateTime);
            }

            return new RemediationResult(
                Success: true,
                Action: "ScaleAppServicePlan",
                Details: $"Successfully initiated scaling of App Service Plan for {resourceId}.",
                OperationId: Guid.NewGuid().ToString(),
                FinishedTime: _timeProvider.GetUtcNow().DateTime);
        }

        public async Task<RemediationResult> CollectMemoryDump(string resourceId)
        {
            if (!_approvalPlugin.ApprovedOperations.Contains("CollectMemoryDump"))
            {
                return new RemediationResult(
                    Success: false,
                    Action: "CollectMemoryDump",
                    Details: $"No approval found for collecting memory dump from {resourceId}.",
                    OperationId: null,
                    FinishedTime: _timeProvider.GetUtcNow().DateTime);
            }

            return new RemediationResult(
                Success: true,
                Action: "CollectMemoryDump",
                Details: $"Memory dump collection initiated for {resourceId}. Memory dump will be available in the storage account.",
                OperationId: Guid.NewGuid().ToString(),
                FinishedTime: _timeProvider.GetUtcNow().DateTime);
        }

        public async Task<RemediationResult> RestartWebApp(string resourceId)
        {
            if (!_approvalPlugin.ApprovedOperations.Contains("RestartWebApp"))
            {
                return new RemediationResult(
                    Success: false,
                    Action: "RestartWebApp",
                    Details: $"No approval found for restarting Web App {resourceId}.",
                    OperationId: null,
                    FinishedTime: _timeProvider.GetUtcNow().DateTime);
            }

            bool success = await _armPlugin.RestartWebApp(resourceId);
            
            return new RemediationResult(
                Success: success,
                Action: "RestartWebApp",
                Details: success ? $"Web App {resourceId} restarted successfully." : $"Failed to restart Web App {resourceId}.",
                OperationId: success ? Guid.NewGuid().ToString() : null,
                FinishedTime: _timeProvider.GetUtcNow().DateTime);
        }

        public async Task<RemediationResult> SuggestNextSku(string resourceId, string direction, string currentSku)
        {
            throw new NotImplementedException();
        }

        public async Task<RemediationResult> CalculateScalingCost(string resourceId, string direction, string currentSku, string targetSku)
        {
            throw new NotImplementedException();
        }
    }
}
