// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public class AppSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool LogGenAICalls { get; set; }
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

    [Required]
    public DurableTaskSchedulerSettings DurableTaskScheduler { get; set; } = new();
}

public class DurableTaskSchedulerSettings
{
    public string ConnectionString { get; set; } 
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

public class ICMSettings
{
    public string PluginUrl { get; set; }
    public string PluginAppKey { get; set; }
}

public class AzureSearchSettings
{
    public string SearchServiceUri { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string UserAssignedMIClientId { get; set; } = string.Empty;
    public string SearchApiKeyOverride { get; set; } = string.Empty;
}

public class OpenAISettings
{
    [Required]
    public string DeploymentName { get; set; } = string.Empty;

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string EmbeddingGeneratorDeploymentName { get; set; } = string.Empty;
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

public class TestSettings
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public bool SkipResourceCleanupAfterTestRun { get; set; } = true;
}

public class KustoCluster
{
    [Required]
    public string Region { get; set; }
    [Required]
    public string ClusterUri { get; set; }
    [Required]
    public string Database { get; set; }
}

public class KustoClusterSettings : List<KustoCluster> { }

public enum KustoAuthenticationType
{
    ManagedIdentity,
    UAMI,
    App,
    User, // for testing
}

public class KustoSettings
{
    [Required]
    public KustoAuthenticationType AuthenticationType { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string AuthorityHost { get; set; } = string.Empty;
    public string ApplicationClientId { get; set; } = string.Empty;
    public string ApplicationCertificate { get; set; } = string.Empty;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
}