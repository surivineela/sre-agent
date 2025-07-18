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
        public string AccountUrl { get; set; } = string.Empty;
        public string IcmAlertConfigsContainerName { get; set; } = string.Empty;
        public string GenevaActionsContainerName { get; set; } = string.Empty;
        public string SreAgentHelperContainerName { get; set; } = string.Empty;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
    }
}
