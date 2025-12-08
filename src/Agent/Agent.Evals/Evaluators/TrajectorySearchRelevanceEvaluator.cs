// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Logging;

namespace Agent.Evals.Evaluators;

/// <summary>
/// Evaluates the relevance and quality of trajectory search results using LLM as judge.
/// Assesses how well the retrieved trajectories match the search query intent.
/// </summary>
public class TrajectorySearchRelevanceEvaluator : IEvaluator
{
    public const string TrajectoryRelevanceMetricName = "TrajectoryRelevance";
    public const string DiversityMetricName = "Diversity";
    public const string RankingQualityMetricName = "RankingQuality";
    public const string ActionabilityMetricName = "Actionability";

    public const string PrecisionMetricName = "Precision";
    public const string RecallMetricName = "Recall";
    public const string F1ScoreMetricName = "F1Score";

    private readonly IChatClientProvider _chatClientProvider;
    private readonly ILogger<TrajectorySearchRelevanceEvaluator> _logger;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> EvaluationMetricNames => [
        TrajectoryRelevanceMetricName,
        PrecisionMetricName,
        RecallMetricName,
        F1ScoreMetricName,
        DiversityMetricName,
        RankingQualityMetricName,
        ActionabilityMetricName
    ];

    public TrajectorySearchRelevanceEvaluator(IChatClientProvider chatClientProvider, ILogger<TrajectorySearchRelevanceEvaluator> logger)
    {
        _chatClientProvider = chatClientProvider ?? throw new ArgumentNullException(nameof(chatClientProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        // This implementation is designed for custom evaluation scenarios
        // For direct trajectory search evaluation, use the EvaluateTrajectorySearchAsync method
        var metric = new StringMetric(TrajectoryRelevanceMetricName,
            "0",
            "This evaluator requires custom input for trajectory search evaluation. Use EvaluateTrajectorySearchAsync method instead.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
    }

    /// <summary>
    /// Evaluates trajectory search results for relevance to the search query using LLM as judge.
    /// Only evaluates metrics that can be reasonably assessed without ground truth.
    /// </summary>
    /// <param name="searchQuery">The search query used to find trajectories</param>
    /// <param name="searchResults">The retrieved trajectory search results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with LLM-assessable relevance metrics</returns>
    public async Task<EvaluationResult> EvaluateTrajectorySearchAsync(
        string searchQuery,
        IList<SearchDocumentResult> searchResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (searchResults == null || searchResults.Count == 0)
            {
                var zeroMetrics = new[]
                {
                    CreateNumericMetric(TrajectoryRelevanceMetricName, "Overall Relevance", 0, 4, 2),
                    CreateNumericMetric(DiversityMetricName,        "Diversity",         0, 0.8, 0.4),
                    CreateNumericMetric(RankingQualityMetricName,   "Ranking Quality",   0, 4, 2),
                    CreateNumericMetric(ActionabilityMetricName,    "Actionability",     0, 4, 2)
                };
                return new EvaluationResult(zeroMetrics);
            }

            _logger.LogInternalInformation("Evaluating trajectory search relevance for query: {Query} with {Count} results",
                searchQuery, searchResults.Count);

            // LLM-based evaluation prompt - only for metrics that don't require ground truth
            var systemPrompt = @"You are an expert evaluator for trajectory search results in an SRE (Site Reliability Engineering) context. A trajectory represents a documented incident resolution process containing symptoms, root causes, and resolution steps.

Your task is to provide an evaluation of how well the retrieved trajectories match the search query intent. You will evaluate only dimensions that can be assessed from the available information.

Evaluate these four dimensions:

1. **Overall Relevance (1-5)**:
   - 5: Trajectories perfectly address the search query problem
   - 4: Trajectories address similar problems with high relevance
   - 3: Trajectories are moderately relevant to the query
   - 2: Trajectories have limited relevance to the query
   - 1: Trajectories are mostly unrelated to the query

2. **Diversity (0.0-1.0)**:
   - 1.0: Excellent variety in problem types, Azure services, and resolution approaches
   - 0.8: Good variety in at least two categories
   - 0.5: Moderate variety, some redundancy in solutions
   - 0.2: Limited variety, mostly similar approaches
   - 0.0: No variety, all trajectories cover identical scenarios

3. **Ranking Quality (1-5)**:
   - 5: Perfect ordering with most relevant results first
   - 4: Good ordering with minor improvements possible
   - 3: Acceptable ordering with some relevant results first
   - 2: Poor ordering with relevant results scattered
   - 1: Random or reversed ordering relative to relevance

4. **Actionability (1-5)**:
   - 5: Trajectories provide complete, detailed steps to solve the problem
   - 4: Trajectories provide clear guidance with minor gaps
   - 3: Trajectories provide adequate guidance requiring some interpretation
   - 2: Trajectories provide limited guidance requiring significant work
   - 1: Trajectories provide minimal actionable information

When evaluating, consider:
- **Symptom Matching**: Do symptoms align with what might be expected from the query?
- **Root Cause Alignment**: Are root causes relevant to the problem space?
- **Solution Applicability**: Would resolution steps help solve the query's problem?
- **Technical Depth**: Are trajectories well-documented with clear technical details?

You MUST return ONLY a valid JSON object with exactly this structure:
{
  ""overall_relevance"": <integer between 1-5>,
  ""diversity"": <number between 0.0-1.0>,
  ""ranking_quality"": <integer between 1-5>,
  ""actionability"": <integer between 1-5>
}

Return ONLY the JSON object. No explanation text, no comments, no introduction, and no additional formatting.";

            var userPrompt = $@"Search Query: ""{searchQuery}"" Retrieved Trajectories: {FormatTrajectories(searchResults)}";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var response = await _chatClientProvider.EvalModel.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var evaluation = response.Text ?? "No evaluation generated";

            _logger.LogInternalInformation("Completed trajectory search relevance evaluation");

            // Parse metrics from the evaluation response
            var metrics = ParseLLMEvaluation(evaluation, searchResults.Count);

            return new EvaluationResult(metrics.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error evaluating trajectory search relevance");
            var errorMetric = new StringMetric(TrajectoryRelevanceMetricName,
                "0",
                $"Error during evaluation: {ex.Message}");
            return new EvaluationResult(errorMetric);
        }
    }

    /// <summary>
    /// Evaluates trajectory search results with ground truth comparison.
    /// Calculates precision, recall, and F1-score using actual ground truth data.
    /// </summary>
    /// <param name="searchQuery">The search query used to find trajectories</param>
    /// <param name="searchResults">The retrieved trajectory search results</param>
    /// <param name="groundTruthTrajectories">Expected relevant trajectories for the query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with precision, recall, and F1-score</returns>
    public EvaluationResult EvaluateTrajectorySearchWithGroundTruthAsync(
        string searchQuery,
        IList<SearchDocumentResult> searchResults,
        HashSet<string> groundTruthTrajectoryIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (groundTruthTrajectoryIds is null || !groundTruthTrajectoryIds.Any())
                throw new ArgumentException("Ground‑truth list must not be null or empty", nameof(groundTruthTrajectoryIds));

            searchResults ??= Array.Empty<SearchDocumentResult>();

            var groundTruthIdSet = new HashSet<string>(groundTruthTrajectoryIds, StringComparer.OrdinalIgnoreCase);
            var retrievedIdSet = new HashSet<string>(searchResults.Select(r => r.Title), StringComparer.OrdinalIgnoreCase);

            int relevantRetrieved = retrievedIdSet.Count(id => groundTruthIdSet.Contains(id));

            double precision = retrievedIdSet.Count > 0 ? (double)relevantRetrieved / retrievedIdSet.Count : 0.0;
            double recall = groundTruthTrajectoryIds.Count > 0 ? (double)relevantRetrieved / groundTruthTrajectoryIds.Count : 0.0;
            double f1Score = (precision + recall) > 0 ? 2 * (precision * recall) / (precision + recall) : 0.0;

            var metrics = new List<NumericMetric>
            {
                CreateNumericMetric(PrecisionMetricName, "Precision", precision, 0.8, 0.4),
                CreateNumericMetric(RecallMetricName, "Recall", recall, 0.8, 0.4),
                CreateNumericMetric(F1ScoreMetricName, "F1-Score", f1Score, 0.8, 0.4)
            };

            return new EvaluationResult(metrics.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error evaluating trajectory search with ground truth");
            var errorMetric = new StringMetric(TrajectoryRelevanceMetricName,
                "0",
                $"Error during evaluation: {ex.Message}");
            return new EvaluationResult(errorMetric);
        }
    }

    private static string FormatTrajectories(IList<SearchDocumentResult> trajectories)
    {
        var formatted = new List<string>();

        for (int i = 0; i < trajectories.Count; i++)
        {
            var trajectory = trajectories[i];
            var trajectoryText = $@"Trajectory {i + 1} (ID: {trajectory.Id}):
- Title: {trajectory.Title ?? "N/A"}
- Symptoms Observed: {trajectory.SymptomsObserved ?? "N/A"}
- Initial Symptoms: {trajectory.InitialSymptoms ?? "N/A"}
- Root Cause: {trajectory.RootCause ?? "N/A"}
- Steps Followed: {trajectory.StepsFollowed ?? "N/A"}
- Resource Types: {(trajectory.ResourceTypes != null ? string.Join(", ", trajectory.ResourceTypes) : "N/A")}
- Search Score: {trajectory.SearchScore:F2}
- Reranker Score: {trajectory.RerankerScore:F2}";

            formatted.Add(trajectoryText);
        }

        return string.Join("\n\n", formatted);
    }

    private static NumericMetric[] ParseLLMEvaluation(string evaluation, int resultCount)
    {
        try
        {
            using var doc = JsonDocument.Parse(evaluation);
            int rel = doc.RootElement.GetProperty("overall_relevance").GetInt32();
            double div = doc.RootElement.GetProperty("diversity").GetDouble();
            int rank = doc.RootElement.GetProperty("ranking_quality").GetInt32();
            int act = doc.RootElement.GetProperty("actionability").GetInt32();

            return new[]
            {
                CreateNumericMetric(TrajectoryRelevanceMetricName, "Overall Relevance", rel, 4, 2),
                CreateNumericMetric(DiversityMetricName,        "Diversity",         div, 0.8, 0.4),
                CreateNumericMetric(RankingQualityMetricName,   "Ranking Quality",   rank,4, 2),
                CreateNumericMetric(ActionabilityMetricName,    "Actionability",     act, 4, 2)
            };
        }
        catch
        {
            return new[]
            {
                CreateNumericMetric(TrajectoryRelevanceMetricName, "Overall Relevance", 0, 4, 2),
                CreateNumericMetric(DiversityMetricName,        "Diversity",         0, 0.8, 0.4),
                CreateNumericMetric(RankingQualityMetricName,   "Ranking Quality",   0, 4, 2),
                CreateNumericMetric(ActionabilityMetricName,    "Actionability",     0, 4, 2)
            };
        }
    }

    public static NumericMetric CreateNumericMetric(string name, string displayName, double value, double goodThreshold, double acceptableThreshold)
    {
        var metric = new NumericMetric(name, value, $"{displayName}: {value:F3}")
        {
            Interpretation = value >= goodThreshold
                ? new(EvaluationRating.Good, reason: $"Excellent {displayName}")
                : value >= acceptableThreshold
                    ? new(EvaluationRating.Inconclusive, reason: $"Moderate {displayName}")
                    : new(EvaluationRating.Unacceptable, failed: true, reason: $"Poor {displayName}")
        };
        return metric;
    }

    private static StringMetric CreateStringMetric(string name, double value, string displayName)
    {
        var metric = new StringMetric(name, $"{displayName}: {value:F3}", $"{displayName}: {value:F3}");
        metric.Interpretation = value switch
        {
            >= 0.8 => new EvaluationMetricInterpretation(EvaluationRating.Good, reason: $"Excellent {displayName}"),
            >= 0.6 => new EvaluationMetricInterpretation(EvaluationRating.Good, reason: $"Good {displayName}"),
            >= 0.4 => new EvaluationMetricInterpretation(EvaluationRating.Inconclusive, reason: $"Moderate {displayName}"),
            >= 0.2 => new EvaluationMetricInterpretation(EvaluationRating.Unacceptable, failed: true, reason: $"Poor {displayName}"),
            _ => new EvaluationMetricInterpretation(EvaluationRating.Unacceptable, failed: true, reason: $"Very poor {displayName}")
        };
        return metric;
    }
}

/// <summary>
/// Input model for trajectory search evaluation
/// </summary>
public record TrajectorySearchEvaluationInput(
    string SearchQuery,
    IList<SearchDocumentResult> SearchResults
);
