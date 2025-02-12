// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public class AppSettings
{
    // Add any general application settings here
    [Required]
    public bool LogGenAICalls { get; set; } = false;
}

public class AzureSettings
{
    [Required]
    public OpenAISettings OpenAI { get; set; } = new();

    [Required]
    public string TeamsEndpoint { get; set; } = string.Empty;

    [Required]
    public GitHubSettings Github { get; set; }

    [Required]
    public string ApprovalUrl { get; set; }

    [Required]
    public string? AppInsightsConnectionString { get; set; }

    [Required]
    public bool OpenSupportTickets { get; set; }

    [Required]
    public GremlinSettings Gremlin { get; set; } = new();
}

public class GitHubSettings
{
    public string ClientId { get; set; }
    public string PatOverride { get; set; }
    public string ClientSecret { get; set; }
    public string CallbackUrl { get; set; }
    public string OidcAudience { get; set; }
    public string[] AllowedRepositories { get; set; }
}

public class OpenAISettings
{
    [Required]
    public string DeploymentName { get; set; } = string.Empty;

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;
}

public class GremlinSettings
{
    [Required]
    public string AccountName { get; set; } = string.Empty;

    [Required]
    public string AccountKey { get; set; } = string.Empty;

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required]
    public string Collection { get; set; } = string.Empty;
}
