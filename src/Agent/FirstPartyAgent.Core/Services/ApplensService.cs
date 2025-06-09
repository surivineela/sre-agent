// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services
{
    public class ApplensService : IApplensService
    {
        private readonly ILogger<ApplensService> _logger;
        private readonly ApplensSettings _applensSettings;
        private readonly DiagnosticsHelper _diagnosticsHelper;

        public ApplensService(ApplensSettings applensSettings, DiagnosticsHelper diagnosticsHelper, ILogger<ApplensService> logger)
        {
            _logger = logger;
            _applensSettings = applensSettings;
            _diagnosticsHelper = diagnosticsHelper;
        }

        public bool IsEnabled()
        {
            return _applensSettings != null && _applensSettings.Enabled;
        }

        /// <summary>
        /// Gets detector response for a resource using DiagnosticHelper
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="detectorId">The ID of the detector to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing detector results</returns>
        public async Task<string> GetDetectorResponse(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (!IsEnabled())
            {
                _logger.LogWarning("Applens service is not enabled");
                return "Applens service is not enabled";
            }

            try
            {
                _logger.LogInformation($"Getting detector response for resource {resourceId} with detector {detectorId}");
                var result = await _diagnosticsHelper.GetDetectorResponseWithTime(resourceId, detectorId, startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting detector response for resource {resourceId} with detector {detectorId}");
                return $"Error getting detector response: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets analysis for a resource using DiagnosticHelper
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="analysisId">The ID of the analysis to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing analysis results</returns>
        public async Task<string> GetAnalysisResponse(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (!IsEnabled())
            {
                _logger.LogWarning("Applens service is not enabled");
                return "Applens service is not enabled";
            }

            try
            {
                _logger.LogInformation($"Getting analysis response for resource {resourceId} with analysis {analysisId}");
                var result = await _diagnosticsHelper.GetAnalysisWithTime(resourceId, analysisId, startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting analysis response for resource {resourceId} with analysis {analysisId}");
                return $"Error getting analysis response: {ex.Message}";
            }
        }
    }

    public class ApplensServiceDisabled : IApplensService
    {
        public bool IsEnabled() => false;

        public Task<string> GetDetectorResponse(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
        {
            return Task.FromResult("Applens service is not enabled");
        }

        public Task<string> GetAnalysisResponse(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null)
        {
            return Task.FromResult("Applens service is not enabled");
        }
    }
}
