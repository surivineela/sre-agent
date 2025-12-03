// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents.Request;

/// <summary>
/// Request model for generating a Python function tool from user intent
/// </summary>
public class GeneratePythonToolRequest
{
    /// <summary>
    /// Natural language description of what the tool should do
    /// </summary>
    public string Intent { get; set; } = string.Empty;

    /// <summary>
    /// Optional suggested name for the tool
    /// </summary>
    public string? SuggestedName { get; set; }

    /// <summary>
    /// Optional timeout in seconds (default 120)
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Optional existing function code to improve/fix (for regeneration scenarios)
    /// </summary>
    public string? ExistingCode { get; set; }
}
