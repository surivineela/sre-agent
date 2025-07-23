// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.AgentMemory;
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

    private readonly IChatClient _chatClient;
    private readonly ILogger<TrajectorySearchRelevanceEvaluator> _logger;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> EvaluationMetricNames => [TrajectoryRelevanceMetricName];

    public TrajectorySearchRelevanceEvaluator(IChatClient chatClient, ILogger<TrajectorySearchRelevanceEvaluator> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
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
    /// Evaluates trajectory search results for relevance to the search query.
    /// </summary>
    /// <param name="searchQuery">The search query used to find trajectories</param>
    /// <param name="searchResults">The retrieved trajectory search results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with relevance score and explanation</returns>
    public async Task<EvaluationResult> EvaluateTrajectorySearchAsync(
        string searchQuery,
        IList<SearchDocumentResult> searchResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (searchResults == null || !searchResults.Any())
            {
                var emptyMetric = new StringMetric(TrajectoryRelevanceMetricName,
                    "0",
                    "No search results to evaluate");
                return new EvaluationResult(emptyMetric);
            }

            _logger.LogInternalInformation("Evaluating trajectory search relevance for query: {Query} with {Count} results",
                searchQuery, searchResults.Count);

            var systemPrompt = @"You are an expert evaluator for trajectory search results. A trajectory represents a documented incident resolution process containing symptoms, root causes, and resolution steps.

Your task is to evaluate how well the retrieved trajectories match the search query intent. Consider:

1. **Relevance**: Do the trajectories address the same or similar problems as described in the search query?
2. **Symptom Matching**: Do the symptoms in the trajectories align with what might be expected from the query?
3. **Root Cause Alignment**: Are the root causes in the trajectories relevant to the problem space of the query?
4. **Solution Applicability**: Would the resolution steps be helpful for someone with the query's problem?
5. **Quality**: Are the trajectories well-documented with clear symptoms, causes, and solutions?

Rate the overall relevance on a scale of 1-5:
- 1: Completely irrelevant trajectories
- 2: Mostly irrelevant with some tangential connection
- 3: Somewhat relevant but missing key aspects
- 4: Highly relevant with good alignment
- 5: Excellent relevance and quality

Provide your rating and a brief explanation of your assessment.";

            var userPrompt = $@"Search Query: ""{searchQuery}""

Retrieved Trajectories:
{FormatTrajectories(searchResults)}

Please evaluate the relevance of these trajectories to the search query. Provide:
1. A relevance score (1-5)
2. A brief explanation of your assessment
3. Any specific strengths or weaknesses you observe

Format your response as:
Score: [1-5]
Explanation: [Your detailed assessment]";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var evaluation = response.Text ?? "No evaluation generated";

            _logger.LogInternalInformation("Completed trajectory search relevance evaluation");

            // Extract score from evaluation response
            var score = ExtractScoreFromEvaluation(evaluation);
            var metric = new StringMetric(TrajectoryRelevanceMetricName, evaluation, evaluation);

            // Add interpretation
            InterpretMetric(metric, score);

            return new EvaluationResult(metric);
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

    private static string FormatTrajectories(IList<SearchDocumentResult> trajectories)
    {
        var formatted = new List<string>();

        for (int i = 0; i < trajectories.Count; i++)
        {
            var trajectory = trajectories[i];
            var trajectoryText = $@"Trajectory {i + 1}:
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

    private static int ExtractScoreFromEvaluation(string evaluation)
    {
        // Try to extract score from "Score: X" pattern
        var scoreMatch = System.Text.RegularExpressions.Regex.Match(evaluation, @"Score:\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (scoreMatch.Success && int.TryParse(scoreMatch.Groups[1].Value, out int score))
        {
            return Math.Clamp(score, 1, 5);
        }

        // Default to neutral score if no clear score found
        return 3;
    }

    private static void InterpretMetric(StringMetric metric, int score)
    {
        metric.Interpretation = score switch
        {
            5 => new EvaluationMetricInterpretation(
                EvaluationRating.Good,
                reason: $"Excellent trajectory relevance with score {score}"),
            4 => new EvaluationMetricInterpretation(
                EvaluationRating.Good,
                reason: $"High trajectory relevance with score {score}"),
            3 => new EvaluationMetricInterpretation(
                EvaluationRating.Inconclusive,
                reason: $"Moderate trajectory relevance with score {score}"),
            2 => new EvaluationMetricInterpretation(
                EvaluationRating.Unacceptable,
                failed: true,
                reason: $"Low trajectory relevance with score {score}"),
            1 => new EvaluationMetricInterpretation(
                EvaluationRating.Unacceptable,
                failed: true,
                reason: $"Poor trajectory relevance with score {score}"),
            _ => new EvaluationMetricInterpretation(
                EvaluationRating.Unknown,
                failed: true,
                reason: $"Invalid score {score}")
        };
    }
}

/// <summary>
/// Input model for trajectory search evaluation
/// </summary>
public record TrajectorySearchEvaluationInput(
    string SearchQuery,
    IList<SearchDocumentResult> SearchResults
);
