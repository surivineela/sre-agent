// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents.Request;

/// <summary>
/// Configuration for an auth scope with its environment variable name
/// </summary>
public class AuthScopeConfig
{
    /// <summary>
    /// The Azure AD scope URL (e.g., "https://management.azure.com/.default")
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The environment variable name to store the token (e.g., "AZURE_ARM_TOKEN")
    /// </summary>
    public string VariableName { get; set; } = string.Empty;
}

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

    /// <summary>
    /// Whether Azure Identity authentication is enabled for this tool
    /// </summary>
    public bool AuthEnabled { get; set; }

    /// <summary>
    /// List of Azure AD scopes configured for this tool with their variable names
    /// </summary>
    public List<AuthScopeConfig> AuthScopes { get; set; } = new();
}
