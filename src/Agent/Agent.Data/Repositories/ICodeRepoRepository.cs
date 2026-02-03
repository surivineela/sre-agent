using Agent.Data.DataModels;

namespace Agent.Data.Repositories;

/// <summary>
/// Repository interface for managing code repository documents in CosmosDB.
/// </summary>
public interface ICodeRepoRepository
{
    /// <summary>
    /// Creates or updates a code repository.
    /// </summary>
    /// <param name="repo">The repository document to upsert.</param>
    /// <param name="operationId">The operation ID for tracking.</param>
    /// <returns>The upserted repository document.</returns>
    Task<CodeRepoDocumentModel> UpsertCodeRepoAsync(CodeRepoDocumentModel repo, string operationId);

    /// <summary>
    /// Gets a code repository by name.
    /// </summary>
    /// <param name="name">The repository name.</param>
    /// <returns>The repository document, or null if not found.</returns>
    Task<CodeRepoDocumentModel?> GetCodeRepoByNameAsync(string name);

    /// <summary>
    /// Gets a code repository by its normalized URL.
    /// </summary>
    /// <param name="normalizedUrl">The normalized repository URL.</param>
    /// <returns>The repository document, or null if not found.</returns>
    Task<CodeRepoDocumentModel?> GetCodeRepoByUrlAsync(string normalizedUrl);

    /// <summary>
    /// Gets all code repositories.
    /// </summary>
    /// <returns>A collection of all repository documents.</returns>
    Task<IEnumerable<CodeRepoDocumentModel>> GetAllCodeReposAsync();

    /// <summary>
    /// Deletes a code repository by name.
    /// </summary>
    /// <param name="name">The repository name.</param>
    /// <returns>True if the repository was deleted; false if it was not found.</returns>
    Task<bool> DeleteCodeRepoAsync(string name);
}
