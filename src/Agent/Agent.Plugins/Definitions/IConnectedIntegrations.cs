// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IConnectedIntegrationsPlugin
    {
        /// <summary>
        /// Retrieves a list of all currently active integrations and their configuration details.
        /// </summary>
        List<IntegrationInfo> GetAllActiveIntegrations();
    }
}
