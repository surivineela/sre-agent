// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Models;

/// <summary>
/// Authentication configuration for MCP server connections.
/// </summary>
public class McpAuthenticationConfig
{
    /// <summary>
    /// The type of authentication to use.
    /// </summary>
    public McpAuthenticationType Type { get; set; } = McpAuthenticationType.None;

    /// <summary>
    /// API Key for API Key authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Header name for API Key (default: "Authorization").
    /// </summary>
    public string ApiKeyHeader { get; set; } = "Authorization";

    /// <summary>
    /// Prefix for API Key header value (e.g., "Bearer ", "ApiKey ").
    /// </summary>
    public string? ApiKeyPrefix { get; set; } = "Bearer";

    /// <summary>
    /// Bearer token for Bearer authentication.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Username for Basic authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for Basic authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Custom headers to include with every request.
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Azure ARM scope for Azure AD token authentication (e.g., "https://management.azure.com/.default").
    /// Used when Type is AzureARM.
    /// </summary>
    public string? ArmScope { get; set; }

    /// <summary>
    /// Azure Key Vault secret URI for retrieving credentials.
    /// Format: https://{vault-name}.vault.azure.net/secrets/{secret-name}/{version}
    /// </summary>
    public string? KeyVaultSecretUri { get; set; }

    /// <summary>
    /// Whether the credentials have been resolved from Key Vault.
    /// </summary>
    public bool IsResolved { get; set; } = false;
}

/// <summary>
/// Authentication types supported for MCP connections.
/// </summary>
public enum McpAuthenticationType
{
    /// <summary>
    /// No authentication required.
    /// </summary>
    None,

    /// <summary>
    /// API Key authentication (custom header).
    /// </summary>
    ApiKey,

    /// <summary>
    /// Bearer token authentication (Authorization: Bearer {token}).
    /// </summary>
    Bearer,

    /// <summary>
    /// Basic authentication (Authorization: Basic {base64}).
    /// </summary>
    Basic,

    /// <summary>
    /// Custom headers authentication.
    /// </summary>
    CustomHeaders,

    /// <summary>
    /// Azure Key Vault - retrieve credentials from Key Vault.
    /// </summary>
    KeyVault,

    /// <summary>
    /// Azure ARM authentication using Azure AD tokens (for Logic Apps and other Azure services).
    /// </summary>
    AzureARM
}
