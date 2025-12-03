// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Web.Models.ExtendedAgents.Request;

/// <summary>
/// Request model for testing a Python function tool
/// </summary>
public class PythonToolTestRequest
{
    /// <summary>
    /// The Python function code to execute (must contain 'def main')
    /// </summary>
    public string FunctionCode { get; set; } = string.Empty;

    /// <summary>
    /// Timeout in seconds (5-900)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Parameters to pass to the main() function
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Dependencies (package names) - future use
    /// </summary>
    public List<string>? Dependencies { get; set; }

    /// <summary>
    /// Parameter definitions extracted from function signature (optional)
    /// </summary>
    public List<YamlParameter> ParameterDefinitions { get; set; } = new();
}
