
using FirstPartyAgent.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Kusto.Cloud.Platform.Data;
using FirstPartyAgent.Core.Clients;
using System.Net;
using FirstPartyAgent.Core.Configuration;

namespace FirstPartyAgent.Core.Services;
public class IcmAgentConfigService : IIcmAgentConfigService
{
    private readonly ICosmosDBService _cosmosDbService;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KustoClient _kustoClient;
    private Task _initializationTask;
    private IICMWorkflowClient _icmworkflowClient;
    private const string _alertConfigContainerName = "IcmAlertConfigs";
    private const string _teamContainerName = "Teams";
    private const string _alertDetailsContainerName = "IcmAlertDetails";
    private const string _genevaActionsContainerName = "GenevaActionsConfigs";
    private const string _agentDeploymentsContainerName = "AgentDeployments";
    private const string _agentFactoryConfigsContainerName = "AgentFactoryConfigs";
    private readonly IcmAgentSettings _icmAgentSettings;

    public IcmAgentConfigService(
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ICosmosDBService cosmosDbService,
        KustoClient kustoClientService,
        IcmAgentSettings icmAgentSettings,
        IICMWorkflowClient icmworkflowClient)
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
        };


        var tasks = containersToCreate.ToDictionary(
            c => c.Id,
            c => db.CreateContainerIfNotExistsAsync(c));

        await Task.WhenAll(tasks.Values);

        if(tasks[_agentFactoryConfigsContainerName].Result.StatusCode == HttpStatusCode.Created)
        {
            await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, new AgentFactoryConfigCosmos<IcmTeam[]> { Id = "icmTeams", Content = new IcmTeam[0] });
            await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, new AgentFactoryConfigCosmos<Dictionary<int, string[]>> { Id = "teamFilters", Content = new Dictionary<int, string[]>() });
        }


    }

    public bool IsEnabled() => _cosmosDbService.IsEnabled;

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
            var icmTeams = await GetAgentFactoryConfig<List<IcmTeam>>("icmTeams");
            var filters = await GetAgentFactoryConfig<Dictionary<int, string[]>>("teamFilters");

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
        catch (KeyNotFoundException ex)
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

        CreateTeamConfigIfNotExists(new TeamConfig
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

        if (genevaActionsConfig.TeamId == null)
        {
            throw new ArgumentException("TeamId cannot be empty", nameof(genevaActionsConfig));
        }

        try
        {
            var existingConfig = await GetGenevaActionConfig(genevaActionsConfig.TeamId);

            existingConfig.GenevaActions = genevaActionsConfig.GenevaActions;

            var result = await _cosmosDbService.UpsertItemAsync<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName, existingConfig);
            return result;
        }
        catch (KeyNotFoundException ex)
        {
            var result = await _cosmosDbService.UpsertItemAsync<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName, genevaActionsConfig);
            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving Geneva actions config: {ex.Message}", ex);
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

        var queryableResult = _cosmosDbService.GetQueryableContainer<TeamConfig>(_cosmosDbService.IcmAgentDatabaseName, _teamContainerName)
            .Where(teamConfig => teamConfig.TeamId == loopId)
            .ToList();

        if (!queryableResult.Any())
        {
            throw new KeyNotFoundException($"Team configuration with ID {loopId} not found.");
        }

        return queryableResult.First();
    }

   
    #endregion
}


public class IcmAgentConfigServiceDisabled : IIcmAgentConfigService
{
    public bool IsEnabled() => false;

    public Task<List<TeamConfig>> GetOnboardedLoops()
    {
        throw new NotImplementedException();
    }

    public Task<List<ICMAlertConfig>> GetLoopAlertConfigs(int? loopId)
    {
        throw new NotImplementedException();
    }

    public Task<List<AlertDetails>> GetLoopAlerts(int loopId)
    {
        throw new NotImplementedException();
    }

    public Task<List<IcmTeam>> GetIcmTeams()
    {
        throw new NotImplementedException();
    }

    public Task<AgentFactoryConfigCosmos<T>> GetAgentFactoryConfig<T>(string id)
    {
        throw new NotImplementedException();
    }

    public Task<List<string>> GetAgentFactoryConfigNames()
    {
        throw new NotImplementedException();
    }

    public Task UpsertAgentFactoryConfig<T>(AgentFactoryConfigCosmos<T> config)
    {
        throw new NotImplementedException();
    }

    public Task<List<AlertDetails>> GetAlerts()
    {
        throw new NotImplementedException();
    }

    public Task<ICMAlertConfig> GetAlertConfig(int loopId, string alertId)
    {
        throw new NotImplementedException();
    }

    public Task<string> CreateAlertConfig(ICMAlertConfig alertConfig)
    {
        throw new NotImplementedException();
    }
    public Task UpdateAlertConfig(ICMAlertConfig alertConfig, int loopId, string alertId)
    {
        throw new NotImplementedException();
    }

    public Task<List<IcmIncidentBasicInfo>> GetIncidentsByTeamAlert(int teamId, int numOfDays, string title)
    {
        throw new NotImplementedException();
    }

    
    public Task<List<AgentDeployment>> GetAgentDeployments(int loopId)
    {
        throw new NotImplementedException();
    }

    public Task<GenevaActionsConfigCosmos> GetGenevaActionConfig(int teamId)
    {
        throw new NotImplementedException();
    }
    public Task<GenevaActionsConfigCosmos> SaveGenevaActionsConfig(GenevaActionsConfigCosmos genevaActionsConfig)
    {
        throw new NotImplementedException();
    }
}
