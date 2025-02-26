using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Kusto.Data.Common.Impl;

namespace Agent.Plugins.Mocks
{
    public class MockArmPlugin : IArmPlugin
    {
        private readonly TimeProvider _timeProvider;
        public readonly Dictionary<string, TlsStatus> TlsStatuses = new();

        public MockArmPlugin(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public MockArmPlugin(TimeProvider timeProvider, List<TlsStatus> tlsStatuses) : this(timeProvider)
        {
            TlsStatuses = tlsStatuses.ToDictionary(k => k.ResourceId);
        }

        public Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion)
        {
            if (!TlsStatuses.ContainsKey(appResourceId))
            {
                throw new ArgumentException($"Resource {appResourceId} not found");
            }

            TlsStatuses[appResourceId].MinimumTlsVersion = minimumTlsVersion;
            var msg = $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {_timeProvider.GetUtcNow():o}";
            return Task.FromResult(msg);
        }
    }
}
