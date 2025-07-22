// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------



namespace Agent.Runtime.Reasoning.Models
{
    public class YamlMetadata
    {
        public string? Owner { get; set; }

        public string? Version { get; set; }

        public List<string>? Tags { get; set; }

        public DateTime? LastUpdated { get; set; }
    }

}
