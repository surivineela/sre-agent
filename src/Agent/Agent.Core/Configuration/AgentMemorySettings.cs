namespace Agent.Core.Configuration;

public class AgentMemorySettings
{
    public bool Enabled { get; set; } = false;
    public bool StorageAccountEnabled { get; set; } = false;
    public bool TrajectoryRetrievalEnabled { get; set; } = false;
    public bool DocumentRetrievalEnabled { get; set; } = false;
    public bool UserMemoryRetrievalEnabled { get; set; } = false;
    public string StorageAccountName { get; set; } = string.Empty;
    public string BlobStorageContainerName { get; set; } = string.Empty;
    public string BlobStorageDomainSuffix { get; set; } = "blob.core.windows.net";
    public string BlobStorageResourceId { get; set; } = string.Empty;
    public string AzureAISearchName { get; set; } = string.Empty;
    public string AzureAISearchIndexName { get; set; } = string.Empty;
    public string AzureAISearchIndexerName { get; set; } = string.Empty;
    public string AzureAISearchDataSourceName { get; set; } = string.Empty;
    public string AzureAISearchSkillSetName { get; set; } = string.Empty;
    public string AzureAISearchDomainSuffix { get; set; } = "search.windows.net";
    public string ManagedIdentityResourceId { get; set; } = string.Empty;
}
