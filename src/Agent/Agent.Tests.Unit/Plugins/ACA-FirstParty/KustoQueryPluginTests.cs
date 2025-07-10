using System.Text;

namespace Agent.Tests.Unit.Plugins
{
    public class KustoQueryPluginTests
    {
        // <summary>
        // This test ensures that all columns projected in KQL files are allowed or not.
        // Rules for each KQL file:
        // 1. The last line must be a "| project" or "| distinct" statement
        // 2. All columns projected must be allowed (case-insensitive) as per the allowedColumns.txt file.
        // 3. If any column is not allowed then search suitable alternative or add it to the allowedColumns.txt file.
        // </summary>
        [Fact]
        public void AllProjectedColumns_AreAllowed_AndNotDisallowed()
        {
            // Arrange
            // Use AppContext.BaseDirectory to ensure compatibility in remote/test environments
            var pluginsDefinitionsDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries");
            var columnsFileBaseDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "ACA-FirstParty");
            var allowedColumnsPath = Path.Combine(columnsFileBaseDir, "allowedColumns.txt");

            var allowedColumns = new HashSet<string>(
                File.ReadAllLines(allowedColumnsPath)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            );

            var kqlFiles = Directory.GetFiles(pluginsDefinitionsDir, "*.kql", SearchOption.AllDirectories);

            // TODO: Remove this list once all KQL files are fixed
            var ignoredKqlFiles = new List<string>
                {
                    "GetEnvoyAccessLogs.kql",
                    "GetEnvoyAccessRequestCountTimeSeries.kql",
                    "GetEnvoyControllerLogs.kql",
                    "GetEnvoyPodLogs.kql",
                    "GetEventProcessorErrors.kql",
                    "GetEventProcessorLeaderElectionEvents.kql",
                    "GetEventProcessorOOMKills.kql",
                    "GetInternalEventProcessorEventsForPod.kql",
                    "GetJobDefinition.kql",
                    "GetKedaOperatorEventsForContainerApp.kql",
                    "GetLegionErrors.kql",
                    "GetLegionSystemLogsForJobExecutionErrors.kql",
                    "GetLogsByCorrelationId.kql",
                    "GetManagedClusterEnvironmentResourceId.kql",
                    "GetManagedClusterLevelEnvoyAccessRequestCount.kql",
                    "GetManagedEnvironment.kql",
                    "GetManagedEnvironmentOperationErrors.kql",
                    "GetManagedEnvironmentStatus.kql",
                    "GetMdmPodHeartbeatMissedTimes.kql",
                    "GetMetricsMdmCount.kql",
                    "GetMissedMdmMetricTimes.kql",
                    "GetNodeHeartbeat.kql",
                    "GetPEConnectionState.kql",
                    "GetPEFrontendVmssProvisioningState.kql",
                    "GetPEProvisioningState.kql",
                    "GetPodFailureEvents.kql",
                    "GetPodGuidFromName.kql",
                    "GetPodHealthStatus.kql",
                    "GetPodHeartbeatStatus.kql",
                    "GetPodsWithPrefix.kql",
                    "GetPrivateEndpointConnectionDetails.kql",
                    "GetRevisionPodNames.kql",
                    "GetRevisionReplicaAndTraffic.kql",
                    "GetRevisionsStatus.kql",
                    "GetSessionPoolCreateOrUpdateLogs.kql",
                    "GetSessionPoolInfo.kql",
                    "GetSwiftNetworkContainerHeartbeat.kql",
                    "GetSystemComponentCpuUsage.kql",
                    "GetSystemComponentErrorEvents.kql",
                    "GetTerminatedConnectionsForPod.kql",
                    "GetVKPodLeaderElection.kql",
                    "GracefulConnectionCount.kql",
                    "KedaEventsJobScaledJobs.kql",
                    "LegionVKEventsForJobsRunningConsumptionV2.kql",
                    "ListRevisions.kql",
                    "GetKustoClusterFromSiteName.kql",
                    "GetKustoClusterFromEventPrimaryStampName.kql",
                    "CheckProcessingDelaysForFunction.kql",
                    "GetScaleControllerErrorsForApp.kql",
                    "CheckScaleControllerVotesToDataService.kql",
                    "GetColdStartProfileData.kql",
                    "GetColdStartProfileDataDetails.kql",
                    "GetColdStartQueryForSlaSites.kql",
                    "GetColdStartRequestDetailsForFlexConsumption.kql",
                    "GetColdStartRequestDetailsForFlexConsumptionFromLegion.kql",
                    "GetColdStartRequestDetailsForLinuxConsumption.kql",
                    "GetColdStartRequestDetailsForWindowsConsumption.kql",
                    "GetColdStartStatusByRegion.kql",
                    "GetColdStartStatusByStage.kql",
                    "GetRequestGeneralInfoQueryFromAnalytics.kql",
                    "GetRequestGeneralInfoQueryFromWaws.kql",
                };

            var invalidColumnsSummary = new StringBuilder();

            foreach (var kqlFile in kqlFiles)
            {
                var fileName = Path.GetFileName(kqlFile);
                if (ignoredKqlFiles.Contains(fileName))
                {
                    continue;
                }
                var lines = File.ReadAllLines(kqlFile)
                                .Reverse()
                                .Select(line => line.Trim())
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .ToList();

                if (lines.Count == 0)
                {
                    continue;
                }

                var lastLine = lines.First(); // This is the last non-empty line in the original file

              
                var trimmedLastLine = lastLine.Trim();
                if (trimmedLastLine.StartsWith("|"))
                {
                    trimmedLastLine = trimmedLastLine.Substring(1).TrimStart();
                }
                else
                {
                    invalidColumnsSummary.AppendLine($"KQL file '{fileName}' does not end with a '| project' or '| distinct' statement");
                    continue;
                }

                var firstWord = trimmedLastLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                if (!string.Equals(firstWord, "project", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(firstWord, "distinct", StringComparison.OrdinalIgnoreCase))
                {
                    invalidColumnsSummary.AppendLine($"KQL file '{fileName}' does not end with a '| project' or '| distinct' statement");
                    continue;
                }

                // Remove the leading "project" or "distinct"
                string columnsPart;
                if (string.Equals(firstWord, "project", StringComparison.OrdinalIgnoreCase))
                {
                    columnsPart = trimmedLastLine.Substring("project".Length).TrimStart();
                }
                else // "distinct"
                {
                    columnsPart = trimmedLastLine.Substring("distinct".Length).TrimStart();
                }

                // remove the trailing ";"
                columnsPart = columnsPart.TrimEnd(' ', ';');

                var columns = columnsPart
                    .Split(',')
                    .Select(col =>
                    {
                        var trimmed = col.Trim();
                        var eqIdx = trimmed.IndexOf('=');
                        return eqIdx >= 0 ? trimmed.Substring(0, eqIdx).Trim() : trimmed;
                    })
                    .Where(col => !string.IsNullOrWhiteSpace(col));

                var invalids = new List<string>();

                foreach (var column in columns)
                {
                    if (!allowedColumns.Any(ac => ac.Equals(column, StringComparison.OrdinalIgnoreCase)))
                    {
                        invalids.Add(column);
                    }
                }

                if (invalids.Count > 0)
                {
                    invalidColumnsSummary.AppendLine($"KQL file: {fileName} InvalidColumns: {string.Join(",", invalids)}");
                }
            }

            if (invalidColumnsSummary.Length > 0)
            {
                Assert.False(true, $"Invalid columns found in KQL files:\n{invalidColumnsSummary}");
            }
        }
    }
}
