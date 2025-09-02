using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using Newtonsoft.Json;

namespace Agent.Runtime.Services
{
    public interface IIncidentHandlerManagementService
    {
        Task<List<IncidentHandlerDocument>> ListIncidentHandlers();
        Task<List<IncidentHandlerDocument>> QueryIncidentHandlers(List<string> keywords);
        Task<IncidentHandlerDocument?> GetIncidentHandler(string handlerId);
        Task<IncidentHandlerDocument> SaveIncidentHandler(IncidentHandlerDocument incidentHandlerDocument);
        Task<bool> DeleteIncidentHandler(string handlerId);
    }

    public class IncidentHandlerManagementService : IIncidentHandlerManagementService
    {
        private readonly Container _container;
        private readonly ILogger<IncidentHandlerManagementService> _logger;
        private readonly IncidentManagementSettings _incidentManagementSettings;
        protected readonly string DocumentType = "IncidentHandler";

        public IncidentHandlerManagementService(
            CosmosClient cosmosClient,
            CosmosDBSettings cosmosDbSettings,
            IncidentManagementSettings incidentManagementSettings,
            ILogger<IncidentHandlerManagementService> logger)
        {
            _incidentManagementSettings = incidentManagementSettings;
            DocumentType = $"IncidentHandler{_incidentManagementSettings.Type}";
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
            _logger = logger;
        }

        public async Task<List<IncidentHandlerDocument>> ListIncidentHandlers()
        {
            _logger.LogInternalInformation(
                "ListIncidentHandlers: Invoked");

            var queryable = _container.GetItemLinqQueryable<IncidentHandlerDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false);

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentHandlerDocument>();
            try
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }
                _logger.LogInternalInformation(
                    "ListIncidentHandlers: Retrieved {Count} handlers", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "ListIncidentHandlers: Error occurred while listing handlers");
                throw;
            }
        }

        public async Task<IncidentHandlerDocument?> GetIncidentHandler(string handlerId)
        {
            _logger.LogInternalInformation(
                "GetIncidentHandler: Invoked for HandlerId: {HandlerId}", handlerId);

            var queryable = _container.GetItemLinqQueryable<IncidentHandlerDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.Id == handlerId && c.IsDeleted == false)
                .Take(1);

            var iterator = queryable.ToFeedIterator();
            try
            {
                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var handler = response.FirstOrDefault();
                    if (handler == null)
                    {
                        _logger.LogInternalWarning(
                            "GetIncidentHandler: No handler found for HandlerId: {HandlerId}", handlerId);
                    }
                    else
                    {
                        _logger.LogInternalInformation(
                            "GetIncidentHandler: Handler found for HandlerId: {HandlerId}", handlerId);
                    }
                    return handler;
                }
                _logger.LogInternalWarning(
                    "GetIncidentHandler: No results for HandlerId: {HandlerId}", handlerId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "GetIncidentHandler: Error occurred for HandlerId: {HandlerId}", handlerId);
                throw;
            }
        }

        public async Task<IncidentHandlerDocument> SaveIncidentHandler(IncidentHandlerDocument document)
        {
            _logger.LogInternalInformation(
                "SaveIncidentHandler: Invoked for HandlerId: {HandlerId}", document?.Id);

            try
            {
                if (document == null)
                {
                    _logger.LogInternalError(
                        new ArgumentNullException(nameof(document)),
                        "SaveIncidentHandler: Document is null");
                    throw new ArgumentNullException(nameof(document));
                }
                var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey ?? document.Id));

                _logger.LogInternalInformation(
                    "SaveIncidentHandler: Successfully saved HandlerId: {HandlerId}", document.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "SaveIncidentHandler: Error occurred for HandlerId: {HandlerId}, Document: {Document}",
                    document?.Id,
                    JsonConvert.SerializeObject(document));
                throw;
            }
        }

        public async Task<bool> DeleteIncidentHandler(string handlerId)
        {
            _logger.LogInternalInformation(
                "DeleteIncidentHandler: Invoked for HandlerId: {HandlerId}", handlerId);

            var handler = await GetIncidentHandler(handlerId);
            if (handler == null)
            {
                _logger.LogInternalWarning(
                    "DeleteIncidentHandler: Handler not found for HandlerId: {HandlerId}", handlerId);
                return false;
            }
            handler.IsDeleted = true;
            handler.UpdatedAt = DateTime.UtcNow;
            try
            {
                var response = await _container.UpsertItemAsync(handler, new PartitionKey(handler.PartitionKey ?? handler.Id));
                bool success = response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created;
                if (success)
                {
                    _logger.LogInternalInformation(
                        "DeleteIncidentHandler: Successfully soft-deleted HandlerId: {HandlerId}", handlerId);
                }
                else
                {
                    _logger.LogInternalWarning(
                        "DeleteIncidentHandler: Soft delete failed for HandlerId: {HandlerId}, StatusCode: {StatusCode}",
                        handlerId, response.StatusCode);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "DeleteIncidentHandler: Error occurred for HandlerId: {HandlerId}", handlerId);
                throw;
            }
        }

        public async Task<List<IncidentHandlerDocument>> QueryIncidentHandlers(List<string> keywords)
        {
            _logger.LogInternalInformation(
                "QueryIncidentHandlers: Invoked with Keywords: {Keywords}", JsonConvert.SerializeObject(keywords));

            // Lowercase keywords for case-insensitive search
            var loweredKeywords = keywords.Select(k => k.ToLower()).ToArray();

            var queryable = _container.GetItemLinqQueryable<IncidentHandlerDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false &&
                            loweredKeywords.Any(kw => c.Name.ToLower().Contains(kw)));

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentHandlerDocument>();

            try
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }
                _logger.LogInternalInformation(
                    "QueryIncidentHandlers: Retrieved {Count} handlers for Keywords: {Keywords}",
                    results.Count, JsonConvert.SerializeObject(keywords));
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "QueryIncidentHandlers: Error occurred for Keywords: {Keywords}",
                    JsonConvert.SerializeObject(keywords));
                throw;
            }
        }
    }
}
