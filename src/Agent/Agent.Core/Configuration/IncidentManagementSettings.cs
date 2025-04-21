// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class IncidentManagementSettings
    {
        [Required]
        public string Kind { get; set; } = "PagerDuty";
    }
}
