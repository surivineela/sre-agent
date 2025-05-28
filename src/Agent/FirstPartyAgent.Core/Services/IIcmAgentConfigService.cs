using FirstPartyAgent.Core.Models;

namespace FirstPartyAgent.Core.Services;
public interface IIcmAgentConfigService
{
    bool IsEnabled();
    Task<List<TeamConfig>> GetOnboardedLoops();
    Task<List<ICMAlertConfig>> GetLoopAlertConfigs(int? loopId);
    Task<List<AlertDetails>> GetLoopAlerts(int loopId);
    Task<List<IcmTeam>> GetIcmTeams();
    Task<AgentFactoryConfigCosmos<T>> GetAgentFactoryConfig<T>(string id);
    Task<List<string>> GetAgentFactoryConfigNames();
    Task UpsertAgentFactoryConfig<T>(AgentFactoryConfigCosmos<T> config);
    Task<List<AlertDetails>> GetAlerts();
    Task<ICMAlertConfig> GetAlertConfig(int loopId, string alertId);
    Task<string> CreateAlertConfig(ICMAlertConfig alertConfig);
    Task UpdateAlertConfig(ICMAlertConfig alertConfig, int loopId, string alertId);
    Task<List<IcmIncidentBasicInfo>> GetIncidentsByTeamAlert(int teamId, int numOfDays, string title);
    Task<List<AgentDeployment>> GetAgentDeployments(int loopId);
    Task<GenevaActionsConfigCosmos> GetGenevaActionConfig(int teamId);
    
    Task<GenevaActionsConfigCosmos> SaveGenevaActionsConfig(GenevaActionsConfigCosmos genevaActionsConfig);
    Task<List<string>> ListAllContainers();
    Task<List<string>> GetAllDocumentIds(string containerName);
    Task<string> GetDocumentById(string containerName, string documentId); // Changed T to string
    Task<string> UpsertDocument(string containerName, string documentJson); // Changed T to string and parameter name

}


