// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class DocsSettings
    {
        [Required]
        public string AccountName { get; set; } = string.Empty;

        [Required]
        public string Database { get; set; } = string.Empty;

        public string DomainSuffix { get; set; } = "documents.azure.com";
    }
}

