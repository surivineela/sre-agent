// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class JavaProfilerSettings
    {
        [Required]
        public string DebugProfileContainer { get; set; } = string.Empty;

        [Required]
        public int ProfileTimeoutMinutes { get; set; } = 5;

        [Required]
        public int MaxDebugContainers { get; set; } = 5;
    }
}
