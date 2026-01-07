// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.Services;
using Agent.Web.ApiResources;
using Agent.Web.Validation;
using Agent.Web.Views.v2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Agent.Web.Services;

public class IncidentFilterApiService : IIncidentFilterApiService
{
    private readonly ILogger<IncidentFilterApiService> _logger;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly Container _container;
    private readonly string _documentType;
    private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    private readonly IIncidentFilterValidator _incidentFilterValidator;

    public IncidentFilterApiService(
        ILogger<IncidentFilterApiService> logger,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IncidentManagementSettings incidentManagementSettings,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentFilterValidator incidentFilterValidator)
    {
        _logger = logger;
        _incidentManagementSettings = incidentManagementSettings;
        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _documentType = IncidentFilterDocumentUtilities.GetDocumentTypeName(_incidentManagementSettings.Type);
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterValidator = incidentFilterValidator;
    }

    private static IncidentFilterView ToIncidentFilterView(IIncidentFilterDocument document)
    {
        return IncidentFilterView.CreateApiResponseEnvelope(document).Properties!;
    }

    public async Task<ApiCommandResult<IIncidentFilterDocument>> CreateOrUpdateIncidentFilterAsync(string filterId, IIncidentFilterDocument model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating incident filter: {FilterId}, DryRun: {DryRun}, OperationId: {OperationId}", filterId, dryRun, operationId);

            // Validate the incident filter document
            var validationResult = _incidentFilterValidator.ValidateIncidentFilter(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Incident filter validation failed for {FilterId}: {Errors}", filterId, string.Join("; ", validationResult.Errors));
                return new ApiCommandResult<IIncidentFilterDocument>(new BadRequestObjectResult(
                    ErrorMap.ValidationFailure.CreateErrorEntity(string.Join("; ", validationResult.Errors))));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for incident filter: {FilterId}", filterId);
                return new ApiCommandResult<IIncidentFilterDocument>(model, operationId);
            }

            // Perform actual database operations
            var savedDocument = await SaveIncidentFilterDocumentAsync(model);

            return new ApiCommandResult<IIncidentFilterDocument>(savedDocument, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating incident filter: {FilterId}", filterId);
            return new ApiCommandResult<IIncidentFilterDocument>(new ObjectResult(ErrorMap.InternalServerError.CreateErrorEntity())
            {
                StatusCode = StatusCodes.Status500InternalServerError
            });
        }
    }

    public async Task<ApiCommandResult<IIncidentFilterDocument>> DeleteIncidentFilterAsync(string filterId, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting incident filter: {FilterId}, DryRun: {DryRun}, OperationId: {OperationId}", filterId, dryRun, operationId);

            // First check if the filter exists
            var existingDocument = await GetIncidentFilterDocumentByIdAsync(filterId);
            if (existingDocument == null)
            {
                _logger.LogInternalWarning("Incident filter not found: {FilterId}", filterId);
                return new ApiCommandResult<IIncidentFilterDocument>(new NotFoundResult());
            }

            // If dry-run, skip database operations
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for deleting incident filter: {FilterId}", filterId);
                return new ApiCommandResult<IIncidentFilterDocument>(existingDocument, operationId);
            }

            // Perform actual delete operation
            var success = await DeleteIncidentFilterByIdAsync(filterId);
            if (!success)
            {
                _logger.LogInternalWarning("Failed to delete incident filter: {FilterId}", filterId);
                return new ApiCommandResult<IIncidentFilterDocument>(new ObjectResult(ErrorMap.InternalServerError.CreateErrorEntity())
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                });
            }

            return new ApiCommandResult<IIncidentFilterDocument>(existingDocument, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting incident filter: {FilterId}", filterId);
            return new ApiCommandResult<IIncidentFilterDocument>(new ObjectResult(ErrorMap.InternalServerError.CreateErrorEntity())
            {
                StatusCode = StatusCodes.Status500InternalServerError
            });
        }
    }

    public async Task<ApiCommandResult<IIncidentFilterDocument>> GetIncidentFilterAsync(string filterId)
    {
        try
        {
            _logger.LogInternalInformation("Getting incident filter: {FilterId}", filterId);

            var document = await GetIncidentFilterDocumentByIdAsync(filterId);

            if (document == null)
            {
                _logger.LogInternalWarning("Incident filter not found: {FilterId}", filterId);
                return new ApiCommandResult<IIncidentFilterDocument>(new NotFoundResult());
            }

            return new ApiCommandResult<IIncidentFilterDocument>(document);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while getting incident filter: {FilterId}", filterId);
            return new ApiCommandResult<IIncidentFilterDocument>(new ObjectResult(ErrorMap.InternalServerError.CreateErrorEntity())
            {
                StatusCode = StatusCodes.Status500InternalServerError
            });
        }
    }

    public async Task<ApiCommandResult<List<IIncidentFilterDocument>>> GetIncidentFiltersAsync()
    {
        try
        {
            _logger.LogInternalInformation("Getting all incident filters");

            var documents = await ListIncidentFilterDocumentsAsync();

            return new ApiCommandResult<List<IIncidentFilterDocument>>(documents);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while getting all incident filters");
            return new ApiCommandResult<List<IIncidentFilterDocument>>(new ObjectResult(ErrorMap.InternalServerError.CreateErrorEntity())
            {
                StatusCode = StatusCodes.Status500InternalServerError
            });
        }
    }

    #region Private Database Operations

    #region Generic Helper Methods

    /// <summary>
    /// Generic method to get a filter by ID from CosmosDB.
    /// </summary>
    private async Task<T?> GetFilterByIdAsync<T>(string filterId) where T : class, IIncidentFilterDocument
    {
        var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
            .Where(c => c.DocumentType == _documentType && c.Id == filterId && c.IsDeleted == false)
            .Take(1);

        var iterator = queryable.ToFeedIterator();
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var filter = response.FirstOrDefault();
            if (filter != null)
            {
                _logger.LogInternalInformation("GetFilterByIdAsync: Found filter for FilterId: {FilterId}", filterId);
                return filter;
            }
        }
        _logger.LogInternalWarning("GetFilterByIdAsync: No filter found for FilterId: {FilterId}", filterId);
        return null;
    }

    /// <summary>
    /// Generic method to list all filters from CosmosDB.
    /// </summary>
    private async Task<List<T>> ListFiltersAsync<T>() where T : class, IIncidentFilterDocument
    {
        var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
            .Where(c => c.DocumentType == _documentType && c.IsDeleted == false)
            .OrderByDescending(c => c.UpdatedAt);

        var iterator = queryable.ToFeedIterator();
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        _logger.LogInternalInformation("ListFiltersAsync: Retrieved {FilterCount} filters.", results.Count);
        return results;
    }

    /// <summary>
    /// Generic method to upsert a filter document in CosmosDB.
    /// </summary>
    private async Task<T> UpsertFilterAsync<T>(T document) where T : class, IIncidentFilterDocument
    {
        var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey));
        _logger.LogInternalInformation("UpsertFilterAsync: Successfully saved filter with FilterId: {FilterId}", document.Id);
        return response.Resource;
    }

    /// <summary>
    /// Generic method to soft delete a filter document in CosmosDB.
    /// </summary>
    private async Task<bool> SoftDeleteFilterAsync<T>(T filter) where T : class, IIncidentFilterDocument
    {
        var response = await _container.UpsertItemAsync(filter, new PartitionKey(filter.PartitionKey));
        bool success = response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created;
        if (success)
        {
            _logger.LogInternalInformation("SoftDeleteFilterAsync: Successfully soft-deleted filter with FilterId: {FilterId}", filter.Id);
        }
        else
        {
            _logger.LogInternalWarning("SoftDeleteFilterAsync: Upsert did not return success for FilterId: {FilterId}, StatusCode: {StatusCode}", filter.Id, response.StatusCode);
        }
        return success;
    }

    #endregion

    /// <summary>
    /// Gets an incident filter document by ID. Returns the IIncidentFilterDocument directly.
    /// </summary>
    private async Task<IIncidentFilterDocument?> GetIncidentFilterDocumentByIdAsync(string filterId)
    {
        _logger.LogInternalInformation("GetIncidentFilterDocumentByIdAsync: Invoked for FilterId: {FilterId}", filterId);

        try
        {
            IIncidentFilterDocument? document = _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => await GetFilterByIdAsync<AzMonitorIncidentFilterDocument>(filterId),
                IncidentManagementType.PagerDuty => await GetFilterByIdAsync<PagerDutyIncidentFilterDocument>(filterId),
                IncidentManagementType.Icm => await GetFilterByIdAsync<IcmIncidentFilterDocument>(filterId),
                IncidentManagementType.ServiceNow => await GetFilterByIdAsync<ServiceNowIncidentFilterDocument>(filterId),
                IncidentManagementType.None => await GetFilterByIdAsync<NullableIncidentFilterDocument>(filterId),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetIncidentFilterDocumentByIdAsync: Exception occurred for FilterId: {FilterId}", filterId);
            throw;
        }
    }

    /// <summary>
    /// Gets an incident filter by ID. Returns the IncidentFilterView representation.
    /// </summary>
    private async Task<IncidentFilterView?> GetIncidentFilterByIdAsync(string filterId)
    {
        _logger.LogInternalInformation("GetIncidentFilterByIdAsync: Invoked for FilterId: {FilterId}", filterId);

        try
        {
            IIncidentFilterDocument? document = _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => await GetFilterByIdAsync<AzMonitorIncidentFilterDocument>(filterId),
                IncidentManagementType.PagerDuty => await GetFilterByIdAsync<PagerDutyIncidentFilterDocument>(filterId),
                IncidentManagementType.Icm => await GetFilterByIdAsync<IcmIncidentFilterDocument>(filterId),
                IncidentManagementType.ServiceNow => await GetFilterByIdAsync<ServiceNowIncidentFilterDocument>(filterId),
                IncidentManagementType.None => await GetFilterByIdAsync<NullableIncidentFilterDocument>(filterId),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
            return document != null ? ToIncidentFilterView(document) : null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetIncidentFilterByIdAsync: Exception occurred for FilterId: {FilterId}", filterId);
            throw;
        }
    }

    /// <summary>
    /// Lists all incident filters. Returns list of IncidentFilterView representations.
    /// </summary>
    private async Task<List<IncidentFilterView>> ListIncidentFiltersAsync()
    {
        _logger.LogInternalInformation("ListIncidentFiltersAsync: Invoked.");

        try
        {
            return _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => (await ListFiltersAsync<AzMonitorIncidentFilterDocument>()).Select(ToIncidentFilterView).ToList(),
                IncidentManagementType.PagerDuty => (await ListFiltersAsync<PagerDutyIncidentFilterDocument>()).Select(ToIncidentFilterView).ToList(),
                IncidentManagementType.Icm => (await ListFiltersAsync<IcmIncidentFilterDocument>()).Select(ToIncidentFilterView).ToList(),
                IncidentManagementType.ServiceNow => (await ListFiltersAsync<ServiceNowIncidentFilterDocument>()).Select(ToIncidentFilterView).ToList(),
                IncidentManagementType.None => (await ListFiltersAsync<NullableIncidentFilterDocument>()).Select(ToIncidentFilterView).ToList(),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ListIncidentFiltersAsync: Exception occurred while listing incident filters.");
            throw;
        }
    }

    /// <summary>
    /// Lists all incident filter documents. Returns list of IIncidentFilterDocument.
    /// </summary>
    private async Task<List<IIncidentFilterDocument>> ListIncidentFilterDocumentsAsync()
    {
        _logger.LogInternalInformation("ListIncidentFilterDocumentsAsync: Invoked.");

        try
        {
            return _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => (await ListFiltersAsync<AzMonitorIncidentFilterDocument>()).Cast<IIncidentFilterDocument>().ToList(),
                IncidentManagementType.PagerDuty => (await ListFiltersAsync<PagerDutyIncidentFilterDocument>()).Cast<IIncidentFilterDocument>().ToList(),
                IncidentManagementType.Icm => (await ListFiltersAsync<IcmIncidentFilterDocument>()).Cast<IIncidentFilterDocument>().ToList(),
                IncidentManagementType.ServiceNow => (await ListFiltersAsync<ServiceNowIncidentFilterDocument>()).Cast<IIncidentFilterDocument>().ToList(),
                IncidentManagementType.None => (await ListFiltersAsync<NullableIncidentFilterDocument>()).Cast<IIncidentFilterDocument>().ToList(),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ListIncidentFilterDocumentsAsync: Exception occurred while listing incident filter documents.");
            throw;
        }
    }

    /// <summary>
    /// Saves (upserts) an incident filter document. Returns the saved document as IncidentFilterView.
    /// </summary>
    private async Task<IncidentFilterView> SaveIncidentFilterAsync(IIncidentFilterDocument document)
    {
        _logger.LogInternalInformation("SaveIncidentFilterAsync: Invoked for FilterId: {FilterId}", document?.Id);

        try
        {
            if (document == null)
            {
                _logger.LogInternalError(new ArgumentNullException(nameof(document)), "SaveIncidentFilterAsync: Document is null.");
                throw new ArgumentNullException(nameof(document));
            }

            // Use type switch to call the correct typed upsert
            IIncidentFilterDocument savedDocument = _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => await UpsertFilterAsync((AzMonitorIncidentFilterDocument)document),
                IncidentManagementType.PagerDuty => await UpsertFilterAsync((PagerDutyIncidentFilterDocument)document),
                IncidentManagementType.Icm => await UpsertFilterAsync((IcmIncidentFilterDocument)document),
                IncidentManagementType.ServiceNow => await UpsertFilterAsync((ServiceNowIncidentFilterDocument)document),
                IncidentManagementType.None => await UpsertFilterAsync((NullableIncidentFilterDocument)document),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
            return ToIncidentFilterView(savedDocument);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "SaveIncidentFilterAsync: Exception occurred for FilterId: {FilterId}", document?.Id);
            throw;
        }
    }

    /// <summary>
    /// Saves (upserts) an incident filter document. Returns the saved IIncidentFilterDocument.
    /// </summary>
    private async Task<IIncidentFilterDocument> SaveIncidentFilterDocumentAsync(IIncidentFilterDocument document)
    {
        _logger.LogInternalInformation("SaveIncidentFilterDocumentAsync: Invoked for FilterId: {FilterId}", document?.Id);

        try
        {
            if (document == null)
            {
                _logger.LogInternalError(new ArgumentNullException(nameof(document)), "SaveIncidentFilterDocumentAsync: Document is null.");
                throw new ArgumentNullException(nameof(document));
            }

            // Use type switch to call the correct typed upsert
            IIncidentFilterDocument savedDocument = _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => await UpsertFilterAsync((AzMonitorIncidentFilterDocument)document),
                IncidentManagementType.PagerDuty => await UpsertFilterAsync((PagerDutyIncidentFilterDocument)document),
                IncidentManagementType.Icm => await UpsertFilterAsync((IcmIncidentFilterDocument)document),
                IncidentManagementType.ServiceNow => await UpsertFilterAsync((ServiceNowIncidentFilterDocument)document),
                IncidentManagementType.None => await UpsertFilterAsync((NullableIncidentFilterDocument)document),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
            return savedDocument;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "SaveIncidentFilterDocumentAsync: Exception occurred for FilterId: {FilterId}", document?.Id);
            throw;
        }
    }

    private async Task<bool> DeleteIncidentFilterByIdAsync(string filterId)
    {
        _logger.LogInternalInformation("DeleteIncidentFilterByIdAsync: Invoked for FilterId: {FilterId}", filterId);

        try
        {
            // Find and delete all related handlers BEFORE deleting the filter
            var allHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
            var relatedHandlers = allHandlers.Where(h => h.IncidentFilterId == filterId).ToList();

            _logger.LogInternalInformation("DeleteIncidentFilterByIdAsync: Found {HandlerCount} related handlers for FilterId: {FilterId}", relatedHandlers.Count, filterId);

            // Delete each related handler
            foreach (var handler in relatedHandlers)
            {
                _logger.LogInternalInformation("DeleteIncidentFilterByIdAsync: Deleting related handler {HandlerId} for FilterId: {FilterId}", handler.Id, filterId);
                await _incidentHandlerManagementService.DeleteIncidentHandler(handler.Id);
            }

            // Use type switch to get and soft-delete the filter
            return _incidentManagementSettings.Type switch
            {
                IncidentManagementType.AzMonitor => await SoftDeleteFilterByTypeAsync<AzMonitorIncidentFilterDocument>(filterId),
                IncidentManagementType.PagerDuty => await SoftDeleteFilterByTypeAsync<PagerDutyIncidentFilterDocument>(filterId),
                IncidentManagementType.Icm => await SoftDeleteFilterByTypeAsync<IcmIncidentFilterDocument>(filterId),
                IncidentManagementType.ServiceNow => await SoftDeleteFilterByTypeAsync<ServiceNowIncidentFilterDocument>(filterId),
                IncidentManagementType.None => await SoftDeleteFilterByTypeAsync<NullableIncidentFilterDocument>(filterId),
                _ => throw new NotSupportedException($"Unsupported incident management type: {_incidentManagementSettings.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DeleteIncidentFilterByIdAsync: Exception occurred for FilterId: {FilterId}", filterId);
            throw;
        }
    }

    /// <summary>
    /// Helper method to get a filter by ID and soft delete it.
    /// </summary>
    private async Task<bool> SoftDeleteFilterByTypeAsync<T>(string filterId) where T : class, IIncidentFilterDocument
    {
        var filter = await GetFilterByIdAsync<T>(filterId);
        if (filter == null)
        {
            _logger.LogInternalWarning("SoftDeleteFilterByTypeAsync: No filter found to delete for FilterId: {FilterId}", filterId);
            return false;
        }

        filter.IsDeleted = true;
        filter.CreatedAt = DateTime.UtcNow;
        filter.UpdatedAt = DateTime.UtcNow;

        return await SoftDeleteFilterAsync(filter);
    }

    #endregion
}
