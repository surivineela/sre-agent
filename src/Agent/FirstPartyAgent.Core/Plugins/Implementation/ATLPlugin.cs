// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Kusto;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public class ATLPlugin
    {
        private readonly ILogger<ATLPlugin> _logger;
        private readonly Kernel _kernel;
        private readonly KustoClient _kustoPlugin;
        private readonly static string _databaseName = "wawsprod";
        private readonly static Dictionary<string, string> _stampToCluster = new Dictionary<string, string>
        {
            { "waws-prod-db3-087", "wawsneu" },
            { "waws-prod-db3-173", "wawsneu" },
            { "waws-prod-db3-167", "wawsneu" },
            { "waws-prod-ln1-053", "wawsneu" },
            { "waws-prod-db3-171", "wawsneu" },
            { "waws-prod-cw1-007", "wawsneu" },
            { "waws-prod-ln1-011", "wawsneu" },
            { "waws-prod-ln1-045", "wawsneu" },
            { "waws-prod-sec-007", "wawsneu" },
            { "waws-prod-db3-101", "wawsneu" },
            { "waws-prod-am2-305", "wawsweu" },
            { "waws-prod-par-011", "wawsweu" },
            { "waws-prod-am2-381", "wawsweu" },
            { "waws-prod-am2-431", "wawsweu" },
            { "waws-prod-osl-003", "wawsweu" },
            { "waws-prod-am2-403", "wawsweu" },
            { "waws-prod-am2-409", "wawsweu" },
            { "waws-prod-am2-395", "wawsweu" },
            { "waws-prod-am2-425", "wawsweu" },
            { "waws-prod-am2-415", "wawsweu" },
            { "waws-prod-am2-177", "wawsweu" },
            { "waws-prod-am2-311", "wawsweu" },
            { "waws-prod-am2-447", "wawsweu" },
            { "waws-prod-am2-167", "wawsweu" },
            { "waws-prod-fra-003", "wawsweu" },
            { "waws-prod-am2-341", "wawsweu" },
            { "waws-prod-zrh-003", "wawsweu" },
            { "waws-prod-am2-385", "wawsweu" },
            { "waws-prod-am2-391", "wawsweu" },
            { "waws-prod-bn1-107", "wawseus" },
            { "waws-prod-bn1-113", "wawseus" },
            { "waws-prod-blu-253", "wawseus" },
            { "waws-prod-blu-183", "wawseus" },
            { "waws-prod-blu-089", "wawseus" },
            { "waws-prod-bn1-025", "wawseus" },
            { "waws-prod-bn1-083", "wawseus" },
            { "waws-prod-yq1-005", "wawseus" },
            { "waws-prod-blu-077", "wawseus" },
            { "waws-prod-blu-247", "wawseus" },
            { "waws-prod-blu-249", "wawseus" },
            { "waws-prod-bn1-097", "wawseus" },
            { "waws-prod-blu-231", "wawseus" },
            { "waws-prod-bn1-123", "wawseus" },
            { "waws-prod-blu-255", "wawseus" },
            { "waws-prod-mwh-007", "wawswus" },
            { "waws-prod-bay-063", "wawswus" },
            { "waws-prod-usw3-003", "wawswus" },
            { "waws-prod-bay-081", "wawswus" },
            { "waws-prod-mwh-075", "wawswus" },
            { "waws-prod-sy3-017", "wawseas" },
            { "waws-prod-hk1-025", "wawseas" },
            { "waws-prod-bm1-005", "wawseas" },
            { "waws-prod-hk1-037", "wawseas" },
            { "waws-prod-sg1-019", "wawseas" },
            { "waws-prod-ty1-021", "wawseas" },
            { "waws-prod-pn1-005", "wawseas" },
            { "waws-prod-dxb-003", "wawseas" },
            { "waws-prod-se1-003", "wawseas" },
            { "waws-prod-jinw-003", "wawseas" },
            { "waws-prod-sy3-091", "wawseas" },
            { "waws-prod-os1-009", "wawseas" },
            { "waws-prod-ty1-053", "wawseas" },
            { "waws-prod-ma1-007", "wawseas" },
            { "waws-prod-sg1-057", "wawseas" },
            { "waws-prod-ml1-027", "wawseas" },
            { "waws-prod-jnb21-009", "wawseas" },
            { "waws-prod-ty1-047", "wawseas" },
            { "waws-prod-sn1-161", "wawscus" },
            { "waws-prod-dm1-181", "wawscus" },
            { "waws-prod-ch1-029", "wawscus" },
            { "waws-prod-dm1-157", "wawscus" },
            { "waws-prod-dm1-185", "wawscus" },
            { "waws-prod-cq1-013", "wawscus" },
            { "waws-prod-sn1-147", "wawscus" },
            { "waws-prod-dm1-171", "wawscus" },
            { "waws-prod-yt1-041", "wawscus" },
            { "waws-prod-cy4-011", "wawscus" },
            { "waws-prod-dm1-081", "wawscus" },
            { "waws-prod-euapdm1-001", "wawscus" },
            { "waws-prod-dm1-159", "wawscus" }
        };

        private readonly static HashSet<string> _stampListForMigration = new HashSet<string>
        {
            "waws-prod-am2-385",
            "waws-prod-am2-391",
            "waws-prod-blu-255",
            "waws-prod-bm1-005",
            "waws-prod-cw1-007",
            "waws-prod-cy4-011",
            "waws-prod-db3-167",
            "waws-prod-db3-171",
            "waws-prod-db3-173",
            "waws-prod-dm1-159",
            "waws-prod-dxb-003",
            "waws-prod-euapdm1-001",
            "waws-prod-hk1-025",
            "waws-prod-hk1-037",
            "waws-prod-jinw-003",
            "waws-prod-jnb21-009",
            "waws-prod-ma1-007",
            "waws-prod-ml1-027",
            "waws-prod-os1-009",
            "waws-prod-osl-003",
            "waws-prod-se1-003",
            "waws-prod-sec-007",
            "waws-prod-ty1-047",
            "waws-prod-ty1-053",
            "waws-prod-usw3-003",
            "waws-prod-yq1-005",
            "waws-prod-zrh-003"
        };

        public ATLPlugin(
            ILogger<ATLPlugin> logger,
            Kernel kernel,
            KustoClient kustoPlugin)
        {
            _logger = logger;
            _kernel = kernel;
            _kustoPlugin = kustoPlugin;
        }

        public sealed class KustoQueryResponse
        {
            public string KustoQuery { get; set; }
            public string KustoResult { get; set; }
        }

        [KernelFunction("LCV2_GetSites_WorkerComputePlatform_Legion")]
        [Description("Gets sites with worker compute platform set to legion on a given stamp")]
        public async Task<KustoQueryResponse> GetSitesWithWorkerComputePlatformSetToLegion(
            string stampName,
            int lookbackInDays)
        {
            try
            {
                _logger.LogInformation($"Initializing LCV2_GetSites_WorkerComputePlatform_Legion.");
                

                var kustoQuery = GetSitesWithWorkerComputePlatformOnLegion_query(stampName, lookbackInDays);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                return new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_GetSites_WorkerComputePlatform_Legion.");
                return new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" };
            }
        }

        [KernelFunction("LCV2_GetSites_ContainerAssignment_Legion")]
        [Description("Get sites with container assignment on legion")]
        public async Task<List<KustoQueryResponse>> GetSitesWithLegionContainerAssignment(string stampName)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing GetSitesWithLegionContainerAssignment.");
                

                var kustoQuery = GetSitesLegionContainerAssignment_query(stampName);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing GetSitesWithLegionContainerAssignment.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_GetCandidateSites_For_Migration")]
        [Description("Get candidate sites for migration")]
        public async Task<List<KustoQueryResponse>> GetCandidateSitesForMigration
            (string stampName,
            int numberOfSites,
            string stampPrefix,
            string tenant)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing GetCandidateSitesForMigration.");
                

                var kustoQuery = GetCandidateSitesForMigration_ExcludeFP_query(stampName, numberOfSites, stampPrefix, tenant);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing GetCandidateSitesForMigration.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_Exclusion_FeatureGap")]
        [Description("Exclusion due to feature gap")]
        public async Task<List<KustoQueryResponse>> Exclusion_FeatureGap
            (string stampName)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing Exclusion_FeatureGap.");
                

                var kustoQuery = Exclusion_FeatureGap_query(stampName);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing Exclusion_FeatureGap.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_GetLinuxConsumptionStamps")]
        [Description("List all linux consumption stamps")]
        public async Task<List<KustoQueryResponse>> GetLinuxConsumptionStamps()
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_GetLinuxConsumptionStamps.");
                

                var kustoQuery = GetLinuxConsumptionStamps_query();

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery("wawseas", _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_GetLinuxConsumptionStamps.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_GetSitesWithSpecializationFailures")]
        [Description("Sites with specialization failures")]
        public async Task<List<KustoQueryResponse>> GetSitesFailingSpecializationOnLegionButGoodOnACI
            (string stampName,
            int lookBackInDays)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_GetSitesWithSpecializationFailures.");
                

                var kustoQuery = GetSitesFailingSpecializationOnLegionButGoodOnACI_query(stampName, lookBackInDays);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_GetSitesWithSpecializationFailures.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_GetSitesWithFunctionsLoadFailures")]
        [Description("Get functions loaded")]
        public async Task<List<KustoQueryResponse>> GetSitesWithFunctionsLoadFailuresOnLegionButGoodOnACI
            (string stampName,
            int lookBackInDays,
            List<string>? sites)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_GetSitesWithFunctionsLoadFailures.");
                

                var kustoQuery = GetSitesWithFunctionsLoaded_query(stampName, lookBackInDays, sites);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_GetSitesWithFunctionsLoadFailures.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_Exclusion_RFPMI_ContentShareMount")]
        [Description("Exclude content share mount")]
        public async Task<List<KustoQueryResponse>> GetSitesWithRFPAndMIAndContentShareMount
            (string stampName,
            int lookBackInDays)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_Exclusion_RFPMI_ContentShareMount.");
                

                var kustoQuery = Exclusion_RFPAndManagedIdentity_AndContentShareMount_BYOS_query(stampName, lookBackInDays);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_Exclusion_RFPMI_ContentShareMount.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_GetSitesOnACI")]
        [Description("Get sites on ACI")]
        public async Task<List<KustoQueryResponse>> GetSitesOnACI
            (string stampName,
            List<string> candidateSites)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_GetSitesOnACI.");
                

                var kustoQuery = GetSitesOnACI_query(stampName, candidateSites);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_GetSitesOnACI.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_GetCandidateStampsForMigration")]
        [Description("List candidate stamps for migration")]
        public async Task<List<KustoQueryResponse>> GetCandidateStampsForMigration()
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_GetCandidateStampsForMigration.");
                

                var kustoQuery = GetCandidateStampsForMigration_query();

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery("wawseas", _databaseName, kustoQuery);
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_GetCandidateStampsForMigration.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        [KernelFunction("LCV2_SpecificScenario_RFP_Zip")]
        [Description("List sites with RFP as zip scenario")]
        public async Task<List<KustoQueryResponse>> GetRFPSiteNames(
            string stampName)
        {
            var responses = new List<KustoQueryResponse>();
            try
            {
                _logger.LogInformation($"Initializing LCV2_SpecificScenario_RFP_Zip.");
                DateTime? nowOverride = null;

                var kustoQuery = SpecificScenario_RFP_Zip_query(stampName);

                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(_stampToCluster[stampName], _databaseName, kustoQuery );
                responses.Add(new KustoQueryResponse { KustoQuery = kustoQuery, KustoResult = kustoResult.Result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while initializing LCV2_SpecificScenario_RFP_Zip.");
                responses.Add(new KustoQueryResponse { KustoQuery = string.Empty, KustoResult = $"An error occurred: {ex.Message}" });
            }

            return responses;
        }

        private static string GetCandidateStampsForMigration_query()
        {
            var result = string.Join(", ", _stampListForMigration.Select(s => $"\"{s}\""));
            var query = $@"
                AllLinuxConsumptionStamps | where EventPrimaryStampName in ({result})";
            return query;
        }

        private static string GetLinuxConsumptionStamps_query()
        {
            var query = $@"
                AllLinuxConsumptionStamps";
            return query;
        }

        private static string GetSitesFailingSpecializationOnLegionButGoodOnACI_query(
            string stampName,
            int lookBackInDays)
        {
            var query = $@"
                let GoodAppsFromACI = AzureContainers
                    | where PreciseTimeStamp > ago(60d)
                    | where EventPrimaryStampName == '{stampName}'
                    | where Address has ""/assign"" and SiteName !startswith ""sla""
                    | where StatusCode < 300
                    | distinct SiteName;
                let GoodAppsFromLegion = AzureContainers
                    | where PreciseTimeStamp > ago(60d)
                    | where EventPrimaryStampName == '{stampName}' and SiteName !startswith ""sla""
                    | where Address startswith ""https://lgn-rcp-api-"" and Address endswith ""specialize""
                    | where StatusCode < 300
                    | distinct SiteName;
                AzureContainers
                    | where PreciseTimeStamp > ago({lookBackInDays}d)
                    | where EventPrimaryStampName == '{stampName}' and SiteName !startswith ""sla""
                    | where Address startswith ""https://lgn-rcp-api-"" and Address endswith ""specialize""
                    | where StatusCode >= 300
                    | where SiteName in (GoodAppsFromACI) or SiteName in (GoodAppsFromLegion)
                    | summarize count() by SiteName, StatusCode";
            return query;
        }

        private static string GetSitesWithFunctionsLoaded_query(
            string stampName,
            int lookBackInDays,
            List<string>? listOfSites)
        {
            if (listOfSites != null && listOfSites.Any())
            {
                listOfSites = listOfSites.Select(a => $"\"{a}\"").ToList();
                string result = string.Join(",", listOfSites);
                return $@"    
                    let sitesWithLegionPods = AzureContainers 
                    | where PreciseTimeStamp > ago(60d)
                    | where EventPrimaryStampName == '{stampName}'
                    | where Message == ""Assign"" and ImageName !has ""mesh"" and ImageName !has ""kudu""
                    | distinct SiteName;
                    FunctionsLogs
                    | where PreciseTimeStamp > ago({lookBackInDays}d)
                    | where EventPrimaryStampName == '{stampName}'
                    | where AppName in (sitesWithLegionPods) and AppName in ({result})
                    | where Summary endswith ""functions loaded""
                    | summarize by Summary, AppName, Role";

            }
            else
            {
                return $@"    
                    let sitesWithLegionPods = AzureContainers 
                    | where PreciseTimeStamp > ago(60d)
                    | where EventPrimaryStampName == '{stampName}'
                    | where Message == ""Assign"" and ImageName !has ""mesh"" and ImageName !has ""kudu""
                    | distinct SiteName;
                    FunctionsLogs
                    | where PreciseTimeStamp > ago({lookBackInDays}d)
                    | where EventPrimaryStampName == '{stampName}'
                    | where AppName in (sitesWithLegionPods)
                    | where Summary endswith ""functions loaded""
                    | summarize by Summary, AppName, Role";
            }
        }

        private static string GetSitesWithWorkerComputePlatformOnLegion_query(
            string stampName,
            int lookBackInDays)
        {
            var query = $@"
                AntaresDataServiceApiTransactions
                | where PreciseTimeStamp > ago({lookBackInDays}d)
                | where EventPrimaryStampName == '{stampName}'
                | where OperationName == ""SetWorkerComputePlatform""
                | extend parts = split(RequestUrl, ""/"")
                | extend siteName = tostring(parts[5])
                | extend isLegion = iif(tostring(parts[7]) has ""Legion"", ""true"", ""false"")
                | top-nested of siteName by max(PreciseTimeStamp),
                top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                top-nested of isLegion by max(1)
                | project siteName, isLegion, PreciseTimeStamp
                | where isLegion == ""true""
                | distinct siteName";
            return query;
        }

        private static string GetSitesOnACI_query(
            string stamp,
            List<string>? listOfSites)
        {
            if (listOfSites != null && listOfSites.Any())
            {
                listOfSites = listOfSites.Select(a => $"\"{a}\"").ToList();
                string result = string.Join(",", listOfSites);
                return $@"
                let sitesWhereCommandsWereRun = AntaresDataServiceApiTransactions
                    | where PreciseTimeStamp > ago(60d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where OperationName == ""SetWorkerComputePlatform""
                    | extend parts = split(RequestUrl, ""/"")
                    | extend siteName = tostring(parts[5])
                    | where siteName in ({result})
                    | extend isLegion = iif(tostring(parts[7]) has ""Legion"", ""true"", ""false"")
                    | top-nested of siteName by max(PreciseTimeStamp),
                    top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                    top-nested of isLegion by max(1)
                    | project siteName, isLegion, PreciseTimeStamp
                    | where isLegion == ""false""
                    | distinct siteName;
                let siteContainersOnLegion = AzureContainers
                    | where PreciseTimeStamp > ago(60d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Message == ""Assign"" and ImageName !has ""kudu""
                    | where SiteName in ({result})
                    | extend isLegion = iif(ImageName has ""mesh"", ""false"", ""true"")
                    | top-nested of SiteName by max(PreciseTimeStamp),
                    top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                    top-nested of isLegion by max(1)
                    | project SiteName, isLegion, PreciseTimeStamp
                    | where isLegion == ""false""
                    | distinct SiteName;
                sitesWhereCommandsWereRun | union siteContainersOnLegion | distinct SiteName";

            }
            else
            {
                return $@"
                let sitesWhereCommandsWereRun = AntaresDataServiceApiTransactions
                    | where PreciseTimeStamp > ago(30d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where OperationName == ""SetWorkerComputePlatform""
                    | extend parts = split(RequestUrl, ""/"")
                    | extend siteName = tostring(parts[5])
                    | extend isLegion = iif(tostring(parts[7]) has ""Legion"", ""true"", ""false"")
                    | top-nested of siteName by max(PreciseTimeStamp),
                    top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                    top-nested of isLegion by max(1)
                    | project siteName, isLegion, PreciseTimeStamp
                    | where isLegion == ""false""
                    | distinct siteName;
                let siteContainersOnLegion = AzureContainers
                    | where PreciseTimeStamp > ago(30d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Message == ""Assign"" and ImageName !has ""kudu""
                    | extend isLegion = iif(ImageName has ""mesh"", ""false"", ""true"")
                    | top-nested of SiteName by max(PreciseTimeStamp),
                    top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                    top-nested of isLegion by max(1)
                    | project SiteName, isLegion, PreciseTimeStamp
                    | where isLegion == ""false""
                    | distinct SiteName;
                sitesWhereCommandsWereRun | union siteContainersOnLegion | distinct SiteName";
            }
        }

        // FP and other SWA sites excluded due to Bug -- https://msazure.visualstudio.com/Antares/_workitems/edit/33193577/?view=edit
        private static string GetCandidateSitesForMigration_ExcludeFP_query(
            string stamp,
            int numberOfSites,
            string stampPrefix,
            string tenant)
        {
            var query = $@"
                let SWASubscriptions = GetStaticSiteBlueRidgeSubscriptions(""{stampPrefix}-linux"", ""functionApp"") | distinct Subscription;
                let sitesWhereCommandsWereRun = AntaresDataServiceApiTransactions
                    | where PreciseTimeStamp > ago(30d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where OperationName == ""SetWorkerComputePlatform""
                    | extend parts = split(RequestUrl, ""/"")
                    | extend siteName = tostring(parts[5])
                    | extend isLegion = iif(tostring(parts[7]) has ""Legion"", ""true"", ""false"")
                    | top-nested of siteName by max(PreciseTimeStamp),
                    top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                    top-nested of isLegion by max(1)
                    | project siteName, isLegion, PreciseTimeStamp
                    | where isLegion == ""true""
                    | distinct siteName;
                let siteContainersOnLegion = AzureContainers
                    | where PreciseTimeStamp > ago(30d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Message == ""Assign"" and ImageName !has ""kudu""
                    | extend isLegion = iif(ImageName has ""mesh"", ""false"", ""true"")
                    | top-nested of SiteName by max(PreciseTimeStamp),
                    top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                    top-nested of isLegion by max(1)
                    | project SiteName, isLegion, PreciseTimeStamp
                    | where isLegion == ""true""
                    | distinct SiteName;
                let featureGaps = AzureContainers
                    | where PreciseTimeStamp > ago(10d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where (Message startswith ""[WorkerComputePlatformSelection"" and Message has  ""have LogAnalytics:"") or (Message startswith ""[WorkerComputePlatformSelection"""" and Message has  """"have BYOS: "")
                    | extend CorrectedSiteName = substring(SiteName, 0, indexof(SiteName, '('))
                    | distinct CorrectedSiteName;
                let supportedPlaceholderIds = AntaresConfigurationTracking
                    | where PreciseTimeStamp > ago(1d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where ConfigurationName has ""ServiceFabricContainersLegionPlaceholderIds""
                    | extend supportedLegionPlaceholderIds = split(ConfigurationValue, "";"")
                    | project  supportedLegionPlaceholderIds;
                let FPSites = SubscriptionHostNameMapping
                    | where PreciseTimeStamp > ago(10d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where (isnotempty(IsFirstParty) and IsFirstParty == ""True"") or SubscriptionId in (SWASubscriptions)
                    | distinct SiteName;
                let thirdPartyStaticWebApps = StaticSitesMSHAEvents
                    | where PreciseTimeStamp > ago(10d)
                    | where Tenant startswith '{tenant}'
                    | where TaskName == ""StaticSitesMSHAConfigurationInformation""
                    | where OperationName == ""SiteConfiguration"" 
                    | where ConfigurationName == ""BackendHostname""
                    | extend SiteName = tostring(split(ConfigurationValue, '.')[0])
                    | where isnotempty(SiteName)
                    | distinct SiteName;
                let sitesWithLogAnalytics = AntaresAdminGeoEvents
                    | where PreciseTimeStamp > ago(30d)
                    | where Tenant == ""rgm-prod-{stampPrefix}""
                    | where Details has ""Unique diagnostic setting logs enabled:"" and Details has ""FunctionAppLogs""
                    | where Address has ""providers/Microsoft.Insights/diagnosticSettings""
                    | extend splitted = split(Address, ""/"")
                    | extend SiteName = tostring(splitted[8])
                    | distinct SiteName;
                AzureContainers
                    | where PreciseTimeStamp > ago(5d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Message == ""Assign""
                    | where SiteName !has ""__"" and SiteName !startswith ""fnpf""
                    and SiteName !in (sitesWithLogAnalytics)
                    and SiteName !in (FPSites)
                    and SiteName !in (featureGaps)
                    and SiteName !in (siteContainersOnLegion)
                    and SiteName !in (sitesWhereCommandsWereRun)
                    and SiteName !in (thirdPartyStaticWebApps)
                    | where ImageName has ""mesh"" and ImageName !has ""kudu""
                    | where PlaceholderId in (supportedPlaceholderIds)
                    | distinct SiteName
                    | take {numberOfSites}";
            return query;
        }

        private static string GetSitesLegionContainerAssignment_query(
            string stampName)
        {
            var query = $@"
                AzureContainers
                | where PreciseTimeStamp > ago(10d)
                | where EventPrimaryStampName == '{stampName}'
                | where Message == ""Assign"" and ImageName !has ""kudu""
                | extend isLegion = iif(ImageName has ""mesh"", ""false"", ""true"")
                | top-nested of SiteName by max(PreciseTimeStamp),
                top-nested 1 of PreciseTimeStamp by Ignore1=max(PreciseTimeStamp),
                top-nested of isLegion by max(1)
                | project SiteName, isLegion, PreciseTimeStamp
                | where isLegion == ""true""
                | distinct SiteName";
            return query;
        }

        // Exclude sites due to feature gaps
        private static string Exclusion_FeatureGap_query(
            string stamp)
        {
            var query = $@"
                AzureContainers
                    | where PreciseTimeStamp > ago(10d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where (Message startswith ""[WorkerComputePlatformSelection"" and Message has  ""have LogAnalytics:"") or (Message startswith ""[WorkerComputePlatformSelection"" and Message has  ""have BYOS: "")
                    | extend CorrectedSiteName = substring(SiteName, 0, indexof(SiteName, '('))
                    | distinct CorrectedSiteName";
            return query;
        }

        private static string Exclusion_RFPAndManagedIdentity_AndContentShareMount_BYOS_query(
            string stamp,
            int lookBackInDays)
        {
            return $@"
                let ACIContainersWithMIDownload = FunctionsLogs
                    | where PreciseTimeStamp > ago({lookBackInDays}d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Summary == ""PackageDownloadHandler: Needs ManagedIdentity Token = \'True\' IsWarmupRequest = \'False\'"" or Summary startswith ""Mounted WEBSITE_CONTENTSHARE at /home"" or Summary endswith ""BYOS storage accounts""
                    | distinct RoleInstance;
                AzureContainers
                    | where PreciseTimeStamp > ago({lookBackInDays}d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Message == ""Assign"" and ImageName has ""mesh""
                    | extend ContainerName = strcat(""App-"", ContainerName)
                    | where ContainerName in (ACIContainersWithMIDownload)
                    | distinct SiteName";
        }

        private static string SpecificScenario_RFP_Zip_query(
            string stamp)
        {
            return $@"
                let ACIContainersWithMIDownload = FunctionsLogs
                    | where PreciseTimeStamp > ago(5d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Summary startswith ""Downloading app contents from"" and Summary endswith "".zip'""
                    | distinct RoleInstance;
                AzureContainers
                    | where PreciseTimeStamp > ago(5d)
                    | where EventPrimaryStampName == '{stamp}'
                    | where Message == ""Assign"" and ImageName has ""mesh""
                    | extend ContainerName = strcat(""App-"", ContainerName)
                    | where ContainerName in (ACIContainersWithMIDownload)
                    | distinct SiteName";
        }
    }
}
