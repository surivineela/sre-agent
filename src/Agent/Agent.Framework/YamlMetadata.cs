// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework.Reasoning.Models
{
    public class YamlMetadata
    {
        [YamlMember(Alias = "owner")]
        public string? Owner { get; set; }

        [YamlMember(Alias = "version")]
        public string? Version { get; set; }

        [YamlMember(Alias = "tags")]
        public List<string>? Tags { get; set; }

        [YamlMember(Alias = "updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [YamlMember(Alias = "created_at")]
        public DateTime? CreatedAt { get; set; }
    }

}
