// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class AzureSearchSettings
    {
        public string SearchServiceUri { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
        public string UserAssignedMIClientId { get; set; } = string.Empty;
        public string SearchApiKeyOverride { get; set; } = string.Empty;
    }
}

