using System.Net.Http.Json;
using Agent.Core.Configuration;
using Agent.Core.Models.ICM;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

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
    }

    public class IncidentFilterManagementService : IIncidentFilterManagementService
    {
        private readonly Container _container;
        protected readonly string DocumentType = "IncidentFilter";
        private readonly IncidentManagementSettings _incidentManagementSettings;
        private readonly IPagerDutyService _pagerDutyService;

        public IncidentFilterManagementService(
            CosmosClient cosmosClient,
            CosmosDBSettings cosmosDbSettings,
            IncidentManagementSettings incidentManagementSettings,
            IPagerDutyService pagerDutyService)
        {
            _incidentManagementSettings = incidentManagementSettings;
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
            _pagerDutyService = pagerDutyService;
        }

        public async Task<bool> CheckConnectivity()
        {
            // Check if the PagerDuty service is reachable
            if (_incidentManagementSettings.Type == IncidentManagementType.PagerDuty)
            {
                try
                {
                    var response = await _pagerDutyService.GetPagerDutyRequest("status");
                    return response.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to connect to PagerDuty service.", ex);
                }
            }
            // For other types, we can assume connectivity is not implemented
            return false;
        }

        public async Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions()
        {
            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    return await ListPagerDutyIncidentFilterFieldOptions();
                case IncidentManagementType.Icm:
                    return ListIcmIncidentFilterFieldOptions();
                case IncidentManagementType.AzMonitor:
                default:
                    throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }

        public async Task<List<IncidentFilterFieldOption>> ListPagerDutyIncidentFilterFieldOptions()
        {
            var result = new List<IncidentFilterFieldOption>();
            // Get Impacted Services List
            var servicesResponse = await _pagerDutyService.GetPagerDutyRequest("services");
            var services = await servicesResponse.Content.ReadFromJsonAsync<PDServicesResponse>();
            if (services != null && services.Services.Any())
            {
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = "ImpactedService",
                    DisplayName = "Impacted Service",
                    Options = services.Services.Select(s => new KeyValuePair<string, string>(s.Id, s.Name)).ToList()
                });
            }

            // Get Incident Types List
            var incidentTypesResponse = await _pagerDutyService.GetPagerDutyRequest("incidents/types");
            var incidentTypes = await incidentTypesResponse.Content.ReadFromJsonAsync<PDIncidentTypesResponse>();
            if (incidentTypes != null && incidentTypes.IncidentTypes.Any())
            {
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = "IncidentType",
                    DisplayName = "Incident Type",
                    Options = incidentTypes.IncidentTypes.Select(it => new KeyValuePair<string, string>(it.Id, it.Name)).ToList()
                });
            }
            // Get Priorities List
            var prioritiesResponse = await _pagerDutyService.GetPagerDutyRequest("priorities");
            var priorities = await prioritiesResponse.Content.ReadFromJsonAsync<PDPrioritiesResponse>();
            if (priorities != null && priorities.Priorities.Any())
            {
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = "Priority",
                    DisplayName = "Priority",
                    Options = priorities.Priorities.Select(p => new KeyValuePair<string, string>(p.Id, p.Name)).ToList()
                });
            }
            return result;
        }

        /// <summary>
        /// For now we are hardcoding the options for ICM incident filters.
        /// Leave ImpactedService as empty as it will need add OwningServiceId into appsettings so can make API call to get all ImpactedService
        /// </summary>
        /// <returns></returns>
        public List<IncidentFilterFieldOption> ListIcmIncidentFilterFieldOptions()
        {
            var result = new List<IncidentFilterFieldOption>();

            var incidentTypeOptions = new List<KeyValuePair<string, string>>();
            foreach (IncidentType incidentType in Enum.GetValues(typeof(IncidentType)))
            {
                incidentTypeOptions.Add(new KeyValuePair<string, string>(incidentType.ToString(),incidentType.ToString()));
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
            return result;
        }

        public async Task<List<IncidentFilterDocument>> ListIncidentFilters()
        {
            // Return all incident filters
            var queryable = _container.GetItemLinqQueryable<IncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false);

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentFilterDocument>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }

        public async Task<IncidentFilterDocument?> GetIncidentFilter(string filterId)
        {
            var queryable = _container.GetItemLinqQueryable<IncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.Id == filterId && c.IsDeleted == false)
                .Take(1);

            var iterator = queryable.ToFeedIterator();
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }

        public async Task<IncidentFilterDocument> SaveIncidentFilter(IncidentFilterDocument document)
        {
            try
            {
                if (document == null)
                    throw new ArgumentNullException(nameof(document));
                var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey ?? document.Id));
                return response.Resource;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<bool> DeleteIncidentFilter(string filterId)
        {
            // do a soft delete by setting isDeleted to true
            var filter = await GetIncidentFilter(filterId);
            if (filter == null)
                return false;
            filter.IsDeleted = true;
            filter.UpdatedAt = DateTime.UtcNow;
            try
            {
                var response = await _container.UpsertItemAsync(filter, new PartitionKey(filter.PartitionKey ?? filter.Id));
                return response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
