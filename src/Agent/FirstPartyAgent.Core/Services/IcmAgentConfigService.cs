
using FirstPartyAgent.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Kusto.Cloud.Platform.Data;
using FirstPartyAgent.Core.Clients;
using System.Net;
using FirstPartyAgent.Core.Configuration;
using Newtonsoft.Json.Linq;
using Gremlin.Net.Process.Traversal;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services;
public class IcmAgentConfigService : IIcmAgentConfigService
{
    private readonly ICosmosDBService _cosmosDbService;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KustoClient _kustoClient;
    private Task _initializationTask;
    private IICMWorkflowClient _icmworkflowClient;
    private ILogger<IcmAgentConfigService> _logger;
    private const string _alertConfigContainerName = "IcmAlertConfigs";
    private const string _teamContainerName = "Teams";
    private const string _alertDetailsContainerName = "IcmAlertDetails";
    private const string _genevaActionsContainerName = "GenevaActionsConfigs";
    private const string _agentDeploymentsContainerName = "AgentDeployments";
    private const string _agentFactoryConfigsContainerName = "AgentFactoryConfigs";
    private const string _icmTeamsContainerName = "IcmTeams";
    private readonly IcmAgentSettings _icmAgentSettings;

    public IcmAgentConfigService(
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ICosmosDBService cosmosDbService,
        KustoClient kustoClientService,
        IcmAgentSettings icmAgentSettings,
        IICMWorkflowClient icmworkflowClient,
        ILogger<IcmAgentConfigService> logger)
    {
        _env = env;
        _httpClientFactory = httpClientFactory;
        _cosmosDbService = cosmosDbService;
        _kustoClient = kustoClientService;
        _icmAgentSettings = icmAgentSettings;

        if (_cosmosDbService.IsEnabled)
        {
            _initializationTask = InitializeCosmosDbTables();
        }

        _icmworkflowClient = icmworkflowClient;
        _logger = logger;
    }

    private async Task InitializeCosmosDbTables()
    {
        var client = _cosmosDbService.CosmosClient;
        // create database if not exists
        var resp = await client.CreateDatabaseIfNotExistsAsync(_cosmosDbService.IcmAgentDatabaseName);
        var db = resp.Database;

        // create containers if not exists
        var containersToCreate = new ContainerProperties[]
        {
            new() { Id = _agentDeploymentsContainerName, PartitionKeyPath = "/id" },
            new() { Id = _agentFactoryConfigsContainerName, PartitionKeyPath = "/id" },
            new() { Id = _genevaActionsContainerName, PartitionKeyPath = "/TeamId" },
            new() { Id = _alertConfigContainerName, PartitionKeyPath = "/TeamId" },
            new() { Id = _alertDetailsContainerName, PartitionKeyPath = "/TeamId" },
            new() { Id = _icmTeamsContainerName, PartitionKeyPath = "/ServiceId" },
        };


        var tasks = containersToCreate.ToDictionary(
            c => c.Id,
            c => db.CreateContainerIfNotExistsAsync(c));

        await Task.WhenAll(tasks.Values);

        if(tasks[_agentFactoryConfigsContainerName].Result.StatusCode == HttpStatusCode.Created)
        {
            await InitializeagentFactoryConfigsContainer();
        }


    }

    public bool IsEnabled() => _cosmosDbService.IsEnabled;

    private async Task InitializeagentFactoryConfigsContainer()
    {
        await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, new AgentFactoryConfigCosmos<IcmTeam[]> { Id = AgentFactoryConfigIds.IcmTeams, Content = Array.Empty<IcmTeam>() });
        await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, new AgentFactoryConfigCosmos<Dictionary<int, string[]>> { Id = AgentFactoryConfigIds.TeamFilters, Content = new Dictionary<int, string[]>() });
        await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, new AgentFactoryConfigCosmos<IcmTeam> { Id = AgentFactoryConfigIds.DefaultIcmTeam, Content = new IcmTeam() });
    }

    private async Task IsReady()
    {
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

        if (_initializationTask != null && !_initializationTask.IsCompleted)
        {
            await _initializationTask;
            _initializationTask = null;
        }
    }

    public async Task<List<TeamConfig>> GetOnboardedLoops()
    {
        await IsReady();

        try
        {
            var queryableResult = _cosmosDbService.GetQueryableContainer<TeamConfig>(_cosmosDbService.IcmAgentDatabaseName, _teamContainerName).ToList();
            return queryableResult;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting onboarded loops: {ex.Message}", ex);
        }
    }

    public async Task<List<ICMAlertConfig>> GetLoopAlertConfigs(int? loopId)
    {
        await IsReady();

        try
        {
            if (loopId == null)
            {
                var queryableResult = _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName)
                .ToList();
                return queryableResult;
            }
            else
            {
                var queryableResult = _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName)
                .Where(c => c.TeamId == loopId)
                .ToList();
                return queryableResult;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting config for alert: {ex.Message}", ex);
        }
    }

    public async Task<List<AlertDetails>> GetLoopAlerts(int loopId)
    {
        await IsReady();

        try
        {
            var queryableResult = _cosmosDbService.GetQueryableContainer<AlertDetails>(_cosmosDbService.IcmAgentDatabaseName, _alertDetailsContainerName)
                .Where(c => c.TeamId == loopId)
                .ToList();

            return queryableResult;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting alerts for loop: {ex.Message}", ex);
        }
    }

    public async Task<List<IcmTeam>> GetIcmTeams()
    {
        await IsReady();

        try
        {
            var icmTeams = await GetAgentFactoryConfig<List<IcmTeam>>(AgentFactoryConfigIds.IcmTeams);
            var filters = await GetAgentFactoryConfig<Dictionary<int, string[]>>(AgentFactoryConfigIds.TeamFilters);

            var dict = filters.Content;
            var result = icmTeams.Content
                .Where(i => !dict.ContainsKey(i.IcmServiceId ?? -1) || dict[i.IcmServiceId ?? -1].Contains(i.IcmTeamName))
                .ToList();

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting Icm teams: {ex.Message}", ex);
        }
    }

    public async Task<IcmTeam> GetDefaultIcmTeam()
    {
        await IsReady();
        try
        {
            var defaultTeamId = await GetAgentFactoryConfig<IcmTeam>(AgentFactoryConfigIds.DefaultIcmTeam);
            return defaultTeamId.Content;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting default ICM team ID: {ex.Message}", ex);
        }
    }

    public async Task<AgentFactoryConfigCosmos<T>> GetAgentFactoryConfig<T>(string id)
    {
        await IsReady();

        try
        {
            var list = await _cosmosDbService.GetQueryableContainer<AgentFactoryConfigCosmos<T>>(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName)
            .Where(c => c.Id == id).ToListAsync();

            if (list != null && !list.Any())
            {
                throw new KeyNotFoundException($"Agent factory config with ID {id} not found.");
            }

            return list.First();
        }
        catch (Exception ex)
        {
            if (ex is KeyNotFoundException)
            {
                throw;
            }

            throw new Exception($"Error getting agent factory config with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> GetAgentFactoryConfigNames()
    {
        await IsReady();

        try
        {
            var list = _cosmosDbService.GetQueryableContainer<AgentFactoryConfigCosmos<string>>(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName)
                .Select(c => c.Id)
                .ToList();

            return list;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting agent factory config names: {ex.Message}", ex);
        }
    }

    public async Task UpsertAgentFactoryConfig<T>(AgentFactoryConfigCosmos<T> config)
    {
        await IsReady();

        try
        {
            await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, config);
        }
        catch(Exception ex)
        {
            throw new Exception($"Error upserting agent factory config: {ex.Message}", ex);
        }
    }


    public async Task<List<AlertDetails>> GetAlerts()
    {
        await IsReady();

        try
        {
            var queryableResult = _cosmosDbService.GetQueryableContainer<AlertDetails>(_cosmosDbService.IcmAgentDatabaseName, _alertDetailsContainerName)
                .ToList();
            return queryableResult;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting alerts: {ex.Message}", ex);
        }
    }

    public async Task<ICMAlertConfig> GetAlertConfig(int loopId, string alertId)
    {
        await IsReady();

        if (string.IsNullOrWhiteSpace(alertId))
        {
            throw new ArgumentException("Alert id cannot be empty", nameof(alertId));
        }

        try
        {
            var queryableResult = _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName)
                .Where(c => c.AlertingId == alertId)
                .ToList();

            if (queryableResult == null || !queryableResult.Any())
            {
                throw new KeyNotFoundException($"Alert config with ID {alertId} not found.");
            }

            return queryableResult.First();
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting alert config: {ex.Message}", ex);
        }
    }

    public async Task<string> CreateAlertConfig(ICMAlertConfig alertConfig)
    {
        await IsReady();

        if (alertConfig == null)
        {
            throw new ArgumentNullException(nameof(alertConfig), "Alert configuration cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(alertConfig.AlertingId))
        {
            throw new ArgumentException("Alerting id cannot be empty", nameof(alertConfig));
        }

        try
        {
            var config = await GetAlertConfig(alertConfig.TeamId, alertConfig.AlertingId);
            throw new InvalidOperationException($"Alert configuration with ID {alertConfig.AlertingId} already exists.");
        }
        catch (KeyNotFoundException)
        {
        }

        await CreateTeamConfigIfNotExists(new TeamConfig
        {
            TeamId = alertConfig.TeamId,
            TeamName = alertConfig.DefaultHumanInterventionLoop
        });

        alertConfig.Id = Guid.NewGuid().ToString();
        await _cosmosDbService.UpsertItemAsync<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName, alertConfig);

        return alertConfig.AlertingId;
    }

    public async Task UpdateAlertConfig(ICMAlertConfig alertConfig, int loopId, string alertId)
    {
        await IsReady();

        if (string.IsNullOrWhiteSpace(alertId))
        {
            throw new ArgumentException("Alert id cannot be empty", nameof(alertId));
        }

        try
        {
            var existingConfig = await GetAlertConfig(loopId, alertId);

            if (alertConfig.TeamId != loopId)
            {
                await CreateTeamConfigIfNotExists(new TeamConfig
                {
                    TeamId = alertConfig.TeamId,
                    TeamName = alertConfig.IncidentTitle
                });
            }

            alertConfig.Id = existingConfig.Id;
            await _cosmosDbService.UpsertItemAsync<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName, alertConfig);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error updating alert config: {ex.Message}", ex);
        }
    }

    public async Task<List<IcmIncidentBasicInfo>> GetIncidentsByTeamAlert(int teamId, int numOfDays, string title)
    {
        await IsReady();

        try
        {
            string query =
@$"
let et = now();
let st = et - {numOfDays}d;
Incidents
| where CreateDate  between (st .. et)
| where OwningTeamId == {teamId} and Title has '{title}'
| where Status in ('RESOLVED', 'MITIGATED')
| summarize arg_max(ModifiedDate, *) by IncidentId
| project Id = toint(IncidentId), Severity, Title, State = Status, CreatedDate = CreateDate
| order by CreatedDate desc";


            var result = await _icmworkflowClient.RunKustoQuery(query);

            // parse json
            var incidents = Newtonsoft.Json.JsonConvert.DeserializeObject<List<IcmIncidentBasicInfo>>(result);

            if (incidents == null)
            {
                throw new Exception("Failed to parse incidents from Kusto query result.");
            }

            foreach (var incident in incidents)
            {
                DateTime.SpecifyKind(incident.CreatedDate, DateTimeKind.Utc);
            }

            return incidents;


        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting incidents by team alert: {ex.Message}", ex);
        }
    }

    private async Task<List<IcmIncidentBasicInfo>> GetIncidentsByTeamAlertFromKusto(int teamId, int numOfDays, string title)
    {
        await IsReady();

        try
        {
            string query =
@$"
declare query_parameters (teamIdParam:int, numOfDaysParam:int, titleParam:string);
let et = now();
let st = et - (numOfDaysParam*1d);
Incidents
| where CreateDate  between (st .. et)
| where OwningTeamId == teamIdParam and Title has titleParam
| where Status in ('RESOLVED', 'MITIGATED')
| summarize arg_max(ModifiedDate, *) by IncidentId
| project Id = toint(IncidentId), Severity, Title, State = Status, CreatedDate = CreateDate
| order by CreatedDate desc";
            // Create parameters dictionary
            var parameters = new Dictionary<string, object>
            {
                { "teamIdParam", teamId },
                { "numOfDaysParam", numOfDays },
                { "titleParam", title }
            };

            using var reader = await _kustoClient.PerformQueryWithParametersAsync(_icmAgentSettings.IcmKustoClusterUri, _icmAgentSettings.IcmKustoDataBase, query, parameters);

            // Process the results
            var incidents = new List<IcmIncidentBasicInfo>();
            var dt = reader?.ToDataSet()?.Tables[0] ?? new System.Data.DataTable();

            foreach (System.Data.DataRow row in dt.Rows)
            {
                var incident = new IcmIncidentBasicInfo
                {
                    Id = (int)row["Id"],
                    Severity = (int)row["Severity"],
                    Title = row["Title"].ToString(),
                    State = row["State"].ToString(),
                    CreatedDate = row["CreatedDate"] is DateTime date ? date : DateTime.Parse(row["CreatedDate"].ToString())
                };
                DateTime.SpecifyKind(incident.CreatedDate, DateTimeKind.Utc);
                incidents.Add(incident);
            }
            return incidents;


        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting incidents by team alert: {ex.Message}", ex);
        }
    }

    public async Task<List<AgentDeployment>> GetAgentDeployments(int loopId)
    {
        await IsReady();

        try
        {
            var queryableResult = _cosmosDbService.GetQueryableContainer<AgentDeployment>(_cosmosDbService.IcmAgentDatabaseName, _agentDeploymentsContainerName)
                            .Where(c => c.TeamId == loopId)
                            .ToList();
            if (queryableResult == null || !queryableResult.Any())
            {
                throw new KeyNotFoundException($"Agent deployment with ID {loopId} not found.");
            }
            return queryableResult;
        }
        catch(Exception ex)
        {
            throw new Exception($"Error getting agent deployments: {ex.Message}", ex);
        }
    }

    public async Task<GenevaActionsConfigCosmos> GetGenevaActionConfig(int teamId)
    {
        await IsReady();

        try
        {
            var queryableResult = _cosmosDbService.GetQueryableContainer<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName)
                .Where(c => c.TeamId == teamId)
                .ToList();
            if (queryableResult == null || !queryableResult.Any())
            {
                throw new KeyNotFoundException($"Geneva action config with ID {teamId} not found.");
            }
            return queryableResult.First();
        }
        catch(Exception ex)
        {
            throw new Exception($"Error getting Geneva action config: {ex.Message}", ex);
        }
    }

    public async Task<GenevaActionsConfigCosmos> SaveGenevaActionsConfig(GenevaActionsConfigCosmos genevaActionsConfig)
    {
        await IsReady();

        try
        {
            var existingConfig = await GetGenevaActionConfig(genevaActionsConfig.TeamId);

            existingConfig.GenevaActions = genevaActionsConfig.GenevaActions;

            var result = await _cosmosDbService.UpsertItemAsync<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName, existingConfig);
            return result;
        }
        catch (KeyNotFoundException)
        {
            var result = await _cosmosDbService.UpsertItemAsync<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName, genevaActionsConfig);
            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving Geneva actions config: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> ListAllContainers()
    {
        await IsReady();

        try
        {
            var client = _cosmosDbService.CosmosClient;
            var database = client.GetDatabase(_cosmosDbService.IcmAgentDatabaseName);
            
            // Fetch all container names from the database
            var containerList = new List<string>();
            using (var iterator = database.GetContainerQueryIterator<ContainerProperties>())
            {
                while (iterator.HasMoreResults)
                {
                    var containers = await iterator.ReadNextAsync();
                    foreach (var container in containers)
                    {
                        containerList.Add(container.Id);
                    }
                }
            }
            
            return containerList;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing containers in database: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> GetAllDocumentIds(string containerName)
    {
        await IsReady();

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name cannot be empty", nameof(containerName));
        }

        try
        {
            var client = _cosmosDbService.CosmosClient;
            var database = client.GetDatabase(_cosmosDbService.IcmAgentDatabaseName);
            var container = database.GetContainer(containerName);
            
            // Query to get all document IDs in the container
            var query = new QueryDefinition("SELECT c.id FROM c");
            var documentIds = new List<string>();
            
            using (var iterator = container.GetItemQueryIterator<dynamic>(query))
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        documentIds.Add(item.id.ToString());
                    }
                }
            }
            
            return documentIds;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting document IDs from container '{containerName}': {ex.Message}", ex);
        }
    }

    public async Task<string> GetDocumentById(string containerName, string documentId)
    {
        await IsReady();

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name cannot be empty", nameof(containerName));
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));
        }

        try
        {
            var client = _cosmosDbService.CosmosClient;
            var database = client.GetDatabase(_cosmosDbService.IcmAgentDatabaseName);
            var container = database.GetContainer(containerName);

            // Query to get the document
            var query = new QueryDefinition($"SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", documentId);

            using var iterator = container.GetItemQueryIterator<dynamic>(query);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var document = response.FirstOrDefault();
                if (document == null)
                {
                    throw new KeyNotFoundException($"Document with ID '{documentId}' not found in container '{containerName}'.");
                }
                return Newtonsoft.Json.JsonConvert.SerializeObject(document);
            }

            throw new KeyNotFoundException($"Document with ID '{documentId}' not found in container '{containerName}'.");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Document with ID '{documentId}' not found in container '{containerName}'.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting document '{documentId}' from container '{containerName}': {ex.Message}", ex);
        }
    }

    public async Task<string> UpsertDocument(string containerName, string documentJson)
    {
        await IsReady();

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name cannot be empty", nameof(containerName));
        }

        if (string.IsNullOrWhiteSpace(documentJson))
        {
            throw new ArgumentNullException(nameof(documentJson), "Document JSON cannot be null or empty");
        }

        try
        {
            var documentObject = Newtonsoft.Json.JsonConvert.DeserializeObject<JToken>(documentJson);
            if (documentObject == null)
            {
                 throw new ArgumentException("Invalid JSON document", nameof(documentJson));
            }

            // Use the existing method in CosmosDBService to handle the upsert
            // Assuming UpsertItemAsync can take a dynamic object and the container name
            // and that it returns the upserted item which can then be serialized.
            var result = await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, containerName, documentObject);
            return result.StatusCode.ToString();
        }
        catch (Newtonsoft.Json.JsonException jsonEx)
        {
            throw new ArgumentException($"Invalid JSON format: {jsonEx.Message}", nameof(documentJson), jsonEx);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error upserting document to container '{containerName}': {ex.Message}", ex);
        }
    }

    #region Private methods
    private async Task CreateTeamConfigIfNotExists(TeamConfig teamConfig)
    {
        if (teamConfig == null || teamConfig.TeamName == null)
        {
            throw new ArgumentNullException(nameof(teamConfig), "Team configuration cannot be empty");
        }

        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

        try
        {
            var existingConfig = await GetTeamConfig(teamConfig.TeamId);

            return;
        }
        catch (KeyNotFoundException)
        {
            teamConfig.Id = Guid.NewGuid().ToString();
            await _cosmosDbService.UpsertItemAsync<TeamConfig>(_cosmosDbService.IcmAgentDatabaseName, _teamContainerName, teamConfig);
        }
    }

    private async Task<TeamConfig> GetTeamConfig(int loopId)
    {
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

        var queryableResult = await _cosmosDbService.GetQueryableContainer<TeamConfig>(_cosmosDbService.IcmAgentDatabaseName, _teamContainerName)
            .Where(teamConfig => teamConfig.TeamId == loopId)
            .ToListAsync();

        if (queryableResult.Count == 0)
        {
            throw new KeyNotFoundException($"Team configuration with ID {loopId} not found.");
        }

        return queryableResult.First();
    }

    public async Task<List<IcmService>> GetIcmServices()
    {
        await IsReady();
        try
        {
            var icmServices = await GetAgentFactoryConfig<List<IcmService>>(AgentFactoryConfigIds.IcmServices);
            if(icmServices.Datetime > DateTimeOffset.UtcNow.AddDays(-7) && icmServices.Content.Count > 0)
            {
                return icmServices.Content;
            }
        }
        catch (KeyNotFoundException)
        {
            
        }

        string query = @"
            Teams
            | summarize by TenantName, TenantId
            | where TenantName !contains ""Deprecate""
            | order by TenantName asc
            | project-rename Name = TenantName, Id = TenantId";

        string json = await _icmworkflowClient.RunKustoQuery(query);
        var services = Newtonsoft.Json.JsonConvert.DeserializeObject<List<IcmService>>(json);

        if (services == null || !services.Any(s => s.Name != null && s.Id != null))
        {
            throw new Exception("Failed to retrieve ICM services from Kusto query.");
        }

        services = services
            .Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.Id != null)
            .ToList();

        // write to cosmos db
        _ = Task.Run(() =>  UpsertAgentFactoryConfig(new AgentFactoryConfigCosmos<List<IcmService>>
        {
            Id = AgentFactoryConfigIds.IcmServices,
            Content = services
        }));

        return services;
    }

    public async Task<IcmTeams> GetIcmTeams(int serviceId)
    {
        await IsReady();
        try
        {
            var list = await _cosmosDbService.GetQueryableContainer<IcmTeams>(_cosmosDbService.IcmAgentDatabaseName, _icmTeamsContainerName)
                .Where(c => c.ServiceId == serviceId).ToListAsync();

            if(list != null && list.Any() && list[0].Datetime > DateTimeOffset.UtcNow.AddDays(-7))
            {
                return list.First();
            }

            string query = @$"
                Teams
                | where TenantId == {serviceId}
                | summarize by Id = TeamId, Name = TeamName, PublicId = PublicTeamId
                | order by Name asc";

            string json = await _icmworkflowClient.RunKustoQuery(query);
            var teams = Newtonsoft.Json.JsonConvert.DeserializeObject<List<IcmTeams.Team>>(json);
            if (teams == null || !teams.Any(t => t.Id != null && t.Name != null && t.PublicId != null))
            {
                throw new Exception("Failed to retrieve ICM teams from Kusto query.");
            }
            var icmTeams = new IcmTeams
            {
                ServiceId = serviceId,
                Teams = teams
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _icmTeamsContainerName, icmTeams);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert ICM teams for service {ServiceId}", serviceId);
                }
            });

            return icmTeams;

        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting ICM teams for service {serviceId}: {ex.Message}", ex);
        }
    }


    #endregion
}


public class IcmAgentConfigServiceDisabled : IIcmAgentConfigService
{
    public bool IsEnabled()
    {
        return false;
    }

    public Task<List<TeamConfig>> GetOnboardedLoops()
    {
        return Task.FromResult(new List<TeamConfig>());
    }

    public Task<List<ICMAlertConfig>> GetLoopAlertConfigs(int? loopId)
    {
        return Task.FromResult(new List<ICMAlertConfig>());
    }

    public Task<List<AlertDetails>> GetLoopAlerts(int loopId)
    {
        return Task.FromResult(new List<AlertDetails>());
    }

    public Task<List<IcmTeam>> GetIcmTeams()
    {
        return Task.FromResult(new List<IcmTeam>());
    }

    public Task<AgentFactoryConfigCosmos<T>> GetAgentFactoryConfig<T>(string id)
    {
        return Task.FromResult<AgentFactoryConfigCosmos<T>>(null);
    }

    public Task<List<string>> GetAgentFactoryConfigNames()
    {
        return Task.FromResult(new List<string>());
    }

    public Task UpsertAgentFactoryConfig<T>(AgentFactoryConfigCosmos<T> config)
    {
        return Task.CompletedTask;
    }

    public Task<List<AlertDetails>> GetAlerts()
    {
        return Task.FromResult(new List<AlertDetails>());
    }

    public Task<ICMAlertConfig> GetAlertConfig(int loopId, string alertId)
    {
        return Task.FromResult<ICMAlertConfig>(null);
    }

    public Task<string> CreateAlertConfig(ICMAlertConfig alertConfig)
    {
        return Task.FromResult(string.Empty);
    }

    public Task UpdateAlertConfig(ICMAlertConfig alertConfig, int loopId, string alertId)
    {
        return Task.CompletedTask;
    }

    public Task<List<IcmIncidentBasicInfo>> GetIncidentsByTeamAlert(int teamId, int numOfDays, string title)
    {
        return Task.FromResult(new List<IcmIncidentBasicInfo>());
    }

    public Task<List<AgentDeployment>> GetAgentDeployments(int loopId)
    {
        return Task.FromResult(new List<AgentDeployment>());
    }

    public Task<GenevaActionsConfigCosmos> GetGenevaActionConfig(int teamId)
    {
        return Task.FromResult<GenevaActionsConfigCosmos>(null);
    }

    public Task<GenevaActionsConfigCosmos> SaveGenevaActionsConfig(GenevaActionsConfigCosmos genevaActionsConfig)
    {
        return Task.FromResult<GenevaActionsConfigCosmos>(null);
    }

    public Task<List<string>> ListAllContainers()
    {
        return Task.FromResult(new List<string>());
    }

    public Task<List<string>> GetAllDocumentIds(string containerName)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<string> GetDocumentById(string containerName, string documentId)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string> UpsertDocument(string containerName, string documentJson)
    {
        return Task.FromResult(string.Empty);
    }

    Task<IcmTeam> IIcmAgentConfigService.GetDefaultIcmTeam()
    {
        return Task.FromResult<IcmTeam>(null);
    }

    public Task<List<IcmService>> GetIcmServices()
    {
        return Task.FromResult(new List<IcmService>());
    }

    public Task<IcmTeams> GetIcmTeams(int serviceId)
    {
        return Task.FromResult<IcmTeams>(null);
    }
}

