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
                    kustoQuery = GetRequestGeneralInfoQueryFromAnalytics(siteName, url, activityId, utcDateTime);
                }
                else
                {
                    kustoQuery = GetRequestGeneralInfoQueryFromWaws(siteName, url, activityId, utcDateTime);
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
                    kustoQuery = GetColdStartRequestDetailsForWindowsConsumption(activityId, utcDateTime);
                }
                else if (consumptionType.Contains("Flex Consumption", StringComparison.OrdinalIgnoreCase))
                {
                    kustoQuery = GetColdStartRequestDetailsForFlexConsumption(activityId, utcDateTime);
                }
                else if (consumptionType.Contains("Linux Consumption", StringComparison.OrdinalIgnoreCase))
                {
                    kustoQuery = GetColdStartRequestDetailsForLinuxConsumption(activityId, utcDateTime);
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

                var kustoQuery = GetColdStartRequestDetailsForFlexConsumptionFromLegion(podName, utcDateTime);

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

                var kustoQuery = GetColdStartQueryForSlaSites(days, platform, stack);

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

                var kustoQuery = GetColdStartProfileDataQuery();

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

                var kustoQuery = GetColdStartProfileDataQueryDetails();

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

                var kustoQuery = GetColdStartStatusByStage();

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

                var kustoQuery = GetColdStartStatusByRegion();

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

        private static string GetColdStartQueryForSlaSites(int days, string platform, string stack)
        {
            var query = $@"
                let operatingSystem = '{platform}';
                let stack = '{stack}';
                WawsAn_dailyfunctionscoldstart
                | where pdate >= ago({days}d)
                | where IsSLACold == int(1)
                | where FunctionMajorVersion == int(4)
                | where OperatingSystem contains operatingSystem
                | where isempty(stack) or (stack =~ 'dotnet' and Stack =~ stack) or (stack !~ 'dotnet' and Stack contains stack)
                | where Sc_status == int(200)
                | extend pdate = format_datetime(pdate, ""yyyy-MM-dd"")
                | summarize P50 = percentile(FETimeTakenMs, 50), P99 = percentile(FETimeTakenMs, 99) by pdate
                | order by pdate asc";
            return query;
        }

        private static string GetRequestGeneralInfoQueryFromWaws(string siteName, string url, string activityId, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let activityId = '{activityId}';
                let siteName = '{siteName}';
                let url = '{url}';
                All(""AntaresIISLogFrontEndTable"")
                | where TIMESTAMP between (approxDateTime - 1h .. approxDateTime + 1h)
                | where (isnotempty(activityId) and ActivityId contains activityId)
                        or (isnotempty(siteName) and S_sitename contains siteName and (isempty(url) or Cs_uri_stem contains url))
                | extend ConsumptionType = case(
                    EventPrimaryStampName in (GetWindowsVmssStamps()), ""Windows Consumption"",
                    EventPrimaryStampName in (AllFlexConsumptionAntaresStamps()), ""Flex Consumption"",
                    EventPrimaryStampName in (GetLinuxStamps()), ""Linux Consumption"",
                    ""Unknown""
                )
                | project KustoCluster, ConsumptionType, TIMESTAMP, S_sitename, ActivityId, Time_taken, UrlRewriteTime, ArrTime, DSCallTime, Sc_status, Cs_method, Cs_uri_stem, EventPrimaryStampName
                | order by Time_taken desc
                | take 10";
            return query;
        }

        private static string GetRequestGeneralInfoQueryFromAnalytics(string siteName, string url, string activityId, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let activityId = '{activityId}';
                let siteName = '{siteName}';
                let url = '{url}';
                let Regions = GetRegions
                | where Cloud == ""Azure""
                | distinct KustoCluster, Region = AntaresAbbreviation;
                cluster(""wawsaneus.eastus"").database(""wawsanprod"").WawsAn_dailyfunctionscoldstart
                | where HttpRequestUTC between (approxDateTime - 1h .. approxDateTime + 1h)
                | where (isnotempty(activityId) and ActivityId contains activityId)
                    or (isnotempty(siteName) and SiteName contains siteName and (isempty(url) or Cs_uri_stem contains url))
                | extend Region = tostring(split(Stamp, '-')[2])
                | extend ConsumptionType = case(
                                               OperatingSystem == ""Windows"", ""Windows Consumption"",
                                               OperatingSystem == ""Legion"", ""Flex Consumption"",
                                               OperatingSystem == ""Linux"", ""Linux Consumption"",
                                               ""Unknown""
                                           )
                | join kind=leftouter Regions on Region
                | project
                    KustoCluster,
                    ConsumptionType,
                    TIMESTAMP = HttpRequestUTC,
                    SiteName,
                    ActivityId,
                    Time_taken = FETimeTakenMs,
                    UrlRewriteTimeMs,
                    //ArrTime
                    DSCallTime,
                    Sc_status,
                    //Cs_method,
                    Cs_uri_stem,
                    EventPrimaryStampName = Stamp
                | order by Time_taken desc
                | take 10";
            return query;
        }


        private static string GetColdStartRequestDetailsForWindowsConsumption(string activityId, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let activityId = '{activityId}';
                AntaresRuntimeWorkerEvents
                | where TIMESTAMP between (approxDateTime - 1h .. approxDateTime + 1h)
                | where RequestId contains activityId
                | where EventId == 15005
                | parse AppHostConfig with * ""DWASFiles\\Sites\\"" PlaceholderProcessName ""\\"" *
                | extend IsExactMatch = iff(PlaceholderMatchScore == 2147483647, 1, 0)
                | project TIMESTAMP, RoleInstance, PlaceholderProcessName, TotalTimeTakenForProvisioning, PlaceholderUsed, IsExactMatch, PlaceholderMatchScore, FcaZipUsed, FcaZipUseFailed, FcaZipWaitMs, ColdStartPerfData";
            return query;
        }

        private static string GetColdStartRequestDetailsForLinuxConsumption(string activityId, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let activityId = '{activityId}';
                AzureContainers
                | where TIMESTAMP between(approxDateTime -1h..approxDateTime + 1h)
                | where ActivityId contains activityId
                | where isnotempty(Address)
                            | extend SiteName = tolower(SiteName)
                | extend ContainerName = tolower(ContainerName)
                | project TIMESTAMP, SiteName, Verb, ContainerName, LatencyInMilliseconds, Address
                | summarize ACIAssignLatency = sum(LatencyInMilliseconds) by SiteName, ContainerName
                | join(
                    FunctionsMetrics
                    | where TIMESTAMP between(approxDateTime - 1h..approxDateTime + 1h)
                    | where Role == ""Microsoft.ContainerInstance""
                    | where EventName in (""linux.container.specialization.zip.download"", ""linux.container.specialization.zip.extract"")
                            | extend AppName = tolower(AppName)
                            | extend RoleInstance = replace_string(tolower(RoleInstance), ""app-"", """")
                    )
                    on $left.ContainerName == $right.RoleInstance, $left.SiteName == $right.AppName
                | summarize
                    AssignTime = take_any(ACIAssignLatency),
                    DownalodTime = maxif(Maximum, EventName == ""linux.container.specialization.zip.download""),
                    ExtractionTime = maxif(Maximum, EventName == ""linux.container.specialization.zip.extract"")";
                return query;
        }

        private static string GetColdStartRequestDetailsForFlexConsumption(string activityId, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let activityId = '{activityId}';
                AntaresRuntimeFrontEndEvents
                | where TIMESTAMP between (approxDateTime - 1h .. approxDateTime + 1h)
                | where ActivityId contains activityId
                | where Details contains ""FirstAcquiredActivityId""
                | parse Details with * ""FirstAcquiredActivityId: "" FirstAcquiredActivityId
                | project FirstAcquiredActivityId, ActivityId
                | join(
                    FunctionsPlatformLogs
                    | where TIMESTAMP between(approxDateTime - 1h..approxDateTime + 1h)
                    | where Address endswith ""/specialize""
                    | project FirstAcquiredActivityId = ActivityId, SpecializationTimeInMs = LatencyInMilliseconds, PodName, ImageName, AllocationLabel
                    ) on FirstAcquiredActivityId
                | join(
                    FunctionsPlatformLogs
                    | where TIMESTAMP between(approxDateTime - 1h..approxDateTime + 1h)
                    | where Address endswith ""/allocate""
                    | project FirstAcquiredActivityId = ActivityId, AllocateTimeInMs = LatencyInMilliseconds, PodName, EventPrimaryStampName
                ) on FirstAcquiredActivityId
                | join cluster(""wawscus"").database(""wawsprod"").FlexConsumptionClusterStampMapping() on $left.EventPrimaryStampName == $right.AntaresStamp
                | parse LegionKustoCluster with LegionCluster "".kusto.windows.net""
                | project  AllocateTimeInMs, SpecializationTimeInMs, ImageName, PodName, AllocationLabel, EventPrimaryStampName, LegionCluster";
            return query;
        }

        private static string GetColdStartRequestDetailsForFlexConsumptionFromLegion(string podName, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let podName = '{podName}';
                SystemLogs
                    | where TIMESTAMP between (approxDateTime - 1h .. approxDateTime + 1h)
                    | where PodName contains podName
                    | where LegionComponent == ""HostNodeAgent""
                    | where Message == ""HNA pod specialize finished.""
                    | where Value contains ""nestednodeagent.specialize""
                    | extend json = parse_json(Value)
                    | extend SpecializationTime =  todatetime(json[""hostrole.specialize.public-start""])
                    | project SpecializationTime, PodName, Tenant, json, LegionStampName, CenturionRoleId, env_dt_traceId    
                    | extend PADownloadAndUnzip = round(todouble(json[""podagent.downloadandunzipconnectionstring-duration""]))
                    | extend PADownloadContentBody = round(todouble(json[""podagent.downloadcontentbody-duration""]))
                    | extend PAUnzip = round(todouble(json[""podagent.unzip-duration""]))";
            return query;
        }

        private static string GetColdStartProfileDataQuery()
        {
            var query = $@"
            FunctionsColdStartAnalyzer
            | where TIMESTAMP > ago(60d)
            | where AppName startswith ""sla-ws-func"" and AppName contains ""cold""
            | extend S_sitename = AppName
            | invoke GetFunctionsSlaSiteProperties()
            | summarize
                count(),
                ColdStartTime = percentile(ColdStartTime, 50),
                FuncHostJitTime = percentile(JitTime, 50),
                FuncHostJitCount = percentile(JitCount, 50),
                FuncHostMemoryHardFaultTime = percentile(FunctionsMemoryHardFaultTime, 50),
                LanguageWorkerJitTime = percentile(LanguageWorkerJitTime, 50),
                LanguageWorkerJitCount = percentile(LanguageWorkerJitCount, 50),
                DiskReadTime = percentile(DiskReadTime, 50),
                LanguageWorkerMemoryHardFaultTime = percentile(LanguageWorkerMemoryHardFaultTime, 50),
                FuncHostGCTime = percentile(FunctionsGCTime, 50),
                FuncHostGCAllocationInBytes = percentile(GCAllocationInBytes, 50),
                LanguageWorkerAssemblyLoaderTime = percentile(LanguageWorkerAssemblyLoaderTime, 50),
                LanguageWorkerAssemblyLoaderCount = percentile(LanguageWorkerAssemblyLoaderCount, 50),
                LanguageWorkerGCTime = percentile(LanguageWorkerGCTime, 50),
                percentile(TotalDwasOutboundCallsTime, 50),
                percentile(TotalDwasProvisioningTime, 50),
                percentile(DwasJitTime, 50),
                percentile(MiniYarpJitTime, 50)
                by Stack
                ";
            return query;
        }

        private static string GetColdStartProfileDataQueryDetails()
        {
            var query = $@"
            FunctionsColdStartAnalyzer
            | where TIMESTAMP > ago(7d)
            | where AppName startswith ""sla-ws-func"" and AppName contains ""cold""
            | extend S_sitename = AppName
            | invoke GetFunctionsSlaSiteProperties()
            | summarize
                take_any(DetailedJIT),
                take_any(DetailedDiskRead),
                take_any(FunctionsHostVersion),
                take_any(FunctionsDetailedMemoryHardFaults),
                take_any(LanguageWorkerDetailedJIT),
                take_any(LanguageWorkerDetailedAssemblyLoader),
                take_any(LanguageWorkerMemoryHardFaults)
                by Stack
            ";
            return query;
        }

        private static string GetColdStartStatusByStage()
        {
            var query = $@"
            let PastNumberOfDays = 120;
            let StartTime = datetime_add('day', -PastNumberOfDays, now());
            let Regions = GetRegions
            | where Cloud == ""Azure""
            | distinct Stage, AntaresAbbreviation;
            cluster(""wawsaneus.eastus"").database(""wawsanprod"").WawsAn_dailyfunctionscoldstart
            | where pdate >= StartTime
            | where SiteName has_all (""sla-ws-func-prod"", ""v4-cold"")
            | where SiteName !contains ""histogram"" and SiteName !contains ""msftint""
            | where Sc_status == int(200)
            | extend S_sitename = SiteName
            | invoke GetFunctionsSlaSiteProperties()
            | parse Stamp with ""waws-prod-"" AntaresAbbreviation ""-"" *
            | join kind=leftouter Regions on AntaresAbbreviation
            | summarize percentiles(FETimeTakenMs, 50, 99) by bin(pdate, 1d), OperatingSystem, Scenario, Stage
            | order by pdate asc
            | summarize
                P50Percentile = percentile(percentile_FETimeTakenMs_50, 50),
                P99Percentile = percentile(percentile_FETimeTakenMs_99, 99),
                P50List = make_list(percentile_FETimeTakenMs_50),
                P99List = make_list(percentile_FETimeTakenMs_99)
                by OperatingSystem, Scenario, Stage
            | order by OperatingSystem, Scenario, Stage asc
            | extend series_decompose_anomalies(P50List), series_decompose_anomalies(P99List, 2.5)
            | mv-expand
                 idx = range(0, array_length(P50List), 1),
                 P50List, series_decompose_anomalies_P50List_ad_flag,
                 P99List, series_decompose_anomalies_P99List_ad_flag
            | extend day = datetime_add('day', toint(idx), StartTime)
            | where  day >= ago(3d)        // only the last 3 days
            | where series_decompose_anomalies_P50List_ad_flag  == 1  or series_decompose_anomalies_P99List_ad_flag == 1
            | project OperatingSystem, Scenario, Stage, P50Number = todouble(P50List), P50ExpectedNumber = P50Percentile, IsP50Regression = tobool(series_decompose_anomalies_P50List_ad_flag), P99Number = todouble(P99List), P99ExpectedNumber = P99Percentile, IsP99Regression = tobool(series_decompose_anomalies_P99List_ad_flag), day
            | order by IsP50Regression desc, P50Number desc, OperatingSystem desc, Stage asc, Scenario asc, IsP99Regression desc, P99Number desc
            | union
            (
            cluster(""wawsaneus.eastus"").database(""wawsanprod"").WawsAn_dailyfunctionscoldstart
            | where pdate >= StartTime
            | where SiteName has_all (""sla-ws-func-prod"", ""v4-cold"")
            | where SiteName !contains ""histogram"" and SiteName !contains ""msftint""
            | where Sc_status == int(200)
            | extend S_sitename = SiteName
            | invoke GetFunctionsSlaSiteProperties()
            | parse Stamp with ""waws-prod-"" AntaresAbbreviation ""-"" *
            | join kind=leftouter Regions on AntaresAbbreviation
            | summarize percentiles(FETimeTakenMs, 50, 99) by bin(pdate, 1d), OperatingSystem, Scenario, Stage
            | order by pdate asc
            | summarize
                P50Percentile = percentile(percentile_FETimeTakenMs_50, 50),
                P99Percentile = percentile(percentile_FETimeTakenMs_99, 99),
                P50List = make_list(percentile_FETimeTakenMs_50),
                P99List = make_list(percentile_FETimeTakenMs_99)
                by OperatingSystem, Scenario, Stage
            | order by OperatingSystem, Scenario, Stage asc
            | extend series_decompose_anomalies(P50List), series_decompose_anomalies(P99List, 2.5)
            | mv-expand
                 idx = range(0, array_length(P50List), 1),
                 P50List, series_decompose_anomalies_P50List_ad_flag,
                 P99List, series_decompose_anomalies_P99List_ad_flag
            | extend day = datetime_add('day', toint(idx), StartTime)
            | where  day >= ago(3d)        // only the last 3 days
            | where series_decompose_anomalies_P50List_ad_flag  == -1  or series_decompose_anomalies_P99List_ad_flag == -1
            | project OperatingSystem, Scenario, Stage, P50Number = todouble(P50List), P50ExpectedNumber = P50Percentile, IsP50Improvement = tobool(series_decompose_anomalies_P50List_ad_flag), P99Number = todouble(P99List), P99ExpectedNumber = P99Percentile, IsP99Improvement = tobool(series_decompose_anomalies_P99List_ad_flag), day
            | order by IsP50Improvement desc, P50Number desc, OperatingSystem desc, Stage asc, Scenario asc, IsP99Improvement desc, P99Number desc
            )
            ";
            return query;
        }

        private static string GetColdStartStatusByRegion()
        {
            var query = $@"
            let PastNumberOfDays = 120;
            let StartTime = datetime_add('day', -PastNumberOfDays, now());
            let Regions = GetRegions
            | where Cloud == ""Azure""
            | distinct Stage, AntaresAbbreviation;
            cluster(""wawsaneus.eastus"").database(""wawsanprod"").WawsAn_dailyfunctionscoldstart
            | where pdate >= StartTime
            | where SiteName has_all (""sla-ws-func-prod"", ""v4-cold"")
            | where SiteName !contains ""histogram"" and SiteName !contains ""msftint""
            | where Sc_status == int(200)
            | extend S_sitename = SiteName
            | invoke GetFunctionsSlaSiteProperties()
            | parse Stamp with ""waws-prod-"" AntaresAbbreviation ""-"" *
            | join kind=leftouter Regions on AntaresAbbreviation
            | summarize percentiles(FETimeTakenMs, 50, 99) by bin(pdate, 1d), OperatingSystem, Scenario, Stage, AntaresAbbreviation
            | order by pdate asc
            | summarize
                P50Percentile = percentile(percentile_FETimeTakenMs_50, 50),
                P99Percentile = percentile(percentile_FETimeTakenMs_99, 99),
                P50List = make_list(percentile_FETimeTakenMs_50),
                P99List = make_list(percentile_FETimeTakenMs_99)
                by OperatingSystem, Scenario, Stage, AntaresAbbreviation
            | order by OperatingSystem, Scenario, Stage asc
            | extend series_decompose_anomalies(P50List), series_decompose_anomalies(P99List, 2.5)
            | mv-expand
                 idx = range(0, array_length(P50List), 1),
                 P50List, series_decompose_anomalies_P50List_ad_flag,
                 P99List, series_decompose_anomalies_P99List_ad_flag
            | extend day = datetime_add('day', toint(idx), StartTime)
            | where  day >= ago(3d)        // only the last 2 days
            | where series_decompose_anomalies_P50List_ad_flag  == 1  or series_decompose_anomalies_P99List_ad_flag == 1
            | project OperatingSystem, Scenario, Stage, AntaresAbbreviation, P50Number = todouble(P50List), P50ExpectedNumber = P50Percentile, IsP50Regression = tobool(series_decompose_anomalies_P50List_ad_flag), P99Number = todouble(P99List), P99ExpectedNumber = P99Percentile, IsP99Regression = tobool(series_decompose_anomalies_P99List_ad_flag), day
            | order by IsP50Regression desc, P50Number desc, OperatingSystem desc, Stage asc, Scenario asc, IsP99Regression desc, P99Number desc
            | union
            (
            cluster(""wawsaneus.eastus"").database(""wawsanprod"").WawsAn_dailyfunctionscoldstart
            | where pdate >= StartTime
            | where SiteName has_all (""sla-ws-func-prod"", ""v4-cold"")
            | where SiteName !contains ""histogram"" and SiteName !contains ""msftint""
            | where Sc_status == int(200)
            | extend S_sitename = SiteName
            | invoke GetFunctionsSlaSiteProperties()
            | parse Stamp with ""waws-prod-"" AntaresAbbreviation ""-"" *
            | join kind=leftouter Regions on AntaresAbbreviation
            | summarize percentiles(FETimeTakenMs, 50, 99) by bin(pdate, 1d), OperatingSystem, Scenario, Stage, AntaresAbbreviation
            | order by pdate asc
            | summarize
                P50Percentile = percentile(percentile_FETimeTakenMs_50, 50),
                P99Percentile = percentile(percentile_FETimeTakenMs_99, 99),
                P50List = make_list(percentile_FETimeTakenMs_50),
                P99List = make_list(percentile_FETimeTakenMs_99)
                by OperatingSystem, Scenario, Stage, AntaresAbbreviation
            | order by OperatingSystem, Scenario, Stage asc
            | extend series_decompose_anomalies(P50List), series_decompose_anomalies(P99List, 2.5)
            | mv-expand
                 idx = range(0, array_length(P50List), 1),
                 P50List, series_decompose_anomalies_P50List_ad_flag,
                 P99List, series_decompose_anomalies_P99List_ad_flag
            | extend day = datetime_add('day', toint(idx), StartTime)
            | where  day >= ago(3d)        // only the last 2 days
            | where series_decompose_anomalies_P50List_ad_flag  == -1  or series_decompose_anomalies_P99List_ad_flag == -1
            | project OperatingSystem, Scenario, Stage, AntaresAbbreviation, P50Number = todouble(P50List), P50ExpectedNumber = P50Percentile, IsP50Improvement = tobool(series_decompose_anomalies_P50List_ad_flag), P99Number = todouble(P99List), P99ExpectedNumber = P99Percentile, IsP99Improvement = tobool(series_decompose_anomalies_P99List_ad_flag), day
            | order by IsP50Improvement desc, P50Number desc, OperatingSystem desc, Stage asc, Scenario asc, IsP99Improvement desc, P99Number desc
            )
            | where Scenario !contains ""ps""
            | where OperatingSystem != ""Linux""
            ";
            return query;
        }

    }
}
