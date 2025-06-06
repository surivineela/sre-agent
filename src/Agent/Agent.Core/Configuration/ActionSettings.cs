// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Configuration
{
    public class ActionSettings
    {
        // 'system' for system managed identity
        // or resource id of user assigned managed identity
        public string? Identity { get; set; }
        public ActionMode? Mode { get; set; } = ActionMode.Review;
    }
}
