// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Link-specific tool specification for YAML configurations (V2).
    /// Extends the base tool spec with Link-specific properties.
    /// </summary>
    public class LinkToolSpecV2 : ToolSpecV2
    {
        [YamlMember(Alias = "template", Order = 10)]
        public string? Template { get; set; }
    }
}
