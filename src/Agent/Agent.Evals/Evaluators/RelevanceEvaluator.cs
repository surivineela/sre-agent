using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Agent.Evals.Evaluators;

public class RelevanceEvaluator : IEvaluator
{
    public const string RelevanceMetricName = "Relevance";

    private readonly string _relevancePromptInfo;

    public RelevanceEvaluator(string relevancePromptInfo)
    {
        _relevancePromptInfo = relevancePromptInfo;
    }

    public IReadOnlyCollection<string> EvaluationMetricNames => [RelevanceMetricName];

    public async ValueTask<Microsoft.Extensions.AI.Evaluation.EvaluationResult> EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse, ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null, CancellationToken cancellationToken = default)
    {
        string evaluationPrompt = GetEvaluationPrompt(modelResponse.Text);
        ChatMessage[] evaluationMessages = [
                    new ChatMessage(ChatRole.System, GetSystemPrompt()),
            new ChatMessage(ChatRole.User, evaluationPrompt)];

        var metric = new StringMetric(RelevanceMetricName);

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

        return new Microsoft.Extensions.AI.Evaluation.EvaluationResult(metric);
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
    }

    #region Prompts
    private string GetSystemPrompt()
    {
        return $@"""

        You are an AI assistant. You will be given a response and you will need to assess it on its relevance.

        Every response will need to satisfy the following criteria:
        {_relevancePromptInfo}

        You will answer on a scale of 1 to 5, 1 being, was not relevant at all, 5 being the prompt was extremely relevant.
        """;
    }

    private static string GetEvaluationPrompt(string? modelResponse) =>
        $"""
        Consider the following response to a user question. How relevant was the response?

        Answer: {modelResponse}
        Relevance rating:
        """;
    #endregion
}
