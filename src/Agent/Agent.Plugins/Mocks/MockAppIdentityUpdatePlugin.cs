// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks
{
    public class MockAppIdentityUpdatePlugin : IAppIdentityUpdatePlugin
    {
        private readonly MockMIConfigurationCheckPlugin _miConfigurationCheckPlugin;
        private IReadOnlyList<AppMigrationStatus> _testApps = [];

        public MockAppIdentityUpdatePlugin(MockMIConfigurationCheckPlugin miConfigurationCheckPlugin)
        {
            _miConfigurationCheckPlugin = miConfigurationCheckPlugin;
        }

        public void ConfigureTestApps(IReadOnlyList<AppMigrationStatus> testApps)
        {
            _testApps = testApps;
        }

        public Task<string> MigrateSqlToManagedIdentityAsync(string resourceId)
        {
            _miConfigurationCheckPlugin.MarkAppAsProcessed(resourceId);
            UpdateTestAppStatus(resourceId);
            return Task.FromResult("Successfully enabled webapp to use Managed Identity. Identity PrincipalId: test-principal-id.");
        }

        public Task<string> MigrateSqlToManagedIdentityAsync(string resourceId, string sqlServer, string database)
        {
            _miConfigurationCheckPlugin.MarkAppAsProcessed(resourceId);
            UpdateTestAppStatus(resourceId);
            return Task.FromResult("Successfully migrated SQL connection to use Managed Identity. Note: I need to to ensure I have SQL Admin set with Identity test-principal-id.");
        }

        public Task<string> EnableSqlAdAuthAsync(string resourceId, string servicePrincipalId)
        {
            _miConfigurationCheckPlugin.MarkAppAsProcessed(resourceId);
            UpdateTestAppStatus(resourceId);
            return Task.FromResult("Successfully enabled Azure AD auth on SQL Server and assigned the specified Service Principal.");
        }

        private void UpdateTestAppStatus(string resourceId)
        {
            var app = _testApps.FirstOrDefault(a => a.ResourceId.Equals(resourceId, StringComparison.OrdinalIgnoreCase));
            if (app != null)
            {
                app.UsesAzureSqlConnectionString = false;
                app.CurrentConnectionMethod = "Managed Identity";
                System.Diagnostics.Debug.WriteLine($"Successfully updated app status for resource ID: {resourceId}");
            }
            else
            {
                // For debugging
                System.Diagnostics.Debug.WriteLine($"Failed to find app with resource ID: {resourceId}");
                System.Diagnostics.Debug.WriteLine($"Available apps: {string.Join(", ", _testApps.Select(a => a.ResourceId))}");
                System.Diagnostics.Debug.WriteLine($"Resource ID comparison: {resourceId} vs {_testApps.FirstOrDefault()?.ResourceId}");
            }
        }
    }
}

