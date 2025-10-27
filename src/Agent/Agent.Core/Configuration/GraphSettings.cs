// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class GraphSettings
    {
        [Required]
        public string AccountName { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string Database { get; set; } = string.Empty;

        [Required]
        public string Collection { get; set; } = string.Empty;

        public string DomainSuffix { get; set; } = "gremlin.cosmos.azure.com";
    }
}

