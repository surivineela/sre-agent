// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class ActionSettings
    {
        // 'system' for system managed identity
        // or resource id of user assigned managed identity
        public string? Identity { get; set; }
        public string? Mode { get; set; }
    }
}
