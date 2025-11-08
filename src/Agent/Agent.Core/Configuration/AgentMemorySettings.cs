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
    
    // Threshold settings for relevance filtering
    public float TrajectoryVectorSimilarityThreshold { get; set; } = 0.7f;
    public float TrajectoryVectorSimilarityThresholdForSameResource { get; set; } = 0.6f;
    public float UserMemoryVectorSimilarityThreshold { get; set; } = 0.1f;
    public float DocumentVectorSimilarityThreshold { get; set; } = 0.65f;
    public float MinimumRerankerScoreThreshold { get; set; } = 2.0f;

    // Session Insights settings
    /// <summary>
    /// Controls whether trajectory insights are generated and posted to threads.
    /// Default: true.
    /// </summary>
    public bool EnableInsightPosting { get; set; } = true;
}
