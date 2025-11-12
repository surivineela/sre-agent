// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Services
{
    public class ApplensService : IApplensService
    {
        private readonly ILogger<ApplensService> _logger;
        private readonly DiagnosticsHelper _diagnosticsHelper;

        public ApplensService(ILogger<ApplensService> logger, DiagnosticsHelper diagnosticsHelper)
        {
            _logger = logger;
            _diagnosticsHelper = diagnosticsHelper;
        }

        /// <summary>
        /// Gets detector response for a resource
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="detectorId">The ID of the detector to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing detector results</returns>
        public async Task<string> GetDetectorResponse(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation($"Getting detector response for resource {resourceId} with detector {detectorId}");
                var result = await _diagnosticsHelper.GetDetectorResponseWithTime(resourceId, detectorId, startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting detector response for resource {resourceId} with detector {detectorId}");
                return $"Error getting detector response: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets analysis for a resource
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="analysisId">The ID of the analysis to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing analysis results</returns>
        public async Task<string> GetAnalysisResponse(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation($"Getting analysis response for resource {resourceId} with analysis {analysisId}");
                var result = await _diagnosticsHelper.GetAnalysisWithTime(resourceId, analysisId, startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting analysis response for resource {resourceId} with analysis {analysisId}");
                return $"Error getting analysis response: {ex.Message}";
            }
        }
    }
}
