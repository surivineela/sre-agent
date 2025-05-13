// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class RedisGenevaActionsPlugin
    {
        private readonly IICMWorkflowClient _icmWorkflowClient;
        private readonly ILogger<RedisGenevaActionsPlugin> _logger;
        private readonly ITeamsClient _teamsClient;

        public RedisGenevaActionsPlugin(IICMWorkflowClient icmWorkflowClient, ILogger<RedisGenevaActionsPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _icmWorkflowClient = icmWorkflowClient;
            _teamsClient = teamsClient;
        }

        [KernelFunction("get_redis_cache_deployment_details")]
        [Description("Get deployment details of redis cache from Geneva")]
        public async Task<string> GetRedisDeploymentDetailsFromGeneva(
            [Description("Incident Id")] string incidentId,
           [Description("Redis Cache Name")] string redisCacheName,
           Kernel kernel)
        {
            var logMessage = $"[get_redis_cache_deployment_details][{DateTime.UtcNow}] Invoked with redisCacheName {redisCacheName}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var result = await _icmWorkflowClient.GetRedisDeploymentDetailsFromGenevaAsync(redisCacheName);
            if (!result.StartsWith("Failed"))
            {
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
            }            
            return result;
        }

        [KernelFunction("get_redis_cache_deployment_history")]
        [Description("Get deployment history of redis cache from Geneva")]
        public async Task<string> GetRedisDeploymentHistoryFromGeneva(
            [Description("Incident Id")] string incidentId,
           [Description("Redis Cache Name")] string redisCacheName,
           Kernel kernel)
        {
            var logMessage = $"[get_redis_cache_deployment_history][{DateTime.UtcNow}] Invoked with redisCacheName {redisCacheName}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var result = await _icmWorkflowClient.GetRedisDeploymentHistoryFromGenevaAsync(redisCacheName);
            if (!result.StartsWith("Failed"))
            {
                var fileContentBase64 = TextProcessingHelpers.Base64Encode(result);
                var fileName = $"{redisCacheName}_deployment_history.txt";
                var attachmentResult = await _icmWorkflowClient.AddAttachmentToIncident(incidentId, fileName, fileContentBase64);
                if (attachmentResult == "Success")
                {
                    _logger.LogInformation($"Successfully added attachment {fileName} to incident {incidentId}");
                    result = result + $"\n\nAdded the full history as an attachment {fileName} to the incident {incidentId}";
                }
                else
                {
                    _logger.LogError($"Failed to add attachment to incident {incidentId}. Error: {attachmentResult}");
                }
            }
            return result;
        }
    }
}

