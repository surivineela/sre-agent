// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class ApplensDetectorPlugin
    {
        private readonly ILogger<ApplensDetectorPlugin> _logger;
        private readonly IApplensService _applensService;
        private readonly ITeamsClient _teamsClient;

        public ApplensDetectorPlugin(IApplensService applensService, ILogger<ApplensDetectorPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _applensService = applensService;
            _teamsClient = teamsClient;
        }

        [KernelFunction("get_detector_response")]
        [Description("Gets detector response for a specified resource")]
        public async Task<string> GetDetectorResponse(
            [Description("Subscription ID")] string subscriptionId,
            [Description("Resource Group name")] string resourceGroup,
            [Description("Provider name")] string provider,
            [Description("Resource Type")] string resourceType,
            [Description("Resource Name")] string resourceName,
            [Description("ID of the detector to run")] string detectorId,
            [Description("Optional start time for the detector in ISO format")] string startTime = null,
            [Description("Optional end time for the detector in ISO format. MUST be within 3 days of startTime")] string endTime = null,
            Kernel kernel = null)
        {
            string resourceId = $"subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}";
            
            var logMessage = $"[get_detector_response][{DateTime.UtcNow}] Invoked with detectorId {detectorId} on resourceId {resourceId}";
            if (kernel != null)
            {
                await kernel.LogInformation(logMessage, _logger, _teamsClient);
            }
            else
            {
                _logger.LogInformation(logMessage);
            }

            DateTime? parsedStartTime = null;
            DateTime? parsedEndTime = null;

            if (!string.IsNullOrEmpty(startTime))
            {
                parsedStartTime = DateTime.Parse(startTime);
            }

            if (!string.IsNullOrEmpty(endTime))
            {
                parsedEndTime = DateTime.Parse(endTime);
            }

            return await _applensService.GetDetectorResponse(resourceId, detectorId, parsedStartTime, parsedEndTime);
        }

        [KernelFunction("get_analysis_response")]
        [Description("Gets analysis response for a specified resource")]
        public async Task<string> GetAnalysisResponse(
            [Description("Subscription ID")] string subscriptionId,
            [Description("Resource Group name")] string resourceGroup,
            [Description("Provider name")] string provider,
            [Description("Resource Type")] string resourceType,
            [Description("Resource Name")] string resourceName,
            [Description("ID of the analysis to run")] string analysisId,
            [Description("Optional start time for the analysis in ISO format")] string startTime = null,
            [Description("Optional end time for the analysis in ISO format. MUST be within 3 days of startTime")] string endTime = null,
            Kernel kernel = null)
        {
            string resourceId = $"subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}";
            
            var logMessage = $"[get_analysis_response][{DateTime.UtcNow}] Invoked with analysisId {analysisId} on resourceId {resourceId}";
            if (kernel != null)
            {
                await kernel.LogInformation(logMessage, _logger, _teamsClient);
            }
            else
            {
                _logger.LogInformation(logMessage);
            }

            DateTime? parsedStartTime = null;
            DateTime? parsedEndTime = null;

            if (!string.IsNullOrEmpty(startTime))
            {
                parsedStartTime = DateTime.Parse(startTime);
            }

            if (!string.IsNullOrEmpty(endTime))
            {
                parsedEndTime = DateTime.Parse(endTime);
            }

            return await _applensService.GetAnalysisResponse(resourceId, analysisId, parsedStartTime, parsedEndTime);
        }
    }
}
