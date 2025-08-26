using Agent.Plugins.Interface;
using Azure.ResourceManager.AppService.Models;

namespace Agent.Plugins.Mocks;
public class MockReliabilityPlugin : IReliabilityPlugin
{
    public Task<string> GetAppsToMonitor(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetReliabilityStatus(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetReliabilityStatusForSubscriptions(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> UpdateAlwaysOn(string resourceId, bool enabled)
    {
        throw new NotImplementedException();
    }

    public Task<string> UpdateAutoHeal(string resourceId, bool autoHealEnabled, AutoHealRules autoHealRules)
    {
        throw new NotImplementedException();
    }

    public Task<string> UpdateHealthCheck(string resourceId, string healthCheckPath)
    {
        throw new NotImplementedException();
    }

    public Task<string> UpdateHostWorkers(string resourceId, int numberOfWorkers)
    {
        throw new NotImplementedException();
    }
}
