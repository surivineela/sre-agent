using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Agent.Evals.Common.Evaluators;

public class MarkdownEvaluator : IEvaluator
{
    public const string MarkdownMetricName = "Markdown";

    public IReadOnlyCollection<string> EvaluationMetricNames => [MarkdownMetricName];

    public async ValueTask<EvaluationResult> EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse, ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null, CancellationToken cancellationToken = default)
    {
        string evaluationPrompt = GetEvaluationPrompt(modelResponse.Text);
        ChatMessage[] evaluationMessages = [
                    new ChatMessage(ChatRole.System, EvaluationSystemPrompt),
            new ChatMessage(ChatRole.User, evaluationPrompt)];

        var metric = new StringMetric(MarkdownMetricName);

        ChatResponse evaluationResponse =
            await chatConfiguration.ChatClient.GetResponseAsync(
                evaluationMessages,
                new ChatOptions
                {
                    Temperature = 0.0f,
                    TopP = 1.0f,
                    PresencePenalty = 0.0f,
                    FrequencyPenalty = 0.0f,
                    ResponseFormat = ChatResponseFormat.Text
                },
                cancellationToken);

        metric.Value = evaluationResponse.Text;

        metric.Reason = $"The detected markdown score was '{metric.Value}'.";

        Interpret(metric);

        return new EvaluationResult(metric);
    }

    private static void Interpret(StringMetric metric)
    {
        var metricString = metric.Value;
        if (string.IsNullOrWhiteSpace(metric.Value))
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect markdown score used in the response.");
            return;
        }

        var metricStringSplit = metricString.Split('\n');
        if (metricStringSplit.Length == 0)
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect markdown score used in the response.");
            return;
        }

        var metricStringLine1 = metricString?.Split('\n')[0];
        if (string.IsNullOrEmpty(metricStringLine1)
            || metricStringLine1.Length < 11)
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect markdown score used in the response.");
            return;
        }

        var metricIntString = metricStringLine1.Substring(10, 1);

        if (string.IsNullOrWhiteSpace(metricIntString))
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect markdown score used in the response.");
        }
        else if (int.TryParse(metricIntString, out int markdownScore))
        {
            metric.Interpretation =
                markdownScore is >= 4
                    ? new EvaluationMetricInterpretation(
                        EvaluationRating.Good,
                        reason: $"Detected markdown score '{metric.Value}' was >= 4.")
                    : new EvaluationMetricInterpretation(
                        EvaluationRating.Unacceptable,
                        failed: true,
                        reason: $"Detected markdown score '{metric.Value}' was < 4.");
        }
        else
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Inconclusive,
                    failed: true,
                    reason: $"The detected markdown score '{metric.Value}' was not valid.");
        }
    }

    #region Prompts
    private const string EvaluationSystemPrompt =
        """

        You are an AI assistant. You will be given a response and you will need to assess it on how well it follows markdown formatting.

        Mardown formatting involves using various # for headers, bullets via *, etc.

        You will answer on a scale of 1 to 5, 1 being, did not follow the Markdown format correctly, 5 being followed the Markdown format correctly.

        Your answer will start with **Rating: ** and then the number, e.g. **Rating: 5**. Include the explanation on a new line.
        """;

    private static string GetEvaluationPrompt(string? modelResponse) =>
        $"""
        Consider the following response to a user question. How well did the response follow the markdown format?

        Answer: {modelResponse}
        Markdown rating:
        """;
    #endregion
}
