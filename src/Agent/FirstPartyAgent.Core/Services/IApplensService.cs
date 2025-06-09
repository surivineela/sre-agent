// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Services
{
    public interface IApplensService
    {
        /// <summary>
        /// Checks if the Applens service is enabled
        /// </summary>
        /// <returns>True if the service is enabled, false otherwise</returns>
        bool IsEnabled();

        /// <summary>
        /// Gets detector response for a resource using ArmHelper
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="detectorId">The ID of the detector to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing detector results</returns>
        Task<string> GetDetectorResponse(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Gets analysis for a resource using ArmHelper
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="analysisId">The ID of the analysis to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing analysis results</returns>
        Task<string> GetAnalysisResponse(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null);
    }
}
