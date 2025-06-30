// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Plugin for invoking Applens detectors and analyses
    /// </summary>
    public class ApplensDetectorPlugin : IApplensDetectorPlugin
    {
        private readonly IApplensService _applensService;
        private readonly ILogger<ApplensDetectorPlugin> _logger;

        public ApplensDetectorPlugin(IApplensService applensService, ILogger<ApplensDetectorPlugin> logger)
        {
            _applensService = applensService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> InvokeDetector(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                return "Resource ID cannot be empty";
            }

            if (string.IsNullOrEmpty(detectorId))
            {
                return "Detector ID cannot be empty";
            }

            try
            {
                _logger.LogInternalInformation($"Invoking detector {detectorId} for resource {resourceId}");
                var result = await _applensService.GetDetectorResponse(resourceId, detectorId, startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error invoking detector {detectorId} for resource {resourceId}");
                return $"Error invoking detector: {ex.Message}";
            }
        }

        /// <inheritdoc/>
        public async Task<string> InvokeAnalysis(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                return "Resource ID cannot be empty";
            }

            if (string.IsNullOrEmpty(analysisId))
            {
                return "Analysis ID cannot be empty";
            }

            try
            {
                _logger.LogInternalInformation($"Invoking analysis {analysisId} for resource {resourceId}");
                var result = await _applensService.GetAnalysisResponse(resourceId, analysisId, startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error invoking analysis {analysisId} for resource {resourceId}");
                return $"Error invoking analysis: {ex.Message}";
            }
        }
    }
}
