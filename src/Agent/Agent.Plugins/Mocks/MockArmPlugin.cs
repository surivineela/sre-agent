// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Models;
using Kusto.Cloud.Platform.Utils;

namespace Agent.Plugins.Mocks
{
    public class MockArmPlugin : IArmPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly MockApprovalPlugin _approvalPlugin;
        private readonly Dictionary<string, TlsStatus> _tlsStatuses = new();
        private readonly Dictionary<string, AppReliability> _reliabilityStatuses = new();

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

        public string GetTlsStatus(string appResourceId)
        {
            if (!_tlsStatuses.ContainsKey(appResourceId))
            {
                throw new ArgumentException($"Resource {appResourceId} not found");
            }
            var status = _tlsStatuses[appResourceId];
            return status.MinimumTlsVersion;
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

        public void ConfigureReliability(
            IReadOnlyDictionary<string, AppReliability> statuses)
        {
            _reliabilityStatuses.Clear();
            _reliabilityStatuses.AddOrSetRange(statuses);
        }

        public Tuple<bool, bool, bool, int> GetAppReliability(string appResourceId)
        {
            if (!_tlsStatuses.ContainsKey(appResourceId))
            {
                throw new ArgumentException($"Resource {appResourceId} not found");
            }
            var ar = _reliabilityStatuses[appResourceId];
            var status = new Tuple<bool, bool, bool, int>(ar.AlwaysOnEnabled, ar.HealthCheckEnabled, ar.AutoHealEnabled, ar.NumberOfWorkers);
            return status;
        }

        public Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CheckIfResourceExists(string appResourceId)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetArmResourceAsJson(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<RemediationResult> PowerOnVirtualMachine(string resourceId)
        {
            return Task.FromResult(new RemediationResult(
                    Success: true,
                    Action: "Power On Azure Virtual Machine",
                    Details: "Virtual machine powered on successfully",
                    OperationId: null,
                    FinishedTime: DateTime.Now));
        }

        public Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(string resourceId)
        {
            return Task.FromResult((IReadOnlyDictionary<string, string>)new Dictionary<string, string>());
        }

        public Task<string> CheckConnectivity(string resourceId, string source, string destination, string destinationPort)
        {
            return Task.FromResult<string>("true");
        }

        public Task<string> CheckTcpConnectivity(string resourceId, string host, int port)
        {
            return Task.FromResult<string>("true");
        }
    }
}

