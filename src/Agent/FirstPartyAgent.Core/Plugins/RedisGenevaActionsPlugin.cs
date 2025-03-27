using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class RedisGenevaActionsPlugin
    {
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly ILogger<RedisGenevaActionsPlugin> _logger;
        private readonly ITeamsClient _teamsClient;

        public RedisGenevaActionsPlugin(ICMWorkflowClient icmWorkflowClient, ILogger<RedisGenevaActionsPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _icmWorkflowClient = icmWorkflowClient;
            _teamsClient = teamsClient;
        }

        private async Task LogInformation(string info)
        {
            _logger.LogInformation(info);
            if (_teamsClient.IsEnabled() && _teamsClient.SendLogsToTeams())
            {
                await _teamsClient.PostMessageOnTeams(info).ConfigureAwait(false);
            }
        }
        
        [KernelFunction("get_redis_cache_deployment_details")]
        [Description("Get deployment details of redis cache from Geneva")]
        public async Task<string> GetRedisDeploymentDetailsFromGeneva(
            [Description("Incident Id")] string incidentId,
           [Description("Redis Cache Name")] string redisCacheName)
        {
            await LogInformation($"[get_redis_cache_deployment_details][{DateTime.UtcNow}] Invoked with redisCacheName {redisCacheName}");
            var result = await _icmWorkflowClient.GetRedisDeploymentDetailsFromGenevaAsync(redisCacheName);
            var fileContentBase64 = TextProcessingHelpers.Base64Encode(result);
            var fileName = $"{redisCacheName}_deployment_details.txt";
            var attachmentResult = await _icmWorkflowClient.AddAttachmentToIncident(incidentId, fileName, fileContentBase64);
            if (attachmentResult == "Success")
            {
                _logger.LogInformation($"Successfully added attachment {fileName} to incident {incidentId}");
                result = result + $"\n\nAdded the full details as an attachment {fileName} to the incident {incidentId}";
            }
            else
            {
                _logger.LogError($"Failed to add attachment to incident {incidentId}. Error: {attachmentResult}");
            }
            
            return result;
        }
    }
}
