using System.ComponentModel;
using System.Reflection;
using System.Text;
using Agent.Core.Models;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Plugins
{
    public class ControlPlanePlugin
    {
        private readonly ICMPlugin _icmPlugin;
        private readonly Kernel _kernel;
        private readonly ILogger<ControlPlanePlugin> _logger;
        private readonly AlertHandlerService _alertHandlerService;

        private ITeamsClient _teamsClient;
        private IKustoPluginClient _kustoPlugin;
        private const string _controlPlaneKustoDbName = "wawsprod";

        public ControlPlanePlugin(
            ICMPlugin icmPlugin,
            Kernel kernel,
            IKustoPluginClient kustoPlugin,
            ITeamsClient teamsClient,
            ILogger<ControlPlanePlugin> logger,
            AlertHandlerService alertHandlerService
            )
        {
            _icmPlugin = icmPlugin;
            _kernel = kernel;
            _kustoPlugin = kustoPlugin;
            _teamsClient = teamsClient;
            _logger = logger;
            _alertHandlerService = alertHandlerService;
        }

        private async Task LogInformation(string info)
        {
            _logger.LogInformation(info);
            if (_teamsClient.IsEnabled() && _teamsClient.SendLogsToTeams())
            {
                var teamsMessage = new TeamsMessage(info, null);
                await _teamsClient.PostMessageOnTeams(agentMode: AgentMode.ControlPlane.ToString(), message: teamsMessage).ConfigureAwait(false);
            }
        }

        [KernelFunction("fetch_transience_detection_instructions")]
        [Description("Fetches the transience detection analysis instructions for the alert. Returns the instructions as a string. If no instructions are found, returns an empty string.")]
        public async Task<string> FetchTransienceDetectionInstructionsForAlert(
            [Description("Incident Id")] string incidentId)
        {
            var logPrefix = "fetch_transience_detection_instructions";
            await LogInformation($"[{logPrefix}] Invoked with incidentId: {incidentId}.");
            var incidentDetails = await _icmPlugin.GetIncidentInfo(incidentId, _kernel);

            await LogInformation($"[{logPrefix}] fetching transience detection instructions");
            var alertConfigs = await _alertHandlerService.GetICMAlertConfigsAsync();
            foreach (var alertId in alertConfigs.Keys)
            {
                var alertConfig = alertConfigs[alertId];
                if (incidentDetails.Title == alertConfig.IncidentTitle
                    || !string.IsNullOrWhiteSpace(alertConfig.IncidentTitleContains) && incidentDetails.Title.Contains(alertConfig.IncidentTitleContains, StringComparison.OrdinalIgnoreCase))
                {
                    var joinedStrings = string.Join(Environment.NewLine, alertConfig.MitigationInstructions).Replace("\\n", Environment.NewLine);
                    var instr = $"LOOP_TO_TRANSFER_TO:{alertConfig.DefaultHumanInterventionLoop}\nTRANSIENCE_DETECTION_INSTRUCTIONS:\n{joinedStrings}";
                    return instr;
                }
            }

            return $"No transience detection instructions for the incidentId: {incidentId}.";
        }

        [KernelFunction("run_sb90_validation_kusto_query")]
        [Description("Runs the kusto query for the sb90 alert and returns the result.")]
        public async Task<string> RunSb90ValidationKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Alert Id")] string alertId,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("ListOfAlertResults")] List<StampBelow90AlertResult> alertResults)
        {
            return await RunValidationKustoQuery(
                logPrefix: "[run_sb90_validation_kusto_query]",
                impactStartDate: impactStartDate,
                alertId: alertId,
                clusterName: clusterName,
                alertResults: alertResults,
                multipleResultsCheck: results => results.Count > 1,
                multipleResultsMessage: results => $"HUMAN_INTERVENTION_REQUIRED More than one stamp found in the results. This LSI is unlikely to be transient. Stamps: {string.Join(", ", results.Select(x => x.StampName))}",
                buildQuery: (results, config) =>
                {
                    // For this LSI, the agent is designed to only handle one result for this alert type as we assume multiple stamps are not transient.
                    var result = results.First();
                    var query = config.ValidationQuery ?? string.Empty;
                    query = query.Replace("<QueryExecutedAtTimeStamp>", $"datetime({impactStartDate})", StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<AffectedStamp>", result.StampName, StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<EventStampName>", result.EventStampName, StringComparison.OrdinalIgnoreCase);
                    return query;
                },
                kustoResult => Sb90CleanUpKustoResult(kustoResult, alertResults.First().StampName)
            );
        }

        private async Task<string> Sb90CleanUpKustoResult(string kustoResult, string affectedStamp)
        {
            if (IsKustoResultEmpty(kustoResult))
            {
                await LogInformation($"[{nameof(Sb90CleanUpKustoResult)}] Kusto result is empty.");
                return kustoResult;
            }

            var availabilityLogEntries = await CleanUpLogEntries<AvailabilityLogEntry>(kustoResult, nameof(Sb90CleanUpKustoResult));

            if (availabilityLogEntries.Count == 0)
            {
                return kustoResult;
            }

            availabilityLogEntries.RemoveAll(logEntry => !string.Equals(logEntry.StampName, affectedStamp, StringComparison.OrdinalIgnoreCase));


            // sort the log entries by PreciseTimeStamp
            availabilityLogEntries.Sort((x, y) => x.PreciseTimeStamp.CompareTo(y.PreciseTimeStamp));

            // locate the latest entry whose availability is less than 90.0 and remove any entries before it
            var latestEntryBelow90 = availabilityLogEntries.LastOrDefault(e => e.Availability < 90.0);
            if (latestEntryBelow90 == null)
            {
                // we found no entries with availability < 90.0, so we return the results
                var cleanResults = $"RECOVERY_CONFIRMED:\n{JsonConvert.SerializeObject(availabilityLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(Sb90CleanUpKustoResult)}] No entries with availability < 90.0 found. Returning all entries: {cleanResults}");
                return cleanResults;
            }

            availabilityLogEntries = availabilityLogEntries.Where(e => e.PreciseTimeStamp >= latestEntryBelow90.PreciseTimeStamp).ToList();

            var jsonEntries = JsonConvert.SerializeObject(availabilityLogEntries, Formatting.Indented);
            if (availabilityLogEntries.Count <= 3)
            {
                var cleanResults = $"STILL_OCCURRING";
                await LogInformation($"[{nameof(Sb90CleanUpKustoResult)}] Not enough entries with availability over 90: {cleanResults}");
                return cleanResults;
            }
            else
            {
                var cleanResults = $"RECOVERY_CONFIRMED:\n{jsonEntries}";
                await LogInformation($"[{nameof(Sb90CleanUpKustoResult)}] Enough entries with availability over 90: {cleanResults}");
                return cleanResults;
            }
        }

        [KernelFunction("run_opBelow90_validation_kusto_query")]
        [Description("Runs the kusto query for the opBelow90 alert and returns the result.")]
        public async Task<string> RunOpBelow90ValidationKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("Alert Id")] string alertId,
            [Description("ListOfAlertResults")] List<OpBelow90AlertResult> alertResults
        )
        {
            return await RunValidationKustoQuery(
                logPrefix: "[run_opBelow90_validation_kusto_query]",
                impactStartDate: impactStartDate,
                alertId: alertId,
                clusterName: clusterName,
                alertResults: alertResults,
                multipleResultsCheck: _ => false, // No special handling for multiple results
                multipleResultsMessage: _ => string.Empty,
                buildQuery: (results, config) =>
                {
                    var query = config.ValidationQuery ?? string.Empty;
                    var kustoClauses = $"({string.Join(" or ", results.Select(x => x.ToKustoClause))})";
                    query = query.Replace("<QueryExecutedAtTimeStamp>", $"datetime({impactStartDate})", StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<TargettedVerbTemplateCombinations>", kustoClauses, StringComparison.OrdinalIgnoreCase);
                    return query;
                },
                cleanUpResult: kustoResult => OpBelow90CleanUpKustoResult(kustoResult)
            );
        }

        private async Task<string> OpBelow90CleanUpKustoResult(string kustoResult)
        {
            if (IsKustoResultEmpty(kustoResult))
            {
                await LogInformation($"[{nameof(OpBelow90CleanUpKustoResult)}] Kusto result is empty.");
                return kustoResult;
            }

            var opBelow90LogEntries = await CleanUpLogEntries<OpBelow90LogEntry>(kustoResult, nameof(OpBelow90CleanUpKustoResult));
            if (opBelow90LogEntries.Count == 0)
            {
                return kustoResult;
            }

            // split the log entries into a map where the key is EntryKey and the value is a list of log entries for that key
            // EntryKey is a combination of Verb, AddressTemplate and StampName
            var logEntriesMap = new Dictionary<string, List<OpBelow90LogEntry>>();
            foreach (var logEntry in opBelow90LogEntries)
            {
                if (!logEntriesMap.ContainsKey(logEntry.EntryKey))
                {
                    logEntriesMap[logEntry.EntryKey] = new List<OpBelow90LogEntry>();
                }

                logEntriesMap[logEntry.EntryKey].Add(logEntry);
            }

            // sort each list of log entries by PreciseTimeStamp
            foreach (var key in logEntriesMap.Keys)
            {
                logEntriesMap[key].Sort((x, y) => x.PreciseTimeStamp.CompareTo(y.PreciseTimeStamp));
            }

            // foreach key, get the latest entry whose availability is less than the threshold
            // and remove any entries before it
            var latestEntriesBelowThreshold = new Dictionary<string, OpBelow90LogEntry>();
            foreach (var key in logEntriesMap.Keys)
            {
                var latestEntryBelowThreshold = logEntriesMap[key].LastOrDefault(e => e.Availability < e.AvailabilityThreshold);
                if (latestEntryBelowThreshold != null)
                {
                    latestEntriesBelowThreshold[key] = latestEntryBelowThreshold;
                    logEntriesMap[key] = logEntriesMap[key].Where(e => e.PreciseTimeStamp >= latestEntryBelowThreshold.PreciseTimeStamp).ToList();
                }
            }

            // if latestEntriesBelowThreshold is empty, it means that all entries for each key are above the threshold
            if (latestEntriesBelowThreshold.Count == 0)
            {
                var noEntriesBelowThresholdRecovery = "RECOVERY_CONFIRMED:\nNo Entries Below Threshold";
                await LogInformation($"[{nameof(OpBelow90CleanUpKustoResult)}] No entries with availability < threshold found. Returning {noEntriesBelowThresholdRecovery}");
                return noEntriesBelowThresholdRecovery;
            }

            // if all the log entries in the map have at least 4 entries after we filtered them, it means that all stamps and address templates are above the threshold
            var operationsStillBelowThreshold = logEntriesMap.Where(x => x.Value.Count < 4).Select(x => x.Key).ToList();
            if (operationsStillBelowThreshold.Count > 0)
            {
                // we found at least one stamp and address template with less than 4 entries after we filtered them, this means that it is still occurring for this stamp and address template
                // log the stamp and address template with less than 4 entries
                await LogInformation($"[{nameof(OpBelow90CleanUpKustoResult)}] Not enough entries with availability over threshold for keys: {string.Join(", ", operationsStillBelowThreshold)}.");
                return $"STILL_OCCURRING:\n{string.Join(", ", operationsStillBelowThreshold)}";
            }

            // if we get here, it means that all stamps and address templates have at least 4 entries after we filtered them
            var results = $"RECOVERY_CONFIRMED:\n{TrimStrToMaxLength(JsonConvert.SerializeObject(logEntriesMap, Formatting.Indented))}...";
            await LogInformation($"[{nameof(OpBelow90CleanUpKustoResult)}] Enough entries with availability over threshold: {results}");
            return results;
        }

        [KernelFunction("run_StampApiImpact_validation_kusto_query")]
        [Description("Runs the kusto query for the StampApiSubscriptionImpact alert and returns the result.")]
        public async Task<string> RunStampApiImpactValidationKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("Alert Id")] string alertId,
            [Description("ListOfAlertResults")] List<StampApiImpactAlertResult> alertResults
            )
        {
            return await RunValidationKustoQuery(
                logPrefix: "[run_StampApiImpactValidationKustoQuery]",
                impactStartDate: impactStartDate,
                alertId: alertId,
                clusterName: clusterName,
                alertResults: alertResults,
                multipleResultsCheck: results => results.Count > 1,
                multipleResultsMessage: results => $"HUMAN_INTERVENTION_REQUIRED More than one stamp found in the results. This LSI is unlikely to be transient. Stamps: {string.Join(", ", results.Select(x => x.EventStampName))}",
                buildQuery: (results, config) =>
                {
                    // For this LSI, the agent is designed to only handle one result for this alert type as we assume multiple stamps are not transient.
                    var result = results.First();
                    var query = config.ValidationQuery ?? string.Empty;
                    query = query.Replace("<QueryExecutedAtTimeStamp>", $"datetime({impactStartDate})", StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<EventStampName>", result.EventStampName, StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<SourceNamespace>", result.SourceNamespace, StringComparison.OrdinalIgnoreCase);
                    return query;
                },
                kustoResult => StampApiSubsImpactCleanUpKustoResult(kustoResult, 10 * 0.3, alertResults.First().SourceNamespace.Equals("WAWS", StringComparison.OrdinalIgnoreCase) ? 20 * 0.3 : 50 * 0.3) // these magic numbers come from the LSI alert query. 10% for WAWS, 20% for other namespaces, 50% for non-WAWS namespaces
            );
        }

        private async Task<string> StampApiSubsImpactCleanUpKustoResult(string result, double generalSubsRecoveryThreshold, double subs409RecoveryThreshold)
        {
            if (IsKustoResultEmpty(result))
            {
                await LogInformation($"[{nameof(StampApiSubsImpactCleanUpKustoResult)}] Kusto result is empty.");
                return result;
            }

            var stampApiImpactLogEntries = await CleanUpLogEntries<StampApiImpactLogEntry>(result, nameof(StampApiSubsImpactCleanUpKustoResult));
            if (stampApiImpactLogEntries.Count == 0)
            {
                return result;
            }

            // sort the log entries by PreciseTimeStamp
            stampApiImpactLogEntries.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));
            // locate the latest entry whose PercentImpactedSubs is above the recovery threshold and remove any entries before it
            var latestEntryAboveGeneralThreshold = stampApiImpactLogEntries.LastOrDefault(e => e.PercentImpactedSubs > generalSubsRecoveryThreshold);
            var latestEntryAbove409Threshold = stampApiImpactLogEntries.LastOrDefault(e => e.Percent409ImpactedSubs > subs409RecoveryThreshold);
            if (latestEntryAboveGeneralThreshold == null && latestEntryAbove409Threshold == null)
            {
                // we found no entries with PercentImpactedSubs above the thresholds, so we return the results
                var cleanResults = $"RECOVERY_CONFIRMED:\n{JsonConvert.SerializeObject(stampApiImpactLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(StampApiSubsImpactCleanUpKustoResult)}] No entries with PercentImpactedSubs > {generalSubsRecoveryThreshold} or {subs409RecoveryThreshold} found. Returning all entries: {cleanResults}");
                return cleanResults;
            }

            // if both are not null, we use the latest entry above their respective thresholds
            if (latestEntryAboveGeneralThreshold != null && latestEntryAbove409Threshold != null)
            {
                // we take the latest of the two
                var latestEntry = latestEntryAboveGeneralThreshold.Timestamp > latestEntryAbove409Threshold.Timestamp ? latestEntryAboveGeneralThreshold : latestEntryAbove409Threshold;
                stampApiImpactLogEntries = stampApiImpactLogEntries.Where(e => e.Timestamp >= latestEntry.Timestamp).ToList();
            }
            else if (latestEntryAboveGeneralThreshold != null && latestEntryAbove409Threshold == null)
            {
                // we only have the general threshold entry
                stampApiImpactLogEntries = stampApiImpactLogEntries.Where(e => e.Timestamp >= latestEntryAboveGeneralThreshold.Timestamp).ToList();
            }
            else if (latestEntryAboveGeneralThreshold == null && latestEntryAbove409Threshold != null)
            {
                // we only have the 409 threshold entry
                stampApiImpactLogEntries = stampApiImpactLogEntries.Where(e => e.Timestamp >= latestEntryAbove409Threshold.Timestamp).ToList();
            }

            if (stampApiImpactLogEntries.Count <= 3)
            {
                var cleanResults = $"STILL_OCCURRING:\n{JsonConvert.SerializeObject(stampApiImpactLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(StampApiSubsImpactCleanUpKustoResult)}] Insufficient entries above generalSubsRecoveryThreshold: {generalSubsRecoveryThreshold} or subs409RecoveryThreshold: {subs409RecoveryThreshold}. Returning: {cleanResults}");
                return cleanResults;
            }
            else
            {
                // we have enough entries above the threshold that was not being met
                var cleanResults = $"RECOVERY_CONFIRMED:\n{JsonConvert.SerializeObject(stampApiImpactLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(StampApiSubsImpactCleanUpKustoResult)}] Enough entries above thresholds (generalSubsRecoveryThreshold: {generalSubsRecoveryThreshold} or subs409RecoveryThreshold: {subs409RecoveryThreshold}): {cleanResults}");
                return cleanResults;
            }
        }

        [KernelFunction("run_GeoApiSubsImpactAll_validation_query")]
        [Description("Runs the kusto query for the GeomasterApiSubscriptionImpactAllRequests alert and returns the result.")]
        public async Task<string> RunGeoApiSubsImpactAllValidationKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("Alert Id")] string alertId,
            [Description("ListOfAlertResults")] List<GeoApiSubsImpactAlertResult> alertResults
            )
        {
            return await RunGeoApiSubsImpactSharedValidationKustoQuery(
                logPrefix: "[run_GeoApiSubsImpactAllValidationKustoQuery]",
                impactStartDate: impactStartDate,
                alertId: alertId,
                clusterName: clusterName,
                alertResults: alertResults
            );
        }

        [KernelFunction("run_GeoApiSubsImpactNonGets_validation_query")]
        [Description("Runs the kusto query for the GeomasterApiSubscriptionImpactNonGets alert and returns the result.")]
        public async Task<string> RunGeoApiSubsImpactNonGetsValidationKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("Alert Id")] string alertId,
            [Description("ListOfAlertResults")] List<GeoApiSubsImpactAlertResult> alertResults
            )
        {
            return await RunGeoApiSubsImpactSharedValidationKustoQuery(
                logPrefix: "[run_GeoApiSubsImpactNonGetsValidationKustoQuery]",
                impactStartDate: impactStartDate,
                alertId: alertId,
                clusterName: clusterName,
                alertResults: alertResults
            );
        }

        // Two very similar alerts GeomasterApiSubscriptionImpactNonGets and GeomasterApiSubscriptionImpactAllRequests share near identical incident handling instructions.
        // Their Validation Queries are also very similar, so we can share the logic for building the Kusto query and cleaning up the results.
        private async Task<string> RunGeoApiSubsImpactSharedValidationKustoQuery(
            string logPrefix,
            string impactStartDate,
            string alertId,
            string clusterName,
            List<GeoApiSubsImpactAlertResult> alertResults)
        {
            return await RunValidationKustoQuery(
                logPrefix: logPrefix,
                impactStartDate: impactStartDate,
                alertId: alertId,
                clusterName: clusterName,
                alertResults: alertResults,
                multipleResultsCheck: results => results.Count > 1,
                multipleResultsMessage: results => $"HUMAN_INTERVENTION_REQUIRED More than one RGM found in the results. This LSI is unlikely to be transient. RGMs: {string.Join(", ", results.Select(x => x.EventStampName))}",
                buildQuery: (results, config) =>
                {
                    // For this LSI, the agent is designed to only handle one result for this alert type as we assume multiple geomasters impacted are not transient.
                    var result = results.First();
                    var query = config.ValidationQuery ?? string.Empty;
                    query = query.Replace("<QueryExecutedAtTimeStamp>", $"datetime({impactStartDate})", StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<EventStampName>", result.EventStampName, StringComparison.OrdinalIgnoreCase);
                    query = query.Replace("<TotalSubs>", result.TotalSubs.ToString(), StringComparison.OrdinalIgnoreCase);
                    return query;
                },
                cleanUpResult: kustoResult => GeoApiSubsImpactCleanUpKustoResult(kustoResult, alertResults.First().ImpactThreshold * 0.3)
            );
        }

        private async Task<string> RunValidationKustoQuery<TAlertResult>(
            string logPrefix,
            string impactStartDate,
            string alertId,
            string clusterName,
            List<TAlertResult> alertResults,
            Func<List<TAlertResult>, bool> multipleResultsCheck,
            Func<List<TAlertResult>, string> multipleResultsMessage,
            Func<List<TAlertResult>, ICMAlertConfig, string> buildQuery,
            Func<string, Task<string>> cleanUpResult) where TAlertResult : IValidatableAlertResult
        {
            await LogInformation($"{logPrefix} Invoked with impactStartDate: {impactStartDate}, clusterName: {clusterName}, alertId: {alertId}, alertResults: {JsonConvert.SerializeObject(alertResults)}");
            if (alertResults == null || alertResults.Count == 0)
            {
                return $"{logPrefix} Primary Kusto Query's results List is empty. Need this to get started.";
            }

            if (multipleResultsCheck(alertResults))
            {
                var msg = multipleResultsMessage(alertResults);
                await LogInformation($"{logPrefix} {msg}");
                return $"{logPrefix} {msg}";
            }

            var validationErrors = alertResults.GetValidationErrors();
            if (validationErrors.Count > 0)
            {
                return $"{logPrefix} alertResults is invalid: {string.Join("; ", validationErrors)}";
            }

            var alertConfig = await _alertHandlerService.GetICMAlertConfigAsync(alertId);
            if (alertConfig == null)
            {
                return $"{logPrefix} Alert Config not found for alertId {alertId}";
            }

            var validationQueryStr = alertConfig.ValidationQuery;
            if (string.IsNullOrWhiteSpace(validationQueryStr))
            {
                return $"{logPrefix} Validation query not found for alertId {alertId}";
            }

            validationQueryStr = buildQuery(alertResults, alertConfig);
            if (string.IsNullOrWhiteSpace(validationQueryStr))
            {
                return $"{logPrefix} error building validation query for alertId {alertId}. Raw query: {alertConfig.ValidationQuery}";
            }

            // Here we execute the Kusto query against the cluster
            // Then we clean up the result using the provided cleanUpResult function
            var kustoResultTask = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, _controlPlaneKustoDbName, validationQueryStr, null);
            var preProcessedResults = cleanUpResult(kustoResultTask.Result);

            return await preProcessedResults;
        }

        private async Task<string> GeoApiSubsImpactCleanUpKustoResult(string result, double recoveryThreshold)
        {
            if (IsKustoResultEmpty(result))
            {
                await LogInformation($"[{nameof(GeoApiSubsImpactCleanUpKustoResult)}] Kusto result is empty.");
                return result;
            }

            var geoApiSubsImpactLogEntries = await CleanUpLogEntries<GeoApiSubsImpactAllLogEntry>(result, nameof(GeoApiSubsImpactCleanUpKustoResult));

            if (geoApiSubsImpactLogEntries.Count == 0)
            {
                return result;
            }

            // sort the log entries by PreciseTimeStamp
            geoApiSubsImpactLogEntries.Sort((x, y) => x.PreciseTimeStamp.CompareTo(y.PreciseTimeStamp));

            // locate the latest entry whose PercentImpactedSubs is above the recovery threshold and remove any entries before it
            var latestEntryAboveThreshold = geoApiSubsImpactLogEntries.LastOrDefault(e => e.PercentImpactedSubs > recoveryThreshold);

            if (latestEntryAboveThreshold == null)
            {
                // we found no entries with PercentImpactedSubs above the threshold, so we return the results
                var cleanResults = $"RECOVERY_CONFIRMED:\n{JsonConvert.SerializeObject(geoApiSubsImpactLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(GeoApiSubsImpactCleanUpKustoResult)}] No entries with PercentImpactedSubs > {recoveryThreshold} found. Returning all entries: {cleanResults}");
                return cleanResults;
            }

            geoApiSubsImpactLogEntries = geoApiSubsImpactLogEntries.Where(e => e.PreciseTimeStamp >= latestEntryAboveThreshold.PreciseTimeStamp).ToList();

            if (geoApiSubsImpactLogEntries.Count <= 3)
            {
                var cleanResults = $"STILL_OCCURRING:\n{JsonConvert.SerializeObject(geoApiSubsImpactLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(GeoApiSubsImpactCleanUpKustoResult)}] Not enough entries with PercentImpactedSubs above {recoveryThreshold}: {cleanResults}");
                return cleanResults;
            }
            else
            {
                var cleanResults = $"RECOVERY_CONFIRMED:\n{JsonConvert.SerializeObject(geoApiSubsImpactLogEntries, Formatting.Indented)}";
                await LogInformation($"[{nameof(GeoApiSubsImpactCleanUpKustoResult)}] Enough entries with PercentImpactedSubs above {recoveryThreshold}: {cleanResults}");
                return cleanResults;
            }
        }

        private static string TrimStrToMaxLength(string str, int maxLength = 200)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return string.Empty;
            }

            if (str.Length > maxLength)
            {
                return $"{str.Substring(0, maxLength)}...";
            }

            return str;
        }

        private async Task<List<T>> CleanUpLogEntries<T>(string kustoResult, string methodName) where T : new()
        {
            var logEntries = new List<T>();
            var lines = kustoResult.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                try
                {
                    var logEntry = ParseLogLine<T>(line);
                    if (logEntry != null)
                    {
                        logEntries.Add(logEntry);
                    }
                }
                catch (Exception ex)
                {
                    await LogInformation($"[{methodName}] Error deserializing log entry: {ex}\nReturning kustoResult: {kustoResult}");
                }
            }
            return logEntries;
        }

        private bool IsKustoResultEmpty(string kustoResult)
        {
            return string.IsNullOrWhiteSpace(kustoResult) ||
                   string.Equals(kustoResult, "ZERO_ROWS_RETURNED", StringComparison.OrdinalIgnoreCase);
        }

        private T ParseLogLine<T>(string logLine) where T : new()
        {
            var fields = logLine.Split('\t');
            var result = new T();

            var properties = typeof(T).GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(LogFieldAttribute)));

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<LogFieldAttribute>();
                if (attribute != null && attribute.Index >= 0 && attribute.Index < fields.Length)
                {
                    var value = fields[attribute.Index];
                    var convertedValue = Convert.ChangeType(value, property.PropertyType);
                    property.SetValue(result, convertedValue);
                }
            }

            return result;
        }


        public interface IValidatableAlertResult
        {
            /// <summary>
            /// Returns null or empty if valid, otherwise a string describing the validation error.
            /// </summary>
            string? Validate();
        }

        public class StampBelow90AlertResult : IValidatableAlertResult
        {
            public string StampName { get; set; } = string.Empty;
            public string EventStampName { get; set; } = string.Empty;

            public string? Validate()
            {
                if (string.IsNullOrWhiteSpace(StampName) || !StampName.StartsWith("waws-prod", StringComparison.OrdinalIgnoreCase))
                {
                    return "StampName should start with 'waws-prod'";
                }
                if (string.IsNullOrWhiteSpace(EventStampName) || !(EventStampName.StartsWith("rgm-prod", StringComparison.OrdinalIgnoreCase) || EventStampName.StartsWith("gm-prod", StringComparison.OrdinalIgnoreCase)))
                {
                    return "EventStampName should start with 'rgm-prod' or 'gm-prod'";
                }
                return null;
            }
        }

        public class OpBelow90AlertResult : IValidatableAlertResult
        {
            public string Verb { get; set; } = string.Empty;
            public string AddressTemplate { get; set; } = string.Empty;
            public string EventStampName { get; set; } = string.Empty;
            public string ToKustoClause => $@"Verb == ""{Verb}"" and AddressTemplate == ""{AddressTemplate}"" and EventStampName == ""{EventStampName}""";

            public string? Validate()
            {
                if (string.IsNullOrWhiteSpace(Verb))
                {
                    return "Verb cannot be null or empty.";
                }
                if (string.IsNullOrWhiteSpace(AddressTemplate))
                {
                    return "AddressTemplate cannot be null or empty.";
                }
                if (string.IsNullOrWhiteSpace(EventStampName) || !(EventStampName.StartsWith("rgm-prod", StringComparison.OrdinalIgnoreCase) || EventStampName.StartsWith("gm-prod", StringComparison.OrdinalIgnoreCase)))
                {
                    return "EventStampName should start with 'rgm-prod' or 'gm-prod'";
                }

                return null;
            }
        }

        public class GeoApiSubsImpactAlertResult : IValidatableAlertResult
        {
            public string EventStampName { get; set; } = string.Empty;
            public int TotalSubs { get; set; } = 0;
            public int ImpactThreshold { get; set; } = 0;

            public string? Validate()
            {
                if (string.IsNullOrWhiteSpace(EventStampName) || !(EventStampName.StartsWith("rgm-prod", StringComparison.OrdinalIgnoreCase) || EventStampName.StartsWith("gm-prod", StringComparison.OrdinalIgnoreCase)))
                {
                    return "EventStampName should start with 'rgm-prod' or 'gm-prod'";
                }
                if (TotalSubs <= 0)
                {
                    return "TotalSubs should be greater than 0.";
                }
                if (ImpactThreshold <= 0)
                {
                    return "ImpactThreshold should be greater than 0.";
                }

                return null;
            }
        }

        public class StampApiImpactAlertResult : IValidatableAlertResult
        {
            public string EventStampName { get; set; } = string.Empty;
            public string SourceNamespace { get; set; } = string.Empty;

            public string? Validate()
            {
                if (string.IsNullOrWhiteSpace(EventStampName) || !EventStampName.StartsWith("waws-prod", StringComparison.OrdinalIgnoreCase))
                {
                    return "EventStampName should start with 'waws-prod'";
                }
                if (string.IsNullOrWhiteSpace(SourceNamespace))
                {
                    return "SourceNamespace cannot be null or empty.";
                }

                return null;
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class LogFieldAttribute : Attribute
        {
            public int Index { get; }
            public LogFieldAttribute(int index)
            {
                Index = index;
            }
        }

        private class GeoApiSubsImpactAllLogEntry
        {
            [LogField(0)]
            public DateTime PreciseTimeStamp { get; set; }

            [LogField(1)]
            public double PercentImpactedSubs { get; set; } = 0.0;

            [LogField(2)]
            public int IsLowImpact { get; set; } = 0;

            [LogField(3)]
            public int LowImpactStreak { get; set; } = 0;

            [LogField(4)]
            public int HasRecovered { get; set; } = 0;
        }

        private class OpBelow90LogEntry
        {
            [LogField(0)]
            public DateTime PreciseTimeStamp { get; set; }

            [LogField(1)]
            public string Verb { get; set; } = string.Empty;

            [LogField(2)]
            public string AddressTemplate { get; set; } = string.Empty;

            [LogField(3)]
            public string EventStampName { get; set; } = string.Empty;

            [LogField(6)]
            public double Availability { get; set; }

            [LogField(8)]
            public int AvailabilityThreshold { get; set; } = 90;

            public string EntryKey => $"{Verb}_{AddressTemplate}_{EventStampName}";
        }

        private class AvailabilityLogEntry
        {
            [LogField(0)]
            public DateTime PreciseTimeStamp { get; set; }

            [LogField(1)]
            public string StampName { get; set; } = string.Empty;

            [LogField(2)]
            public int TotalReqs { get; set; }

            [LogField(3)]
            public int FailedReqs { get; set; }

            [LogField(4)]
            public double Availability { get; set; }
        }


        private class StampApiImpactLogEntry
        {
            [LogField(0)]
            public DateTime Timestamp { get; set; } = DateTime.MinValue;

            [LogField(1)]
            public double PercentImpactedSubs { get; set; } = -1;

            [LogField(2)]
            public double Percent409ImpactedSubs { get; set; } = -1;

            [LogField(3)]
            public int IsLowImpactSubs { get; set; } = 0;

            [LogField(4)]
            public int IsLowImpactSubs409 { get; set; } = 0;

            [LogField(5)]
            public int LowImpactStreak { get; set; } = 0;

            [LogField(6)]
            public int HasRecovered { get; set; } = 0;
        }

    }
}
