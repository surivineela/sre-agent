// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class LogAnalysisService : ILogAnalysisService
{
    private readonly Dictionary<string, (string Pattern, string Message, string Solution)> _errorPatterns;

    public LogAnalysisService()
    {
        _errorPatterns = new Dictionary<string, (string Pattern, string Message, string Solution)>
        {
            // Container App specific patterns
            { "ContainerApp_NoImage", (
                @"Failed to resolve image|image .* not found|repository .* not found",
                "Container image not found in the registry",
                "Verify the image name, tag, and registry path are correct"
            )},
            { "ContainerApp_Auth", (
                @"unauthorized|authentication required|denied: access forbidden|access denied",
                "Authentication failed when pulling the image",
                "Check registry credentials or managed identity configuration"
            )},
            { "ContainerApp_Network", (
                @"network timeout|connection refused|connection timed out|TLS handshake timeout",
                "Network connectivity issues when pulling the image",
                "Verify network configuration, NSG rules, and registry endpoint accessibility"
            )},
            // Linux Web App specific patterns
            { "WebApp_StartupFailed", (
                @"failed to start container|container .* failed to start",
                "Container failed to start after pulling",
                "Check container startup configuration and environment variables"
            )},
            { "WebApp_ImagePull", (
                @"Error: ImagePullBackOff|Back-off pulling image",
                "Container runtime is backing off from pulling the image",
                "Check previous error messages for root cause and verify registry access"
            )},
            { "WebApp_Registry", (
                @"cannot pull from registry|registry lookup failed",
                "Failed to communicate with container registry",
                "Verify registry URL and network connectivity"
            )}
        };
    }
    public LogAnalysisResult AnalyzeContainerAppLogs(IReadOnlyCollection<string> logs)
    {
        var result = new LogAnalysisResult ( HasPullFailure: false );
        if (logs == null || !logs.Any())
        {
            return result with { ErrorMessage = "No logs available for analysis" };
        }

        // Common error patterns
        var errorPatterns = new Dictionary<string, (string Diagnosis, string Fix)>
        {
            { @"unauthorized|authentication required|access denied",
                ("Authentication failure when pulling the image",
                    "Verify registry credentials or managed identity configuration") },

            { @"not found|404|no such image",
                ("Image not found in the registry",
                    "Verify the image name and tag are correct") },

            { @"exceeded rate limit|rate limited|too many requests",
                ("Registry rate limit exceeded",
                    "Use authenticated pulls or wait for rate limit reset") },

            { @"network timeout|connection refused|cannot connect",
                ("Network connectivity issues",
                    "Check network configuration and NSG rules") },

            { @"insufficient memory|no space left|disk pressure",
                ("Resource constraints preventing image pull",
                    "Check available resources and cleanup unused images") },

            { @"manifest unknown|manifest invalid|unsupported manifest",
                ("Invalid or unsupported image manifest",
                    "Verify image architecture compatibility and manifest format") }
        };

        foreach (var log in logs)
        {
            foreach (var pattern in errorPatterns)
            {
                if (Regex.IsMatch(log, pattern.Key, RegexOptions.IgnoreCase))
                {
                    return new LogAnalysisResult(
                        HasPullFailure: true,
                        ErrorMessage: log,
                        DetailedDiagnosis: pattern.Value.Diagnosis,
                        SuggestedFix: pattern.Value.Fix
                    );
                }
            }

            // Check for specific error codes
            if (log.Contains("ExitCode="))
            {
                var exitCodeMatch = Regex.Match(log, @"ExitCode=(\d+)");
                if (exitCodeMatch.Success)
                {
                    string exitCode = exitCodeMatch.Groups[1].Value;
                    switch (exitCode)
                    {
                        case "125":
                            result = new LogAnalysisResult(
                                HasPullFailure: true,
                                DetailedDiagnosis: "Container runtime error during image pull",
                                SuggestedFix: "Check container runtime health and configuration"
                            );
                            break;
                        case "127":
                            result = new LogAnalysisResult(
                                HasPullFailure: true,
                                DetailedDiagnosis: "Command not found error, possible container runtime issue",
                                SuggestedFix: "Verify container runtime installation and configuration"
                            );
                            break;
                        // Add more exit codes as needed
                    }

                    if (result.HasPullFailure)
                    {
                        return result with { ErrorMessage = log };
                    }
                }
            }
        }

        // Special case: Check for Back-off pattern
        var backoffLogs = logs.Where(l => l.Contains("Back-off pulling image")).ToList();
        if (backoffLogs.Any())
        {
            return new LogAnalysisResult(
                HasPullFailure: true,
                ErrorMessage: backoffLogs.First(),
                DetailedDiagnosis:
                "Container runtime is backing off from pulling the image due to repeated failures",
                SuggestedFix: "Check previous error messages for root cause");
        }

        return result;
    }

    public LogAnalysisResult AnalyzeContainerAppLogs(IEnumerable<(DateTime Timestamp, string Message)> logs)
    {
        var result = new LogAnalysisResult(HasPullFailure: false);

        if (logs?.Any() != true)
        {
            return result with { ErrorMessage = "No logs available for analysis"};
        }

        // Get the most recent logs first
        var orderedLogs = logs.OrderByDescending(l => l.Timestamp);

        foreach (var (timestamp, message) in orderedLogs)
        {
            foreach (var errorPattern in _errorPatterns)
            {
                if (Regex.IsMatch(message, errorPattern.Value.Pattern, RegexOptions.IgnoreCase))
                {
                    return new LogAnalysisResult(
                        HasPullFailure: true,
                        ErrorMessage: message,
                        DetailedDiagnosis: $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {errorPattern.Value.Message}",
                        SuggestedFix: errorPattern.Value.Solution);
                }
            }
        }

        // Check for cyclic failures
        if (HasCyclicFailures(orderedLogs))
        {
            return new LogAnalysisResult(
                HasPullFailure: true,
                ErrorMessage: "Detected repeated pull failures",
                DetailedDiagnosis: "Container is experiencing cyclic pull failures",
                SuggestedFix: "Review authentication configuration and network connectivity");
        }

        return result;
    }

    public LogAnalysisResult AnalyzeWebAppLogs(IEnumerable<string> logs)
    {
        var result = new LogAnalysisResult(HasPullFailure: false);

        if (logs?.Any() != true)
        {
            return result with { ErrorMessage = "No logs available for analysis" };
        }

        // Parse timestamp if available
        var parsedLogs = logs.Select(log =>
            {
                var match = Regex.Match(log, @"^\[(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})\](.*)$");
                if (match.Success)
                {
                    return (
                        Timestamp: DateTime.Parse(match.Groups[1].Value),
                        Message: match.Groups[2].Value.Trim()
                    );
                }
                return (Timestamp: DateTime.MinValue, Message: log);
            })
            .OrderByDescending(l => l.Timestamp);

        foreach (var (timestamp, message) in parsedLogs)
        {
            foreach (var errorPattern in _errorPatterns)
            {
                if (Regex.IsMatch(message, errorPattern.Value.Pattern, RegexOptions.IgnoreCase))
                {
                    return new LogAnalysisResult(
                        HasPullFailure: true,
                        ErrorMessage: message,
                        DetailedDiagnosis: timestamp != DateTime.MinValue
                            ? $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {errorPattern.Value.Message}"
                            : errorPattern.Value.Message,
                        SuggestedFix: errorPattern.Value.Solution);
                }
            }
        }

        // Check for environment-specific issues
        if (HasEnvironmentIssues(logs))
        {
            return new LogAnalysisResult(
                HasPullFailure: true,
                ErrorMessage: "Detected environment configuration issues",
                DetailedDiagnosis: "Container environment variables or configuration may be incorrect",
                SuggestedFix: "Review environment variables and app settings");
        }

        return result;
    }

    public bool IsCriticalError(string logMessage)
    {
        var criticalPatterns = new[]
        {
            @"authentication failed",
            @"access denied",
            @"permission denied",
            @"certificate error",
            @"TLS handshake failure",
            @"network is unreachable",
            @"operation not permitted"
        };

        return criticalPatterns.Any(pattern =>
            Regex.IsMatch(logMessage, pattern, RegexOptions.IgnoreCase));
    }

    public string GetErrorSeverity(string logMessage)
    {
        if (IsCriticalError(logMessage))
            return "Critical";

        if (logMessage.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
            logMessage.Contains("retry", StringComparison.OrdinalIgnoreCase))
            return "Warning";

        return "Info";
    }

    private bool HasEnvironmentIssues(IEnumerable<string> logs)
    {
        var envIssuePatterns = new[]
        {
            @"invalid environment variable",
            @"missing required environment variable",
            @"configuration error",
            @"invalid application setting",
            @"environment variable .* not set",
            @"required configuration .* missing"
        };

        return logs.Any(log =>
            envIssuePatterns.Any(pattern =>
                Regex.IsMatch(log, pattern, RegexOptions.IgnoreCase)));
    }

    private bool HasCyclicFailures(IEnumerable<(DateTime Timestamp, string Message)> logs)
    {
        const int failureThreshold = 3;
        const int timeWindowMinutes = 15;

        var recentFailures = logs
            .Where(l => DateTime.UtcNow.Subtract(l.Timestamp).TotalMinutes <= timeWindowMinutes)
            .Where(l => l.Message.Contains("failed to pull") ||
                        l.Message.Contains("ImagePullBackOff") ||
                        l.Message.Contains("ErrImagePull"))
            .Take(failureThreshold + 1)
            .ToList();

        return recentFailures.Count >= failureThreshold;
    }
}
