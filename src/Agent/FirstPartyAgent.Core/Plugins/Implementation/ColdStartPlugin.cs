// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public class ColdStartPlugin
    {
        private readonly ILogger<ColdStartPlugin> _logger;
        private readonly Kernel _kernel;
        private readonly IKustoPluginClient _kustoPlugin;
        private readonly AlertHandlerService _alertHandlerService;

        public ColdStartPlugin(ILogger<ColdStartPlugin> logger, Kernel kernel, IKustoPluginClient kustoPlugin, AlertHandlerService alertHandlerService)
        {
            _logger = logger;
            _kernel = kernel;
            _kustoPlugin = kustoPlugin;
            _alertHandlerService = alertHandlerService;
        }

        public sealed class KustoQueryResponse
        {
            public string KustoQuery { get; set; }
            public string KustoResult { get; set; }
        }

        [KernelFunction("find_request_general_info")]
        [Description("find general info about the http request.")]
        public async Task<List<KustoQueryResponse>> FindRequestGeneralInfo(
            string siteName,
            string url,
            string activityId,
            string utcDateTime)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing FindColdStartRegion for SiteName {siteName}, Url {url}, ActivityId: {activityId}, UTC DateTime: {utcDateTime}.");
                var clusterName = "wawscus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                if (!DateTime.TryParse(utcDateTime, out var utcDateTimeParsed))
                {
                    responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"Invalid  DateTime: {utcDateTime}" });
                    return responses;
                }

                string kustoQuery = string.Empty;
                // If the UTC date time is older than 18 hours, use the analytics query for better performance
                if (utcDateTimeParsed <= DateTime.Now.AddHours(-30))
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["siteName"] = siteName,
                        ["url"] = url,
                        ["activityId"] = activityId,
                        ["utcDateTime"] = utcDateTime
                    };
                    kustoQuery = ReadAndFormatKqlQuery("GetRequestGeneralInfoQueryFromAnalytics", parameters);
                }
                else
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["siteName"] = siteName,
                        ["url"] = url,
                        ["activityId"] = activityId,
                        ["utcDateTime"] = utcDateTime
                    };
                    kustoQuery = ReadAndFormatKqlQuery("GetRequestGeneralInfoQueryFromWaws", parameters);
                }

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);

                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for ActivityId: {activityId}.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("find_coldtart_request_breakdown")]
        [Description("find breakdown of the http cold start request.")]
        public async Task<KustoQueryResponse> GetColdStartRequestDetails(
            string clusterName,
            string consumptionType,
            string activityId,
            string utcDateTime)
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartRequestDetails for ActivityId: {activityId}, UTC DateTime: {utcDateTime}.");
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                string kustoQuery = string.Empty;
                if (consumptionType.Contains("Windows Consumption", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["activityId"] = activityId,
                        ["utcDateTime"] = utcDateTime
                    };
                    kustoQuery = ReadAndFormatKqlQuery("GetColdStartRequestDetailsForWindowsConsumption", parameters);
                }
                else if (consumptionType.Contains("Flex Consumption", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["activityId"] = activityId,
                        ["utcDateTime"] = utcDateTime
                    };
                    kustoQuery = ReadAndFormatKqlQuery("GetColdStartRequestDetailsForFlexConsumption", parameters);
                }
                else if (consumptionType.Contains("Linux Consumption", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["activityId"] = activityId,
                        ["utcDateTime"] = utcDateTime
                    };
                    kustoQuery = ReadAndFormatKqlQuery("GetColdStartRequestDetailsForLinuxConsumption", parameters);
                }
                else
                {
                    return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = "Unsupported consumption type." };
                }

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for ActivityId: {activityId}.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("find_coldtart_request_breakdown_legion")]
        [Description("find breakdown of the http cold start request from legion.")]
        public async Task<KustoQueryResponse> GetColdStartRequestDetailsFromLegion(
        string legionClusterName,
        string podName,
        string utcDateTime)
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartRequestDetailsFromLegion for PodName: {podName}, UTC DateTime: {utcDateTime}.");
                var databaseName = "legion";
                DateTime? nowOverride = null;

                var parameters = new Dictionary<string, string>
                {
                    ["podName"] = podName,
                    ["utcDateTime"] = utcDateTime
                };
                var kustoQuery = ReadAndFormatKqlQuery("GetColdStartRequestDetailsForFlexConsumptionFromLegion", parameters);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(legionClusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for PodName: {podName}.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("coldtart_for_sla_sites")]
        [Description("Show cold start trends for SLA sites.")]
        public async Task<KustoQueryResponse> GetColdStartDetailsForSlaSites(
        int days = 120,
        string platform = "Legion",
        string stack = "")
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartDetailsForSlaSites for Platform: {platform}, Stack: {stack}, Days: {days}.");
                var clusterName = "wawsaneus.eastus";
                var databaseName = "wawsanprod";
                DateTime? nowOverride = null;

                var parameters = new Dictionary<string, string>
                {
                    ["days"] = days.ToString(),
                    ["platform"] = platform,
                    ["stack"] = stack
                };
                var kustoQuery = ReadAndFormatKqlQuery("GetColdStartQueryForSlaSites", parameters);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for SLA sites.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("coldstart_profile_data")]
        [Description("Show Profile data for prod cold start SLA sites.")]
        public async Task<KustoQueryResponse> GetColdStartProfileData()
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartProfileData");
                var clusterName = "wawseus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var parameters = new Dictionary<string, string>();
                var kustoQuery = ReadAndFormatKqlQuery("GetColdStartProfileData", parameters);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for SLA sites.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("coldstart_profile_data_details")]
        [Description("Show detailed Profile data for prod cold start SLA sites.")]
        public async Task<KustoQueryResponse> GetColdStartProfileDataDetails()
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartProfileDataDetails");
                var clusterName = "wawseus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var parameters = new Dictionary<string, string>();
                var kustoQuery = ReadAndFormatKqlQuery("GetColdStartProfileDataDetails", parameters);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for SLA sites.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("run_coldstart_status")]
        [Description("Runs the cold start mission report alert for pthe past 1 day.")]
        public async Task<List<KustoQueryResponse>> RunColdStartStatus()
        {
            var responses = new List<KustoQueryResponse>();
            var alertId = "83083541-dadd-4174-9abc-ded155969264"; // Cold Start mission report Status Alert Id
            try
            {
                var alertDetails = await _alertHandlerService.GetAzureAlertingDetailsById(alertId);

                if (alertDetails == null)
                {
                    _logger.LogError($"Alert details not found for AlertId: {alertId}.");
                    responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = "Alert details not found." });
                    return responses;
                }

                var kustoQuery = alertDetails.PrimaryKustoQuery.KustoQuery;
                var clusterName = "wawscus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, NowOverride: nowOverride);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });

                foreach(var secondaryKustoQuery in alertDetails.SecondaryKustoQueries)
                {
                    if (secondaryKustoQuery.Title.Contains("SLA Flex Consumption", StringComparison.OrdinalIgnoreCase) ||
                        secondaryKustoQuery.Title.Contains("Consumption P99 and P99.9 per OS", StringComparison.OrdinalIgnoreCase) ||
                        secondaryKustoQuery.Title.Contains("Flex P99 breakdown", StringComparison.OrdinalIgnoreCase))
                    {
                        var secondaryKustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, secondaryKustoQuery.KustoQuery, NowOverride: nowOverride);
                        responses.Add(new KustoQueryResponse { KustoQuery = secondaryKustoQuery.KustoQuery, KustoResult = secondaryKustoResult.Result });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while running the alert kusto query for AlertId: {alertId}.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("run_coldstart_regression_analysis")]
        [Description("Runs the cold start regression analysis.")]
        public async Task<KustoQueryResponse> RunColdStartRegressionAnalysis()
        {
            try
            {
                _logger.LogInformation($"Initializing ColdStartRegressionAnalysis.");
                var clusterName = "wawscus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var parameters = new Dictionary<string, string>();
                var kustoQuery = ReadAndFormatKqlQuery("GetColdStartStatusByStage", parameters);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing ColdStartRegressionAnalysis.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("run_coldstart_regression_analysis_per_region")]
        [Description("Runs the cold start regression analysis per region.")]
        public async Task<KustoQueryResponse> RunColdStartRegressionAnalysisPerRegion()
        {
            try
            {
                _logger.LogInformation($"Initializing ColdStartRegressionAnalysisPerRegion.");
                var clusterName = "wawscus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var parameters = new Dictionary<string, string>();
                var kustoQuery = ReadAndFormatKqlQuery("GetColdStartStatusByRegion", parameters);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing ColdStartRegressionAnalysisPerRegion.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("run_coldstart_alert_kusto_query")]
        [Description("Runs the kusto query for the cold start alert and returns the result.")]
        public async Task<KustoQueryResponse> RunAlertKustoQuery(string alertId)
        {
            try
            {
                var alertDetails = await _alertHandlerService.GetAzureAlertingDetailsById(alertId);

                if (alertDetails == null)
                {
                    _logger.LogError($"Alert details not found for AlertId: {alertId}.");
                    return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = "Alert details not found." };
                }

                if (alertDetails.KustoClusters == null)
                {
                    _logger.LogWarning($"No Kusto clusters found for AlertId: {alertId}.");
                    alertDetails.KustoClusters = new List<KustoCluster>();
                    alertDetails.KustoClusters.Add(new KustoCluster
                    {
                        Cloud = "wawscus",
                        ServiceName = "wawsprod",
                        Cluster = "wawscus",
                        Database = "wawsprod"
                    });
                }

                var primaryCluster = alertDetails.KustoClusters.FirstOrDefault();
                var kustoQuery = alertDetails.PrimaryKustoQuery.KustoQuery;
                var clusterName = primaryCluster.Cluster;
                var databaseName = primaryCluster.Database;
                DateTime? nowOverride = null;

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, NowOverride: nowOverride);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while running the alert kusto query for AlertId: {alertId}.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        /// <summary>
        /// Reads a KQL query from a file and formats it with the provided parameters.
        /// </summary>
        /// <param name="fileName">The name of the KQL file (without extension)</param>
        /// <param name="parameters">Dictionary of parameter names and values to replace in the query</param>
        /// <returns>Formatted query string</returns>
        private static string ReadAndFormatKqlQuery(string fileName, Dictionary<string, string> parameters)
        {
            var kqlFilePath = GetKqlFilePath(fileName);

            if (!File.Exists(kqlFilePath))
            {
                throw new FileNotFoundException($"KQL file not found: {kqlFilePath}");
            }

            var queryTemplate = File.ReadAllText(kqlFilePath);
            return FormatQuery(parameters, queryTemplate);
        }

        /// <summary>
        /// Gets the full path to a KQL file in the ColdStart queries directory.
        /// </summary>
        /// <param name="fileName">The name of the KQL file (without extension)</param>
        /// <returns>Full path to the KQL file</returns>
        private static string GetKqlFilePath(string fileName)
        {
            var baseDirectory = AppContext.BaseDirectory;
            return Path.Combine(baseDirectory, "Plugins", "Definitions", "Queries", "ColdStart", $"{fileName}.kql");
        }

        /// <summary>
        /// Replaces ##parameter## placeholders in the query template with actual values.
        /// </summary>
        /// <param name="parameters">Dictionary of parameter names and values</param>
        /// <param name="queryTemplate">The query template with ##parameter## placeholders</param>
        /// <returns>Formatted query with parameters replaced</returns>
        private static string FormatQuery(Dictionary<string, string> parameters, string queryTemplate)
        {
            var formatted = queryTemplate;

            foreach (var param in parameters)
            {
                var placeholder = $"##{param.Key}##";
                formatted = formatted.Replace(placeholder, param.Value);
            }

            return formatted;
        }
    }
}
