// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace Agent.Plugins.Interface
{
    public interface IApplensDetectorPlugin
    {
        /// <summary>
        /// Invokes a detector for a given resource
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="detectorId">The ID of the detector to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing detector results</returns>
        Task<string> InvokeDetector(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null);
        
        /// <summary>
        /// Invokes an analysis for a given resource
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="analysisId">The ID of the analysis to run</param>
        /// <param name="startTime">Optional start time for the analysis</param>
        /// <param name="endTime">Optional end time for the analysis</param>
        /// <returns>JSON string containing analysis results</returns>
        Task<string> InvokeAnalysis(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null);
    }
}
