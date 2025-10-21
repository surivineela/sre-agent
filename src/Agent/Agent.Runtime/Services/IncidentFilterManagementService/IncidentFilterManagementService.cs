// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Runtime.Reasoning;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services
{
    public enum IncidentFilterInputType
    {
        Dropdown,
        TextField
    }
    public class IncidentFilterFieldOption
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<KeyValuePair<string, string>> Options { get; set; } = new List<KeyValuePair<string, string>>();
        public IncidentFilterInputType FieldInputType { get; set; } = IncidentFilterInputType.Dropdown;
        public bool IsRequired { get; set; } = false;
    }

    public interface IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterPayload>
        where TIncidentFilterDocument : TIncidentFilterPayload, IIncidentFilterDocument
        where TIncidentFilterPayload : IncidentFilterDocumentPayload
    {
        Task<bool> CheckConnectivity();
        Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions();
        Task<List<TIncidentFilterDocument>> ListIncidentFilters(bool includeDisabled = true);
        Task<TIncidentFilterDocument?> GetIncidentFilter(string filterId);
        Task<TIncidentFilterDocument> SaveIncidentFilter(TIncidentFilterDocument incidentFilterDocument);
        Task<bool> DeleteIncidentFilter(string filterId);
    }

    public abstract class IncidentFilterManagementServiceBase<TIncidentFilterDocument, TIncidentFilterPayload> : IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterPayload>
        where TIncidentFilterDocument : TIncidentFilterPayload, IIncidentFilterDocument, new()
        where TIncidentFilterPayload : IncidentFilterDocumentPayload
    {
        protected readonly Container _container;
        protected readonly string DocumentType;
        protected readonly ILogger _logger;

        public IncidentFilterManagementServiceBase(
            Container container,
            ILogger logger,
            IncidentManagementSettings incidentManagementSettings)
        {
            _container = container;
            _logger = logger;
            DocumentType = IncidentFilterDocumentUtilities.GetDocumentTypeName(incidentManagementSettings.Type);
        }

        public abstract Task<bool> CheckConnectivity();

        /// <summary>
        /// Add extra filter field options specific to the incident type.
        /// </summary>
        /// <returns></returns>
        protected abstract Task<List<IncidentFilterFieldOption>> GetExtraFilterFieldOptions();

        /// <summary>
        /// List all possible filter field options.
        /// If needed, you can override this method completely in the derived class.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions()
        {
            var deepInvestigationOptions = new List<KeyValuePair<string, string>>
            {
                new("false", "Disabled"),
                new("true", "Enabled")
            };

            var fieldOptions = new List<IncidentFilterFieldOption>()
            {
                new() {
                    FieldName = nameof(IncidentFilterDocumentPayload.AgentMode),
                    DisplayName = "Agent Mode",
                    Options = new List<KeyValuePair<string, string>>
                    {
                        new(AgentModes.Review, AgentModes.Review),
                        new(AgentModes.Autonomous, AgentModes.Autonomous),
                    }
                },
                new() {
                    FieldName = nameof(IncidentFilterDocumentPayload.DeepInvestigationEnabled),
                    DisplayName = "Enable Deep Investigation",
                    FieldInputType = IncidentFilterInputType.Dropdown,
                    Options = deepInvestigationOptions
                }
            };

            var set = new HashSet<string>(fieldOptions.Select(r => r.FieldName));

            var extraFieldOptions = await GetExtraFilterFieldOptions();

            //When duplication for same fieldName, overwrite with new value if any
            foreach (var item in extraFieldOptions)
            {
                if (set.Contains(item.FieldName))
                {
                    fieldOptions.RemoveAll(r => r.FieldName == item.FieldName);
                }
                fieldOptions.Add(item);
                set.Add(item.FieldName);
            }
            return fieldOptions;
        }

        public virtual async Task<List<TIncidentFilterDocument>> ListIncidentFilters(bool includeDisabled = true)
        {
            _logger.LogInternalInformation("ListIncidentFilters: Invoked.");

            var queryable = _container.GetItemLinqQueryable<TIncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false && (includeDisabled || c.IsEnabled))
                .OrderByDescending(c => c.UpdatedAt);

            var iterator = queryable.ToFeedIterator();
            var results = new List<TIncidentFilterDocument>();
            try
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }
                _logger.LogInternalInformation("ListIncidentFilters: Retrieved {FilterCount} filters.", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ListIncidentFilters: Exception occurred while listing incident filters.");
                throw;
            }
            return results;
        }

        public async Task<TIncidentFilterDocument?> GetIncidentFilter(string filterId)
        {
            _logger.LogInternalInformation("GetIncidentFilter: Invoked for FilterId: {FilterId}", filterId);

            var queryable = _container.GetItemLinqQueryable<TIncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.Id == filterId && c.IsDeleted == false)
                .Take(1);

            var iterator = queryable.ToFeedIterator();
            try
            {
                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var filter = response.FirstOrDefault();
                    if (filter != null)
                    {
                        _logger.LogInternalInformation("GetIncidentFilter: Found filter for FilterId: {FilterId}", filterId);
                    }
                    else
                    {
                        _logger.LogInternalWarning("GetIncidentFilter: No filter found for FilterId: {FilterId}", filterId);
                    }
                    return filter;
                }
                _logger.LogInternalWarning("GetIncidentFilter: No results for FilterId: {FilterId}", filterId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIncidentFilter: Exception occurred for FilterId: {FilterId}", filterId);
                throw;
            }
        }

        public async Task<TIncidentFilterDocument> SaveIncidentFilter(TIncidentFilterDocument? document)
        {
            _logger.LogInternalInformation("SaveIncidentFilter: Invoked for FilterId: {FilterId}", document?.Id);

            try
            {
                if (document == null)
                {
                    _logger.LogInternalError(new ArgumentNullException(nameof(document)), "SaveIncidentFilter: Document is null.");
                    throw new ArgumentNullException(nameof(document));
                }
                var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey ?? document.Id));
                _logger.LogInternalInformation("SaveIncidentFilter: Successfully saved filter with FilterId: {FilterId}", document.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "SaveIncidentFilter: Exception occurred for FilterId: {FilterId}", document?.Id);
                throw;
            }
        }

        public async Task<bool> DeleteIncidentFilter(string filterId)
        {
            _logger.LogInternalInformation("DeleteIncidentFilter: Invoked for FilterId: {FilterId}", filterId);

            var filter = await GetIncidentFilter(filterId);
            if (filter == null)
            {
                _logger.LogInternalWarning("DeleteIncidentFilter: No filter found to delete for FilterId: {FilterId}", filterId);
                return false;
            }
            //filter.IsDeleted = true;
            //filter.UpdatedAt = DateTime.UtcNow;
            filter = filter with { IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            try
            {
                var response = await _container.UpsertItemAsync(filter, new PartitionKey(filter.PartitionKey ?? filter.Id));
                bool success = response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created;
                if (success)
                {
                    _logger.LogInternalInformation("DeleteIncidentFilter: Successfully soft-deleted filter with FilterId: {FilterId}", filterId);
                }
                else
                {
                    _logger.LogInternalWarning("DeleteIncidentFilter: Upsert did not return success for FilterId: {FilterId}, StatusCode: {StatusCode}", filterId, response.StatusCode);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "DeleteIncidentFilter: Exception occurred for FilterId: {FilterId}", filterId);
                throw;
            }
        }
    }
}
