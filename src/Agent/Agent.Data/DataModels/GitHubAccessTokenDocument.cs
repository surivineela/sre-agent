using System;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;
public record GitHubAccessTokenDocument(
    string AccessToken,
    DateTime? ExpiresOn) : ICosmosDocument
{
    public string Id => "GitHubAccessToken";
    public string DocumentType => "GitHubAccessToken";
    public string PartitionKey => "GitHubAccessToken"; // Use a constant partition key for GitHub access token
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static GitHubAccessTokenDocument FromDomainModel(GitHubAccessToken token) =>
        new GitHubAccessTokenDocument(token.AccessToken, token.ExpiresOn);

    public GitHubAccessToken ToDomainModel() =>
        new GitHubAccessToken(AccessToken, ExpiresOn);
}
