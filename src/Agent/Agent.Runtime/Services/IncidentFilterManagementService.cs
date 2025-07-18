using System.Net.Http.Json;
using Agent.Core.Configuration;
using Agent.Core.Models.ICM;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Agent.Core.Services;
using Agent.Runtime.Reasoning;
using Agent.Core.Interfaces;

namespace Agent.Runtime.Services
{
    public class IncidentFilterFieldOption
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<KeyValuePair<string, string>> Options { get; set; } = new List<KeyValuePair<string, string>>();
    }

    public interface IIncidentFilterManagementService
    {
        Task<bool> CheckConnectivity();
        Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions();
        Task<List<IncidentFilterDocument>> ListIncidentFilters();
        Task<IncidentFilterDocument?> GetIncidentFilter(string filterId);
        Task<IncidentFilterDocument> SaveIncidentFilter(IncidentFilterDocument IncidentFilterDocument);
        Task<bool> DeleteIncidentFilter(string filterId);
        bool ValidateAgentMode(string agentMode);
    }

    public class IncidentFilterManagementService : IIncidentFilterManagementService
    {
        private readonly Container _container;
        protected readonly string DocumentType = "IncidentFilter";
        private readonly IncidentManagementSettings _incidentManagementSettings;
        private readonly IPagerDutyService _pagerDutyService;
        private readonly IICMAPIClient _icmApiClient;
        private readonly IServiceNowAPIClient _serviceNowAPIClient;
        private readonly ILogger<IncidentFilterManagementService> _logger;

        public IncidentFilterManagementService(
            CosmosClient cosmosClient,
            CosmosDBSettings cosmosDbSettings,
            IncidentManagementSettings incidentManagementSettings,
            IPagerDutyService pagerDutyService,
            ILogger<IncidentFilterManagementService> logger,
            IICMAPIClient icmApiClient,
            IServiceNowAPIClient serviceNowAPIClient)
        {
            _incidentManagementSettings = incidentManagementSettings;
            DocumentType = $"IncidentFilter{incidentManagementSettings.Type.ToString()}";
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
            _pagerDutyService = pagerDutyService;
            _logger = logger;
            _icmApiClient = icmApiClient;
            _serviceNowAPIClient = serviceNowAPIClient;
        }

        public async Task<bool> CheckConnectivity()
        {
            _logger.LogInternalInformation("CheckConnectivity: Invoked for IncidentManagementType: {IncidentManagementType}", _incidentManagementSettings.Type);
            try
            {
                switch (_incidentManagementSettings.Type)
                {
                    case IncidentManagementType.PagerDuty:
                        _logger.LogInternalInformation($"CheckConnectivity: Checking {_incidentManagementSettings.Type} service connectivity.");
                        var response = await _pagerDutyService.GetPagerDutyRequest("status");
                        _logger.LogInternalInformation($"CheckConnectivity: {_incidentManagementSettings.Type} service responded with status code {response.StatusCode}");
                        return response.IsSuccessStatusCode;
                    case IncidentManagementType.Icm:
                        _logger.LogInternalInformation($"CheckConnectivity: Checking {_incidentManagementSettings.Type} service connectivity.");
                        await _icmApiClient.GetIncidentsAsync(1, 0, null, null, null);
                        return true;
                    case IncidentManagementType.ServiceNow:
                        _logger.LogInternalInformation($"CheckConnectivity: Checking {_incidentManagementSettings.Type} service connectivity.");
                        await _serviceNowAPIClient.GetIncidentsAsync(1, 0, null, null, null);
                        return true;
                    default:
                        _logger.LogInternalWarning("CheckConnectivity: Connectivity check not implemented for IncidentManagementType: {IncidentManagementType}", _incidentManagementSettings.Type);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "CheckConnectivity: Exception occurred while checking connectivity for IncidentManagementType: {IncidentManagementType}", _incidentManagementSettings.Type);
                throw new Exception($"Failed to connect to {_incidentManagementSettings.Type} service.", ex);
            }
        }

        public async Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions()
        {
            _logger.LogInternalInformation("ListIncidentFilterFieldOptions: Invoked for IncidentManagementType: {IncidentManagementType}", _incidentManagementSettings.Type);

            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    return await ListPagerDutyIncidentFilterFieldOptions();
                case IncidentManagementType.Icm:
                    return ListIcmIncidentFilterFieldOptions();
                case IncidentManagementType.ServiceNow:
                    return await ListServiceNowIncidentFilterFieldOptions();
                case IncidentManagementType.AzMonitor:
                default:
                    _logger.LogInternalWarning("ListIncidentFilterFieldOptions: Not implemented for IncidentManagementType: {IncidentManagementType}", _incidentManagementSettings.Type);
                    throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }

        public async Task<List<IncidentFilterFieldOption>> ListPagerDutyIncidentFilterFieldOptions()
        {
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Invoked.");

            var result = new List<IncidentFilterFieldOption>();
            try
            {
                // Get Impacted Services List
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty services.");
                var servicesResponse = await _pagerDutyService.GetPagerDutyRequest("services");
                var services = await servicesResponse.Content.ReadFromJsonAsync<PDServicesResponse>();
                if (services != null && services.Services.Any())
                {
                    _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {ServiceCount} services.", services.Services.Count);
                    result.Add(new IncidentFilterFieldOption
                    {
                        FieldName = "ImpactedService",
                        DisplayName = "Impacted Service",
                        Options = services.Services.Select(s => new KeyValuePair<string, string>(s.Id, s.Name)).ToList()
                    });
                }
                else
                {
                    _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No services found in PagerDuty response.");
                }

                // Get Incident Types List
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty incident types.");
                var incidentTypesResponse = await _pagerDutyService.GetPagerDutyRequest("incidents/types");
                var incidentTypes = await incidentTypesResponse.Content.ReadFromJsonAsync<PDIncidentTypesResponse>();
                if (incidentTypes != null && incidentTypes.IncidentTypes.Any())
                {
                    _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {IncidentTypeCount} incident types.", incidentTypes.IncidentTypes.Count);
                    result.Add(new IncidentFilterFieldOption
                    {
                        FieldName = "IncidentType",
                        DisplayName = "Incident Type",
                        Options = incidentTypes.IncidentTypes.Select(it => new KeyValuePair<string, string>(it.Id, it.Name)).ToList()
                    });
                }
                else
                {
                    _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No incident types found in PagerDuty response.");
                }

                // Get Priorities List
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty priorities.");
                var prioritiesResponse = await _pagerDutyService.GetPagerDutyRequest("priorities");
                var priorities = await prioritiesResponse.Content.ReadFromJsonAsync<PDPrioritiesResponse>();
                if (priorities != null && priorities.Priorities.Any())
                {
                    _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {PriorityCount} priorities.", priorities.Priorities.Count);
                    result.Add(new IncidentFilterFieldOption
                    {
                        FieldName = "Priority",
                        DisplayName = "Priority",
                        Options = priorities.Priorities.Select(p => new KeyValuePair<string, string>(p.Id, p.Name)).ToList()
                    });
                }
                else
                {
                    _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No priorities found in PagerDuty response.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ListPagerDutyIncidentFilterFieldOptions: Exception occurred while retrieving PagerDuty field options.");
                throw;
            }
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
            return result;
        }

        /// <summary>
        /// For now we are hardcoding the options for ICM incident filters.
        /// Leave ImpactedService as empty as it will need add OwningServiceId into appsettings so can make API call to get all ImpactedService
        /// </summary>
        /// <returns></returns>
        public List<IncidentFilterFieldOption> ListIcmIncidentFilterFieldOptions()
        {
            _logger.LogInternalInformation("ListIcmIncidentFilterFieldOptions: Invoked.");

            var result = new List<IncidentFilterFieldOption>();

            var incidentTypeOptions = new List<KeyValuePair<string, string>>();
            foreach (IncidentType incidentType in Enum.GetValues(typeof(IncidentType)))
            {
                incidentTypeOptions.Add(new KeyValuePair<string, string>(incidentType.ToString(), incidentType.ToString()));
            }

            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "IncidentType",
                DisplayName = "Incident Type",
                Options = incidentTypeOptions
            });

            var priorityOptions = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("0", "0"),
                    new KeyValuePair<string, string>("1", "1"),
                    new KeyValuePair<string, string>("2", "2"),
                    new KeyValuePair<string, string>("3", "3"),
                    new KeyValuePair<string, string>("4", "4")
                };
            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "Priority",
                DisplayName = "Priority",
                Options = priorityOptions
            });

            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "ImpactedService",
                DisplayName = "Impacted Service",
                Options = new List<KeyValuePair<string, string>>()
            });

            var agentModeOptions = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(AgentModes.Review, AgentModes.Review),
                    new KeyValuePair<string, string>(AgentModes.Autonomous, AgentModes.Autonomous),
                };

            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "AgentMode",
                DisplayName = "Agent Mode",
                Options = agentModeOptions
            });

            _logger.LogInternalInformation("ListIcmIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
            return result;
        }

        public async Task<List<IncidentFilterFieldOption>> ListServiceNowIncidentFilterFieldOptions()
        {
            _logger.LogInternalInformation("ListServiceNowIncidentFilterFieldOptions: Invoked.");

            var result = new List<IncidentFilterFieldOption>();

            // In a real implementation, you would fetch these from ServiceNow API
            // For now, we'll use hardcoded values similar to the ICM implementation

            var incidentTypeOptions = new List<KeyValuePair<string, string>>();
            incidentTypeOptions.Add(new KeyValuePair<string, string>("ServiceNow", "ServiceNow"));

            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "IncidentType",
                DisplayName = "Incident Type",
                Options = incidentTypeOptions
            });

            var priorityOptions = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("1", "Critical"),
                new KeyValuePair<string, string>("2", "High"),
                new KeyValuePair<string, string>("3", "Moderate"),
                new KeyValuePair<string, string>("4", "Low"),
                new KeyValuePair<string, string>("5", "Planning")
            };

            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "Priority",
                DisplayName = "Priority",
                Options = priorityOptions
            });

            // In a real implementation, you would fetch service list from ServiceNow
            result.Add(new IncidentFilterFieldOption
            {
                FieldName = "ImpactedService",
                DisplayName = "Impacted Service",
                Options = new List<KeyValuePair<string, string>>()
            });

            _logger.LogInternalInformation("ListServiceNowIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
            return result;
        }

        public async Task<List<IncidentFilterDocument>> ListIncidentFilters()
        {
            _logger.LogInternalInformation("ListIncidentFilters: Invoked.");

            var queryable = _container.GetItemLinqQueryable<IncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false);

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentFilterDocument>();
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

        public async Task<IncidentFilterDocument?> GetIncidentFilter(string filterId)
        {
            _logger.LogInternalInformation("GetIncidentFilter: Invoked for FilterId: {FilterId}", filterId);

            var queryable = _container.GetItemLinqQueryable<IncidentFilterDocument>(allowSynchronousQueryExecution: false)
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

        public async Task<IncidentFilterDocument> SaveIncidentFilter(IncidentFilterDocument document)
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
            filter.IsDeleted = true;
            filter.UpdatedAt = DateTime.UtcNow;
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

        public bool ValidateAgentMode(string agentMode)
        {
            bool isValid = AgentModes.IsModeValid(agentMode);
            if (!isValid)
            {
                _logger.LogInternalInformation($"[IncidentFilterManagementService] Validating Agent Mode failed, RequestAgentMode: {agentMode}");
            }
            return isValid;
        }
    }
}
