// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces;

public record LogAnalysisResult(
    bool HasPullFailure,
    string? ErrorMessage = null,
    string? DetailedDiagnosis = null,
    string? SuggestedFix = null);

public interface ILogAnalysisService
{
    public LogAnalysisResult AnalyzeContainerAppLogs(IReadOnlyCollection<string> logs);

    public LogAnalysisResult AnalyzeContainerAppLogs(IEnumerable<(DateTime Timestamp, string Message)> logs);

    public LogAnalysisResult AnalyzeWebAppLogs(IEnumerable<string> logs);

    public bool IsCriticalError(string logMessage);

    public string GetErrorSeverity(string logMessage);
}
