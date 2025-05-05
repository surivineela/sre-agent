
using FirstPartyAgent.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Agent.Core.Helpers;
using Kusto.Cloud.Platform.Data;

namespace FirstPartyAgent.Core.Services;
public class IcmAgentConfigService : IIcmAgentConfigService
{
    private readonly ICosmosDBService _cosmosDbService;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KustoClientService _kustoClientService;

    private const string _databaseName = "HotsiteAgent";
    private const string _alertConfigContainerName = "IcmAlertConfigs";
    private const string _teamContainerName = "Teams";
    private const string _alertDetailsContainerName = "IcmAlertDetails";
    private const string _genevaActionsContainerName = "GenevaActionsConfigs";
    private const string _agentDeploymentsContainerName = "AgentDeployments";
    private const string _agentFactoryConfigsContainerName = "AgentFactoryConfigs";

    public IcmAgentConfigService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory, ICosmosDBService cosmosDbService, KustoClientService kustoClientService)
    {
        _env = env;
        _httpClientFactory = httpClientFactory;
        _cosmosDbService = cosmosDbService;
        _kustoClientService = kustoClientService;
    }

    public bool IsEnabled() => _cosmosDbService.IsEnabled;

    public async Task<List<TeamConfig>> GetOnboardedLoops()
    {
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

        try
        {
            var list = _cosmosDbService.GetQueryableContainer<AgentFactoryConfigCosmos<T>>(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName)
            .Where(c => c.Id == id).ToList();

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
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }
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
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }
        try
        {
            await _cosmosDbService.UpsertItemAsync(_cosmosDbService.IcmAgentDatabaseName, _agentFactoryConfigsContainerName, config, new PartitionKey(config.Id));
        }
        catch(Exception ex)
        {
            throw new Exception($"Error upserting agent factory config: {ex.Message}", ex);
        }
    }

    public async Task<List<AlertDetails>> GetAlerts()
    {
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        catch (Exception ex)
        {
            throw new Exception($"Error getting alert config: {ex.Message}", ex);
        }
    }

    public async Task<string> CreateAlertConfig(ICMAlertConfig alertConfig)
    {
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        await _cosmosDbService.UpsertItemAsync<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName, alertConfig, new PartitionKey(alertConfig.TeamId));

        return alertConfig.AlertingId;
    }

    public async Task UpdateAlertConfig(ICMAlertConfig alertConfig, int loopId, string alertId)
    {
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

        if(string.IsNullOrWhiteSpace(alertId))
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
            await _cosmosDbService.UpsertItemAsync<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, _alertConfigContainerName, alertConfig, new PartitionKey(alertConfig.TeamId));
        }
        catch (Exception ex)
        {
            throw new Exception($"Error updating alert config: {ex.Message}", ex);
        }
    }

    public async Task<List<IcmIncidentBasicInfo>> GetIncidentsByTeamAlert(int teamId, int numOfDays, string title)
    {
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
| order by CreatedDate desc
| take 20";
            // Create parameters dictionary
            var parameters = new Dictionary<string, object>
            {
                { "teamIdParam", teamId },
                { "numOfDaysParam", numOfDays },
                { "titleParam", title }
            };

            using var reader = await _kustoClientService.PerformQueryWithParametersAsync(query, parameters, new Agent.Core.Models.KustoCluster() {
                ClusterUri = "https://IcMDataWarehouse.kusto.windows.net",
                Database = "IcMDataWarehouse",
                Region = "primary"
            });

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
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if (!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

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
        if(!IsEnabled())
        {
            throw new InvalidOperationException("Icm service disabled");
        }

        if (genevaActionsConfig.TeamId == null)
        {
            throw new ArgumentException("TeamId cannot be empty", nameof(genevaActionsConfig));
        }

        try
        {
            var existingConfig = await GetGenevaActionConfig(genevaActionsConfig.TeamId);

            existingConfig.GenevaActions = genevaActionsConfig.GenevaActions;

            var result = await _cosmosDbService.UpsertItemAsync<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName, existingConfig, new PartitionKey(existingConfig.TeamId));
            return result;
        }
        catch (KeyNotFoundException ex)
        {
            var result = await _cosmosDbService.UpsertItemAsync<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, _genevaActionsContainerName, genevaActionsConfig, new PartitionKey(genevaActionsConfig.TeamId));
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
            await _cosmosDbService.UpsertItemAsync<TeamConfig>(_cosmosDbService.IcmAgentDatabaseName, _teamContainerName, teamConfig, new PartitionKey(teamConfig.TeamId));
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
