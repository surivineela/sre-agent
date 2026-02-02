using System;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

/// <summary>
/// General OAuth token document for storing OAuth access tokens for various services (GitHub, Azure DevOps, etc.)
/// Services are differentiated by PartitionKey (e.g., "GitHubOAuth", "AzureDevOpsOAuth")
/// Azure DevOps tokens are stored per organization (ID = organization name), allowing multiple connectors to share the same token
/// </summary>
public record OAuthTokenDocument(
    string AccessToken,
    DateTime? ExpiresOn,
    string? RefreshToken = null) : ICosmosDocument
{
    public required string PartitionKey { get; init; }
    public required string Id { get; init; }
    public string DocumentType => "OAuthToken";
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static OAuthTokenDocument FromGitHubToken(GitHubAccessToken token) =>
        new OAuthTokenDocument(token.AccessToken, token.ExpiresOn)
        {
            PartitionKey = "GitHubOAuth",
            Id = "github"
        };

    public static OAuthTokenDocument FromAzureDevOpsToken(AzureDevOpsAccessToken token, string organizationName) =>
        new OAuthTokenDocument(token.AccessToken, token.ExpiresOn, token.RefreshToken)
        {
            PartitionKey = "AzureDevOpsOAuth",
            Id = organizationName
        };

    public GitHubAccessToken ToGitHubToken() =>
        new GitHubAccessToken(AccessToken, ExpiresOn);

    public AzureDevOpsAccessToken ToAzureDevOpsToken() =>
        new AzureDevOpsAccessToken(AccessToken, ExpiresOn, RefreshToken);
}

/// <summary>
/// Legacy GitHub access token document - maintained for backward compatibility
/// </summary>
public record GitHubAccessTokenDocument(
    string AccessToken,
    DateTime? ExpiresOn) : ICosmosDocument
{
    public string Id => "GitHubAccessToken";
    public string DocumentType => "GitHubAccessToken";
    public string PartitionKey => "GitHubAccessToken";
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static GitHubAccessTokenDocument FromDomainModel(GitHubAccessToken token) =>
        new GitHubAccessTokenDocument(token.AccessToken, token.ExpiresOn);

    public GitHubAccessToken ToDomainModel() =>
        new GitHubAccessToken(AccessToken, ExpiresOn);
}
