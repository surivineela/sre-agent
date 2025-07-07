using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;
public class AzureDevOpsAccessTokenDocument(
    string AccessToken,
    DateTime? CreatedAt) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public const string KeyName = "AzureDevOpsAccessToken";
    public string Id { get; set; }
    public string DocumentType => KeyName; 
    public string PartitionKey => KeyName;
    public string AccessToken { get; set; } = AccessToken;
    public DateTime? CreatedAt { get; set; } = CreatedAt;

    internal static readonly Guid _sessionGuid = Guid.NewGuid();

    public static AzureDevOpsAccessTokenDocument FromDomainModel(AzureDevOpsAccessToken token, string resourceId)
    {
        var document = new AzureDevOpsAccessTokenDocument(token.AccessToken, token.ExpiresOn);
        document.Id = $"{KeyName}_{resourceId}_{_sessionGuid}";
        return document;
    }

    public AzureDevOpsAccessToken ToDomainModel()
        => new AzureDevOpsAccessToken(AccessToken, CreatedAt);
}
