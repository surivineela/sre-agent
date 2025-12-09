// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Crawler.Metrics;
using Agent.Logging;
using Agent.Plugins.Implementation.DiagnosticsPlugin;
using Agent.Plugins.Interface;
using Agent.Prometheus;
using Agent.Prometheus.Services;

using k8s;
using k8s.Models;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using YamlDotNet.Serialization;

using CrawlerConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Plugins
{
    /// <summary>
    ///  Kubernetes plugin for Java application analysis using debug containers.
    /// </summary>
    public class KubePluginJava : IKubeJavaPlugin
    {
        private readonly ILogger? _logger;
        private readonly IKubernetesClientFactory _kubernetesClientFactory;
        private readonly JavaProfilerSettings _javaProfilerSettings;

        private static readonly ISerializer _configJsonSerializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();

        private const string analysisStartMarker = "-------------------- ANALYSIS START --------------------";
        private const string analysisEndMarker = "-------------------- ANALYSIS END ----------------------";
        private const string metadataStartMarker = "-------------------- METADATA START --------------------";
        private const string metadataEndMarker = "-------------------- METADATA END ----------------------";
        private const string diagnosisStartMarker = "-------------------- DIAGNOSIS REPORT START ----------------------";
        private const string diagnosisEndMarker = "-------------------- DIAGNOSIS REPORT END ----------------------";
        private const string diagnosisStartMarkerAlt = "-------------------- DIAGNOSIS DESCRIPTION START ----------------------";
        private const string diagnosisEndMarkerAlt = "-------------------- DIAGNOSIS DESCRIPTION END ----------------------";

        public KubePluginJava(
            ILogger<KubePluginJava>? logger,
            IKubernetesClientFactory kubernetesClientFactory,
            JavaProfilerSettings javaProfilerSettings
        )
        {
            _logger = logger;
            _kubernetesClientFactory = kubernetesClientFactory;
            _javaProfilerSettings = javaProfilerSettings;
        }

        public async Task<string> AnalyzeJavaApplicationAsync(
            Guid? threadId,
            string resourceId,
            IKubernetes client,
            V1Pod pod,
            string targetContainerName,
            IKubePlugin kubePlugin
            )
        {
            threadId ??= Agent.Core.ToolStatic.AsyncLocalThreadId.Value;

            if (threadId == null)
            {
                _logger?.LogWarning("Thread ID is null. Cannot proceed with Java application analysis.");
                return "Error: Thread ID is null. Cannot proceed with Java application analysis.";
            }

            // Validate targetContainerName conforms to Kubernetes naming conventions
            var containerNamePattern = @"^[-a-z0-9]+$";
            if (!Regex.IsMatch(targetContainerName, containerNamePattern))
            {
                var errorMessage = $"Invalid target container name '{targetContainerName}'. Container names must conform to the pattern: {containerNamePattern}";
                _logger?.LogError(errorMessage);
                return $"Error: {errorMessage}";
            }

            // Create debug container for enhanced profiling capabilities
            _logger?.LogInformation("Creating debug container for enhanced profiling access to pod '{PodName}'", pod.Name());

            if (string.IsNullOrWhiteSpace(_javaProfilerSettings.DebugProfileContainer))
            {
                _logger?.LogError("Java profiler debug profile container is not configured.");
                return "Error: Java profiler debug profile container is not configured.";
            }

            var containerName = $"java-profiler-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            var debugCreateResult = await CreateDebugContainerAsync(
                threadId.Value,
                resourceId,
                client,
                pod,
                debugImageName: _javaProfilerSettings.DebugProfileContainer,
                targetContainerName,
                kubePlugin,
                true, // Use interactive mode for profiling
                containerName: containerName,
                agentMode: "Write"
            );

            // Check if debug container creation failed
            if (debugCreateResult.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogError("Debug container creation failed: {Error}", debugCreateResult);
                return debugCreateResult;
            }

            var debugResult = await PollForDebugContainerResultAsync(
                threadId.Value,
                resourceId,
                client,
                pod,
                targetContainerName,
                kubePlugin,
                containerName
            );

            var markdown = "<system-reminder><critical>Display the markdown performance diagnosis below to the users thread verbatim</critical></system-reminder>\n# JVM diagnosis\n\n";
            markdown += ExtractDiagnosisReportFromDebugResult(debugResult, pod.Name());

            return ExtractAnalysisFromDebugResult(debugResult, pod.Name()) +
                   "\n\n" +
                   ExtractMetadataFromDebugResult(debugResult, pod.Name()) +
                   "\n\n" +
                   markdown;
        }

        private async Task<T> RunKubeCommandWithThreadId<T>(Guid threadId, IKubePlugin kubePlugin, Func<Task<T>> kubeCommand)
        {
            if (kubePlugin is KubePlugin plugin)
            {
                var previousThreadId = plugin.ThreadId;
                try
                {
                    if (plugin.ThreadId == null)
                    {
                        plugin.ThreadId = threadId;
                    }
                    return await kubeCommand();
                }
                finally
                {
                    plugin.ThreadId = previousThreadId;
                }
            }

            return await kubeCommand();
        }

        public async Task<string> PollForDebugContainerResultAsync(
            Guid threadId,
            string resourceId,
            IKubernetes client,
            V1Pod pod,
            string targetContainerName,
            IKubePlugin kubePlugin,
            string containerName
        )
        {
            // Poll for the completion of the debug container and retrieve results
            _logger?.LogInformation("Polling for debug container '{ContainerName}' completion on pod '{PodName}'", containerName, pod.Name());

            TimeSpan? timeout = _javaProfilerSettings.ProfileTimeoutMinutes > 0
                ? TimeSpan.FromMinutes(_javaProfilerSettings.ProfileTimeoutMinutes)
                : null;

            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(10); // Poll every 10 seconds
            var maxWaitTime = timeout ?? TimeSpan.FromMinutes(30); // Default 30 minutes if no timeout set

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                try
                {
                    // Check the container status
                    var statusCommand = $"kubectl get pod {pod.Name()} -n {pod.Namespace()} -o json";

                    var statusResult = await RunKubeCommandWithThreadId<CliExecutionResult>(threadId, kubePlugin, async () =>
                     await kubePlugin.ExecuteKubectlCommandSafely(resourceId, statusCommand, "", TimeSpan.FromMinutes(2))
                    );

                    if (statusResult.ErrorOccurred == true)
                    {
                        _logger?.LogError("Failed to get pod status. Error Type: {Error}", statusResult.ErrorType);
                        await Task.Delay(pollInterval);
                        continue;
                    }

                    // Parse the JSON response to check ephemeral container status
                    var podJson = JsonDocument.Parse(statusResult.Output);

                    // Check if ephemeralContainerStatuses exists
                    if (!podJson.RootElement.GetProperty("status").TryGetProperty("ephemeralContainerStatuses", out var ephemeralContainers))
                    {
                        _logger?.LogDebug("No ephemeral container statuses found yet for pod '{PodName}'", pod.Name());
                        await Task.Delay(pollInterval);
                        continue;
                    }

                    var targetContainer = ephemeralContainers.EnumerateArray()
                        .FirstOrDefault(c => c.GetProperty("name").GetString() == containerName);

                    if (targetContainer.ValueKind == JsonValueKind.Undefined)
                    {
                        _logger?.LogDebug("Debug container '{ContainerName}' not found in status yet", containerName);
                        await Task.Delay(pollInterval);
                        continue;
                    }

                    var state = targetContainer.GetProperty("state");

                    if (state.TryGetProperty("terminated", out var terminatedState))
                    {
                        var exitCode = terminatedState.GetProperty("exitCode").GetInt32();
                        var reason = terminatedState.GetProperty("reason").GetString();
                        var finishedAt = terminatedState.GetProperty("finishedAt").GetString();

                        _logger?.LogInformation("Debug container '{ContainerName}' has terminated with exit code {ExitCode} and reason '{Reason}' at {FinishedAt}",
                            containerName, exitCode, reason, finishedAt);

                        // Container has terminated, get full logs
                        var logCommand = $"kubectl logs {pod.Name()} -n {pod.Namespace()} -c {containerName}";
                        var logsResult = await RunKubeCommandWithThreadId<CliExecutionResult>(threadId, kubePlugin, async () =>
                         await kubePlugin.ExecuteKubectlCommandSafely(resourceId, logCommand, "", timeout)
                         );

                        if (logsResult.ErrorOccurred == true)
                        {
                            _logger?.LogError("Failed to retrieve logs from terminated debug container. Error Type: {Error}", logsResult.ErrorType);
                            return $"Debug container terminated (exit code: {exitCode}, reason: {reason}) but failed to retrieve logs: {logsResult.ErrorType}";
                        }

                        return logsResult.Output;
                    }
                    else if (state.TryGetProperty("running", out var runningState))
                    {
                        var startedAt = runningState.GetProperty("startedAt").GetString();
                        _logger?.LogDebug("Debug container '{ContainerName}' is still running (started at {StartedAt})", containerName, startedAt);
                    }
                    else if (state.TryGetProperty("waiting", out var waitingState))
                    {
                        var reason = waitingState.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : "Unknown";
                        var message = waitingState.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : "";

                        _logger?.LogDebug("Debug container '{ContainerName}' is waiting. Reason: {Reason}, Message: {Message}",
                            containerName, reason, message);

                        // Check for error conditions that won't resolve
                        if (reason == "ImagePullBackOff" || reason == "ErrImagePull" || reason == "CrashLoopBackOff")
                        {
                            _logger?.LogError("Debug container '{ContainerName}' is in error state: {Reason} - {Message}",
                                containerName, reason, message);
                            return $"Debug container '{containerName}' failed to start. Reason: {reason}. Message: {message}";
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogError(ex, "Failed to parse pod status JSON");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error while polling for container status");
                }

                // Wait before next poll
                await Task.Delay(pollInterval);
            }

            // Timeout reached
            _logger?.LogWarning("Timeout reached while waiting for debug container '{ContainerName}' to terminate", containerName);

            // Try to get logs anyway in case there's partial output
            var timeoutLogCommand = $"kubectl logs {pod.Name()} -n {pod.Namespace()} -c {containerName}";
            var timeoutLogsResult = await RunKubeCommandWithThreadId<CliExecutionResult>(threadId, kubePlugin, async () =>
             await kubePlugin.ExecuteKubectlCommandSafely(resourceId, timeoutLogCommand, "", TimeSpan.FromMinutes(2))
             );

            var timeoutMessage = $"Timeout reached after {maxWaitTime.TotalMinutes} minutes waiting for debug container '{containerName}' to complete.";

            if (timeoutLogsResult.ErrorOccurred == false && !string.IsNullOrEmpty(timeoutLogsResult.Output))
            {
                return $"{timeoutMessage}\n\nPartial logs retrieved:\n{timeoutLogsResult.Output}";
            }
            else
            {
                return $"{timeoutMessage}\nNo logs could be retrieved.";
            }
        }

        /// <summary>
        /// Checks if a pod has any ephemeral containers.
        /// </summary>
        /// <param name="resourceId">The resource ID for the kubectl command</param>
        /// <param name="pod">The pod to check</param>
        /// <param name="kubePlugin">The Kubernetes plugin instance</param>
        /// <returns>Whether ephemeral containers exist</returns>
        private async Task<bool> HasEphemeralContainersAsync(
            Guid threadId,
            string resourceId,
            V1Pod pod,
            IKubePlugin kubePlugin)
        {
            try
            {
                _logger?.LogDebug("Checking for any ephemeral containers on pod '{PodName}'", pod.Name());

                var statusCommand = $"kubectl get pod {pod.Name()} -n {pod.Namespace()} -o json";
                var statusResult = await RunKubeCommandWithThreadId<CliExecutionResult>(threadId, kubePlugin, async () =>
                  await kubePlugin.ExecuteKubectlCommandSafely(resourceId, statusCommand, "", TimeSpan.FromMinutes(2))
                 );

                if (statusResult.ErrorOccurred == true)
                {
                    _logger?.LogError("Failed to get pod status while checking ephemeral containers. Error Type: {Error}", statusResult.ErrorType);
                    return false;
                }

                var podJson = JsonDocument.Parse(statusResult.Output);

                // Check ephemeral containers in spec (desired state)
                if (podJson.RootElement.GetProperty("spec").TryGetProperty("ephemeralContainers", out var specEphemeralContainers))
                {
                    var containers = specEphemeralContainers.EnumerateArray().ToList();

                    return containers.Count > 0;
                }

                return false;
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "Failed to parse pod status JSON while checking ephemeral containers");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while checking for ephemeral containers on pod '{PodName}'", pod.Name());
                return false;
            }
        }

        public async Task<string> CreateDebugContainerAsync(
            Guid threadId,
            string resourceId,
            IKubernetes client,
            V1Pod pod,
            string debugImageName,
            string targetContainerName,
            IKubePlugin kubePlugin,
            bool interactive = false,
            string? containerName = null,
            string agentMode = "ReadOnly")
        {
            try
            {
                var cmd = $$"""[{"op" : "add","path" : "/spec/ephemeralContainers","value" : [{"command" : ["/home/illuminate/diagnose.sh"],"image" : "{{debugImageName}}","imagePullPolicy" : "Always","name" : "{{containerName}}","securityContext" : {"privileged" : true},"stdin" : true,"targetContainerName" : "{{targetContainerName}}","terminationMessagePath" : "/dev/termination-log","terminationMessagePolicy" : "File","tty" : true}]}]""";

                if (await HasEphemeralContainersAsync(threadId, resourceId, pod, kubePlugin))
                {
                    // slightly different patch command if ephemeralContainers already exist
                    cmd = $$$"""[{"op" : "add","path" : "/spec/ephemeralContainers/-","value" : {"command" : ["/home/illuminate/diagnose.sh"],"image" : "{{{debugImageName}}}","imagePullPolicy" : "Always","name" : "{{{containerName}}}","securityContext" : {"privileged" : true},"stdin" : true,"targetContainerName" : "{{{targetContainerName}}}","terminationMessagePath" : "/dev/termination-log","terminationMessagePolicy" : "File","tty" : true}}]""";
                }

                // Build kubectl debug command
                var debugCommandParts = new List<string>
                {
                    "kubectl",
                    "patch",
                    "pod",
                    pod.Name(),
                    "-n", pod.Namespace(),
                    "--type=json",
                    "--subresource=ephemeralcontainers",
                    "--patch-file /dev/stdin"
                };

                var debugCommand = string.Join(" ", debugCommandParts);

                if (agentMode == ActionMode.ReadOnly.ToString())
                {
                    _logger?.LogInformation("Debug container command (ReadOnly mode): kubectl {Command}", debugCommand);
                    return $"Debug container command that would be executed:\nkubectl {debugCommand}\n\n" +
                        $"This would create an ephemeral debug container '{containerName}' using image '{debugImageName}' " +
                        $"attached to pod '{pod.Name}' in namespace '{pod.Namespace}' with shared process namespace.";
                }

                _logger?.LogInformation(
                    "Creating debug container '{ContainerName}' for pod '{PodName}' in namespace '{Namespace}' using image '{Image}'",
                    containerName, pod.Name(), pod.Namespace(), debugImageName);

                TimeSpan? timeout = _javaProfilerSettings.ProfileTimeoutMinutes > 0
                    ? TimeSpan.FromMinutes(_javaProfilerSettings.ProfileTimeoutMinutes)
                    : null;

                var cliExecutionResult = await RunKubeCommandWithThreadId<CliExecutionResult>(threadId, kubePlugin, async () =>
                 await kubePlugin.ExecuteKubectlCommandSafely(resourceId, debugCommand, cmd, timeout)
                 );

                if (cliExecutionResult.ErrorOccurred == true)
                {
                    _logger?.LogError("Failed to create debug container. Error Type: {Error}", cliExecutionResult.ErrorType);
                    return $"Error creating debug container: {cliExecutionResult.ErrorType}";
                }

                return cliExecutionResult.Output;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating debug container for pod '{PodName}' in namespace '{Namespace}'", pod.Name(), pod.Namespace());
                return $"Error creating debug container: {ex.Message}";
            }
        }

        private string ExtractAnalysisFromDebugResult(string debugResult, string podName)
        {
            // Extract analysis section from debug result
            if (string.IsNullOrEmpty(debugResult))
            {
                _logger?.LogError("Debug container returned empty result for pod '{PodName}'", podName);
                return $"Error: Debug container creation failed or returned empty result for pod '{podName}'";
            }

            int startIndex = debugResult.IndexOf(analysisStartMarker);
            int endIndex = debugResult.IndexOf(analysisEndMarker);

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                return debugResult.Substring(startIndex, endIndex - startIndex + analysisEndMarker.Length).Trim();
            }
            else
            {
                _logger?.LogWarning("Analysis delimiters not found in debug container output for pod '{PodName}'. StartIndex: {StartIndex}, EndIndex: {EndIndex}",
                                podName, startIndex, endIndex);

                // Return the full debug result if delimiters are not found
                return $"Java CPU Profiling for Pod: {podName}\n" +
                       "Warning: Analysis delimiters not found in output. Full debug container result:\n\n" +
                       debugResult;
            }
        }

        private string ExtractMetadataFromDebugResult(string debugResult, string podName)
        {
            // Extract metadata section from debug result
            if (string.IsNullOrEmpty(debugResult))
            {
                _logger?.LogError("Debug container returned empty result for pod '{PodName}'", podName);
                return $"Error: Debug container creation failed or returned empty result for pod '{podName}'";
            }

            int startIndex = debugResult.IndexOf(metadataStartMarker);
            int endIndex = debugResult.IndexOf(metadataEndMarker);

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                startIndex += metadataStartMarker.Length;
                return debugResult.Substring(startIndex, endIndex - startIndex).Trim();
            }
            else
            {
                _logger?.LogWarning("Metadata delimiters not found in debug container output for pod '{PodName}'. StartIndex: {StartIndex}, EndIndex: {EndIndex}",
                                podName, startIndex, endIndex);

                // Return the full debug result if delimiters are not found
                return $"Java Metadata for Pod: {podName}\n" +
                       "Warning: Metadata delimiters not found in output. Full debug container result:\n\n" +
                       debugResult;
            }
        }

        private string ExtractDiagnosisReportFromDebugResult(string debugResult, string podName)
        {
            // Extract diagnosis REPORT section from debug result

            if (string.IsNullOrEmpty(debugResult))
            {
                _logger?.LogError("Debug container returned empty result for pod '{PodName}'", podName);
                return $"Error: Debug container creation failed or returned empty result for pod '{podName}'";
            }

            int startIndex = debugResult.IndexOf(diagnosisStartMarker);
            int endIndex = debugResult.IndexOf(diagnosisEndMarker);

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                startIndex += diagnosisStartMarker.Length;
                return debugResult.Substring(startIndex, endIndex - startIndex).Trim();
            }

            startIndex = debugResult.IndexOf(diagnosisStartMarkerAlt);
            endIndex = debugResult.IndexOf(diagnosisEndMarkerAlt);

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                startIndex += diagnosisStartMarkerAlt.Length;
                return debugResult.Substring(startIndex, endIndex - startIndex).Trim();
            }
            else
            {
                _logger?.LogWarning("Diagnosis report delimiters not found in debug container output for pod '{PodName}'. StartIndex: {StartIndex}, EndIndex: {EndIndex}",
                                podName, startIndex, endIndex);

                // Return the full debug result if delimiters are not found
                return $"Java Diagnosis report for Pod: {podName}\n" +
                       "Warning: Diagnosis report delimiters not found in output. Full debug container result:\n\n" +
                       debugResult;
            }
        }
    }
}
