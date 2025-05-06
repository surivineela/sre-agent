// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Services;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;
using StackExchange.Redis;
using System.ComponentModel;
using static Kusto.Cloud.Platform.Instrumentation.DatabasesNamesMapping;

namespace FirstPartyAgent.Plugins
{
    public class ColdStartPlugin
    {
        private readonly ILogger<ColdStartPlugin> _logger;
        private readonly Kernel _kernel;
        private IKustoPlugin _kustoPlugin;

        public ColdStartPlugin(ILogger<ColdStartPlugin> logger, Kernel kernel, IKustoPlugin kustoPlugin)
        {
            _logger = logger;
            _kernel = kernel;
            _kustoPlugin = kustoPlugin;
        }

        [KernelFunction("find_request_general_info")]
        [Description("find general info about the http request.")]
        public async Task<string> FindRequestGeneralInfo(
            string activityId,
            string utcDateTime)
        {
            try
            {
                _logger.LogInformation($"Initializing FindColdStartRegion for ActivityId: {activityId}, UTC DateTime: {utcDateTime}.");
                var clusterName = "wawscus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var kustoQuery = GetRequestGeneralInfoQuery(activityId, utcDateTime);
                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride, _kernel);
                return kustoResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for ActivityId: {activityId}.");
                return $"An error occurred: {ex.Message}";
            }
        }

        [KernelFunction("find_coldtart_request_breakdown")]
        [Description("find breakdown of the http cold start request.")]
        public async Task<string> GetColdStartRequestDetails(
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
                else
                {
                    return "Unsupported consumption type.";
                }

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride, _kernel);
                return kustoResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for ActivityId: {activityId}.");
                return $"An error occurred: {ex.Message}";
            }
        }

        [KernelFunction("find_coldtart_request_breakdown_legion")]
        [Description("find breakdown of the http cold start request from legion.")]
        public async Task<string> GetColdStartRequestDetailsFromLegion(
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

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(legionClusterName, databaseName, kustoQuery, nowOverride, _kernel);
                return kustoResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for PodName: {podName}.");
                return $"An error occurred: {ex.Message}";
            }
        }

        [KernelFunction("coldtart_for_sla_sites")]
        [Description("Show cold start trends for SLA sites.")]
        public async Task<string> GetColdStartDetailsForSlaSites(
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

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride, _kernel);
                return kustoResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for SLA sites.");
                return $"An error occurred: {ex.Message}";
            }
        }

        [KernelFunction("coldstart_profile_data")]
        [Description("Show Profile data for prod cold start SLA sites.")]
        public async Task<string> GetColdStartProfileData()
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartProfileData");
                var clusterName = "wawseus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var kustoQuery = GetColdStartProfileDataQuery();

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride, _kernel);
                return kustoResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for SLA sites.");
                return $"An error occurred: {ex.Message}";
            }
        }

        [KernelFunction("coldstart_profile_data_details")]
        [Description("Show detailed Profile data for prod cold start SLA sites.")]
        public async Task<string> GetColdStartProfileDataDetails()
        {
            try
            {
                _logger.LogInformation($"Initializing GetColdStartProfileDataDetails");
                var clusterName = "wawseus";
                var databaseName = "wawsprod";
                DateTime? nowOverride = null;

                var kustoQuery = GetColdStartProfileDataQueryDetails();

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, nowOverride, _kernel);
                return kustoResult.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing the cold start process for SLA sites.");
                return $"An error occurred: {ex.Message}";
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

        private static string GetRequestGeneralInfoQuery(string activityId, string utcDateTime)
        {
            var query = $@"
                let approxDateTime = datetime({utcDateTime});
                let activityId = '{activityId}';
                All(""AntaresIISLogFrontEndTable"")
                | where TIMESTAMP between (approxDateTime - 1h .. approxDateTime + 1h)
                | where ActivityId contains activityId
                | extend ConsumptionType = case(
                    EventPrimaryStampName in (GetWindowsVmssStamps()), ""Windows Consumption"",
                    EventPrimaryStampName in (AllFlexConsumptionAntaresStamps()), ""Flex Consumption"",
                    EventPrimaryStampName in (GetLinuxStamps()), ""Linux Consupmtion CV1"",
                    ""Unknown""
                )
                | project KustoCluster, ConsumptionType, TIMESTAMP, S_sitename, Time_taken, UrlRewriteTime, ArrTime, DSCallTime, Sc_status, Cs_method, Cs_uri_stem, EventPrimaryStampName";
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
                | parse LegionKustoCluster with LegionCluster ""kusto.windows.net""
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
                percentile(ColdStartTime, 50),
                percentile(JitTime, 50),
                percentile(FunctionsGCTime, 50),
                percentile(DiskReadTime, 50),
                percentile(GCAllocationInBytes, 50),
                percentile(FunctionsMemoryHardFaultTime, 50),
                percentile(TotalDwasOutboundCallsTime, 50),
                percentile(TotalDwasProvisioningTime, 50),
                percentile(DwasJitTime, 50),
                percentile(LanguageWorkerJitTime, 50),
                percentile(LanguageWorkerGCTime, 50),
                percentile(LanguageWorkerMemoryHardFaultTime, 50),
                percentile(LanguageWorkerAssemblyLoaderTime, 50),
                percentile(JitCount, 50), 
                percentile(LanguageWorkerJitCount, 50),
                percentile(LanguageWorkerAssemblyLoaderCount, 50),
                percentile(MiniYarpJitTime, 50)
                by bin(TIMESTAMP, 7d), Stack
            | order by TIMESTAMP asc
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

    }
}
