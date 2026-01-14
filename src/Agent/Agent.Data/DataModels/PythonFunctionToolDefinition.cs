// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Data.DataModels;
using Agent.Framework;
using YamlDotNet.Serialization;
namespace Agent.Data.Tools
{
    /// <summary>
    /// YAML tool definition for Python function tools.
    /// </summary>
    public class PythonFunctionToolDefinition : YamlToolDefinitionBase
    {
        [YamlMember(Alias = "function_code")]
        public string FunctionCode { get; set; } = string.Empty;

        [YamlMember(Alias = "timeout_seconds")]
        public int TimeoutSeconds { get; set; } = Constants.PythonToolDefaultTimeoutSeconds;

        [YamlMember(Alias = "dependencies")]
        public List<string> Dependencies { get; set; } = new List<string>();

        [YamlMember(Alias = "auth_enabled")]
        public bool AuthEnabled { get; set; } = false;

        [YamlMember(Alias = "auth_scopes")]
        public List<string>? AuthScopes { get; set; }

        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(FunctionCode))
                throw new ArgumentException("Python tool must define 'function_code'.");

            if (TimeoutSeconds < 5 || TimeoutSeconds > 900)
                throw new ArgumentException("Timeout must be between 5 and 900 seconds.");

            // Basic validation: function_code should contain 'def main'
            if (!FunctionCode.Contains("def main"))
                throw new ArgumentException("Python function_code must contain a 'main' function definition.");

        }
    }
}
