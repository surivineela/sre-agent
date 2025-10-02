// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Configuration;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agent.Web.Controllers.v1
{
    public record FeatureStatusResponse(
        Dictionary<string, bool> Features
    );

    [ApiController]
    [Route("api/v1/[controller]")]
    public class FeatureController(
        IOptions<ScheduledTaskSettings> scheduledTaskSettings,
        IOptions<AgentMemorySettings> agentMemorySettings,
        ILogger<FeatureController> logger) : ControllerBase
    {
        [HttpGet("status")]
        [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadReadActionId)]
        public IActionResult GetFeatureStatus()
        {
            try
            {
                var features = new Dictionary<string, bool>
                {
                    ["scheduledTasks"] = scheduledTaskSettings.Value.Enabled,
                    ["agentMemory"] = agentMemorySettings.Value.Enabled
                };

                var response = new FeatureStatusResponse(Features: features);

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error retrieving feature status");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("status/{featureName}")]
        [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadReadActionId)]
        public IActionResult GetFeatureStatus(string featureName)
        {
            try
            {
                bool? featureEnabled = featureName.ToLowerInvariant() switch
                {
                    "scheduledtasks" => scheduledTaskSettings.Value.Enabled,
                    "agentmemory" => agentMemorySettings.Value.Enabled,
                    _ => null
                };

                if (featureEnabled == null)
                {
                    return NotFound($"Feature '{featureName}' not found");
                }

                return Ok(new { feature = featureName, enabled = featureEnabled.Value });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error retrieving feature status for {FeatureName}", featureName);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
