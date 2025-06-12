// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins.Interface
{
    public interface IConnectedIntegrationsPlugin
    {
        /// <summary>
        /// Retrieves a list of all currently active integrations and their configuration details.
        /// </summary>
        List<IntegrationInfo> GetAllActiveIntegrations();
    }
}
