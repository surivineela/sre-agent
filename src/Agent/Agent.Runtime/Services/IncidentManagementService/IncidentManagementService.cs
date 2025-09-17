// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public interface IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    Task<IncidentQueryResult<TIncidentDocument>> QueryIncidents(IncidentQueryRequest<TIncidentFilterDocumentPayload> request);
    Task<TIncidentDocument?> GetIncidentDetails(string incidentId);
    Task<TIncidentDocument?> SaveDocument(TIncidentDocument? document);
}

public abstract class IncidentManagementServiceBase<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload> : IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument, new()
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload, new()
{
    protected readonly Container _container;
    protected abstract string DocumentType { get; }
    protected readonly ILogger _logger;
    protected readonly IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> _incidentFilterManagementService;

    public IncidentManagementServiceBase(
        Container container,
        IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> incidentFilterManagementService,
        ILogger logger)
    {
        _container = container;
        _incidentFilterManagementService = incidentFilterManagementService;
        _logger = logger;
    }

    public abstract Task<IncidentQueryResult<TIncidentDocument>> QueryIncidents(IncidentQueryRequest<TIncidentFilterDocumentPayload> request);

    public abstract Task<TIncidentDocument?> GetIncidentDetails(string incidentId);

    public async Task<TIncidentDocument?> SaveDocument(TIncidentDocument? document)
    {
        _logger.LogInternalInformation(
            "SaveDocument: Invoked for DocumentId: {DocumentId}, DocumentType: {DocumentType}",
            document?.Id,
            document?.DocumentType
        );

        try
        {
            if (document == null)
            {
                _logger.LogInternalWarning(
                    "SaveDocument: Document is null"
                );
                throw new ArgumentNullException(nameof(document));
            }
            var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey ?? document.Id));
            _logger.LogInternalInformation(
                "SaveDocument: Successfully saved DocumentId: {DocumentId}, DocumentType: {DocumentType}",
                document.Id,
                document.DocumentType
            );
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "SaveDocument: Exception occurred for DocumentId: {DocumentId}, DocumentType: {DocumentType}",
                document?.Id,
                document?.DocumentType
            );
            throw;
        }
    }

    /// <summary>
    /// Todo, can make QueryIncidentsInternal override in so can applys to different incident platforms
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected async Task<IncidentQueryResult<TIncidentDocument>> QueryIncidentsInternal(IncidentQueryRequest<TIncidentFilterDocumentPayload> request, Func<TIncidentDocument, TIncidentFilterDocumentPayload, bool>? advancedFilterFun = null)
    {
        _logger.LogInternalInformation(
            "QueryIncidentsInternal: Invoked with Request: {Request}",
            Newtonsoft.Json.JsonConvert.SerializeObject(request)
        );

        try
        {
            // Validate pagination parameters
            if (request.PageNumber <= 0)
            {
                _logger.LogInternalWarning(
                    "QueryIncidentsInternal: PageNumber <= 0, resetting to 1"
                );
                request.PageNumber = 1;
            }
            if (request.PageSize <= 0)
            {
                _logger.LogInternalWarning(
                    "QueryIncidentsInternal: PageSize <= 0, resetting to 20"
                );
                request.PageSize = 20;
            }

            var pagedResult = new IncidentQueryResult<TIncidentDocument>();
            var filteredResults = new List<TIncidentDocument>();
            int totalCount = 0;
            if (request.DurationInDays > 90)
            {
                _logger.LogInternalWarning(
                    "QueryIncidentsInternal: DurationInDays > 90, resetting to 90"
                );
                request.DurationInDays = 90;
            }
            var since = DateTime.UtcNow.AddDays(-request.DurationInDays);

            int skip = (request.PageNumber - 1) * request.PageSize;
            int take = request.PageSize;

            if (request.Filter is null)
            {
                if (
                        (request.Keywords is null || request.Keywords.Length == 0) &&
                        (request.SearchTerm is null || request.SearchTerm.Length == 0)
                    )
                {
                    _logger.LogInternalWarning(
                        "QueryIncidentsInternal: No filter, keywords, or search term provided"
                    );
                    pagedResult.Items = new List<TIncidentDocument>();
                    pagedResult.TotalCount = 0;
                    return pagedResult;
                }

                // Fetch only recent incidents from Cosmos DB
                _logger.LogInternalInformation(
                    "QueryIncidentsInternal: Querying Cosmos DB for DocumentType: {DocumentType} since {Since}",
                    DocumentType, since
                );
                var queryable = _container.GetItemLinqQueryable<TIncidentDocument>(allowSynchronousQueryExecution: false)
                    .Where(c => c.DocumentType == DocumentType && c.CreatedAt >= since);

                if (request.Statuses != null && request.Statuses.Count() > 0)
                {
                    request.Statuses = request.Statuses.Select(s => s.ToLower()).ToArray();
                    queryable = queryable.Where(c => request.Statuses.Contains(c.Status.ToLower()));
                }

                var iterator = queryable.ToFeedIterator();
                var results = new List<TIncidentDocument>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                _logger.LogInternalInformation(
                    "QueryIncidentsInternal: Retrieved {Count} incidents from Cosmos DB",
                    results.Count
                );

                // Filter in-memory by Keywords and/or SearchTerm (case-insensitive)
                filteredResults = results
                    .Where(c =>
                    {
                        if (request.Keywords != null && request.Keywords.Length > 0)
                        {
                            var loweredKeywords = request.Keywords.Select(k => k.ToLower()).ToArray();
                            var matchesKeywords = loweredKeywords.Any(kw => c.Title != null && c.Title.ToLower().Contains(kw));
                            if (!matchesKeywords) return false;
                        }

                        if (request.SearchTerm != null && request.SearchTerm.Length > 0)
                        {
                            var loweredSearchTerm = request.SearchTerm.ToLower();
                            var matchesSearchTerm = (
                                (c.Title != null && c.Title.ToLower().Contains(loweredSearchTerm)) ||
                                (c.Id != null && c.Id.ToLower().Contains(loweredSearchTerm))
                            );
                            if (!matchesSearchTerm) return false;
                        }

                        return true;
                    })
                    .ToList();

                var hasKeywords = request.Keywords != null && request.Keywords.Length > 0;
                var hasSearchTerm = request.SearchTerm != null && request.SearchTerm.Length > 0;
                var filteredBy = hasKeywords && hasSearchTerm ? "Keywords and SearchTerm" : hasKeywords ? "Keywords" : "SearchTerm";

                _logger.LogInternalInformation(
                    "QueryIncidentsInternal: Filtered results by {FilteredBy}. FilteredCount: {FilteredCount}",
                    filteredBy,
                    filteredResults.Count
                );
            }
            else
            {
                // Use the filter to query incidents
                _logger.LogInternalInformation(
                    "QueryIncidentsInternal: Querying with filter: {Filter}",
                    Newtonsoft.Json.JsonConvert.SerializeObject(request.Filter)
                );
                var queryable = _container.GetItemLinqQueryable<TIncidentDocument>(allowSynchronousQueryExecution: false)
                    .Where(c => c.DocumentType == DocumentType && c.CreatedAt >= since);

                if (request.Statuses != null && request.Statuses.Count() > 0)
                {
                    var normalizedStatuses = NormalizeStatusesForFiltering(request.Statuses.ToArray());
                    queryable = queryable.Where(c => normalizedStatuses.Contains(c.Status.ToLower()));
                }

                TIncidentFilterDocumentPayload? filter = null;

                if (!string.IsNullOrEmpty(request.Filter.Id))
                {
                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Fetching filter document for FilterId: {FilterId}",
                        request.Filter.Id
                    );
                    var filterDocument = await _incidentFilterManagementService.GetIncidentFilter(request.Filter.Id);
                    if (filterDocument is not null)
                    {
                        filter = (TIncidentFilterDocumentPayload)filterDocument;
                        _logger.LogInternalInformation(
                            "QueryIncidentsInternal: Loaded filter document for FilterId: {FilterId}",
                            filterDocument.Id
                        );
                    }
                    else
                    {
                        _logger.LogInternalWarning(
                            "QueryIncidentsInternal: No filter document found for FilterId: {FilterId}",
                            request.Filter.Id
                        );
                    }
                }

                // If filter hasn't been found or filterId is empty, then use the filter attributes from incoming request
                if (filter is null)
                {
                    filter = request.Filter;
                }

                if (!string.IsNullOrEmpty(filter.ImpactedService))
                {
                    queryable = queryable.Where(c => c.ImpactedServiceName.Equals(filter.ImpactedService, StringComparison.OrdinalIgnoreCase) || c.ImpactedServiceId.Equals(filter.ImpactedService, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(filter.Priority))
                {
                    var normalizedPriorities = NormalizePriorityForFiltering(filter.Priority);
                    queryable = queryable.Where(c => normalizedPriorities.Contains(c.Priority.ToLower()));
                }
                if (!string.IsNullOrEmpty(filter.IncidentType))
                {
                    queryable = queryable.Where(c => c.IncidentType.Equals(filter.IncidentType, StringComparison.OrdinalIgnoreCase));
                }
                var iterator = queryable.ToFeedIterator();
                var results = new List<TIncidentDocument>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                _logger.LogInternalInformation(
                    "QueryIncidentsInternal: Retrieved {Count} incidents from Cosmos DB with filter",
                    results.Count
                );

                // Filter by TitleContains and/or SearchTerm if provided
                if (
                    (filter.TitleContains != null && filter.TitleContains.Length > 0) ||
                    (request.SearchTerm != null && request.SearchTerm.Length > 0)
                )
                {
                    filteredResults = results
                        .Where(c =>
                        {
                            if (filter.TitleContains != null && filter.TitleContains.Length > 0)
                            {
                                var loweredTitleContains = filter.TitleContains.ToLower();
                                var matchesTitleContains = c.Title != null && c.Title.ToLower().Contains(loweredTitleContains);
                                if (!matchesTitleContains) return false;
                            }

                            if (request.SearchTerm != null && request.SearchTerm.Length > 0)
                            {
                                var loweredSearchTerm = request.SearchTerm.ToLower();
                                var matchesSearchTerm = (
                                    (c.Title != null && c.Title.ToLower().Contains(loweredSearchTerm)) ||
                                    (c.Id != null && c.Id.ToLower().Contains(loweredSearchTerm))
                                );
                                if (!matchesSearchTerm) return false;
                            }

                            return true;
                        })
                        .ToList();

                    var hasTitleContains = filter.TitleContains != null && filter.TitleContains.Length > 0;
                    var hasSearchTerm = request.SearchTerm != null && request.SearchTerm.Length > 0;
                    var filteredBy = hasTitleContains && hasSearchTerm ? "TitleContains and SearchTerm" : hasTitleContains ? "TitleContains" : "SearchTerm";

                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Filtered results by {FilteredBy}. FilteredCount: {FilteredCount}",
                        filteredBy,
                        filteredResults.Count
                    );
                }
                else
                {
                    filteredResults = results;
                }

                if (advancedFilterFun is not null)
                {
                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Applying advanced filter function"
                    );
                    filteredResults = filteredResults
                        .Where(c => advancedFilterFun(c, filter))
                        .ToList();
                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Advanced filter applied. FilteredCount: {FilteredCount}",
                        filteredResults.Count
                    );
                }
            }

            totalCount = filteredResults.Count;
            pagedResult.TotalCount = totalCount;
            pagedResult.Items = filteredResults.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(take).ToList();

            _logger.LogInternalInformation(
                "QueryIncidentsInternal: Returning paged result. PageNumber: {PageNumber}, PageSize: {PageSize}, TotalCount: {TotalCount}",
                request.PageNumber, request.PageSize, totalCount
            );

            return pagedResult;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "QueryIncidentsInternal: Exception occurred for Request: {Request}",
                Newtonsoft.Json.JsonConvert.SerializeObject(request)
            );
            throw;
        }
    }

    protected async Task<TIncidentDocument?> GetIncidentDetailsInternal(string incidentId)
    {
        _logger.LogInternalInformation(
            "GetIncidentDetailsInternal: Invoked for IncidentId: {IncidentId}",
            incidentId
        );

        try
        {
            var iterator = _container.GetItemLinqQueryable<TIncidentDocument>(allowSynchronousQueryExecution: false)
                        .Where(c => c.DocumentType == DocumentType && c.Id == incidentId)
                        .Take(1)
                        .ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var result = response.FirstOrDefault();
                if (result == null)
                {
                    _logger.LogInternalWarning(
                        "GetIncidentDetailsInternal: No incident found for IncidentId: {IncidentId}",
                        incidentId
                    );
                }
                else
                {
                    _logger.LogInternalInformation(
                        "GetIncidentDetailsInternal: Successfully retrieved incident for IncidentId: {IncidentId}",
                        incidentId
                    );
                }
                return result;
            }
            _logger.LogInternalWarning(
                "GetIncidentDetailsInternal: No results for IncidentId: {IncidentId}",
                incidentId
            );
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "GetIncidentDetailsInternal: Exception occurred for IncidentId: {IncidentId}",
                incidentId
            );
            throw;
        }
    }

    /// <summary>
    /// Normalizes status values for filtering, handling ServiceNow numeric states
    /// and converting human-readable status names to their corresponding values.
    /// Only used for filtering ServiceNow incidents.
    /// </summary>
    /// <param name="statuses">Array of status values to normalize</param>
    /// <returns>Array of normalized status values for filtering</returns>
    protected virtual string[] NormalizeStatusesForFiltering(string[] status)
    {
        return status.Select(s => s.ToLower()).ToArray();
    }

    public virtual string[] NormalizePriorityForFiltering(string priority)
    {
        return new[] { priority.ToLower() };
    }
}

public class IncidentQueryRequest<TIncidentFilterDocumentPayload> where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    //FilterDocumentPayload should be passing by a generic type
    public TIncidentFilterDocumentPayload? Filter { get; set; }

    // Should only use Keywords in special scenarios where Filter isn't viable
    public string[] Keywords { get; set; } = [];
    public int DurationInDays { get; set; } = 60; // Default to 60 days for incident history

    public string[] Statuses { get; set; } = [];
    public string[] Priorities { get; set; } = [];

    // 1-based index, in later .net version can use "field" to replace _pageNumber
    private int _pageNumber = 1;
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = Math.Max(1, value);
    }
    public int PageSize { get; set; } = 20;  // Default page size
    public string SearchTerm { get; set; } = string.Empty;
}

public class IncidentQueryResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
