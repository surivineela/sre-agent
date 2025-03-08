using Agent.Core.Models;
using Kusto.Cloud.Platform.Utils;

namespace Agent.Plugins.Mocks
{
    public class MockArmPlugin : IArmPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly MockApprovalPlugin _approvalPlugin;
        private readonly Dictionary<string, TlsStatus> _tlsStatuses = new();

        public MockArmPlugin(TimeProvider timeProvider, MockApprovalPlugin approvalPlugin)
        {
            _timeProvider = timeProvider;
            _approvalPlugin = approvalPlugin;
        }

        public void ConfigureTlsStatus(
            IReadOnlyDictionary<string, TlsStatus> tlsStatuses)
        {
            _tlsStatuses.Clear();
            _tlsStatuses.AddOrSetRange(tlsStatuses);
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

            _tlsStatuses[appResourceId] = _tlsStatuses[appResourceId] with
            {
                MinimumTlsVersion = minimumTlsVersion
            };
            var msg = $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {_timeProvider.GetUtcNow():o}";
            return Task.FromResult(msg);
        }

        public Task<bool> RestartWebApp(string appResourceId)
        {
            return Task.FromResult(true);
        }
    }
}
