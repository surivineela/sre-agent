using Agent.Core.Models;
using System.Collections.Immutable;

namespace Agent.Plugins.Mocks
{
    public class MockArmPlugin : IArmPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly MockApprovalPlugin _approvalPlugin;
        private IReadOnlyDictionary<string, TlsStatus> _tlsStatuses = ImmutableDictionary<string, TlsStatus>.Empty;

        public MockArmPlugin(TimeProvider timeProvider, MockApprovalPlugin approvalPlugin)
        {
            _timeProvider = timeProvider;
            _approvalPlugin = approvalPlugin;
        }

        public void ConfigureTlsStatus(
            IReadOnlyDictionary<string, TlsStatus> tlsStatuses)
        {
            _tlsStatuses = tlsStatuses;
        }

        public Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion)
        {
            if (!_tlsStatuses.ContainsKey(appResourceId))
            {
                throw new ArgumentException($"Resource {appResourceId} not found");
            }

            if (!_approvalPlugin.ApprovedOperations.Contains("UpdateTls"))
            {
                throw new Exception("No approval found for TLS update for resource {appResourceId}.");
            }

            _tlsStatuses[appResourceId].MinimumTlsVersion = minimumTlsVersion;
            var msg = $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {_timeProvider.GetUtcNow():o}";
            return Task.FromResult(msg);
        }

        public Task<bool> RestartWebApp(string appResourceId)
        {
            return Task.FromResult(true);
        }
    }
}
