// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Definition for the ApplensDetector plugin
    /// </summary>
    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.Diagnostics)]
    public class ApplensDetectorPluginDefinition
    {
        private readonly IApplensDetectorPlugin _plugin;

        /// <summary>
        /// Creates a new instance of the ApplensDetectorPluginDefinition
        /// </summary>
        /// <param name="plugin">The ApplensDetector plugin implementation</param>
        public ApplensDetectorPluginDefinition(IApplensDetectorPlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Invokes an Applens detector for a specific Azure resource
        /// </summary>
        /// <param name="subscriptionId">The subscription ID of the Azure resource</param>
        /// <param name="resourceGroup">The resource group name</param>
        /// <param name="provider">The provider name</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="resourceName">The resource name</param>
        /// <param name="detectorId">The ID of the detector to run</param>
        /// <param name="startTime">Optional start time for the analysis in ISO 8601 format</param>
        /// <param name="endTime">Optional end time for the analysis in ISO 8601 format</param>
        /// <returns>JSON string containing detector results</returns>
        [Description("Invokes an Applens detector for a specific Azure resource")]
        public Task<string> InvokeDetector(
            [Description("Subscription ID")] string subscriptionId,
            [Description("Resource Group name")] string resourceGroup,
            [Description("Provider name")] string provider,
            [Description("Resource Type")] string resourceType,
            [Description("Resource Name")] string resourceName,
            [Description("The ID of the detector to run")] string detectorId,
            [Description("Optional start time for the analysis in ISO 8601 format")] string startTime = "",
            [Description("Optional end time for the analysis in ISO 8601 format")] string endTime = "")
        {
            DateTime? startTimeDate = TryParseDateTime(startTime);
            DateTime? endTimeDate = TryParseDateTime(endTime);

            string resourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}";

            return _plugin.InvokeDetector(resourceId, detectorId, startTimeDate, endTimeDate);
        }

        /// <summary>
        /// Invokes an Applens analysis for a specific Azure resource
        /// </summary>
        /// <param name="subscriptionId">The subscription ID of the Azure resource</param>
        /// <param name="resourceGroup">The resource group name</param>
        /// <param name="provider">The provider name</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="resourceName">The resource name</param>
        /// <param name="analysisId">The ID of the analysis to run</param>
        /// <param name="startTime">Optional start time for the analysis in ISO 8601 format</param>
        /// <param name="endTime">Optional end time for the analysis in ISO 8601 format</param>
        /// <returns>JSON string containing analysis results</returns>
        [Description("Invokes an Applens analysis for a specific Azure resource")]
        public Task<string> InvokeAnalysis(
            [Description("Subscription ID")] string subscriptionId,
            [Description("Resource Group name")] string resourceGroup,
            [Description("Provider name")] string provider,
            [Description("Resource Type")] string resourceType,
            [Description("Resource Name")] string resourceName,
            [Description("The ID of the analysis to run")] string analysisId,
            [Description("Optional start time for the analysis in ISO 8601 format")] string startTime = "",
            [Description("Optional end time for the analysis in ISO 8601 format")] string endTime = "")
        {
            DateTime? startTimeDate = TryParseDateTime(startTime);
            DateTime? endTimeDate = TryParseDateTime(endTime);

            string resourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}";

            return _plugin.InvokeAnalysis(resourceId, analysisId, startTimeDate, endTimeDate);
        }

        /// <summary>
        /// Attempts to parse a string to DateTime
        /// </summary>
        /// <param name="dateTimeString">The string to parse</param>
        /// <returns>DateTime if parsing succeeded, null otherwise</returns>
        private static DateTime? TryParseDateTime(string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return null;
            }

            if (DateTime.TryParse(dateTimeString, out DateTime dateTime))
            {
                return dateTime;
            }

            return null;
        }
    }
}
