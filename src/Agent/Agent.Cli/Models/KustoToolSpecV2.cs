// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Kusto-specific tool specification for YAML configurations (V2).
    /// Extends the base tool spec with Kusto-specific properties.
    /// </summary>
    public class KustoToolSpecV2 : ToolSpecV2
    {
        [YamlMember(Alias = "database", Order = 10)]
        public string? Database { get; set; }

        [YamlMember(Alias = "query", Order = 11, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Query { get; set; }
    }
}
