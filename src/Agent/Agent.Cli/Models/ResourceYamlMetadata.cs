// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Resource metadata for YAML agent configurations.
    /// Contains name, owner, and tags.
    /// </summary>
    public class ResourceMetadataModel
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "owner")]
        public string? Owner { get; set; }

        [YamlMember(Alias = "tags")]
        public List<string>? Tags { get; set; }
    }
}
