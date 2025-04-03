// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Models
{
    public class KustoCluster
    {
        [Required]
        public string Region { get; set; }
        [Required]
        public string ClusterUri { get; set; }
        [Required]
        public string Database { get; set; }
    }
}

