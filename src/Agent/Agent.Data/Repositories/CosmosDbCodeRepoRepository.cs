using System.Net;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

/// <summary>
/// CosmosDB implementation of code repository storage.
/// </summary>
public class CosmosDbCodeRepoRepository : ICodeRepoRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbCodeRepoRepository> _logger;

    public CosmosDbCodeRepoRepository(
        CosmosClient cosmosClient,
        string databaseName,
        ILogger<CosmosDbCodeRepoRepository> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CodeRepoDocumentModel> UpsertCodeRepoAsync(CodeRepoDocumentModel repo, string operationId)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CodeRepoDocumentModel.ContainerName);
            var response = await container.UpsertItemAsync(repo, new PartitionKey(CodeRepoDocumentModel.GetPartitionKey()));
            _logger.LogInternalInformation("Successfully upserted code repository document {RepoName} for operation {OperationId}",
                repo.Name, operationId);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to upsert code repository document {RepoName} for operation {OperationId}",
                repo.Name, operationId);
            throw;
        }
    }

    public async Task<CodeRepoDocumentModel?> GetCodeRepoByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CodeRepoDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<CodeRepoDocumentModel>(
                CodeRepoDocumentModel.GetId(name),
                new PartitionKey(CodeRepoDocumentModel.GetPartitionKey()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Code repository {RepoName} not found", name);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve code repository {RepoName}", name);
            throw;
        }
    }

    public async Task<CodeRepoDocumentModel?> GetCodeRepoByUrlAsync(string normalizedUrl)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CodeRepoDocumentModel.ContainerName);
            var query = container.GetItemLinqQueryable<CodeRepoDocumentModel>()
                .Where(d => d.DocumentType == CodeRepoDocumentModel.DocumentTypeName && d.Spec.Url == normalizedUrl);

            using var iterator = query.ToFeedIterator();
            var results = new List<CodeRepoDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results.FirstOrDefault();
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve code repository by URL {Url}", normalizedUrl);
            throw;
        }
    }

    public async Task<IEnumerable<CodeRepoDocumentModel>> GetAllCodeReposAsync()
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CodeRepoDocumentModel.ContainerName);
            var query = container.GetItemLinqQueryable<CodeRepoDocumentModel>()
                .Where(d => d.DocumentType == CodeRepoDocumentModel.DocumentTypeName);

            using var iterator = query.ToFeedIterator();
            var results = new List<CodeRepoDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            _logger.LogInternalInformation("Retrieved all {Count} code repositories", results.Count);
            return results;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve all code repositories");
            throw;
        }
    }

    public async Task<bool> DeleteCodeRepoAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CodeRepoDocumentModel.ContainerName);
            await container.DeleteItemAsync<CodeRepoDocumentModel>(
                CodeRepoDocumentModel.GetId(name),
                new PartitionKey(CodeRepoDocumentModel.GetPartitionKey()));
            _logger.LogInternalInformation("Successfully deleted code repository {RepoName}", name);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Code repository {RepoName} not found for deletion", name);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete code repository {RepoName}", name);
            throw;
        }
    }
}
