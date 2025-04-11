using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Agent.Evals.Common.Evaluators;

public abstract class CustomRatingEvaluatorWithReasoning : IEvaluator
{
    protected abstract string GetSystemPrompt();

    protected abstract string GetMetricName();

    public IReadOnlyCollection<string> EvaluationMetricNames => [GetMetricName()];

    public async ValueTask<EvaluationResult> EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse, ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null, CancellationToken cancellationToken = default)
    {
        string evaluationPrompt = GetEvaluationPrompt(modelResponse.Text);
        ChatMessage[] evaluationMessages = [
                    new ChatMessage(ChatRole.System, GetSystemPrompt()),
            new ChatMessage(ChatRole.User, evaluationPrompt)];

        var metric = new StringMetric(GetMetricName());

        try
        {
            ChatResponse evaluationResponse =
            await chatConfiguration.ChatClient.GetResponseAsync(
                evaluationMessages,
                new ChatOptions
                {
                },
                cancellationToken);

            metric.Value = evaluationResponse.Text;

            metric.Reason = $"The detected relevance score was '{metric.Value}'.";
        }
        catch (ClientResultException e)
        {
            Thread.Sleep(Random.Shared.Next(500, 1000));
            ChatResponse evaluationResponse =
            await chatConfiguration.ChatClient.GetResponseAsync(
                evaluationMessages,
                new ChatOptions
                {
                },
                cancellationToken);

            metric.Value = evaluationResponse.Text;

            metric.Reason = $"The detected relevance score was '{metric.Value}'.";
        }

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
                    reason: "Failed to detect relevance score used in the response.");
            return;
        }

        var metricStringSplit = metricString.Split('\n');
        if (metricStringSplit.Length == 0)
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect relevance score used in the response.");
            return;
        }

        var metricStringLine1 = metricString?.Split('\n')[0];
        if (string.IsNullOrEmpty(metricStringLine1) && metricStringLine1.Length > 1)
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect relevance score used in the response.");
            return;
        }

        var metricIntString = metricStringLine1.Substring(0, 1);

        if (string.IsNullOrWhiteSpace(metricIntString))
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to detect relevance score used in the response.");
        }
        else if (int.TryParse(metricIntString, out int markdownScore))
        {
            metric.Value = markdownScore.ToString();
            metric.Interpretation =
                markdownScore is >= 4
                    ? new EvaluationMetricInterpretation(
                        EvaluationRating.Good,
                        reason: $"Detected relevance score '{metric.Value}' was >= 4.")
                    : new EvaluationMetricInterpretation(
                        EvaluationRating.Unacceptable,
                        failed: true,
                        reason: $"Detected relevance score '{metric.Value}' was < 4.");
        }
        else
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Inconclusive,
                    failed: true,
                    reason: $"The detected relevance score '{metric.Value}' was not valid.");
        }

        metric.Reason = string.Join('\n', metricStringSplit[1..]);
    }

    private static string GetEvaluationPrompt(string? modelResponse) =>
        $"""
        Consider the following response. How was the response? Please provide a singular number between 1 and 5 on the first line of your response, and then provide a short explanation of your reasoning on the next line. Please do not include any other text in your response.

        Answer: {modelResponse}
        Relevance rating:
        """;
}
