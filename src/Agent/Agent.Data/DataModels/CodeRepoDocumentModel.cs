using System.Text.Json.Serialization;
using Agent.Core.Helpers;
using Agent.Core.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// Document model for storing code repositories in CosmosDB.
/// </summary>
/// <param name="Metadata">Resource metadata including name and timestamps.</param>
/// <param name="Spec">Repository specification including URL and type.</param>
public record CodeRepoDocumentModel(
    ResourceMetadata Metadata,
    CodeRepoSpec Spec
) : ICosmosDocument
{
    /// <summary>
    /// The document type name for code repositories.
    /// </summary>
    public const string DocumentTypeName = "CodeRepo";

    /// <summary>
    /// Gets the document ID (lowercase name).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id => GetId(Name);

    /// <summary>
    /// Gets the document type.
    /// </summary>
    [JsonPropertyName("documentType")]
    public string DocumentType => DocumentTypeName;

    /// <summary>
    /// Gets the partition key for the document.
    /// </summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey => GetPartitionKey();

    /// <summary>
    /// Gets the container name where this document is stored.
    /// </summary>
    [JsonIgnore]
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    /// <summary>
    /// Gets the repository name from metadata.
    /// </summary>
    [JsonIgnore]
    public string Name => Metadata.Name;

    /// <summary>
    /// Generates the document ID from a repository name.
    /// </summary>
    /// <param name="name">The repository name.</param>
    /// <returns>The document ID (lowercase name).</returns>
    public static string GetId(string name) => name.ToLowerInvariant();

    /// <summary>
    /// Gets the partition key for code repository documents.
    /// </summary>
    /// <returns>The partition key value.</returns>
    public static string GetPartitionKey() => DocumentTypeName;
}

/// <summary>
/// Specification for a code repository.
/// </summary>
public class CodeRepoSpec
{
    /// <summary>
    /// Gets or sets the normalized repository URL.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the repository type (GitHub or AzureDevOps).
    /// </summary>
    [JsonPropertyName("type")]
    public required RepoType Type { get; set; }
}
