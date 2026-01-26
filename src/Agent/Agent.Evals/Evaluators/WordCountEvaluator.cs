// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Evaluation.Evaluators;

/// <summary>
/// A non-AI-based evaluator that counts the number of words present in the response that is being evaluated.
/// </summary>
/// <remarks>
/// The word count is returned via a <see cref="NumericMetric"/> as part of the returned
/// <see cref="EvaluationResult"/>.
/// </remarks>
public class WordCountEvaluator : IEvaluator
{
    public const string WordCountMetricName = "Words";

    /// <inheritdoc/>
    public IReadOnlyCollection<string> EvaluationMetricNames => [WordCountMetricName];

    /// <summary>
    /// Counts the number of words in the supplied string.
    /// </summary>
    private static int CountWords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        MatchCollection matches = Regex.Matches(input, @"\b\w+\b");
        return matches.Count;
    }

    /// <summary>
    /// Provides a default interpretation for the supplied <paramref name="metric"/>.
    /// </summary>
    /// <remarks>
    /// The default interpretation provided in this method considers the supplied <paramref name="metric"/> to be good
    /// (acceptable) if the detected word count is at or under 100. Otherwise, the/ <paramref name="metric"/> is
    /// considered as failed.
    /// </remarks>
    private static void Interpret(StringMetric metric)
    {
        if (metric.Value is null)
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to calculate word count for the response.");
        }

        if (string.IsNullOrWhiteSpace(metric.Value))
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect word count score used in the response.");
        }
        else if (int.TryParse(metric.Value, out int markdownScore))
        {
            metric.Value = markdownScore.ToString();
            metric.Interpretation =
                markdownScore is >= 4
                    ? new EvaluationMetricInterpretation(
                        EvaluationRating.Good,
                        reason: $"Detected word count score '{metric.Value}' was >= 4.")
                    : new EvaluationMetricInterpretation(
                        EvaluationRating.Unacceptable,
                        failed: true,
                        reason: $"Detected word count score '{metric.Value}' was < 4.");
        }
        else
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Inconclusive,
                    failed: true,
                    reason: $"The detected word count score '{metric.Value}' was not valid.");
        }
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        /// Count the number of words in the supplied <see cref="modelResponse"/>.
        int wordCount = CountWords(modelResponse.Text);

        var value = 1;
        if (wordCount < 150)
        {
            value = 5;
        }
        else if (wordCount < 175)
        {
            value = 4;
        }
        else if (wordCount < 200)
        {
            value = 3;
        }
        else if (wordCount < 225)
        {
            value = 2;
        }

        var reason =
            $"This {WordCountMetricName} metric has value {value} because the evaluated model response contained {wordCount} words.";

        /// Create a <see cref="NumericMetric"/> with value set to the word count. Also include a reason that provides
        /// some commentary around the result. An <see cref="IEvaluator"/> can optionally include such commentary
        /// to explain the scores present within any <see cref="EvaluationMetric"/> that it returns.
        var metric = new StringMetric(WordCountMetricName, value: value.ToString(), reason);

        /// Attach a default <see cref="EvaluationMetricInterpretation"/> for the metric. An evaluator can provide a
        /// default interpretation for each metric that it produces. This default interpretation can be overridden by
        /// the caller if needed as demonstrated in
        /// <see cref="EvaluationExamples.Example06_ChangingInterpretationOfMetrics"/>.
        Interpret(metric);

        /// Return an <see cref="EvaluationResult"/> that contains the above metric.
        return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
    }
}
