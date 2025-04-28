// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class IncidentManagementSettings
    {
        [Required]
        public string Type { get; set; }

        public string? ConnectionName  { get; set; }

        public string? ConnectionUrl { get; set; }

        public string? ConnectionKey { get; set; }
    }
}
