// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Helpers;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1
{
    public record AgentMetadataResponse(
        string Name,
        string SubscriptionId,
        string ResourceGroup
    );

    [ApiController]
    [Route("api/v1/[controller]")]
    public class MetadataController(
        ILogger<MetadataController> logger,
        IWebHostEnvironment environment) : ControllerBase
    {
        /// <summary>
        /// Returns metadata about the current agent instance including name, subscription, and resource group.
        /// </summary>
        [HttpGet]
        [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadReadActionId)]
        public IActionResult GetAgentMetadata()
        {
            try
            {
                var isProd = environment.IsProduction();

                var response = new AgentMetadataResponse(
                    Name: AgentNameHelper.GetAgentName(isProd),
                    SubscriptionId: AgentNameHelper.GetSubscriptionId(isProd),
                    ResourceGroup: AgentNameHelper.GetResourceGroupName(isProd)
                );

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogInternalError(ex, "Failed to retrieve agent metadata - environment variables not set");
                return StatusCode(503, new { error = "Agent metadata not available", message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error retrieving agent metadata");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
