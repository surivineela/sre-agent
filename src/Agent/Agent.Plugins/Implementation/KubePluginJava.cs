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
            string resourceId,
            IKubernetes client,
            V1Pod pod,
            string targetContainerName,
            IKubePlugin kubePlugin
            )
        {

            // Create debug container for enhanced profiling capabilities
            _logger?.LogInformation("Creating debug container for enhanced profiling access to pod '{PodName}'", pod.Name());

            if (string.IsNullOrWhiteSpace(_javaProfilerSettings.DebugProfileContainer))
            {
                _logger?.LogError("Java profiler debug profile container is not configured.");
                return "Error: Java profiler debug profile container is not configured.";
            }

            var debugResult = await CreateDebugContainerAsync(
                resourceId,
                client,
                pod,
                debugImageName: _javaProfilerSettings.DebugProfileContainer,
                targetContainerName,
                kubePlugin,
                true, // Use interactive mode for profiling
                containerName: $"java-profiler-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                agentMode: "Write"
            );

            return ExtractAnalysisFromDebugResult(debugResult, pod.Name()) +
                   "\n\n" +
                   ExtractMetadataFromDebugResult(debugResult, pod.Name()) +
                   "\n\n" +
                   ExtractDiagnosisReportFromDebugResult(debugResult, pod.Name());
        }

        public async Task<string> CreateDebugContainerAsync(
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
                // Generate debug container name if not provided
                containerName ??= $"profiler-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

                // Build kubectl debug command
                var debugCommandParts = new List<string>
                {
                    "debug",
                    pod.Name(),
                    "-n", pod.Namespace(),
                    "--image", debugImageName,
                    "--target", targetContainerName,
                    "--container", containerName,
                    "--profile", "sysadmin",
                    "--share-processes"
                };

                if (interactive)
                {
                    debugCommandParts.AddRange(new[] { "-it", "--stdin", "--tty" });
                }
                else
                {
                    debugCommandParts.Add("--attach=false");
                }

                debugCommandParts.AddRange(new[] { "--", "/home/illuminate/diagnose.sh" });

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

                var cliExecutionResult = await kubePlugin.ExecuteKubectlCommandSafely(resourceId, debugCommand, "", timeout);

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
            const string analysisStartMarker = "-------------------- ANALYSIS START --------------------";
            const string analysisEndMarker = "-------------------- ANALYSIS END ----------------------";

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
            const string metadataStartMarker = "-------------------- METADATA START --------------------";
            const string metadataEndMarker = "-------------------- METADATA END ----------------------";

            if (string.IsNullOrEmpty(debugResult))
            {
                _logger?.LogError("Debug container returned empty result for pod '{PodName}'", podName);
                return $"Error: Debug container creation failed or returned empty result for pod '{podName}'";
            }

            int startIndex = debugResult.IndexOf(metadataStartMarker);
            int endIndex = debugResult.IndexOf(metadataEndMarker);

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                return debugResult.Substring(startIndex, endIndex - startIndex + metadataEndMarker.Length).Trim();
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
            const string diagnosisStartMarker = "-------------------- DIAGNOSIS REPORT START ----------------------";
            const string diagnosisEndMarker = "-------------------- DIAGNOSIS REPORT END ----------------------";

            if (string.IsNullOrEmpty(debugResult))
            {
                _logger?.LogError("Debug container returned empty result for pod '{PodName}'", podName);
                return $"Error: Debug container creation failed or returned empty result for pod '{podName}'";
            }

            int startIndex = debugResult.IndexOf(diagnosisStartMarker);
            int endIndex = debugResult.IndexOf(diagnosisEndMarker);

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                return debugResult.Substring(startIndex, endIndex - startIndex + diagnosisEndMarker.Length).Trim();
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
