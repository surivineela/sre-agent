// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins
{
    public class MIConfigurationCheckPluginDefinition
    {
        private readonly IMIConfigurationCheckPlugin _miConfigurationCheckPlugin;

        public MIConfigurationCheckPluginDefinition(IMIConfigurationCheckPlugin miConfigurationCheckPlugin)
        {
            _miConfigurationCheckPlugin = miConfigurationCheckPlugin;
        }

        [Description("Checks if an app service connects to a SQL server and whether it uses password or managed identity for authentication.")]
        public async Task<SqlConnectionDescriptor> CheckSqlConnectionTypeAsync(
            [Description("The resource ID of the App Service.")]
            string resourceId)
        {
            return await _miConfigurationCheckPlugin.CheckSqlConnectionTypeAsync(resourceId);
        }

        [Description("Helps find Azure Resource ID for the SQL Server being used by a webapp")]
        public async Task<string> CheckSqlResourceIdForAppAsync(
            [Description("The resource ID of the webapp or App Service.")]
            string resourceId)
        {
            return await _miConfigurationCheckPlugin.CheckSqlResourceIdForAppAsync(resourceId);
        }
    }
}

