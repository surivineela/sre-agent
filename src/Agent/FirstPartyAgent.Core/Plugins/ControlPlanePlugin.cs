using Agent.Core.Models;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;
using System.Text;

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
            const string logPrefix = "[run_sb90_validation_kusto_query]";
            // Check of alertResults is null or empty
            if (alertResults is null || alertResults.Count == 0)
            {
                return $"{logPrefix} Primary Kusto Query's results List (ListOfAlertResults) is empty. Need this to get started.";
            }

            // Safeguard against multiple stamps in the results
            if (alertResults.Count > 1)
            {
                // select all the StampNames from the alertResults
                var stampNames = string.Join(", ", alertResults.Select(x => x.StampName));
                await LogInformation($"{logPrefix} HUMAN_INTERVENTION_REQUIRED More than one stamp found in the results. This LSI is unlikely to be transient. Stamps: {stampNames}");
                return $"{logPrefix} HUMAN_INTERVENTION_REQUIRED More than one stamp found in the results. This LSI is unlikely to be transient. Stamps: ${stampNames}";
            }

            var checkableAlertResult = alertResults.First();
            var affectedStamp = checkableAlertResult.StampName;
            var eventStampName = checkableAlertResult.EventStampName;

            var validationResults = ValidateSb90Inputs(affectedStamp, eventStampName);
            if (!string.IsNullOrWhiteSpace(validationResults))
            {
                return $"{logPrefix} alertResults is invalid: {validationResults}";
            }

            await LogInformation($"{logPrefix} Invoked with impactStartDate: {impactStartDate}, affectedStamp: {affectedStamp}, eventStampName: {eventStampName}, alertId: {alertId}, clusterName: {clusterName}");

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

            validationQueryStr = validationQueryStr.Replace("<QueryExecutedAtTimeStamp>", $"datetime({impactStartDate})", StringComparison.OrdinalIgnoreCase);
            validationQueryStr = validationQueryStr.Replace("<AffectedStamp>", $"{affectedStamp}", StringComparison.OrdinalIgnoreCase);
            validationQueryStr = validationQueryStr.Replace("<EventStampName>", $"{eventStampName}", StringComparison.OrdinalIgnoreCase);

            var kustoResultTask = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, _controlPlaneKustoDbName, validationQueryStr, null);
            var preProcessedResults = Sb90CleanUpKustoResult(kustoResultTask.Result, affectedStamp);
            return await preProcessedResults;
        }

        private string? ValidateSb90Inputs(string affectedStamp, string eventStampName)
        {
            var validationResults = new StringBuilder();
            if (string.IsNullOrWhiteSpace(affectedStamp) || !affectedStamp.StartsWith("waws-prod"))
            {
                validationResults.Append($"StampName should start with 'waws-prod'");
            }

            if (string.IsNullOrWhiteSpace(eventStampName) || !eventStampName.StartsWith("rgm-prod"))
            {
                validationResults.Append($"EventStampName should start with 'rgm-prod'");
            }

            return validationResults.Length > 0 ? validationResults.ToString() : null;
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
            const string logPrefix = "[run_opBelow90_validation_kusto_query]";
            await LogInformation($"{logPrefix} Invoked with impactStartDate: {impactStartDate}, clusterName: {clusterName}, alertId: {alertId}, alertResults: {JsonConvert.SerializeObject(alertResults)}");
            if (alertResults == null || alertResults.Count == 0)
            {
                return $"{logPrefix} Primary Kusto Query's results List is empty. Need this to get started.";
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

            var kustoClauses = $"({string.Join(" or ", alertResults.Select(x => x.ToKustoClause))})";

            validationQueryStr = validationQueryStr.Replace("<QueryExecutedAtTimeStamp>", $"datetime({impactStartDate})", StringComparison.OrdinalIgnoreCase);
            validationQueryStr = validationQueryStr.Replace("<TargettedVerbTemplateCombinations>", kustoClauses, StringComparison.OrdinalIgnoreCase);

            var kustoResultTask = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, _controlPlaneKustoDbName, validationQueryStr, null);
            var preProcessedResults = OpBelow90CleanUpKustoResult(kustoResultTask.Result);

            return await preProcessedResults;
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

        public class StampBelow90AlertResult
        {
            public string StampName { get; set; } = string.Empty;
            public string EventStampName { get; set; } = string.Empty;
        }

        public class OpBelow90AlertResult
        {
            public string Verb { get; set; } = string.Empty;
            public string AddressTemplate { get; set; } = string.Empty;
            public string EventStampName { get; set; } = string.Empty;
            public string ToKustoClause => $@"Verb == ""{Verb}"" and AddressTemplate == ""{AddressTemplate}"" and EventStampName == ""{EventStampName}""";
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
    }
}
