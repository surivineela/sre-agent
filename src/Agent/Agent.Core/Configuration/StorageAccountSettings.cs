// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    /// <summary>
    /// Configuration settings for Azure Storage Account connections
    /// </summary>
    public class StorageAccountSettings
    {
        public string AccountUrl { get; set; }
        public string IcmAlertConfigsContainerName { get; set; }
        public string GenevaActionsContainerName { get; set; }
        public string SreAgentHelperContainerName { get; set; }
        public string ManagedIdentityClientId { get; set; }

    }
}
