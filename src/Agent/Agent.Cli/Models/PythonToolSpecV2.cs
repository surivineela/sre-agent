// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Python-specific tool specification for YAML configurations (V2).
    /// Extends the base tool spec with Python-specific properties.
    /// </summary>
    public class PythonToolSpecV2 : ToolSpecV2
    {
        [YamlMember(Alias = "functionCode", Order = 10, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? FunctionCode { get; set; }

        [YamlMember(Alias = "timeoutSeconds", Order = 11, DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
        public int TimeoutSeconds { get; set; } = Constants.PythonToolDefaultTimeoutSeconds;

        [YamlMember(Alias = "dependencies", Order = 12, DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
        public List<string>? Dependencies { get; set; }

        [YamlMember(Alias = "authEnabled", Order = 13, DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
        public bool AuthEnabled { get; set; } = false;

        [YamlMember(Alias = "authScopes", Order = 14, DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
        public List<string>? AuthScopes { get; set; }
    }
}
