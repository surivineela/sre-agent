using Evaluation.Evaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Agent.Evals.Evaluators;
using Agent.Evals.Common;
using EvaluationResult = Agent.Evals.Common.EvaluationResult;
using Newtonsoft.Json;

namespace Agent.Evals;

public static class EvaluatorExtensions
{
    public static EvaluationResult GetEvaluationResult(this StringMetric metric)
    {
        return new EvaluationResult
        {
            Value = int.Parse(metric.Value ?? "0"),
            Reason = metric.Reason,
        };
    }

    public static async Task<EvaluationResults?> EvaluateAsync(
        this ChatResponse chatResponse,
        TestContext testContext,
        ChatConfiguration? chatConfiguration,
        IEnumerable<ChatMessage> messages,
        string groundedContext,
        string exampleResponse,
        string? llmDeploymentName)
    {
        IEvaluator sreAgentCoherenceEvaluator = new SreAgentCoherenceEvaluator();
        IEvaluator sreAgentFluencyEvaluator = new SreAgentFluencyEvaluator();
        IEvaluator equivalenceEvaluator = new SreAgentEquivalenceEvaluator(groundedTruth: exampleResponse, question: messages.Last().Text, chatResponse.Text);
        IEvaluator groundednessEvaluator = new SreAgentGroundednessEvaluator(groundedContext: groundedContext, question: messages.Last().Text, chatResponse.Text);
        IEvaluator wordCoundEvaluator = new WordCountEvaluator();
        IEvaluator compositeEvaluator = new CompositeEvaluator(new[] { wordCoundEvaluator, sreAgentCoherenceEvaluator, sreAgentFluencyEvaluator, equivalenceEvaluator, groundednessEvaluator });
        var result = await compositeEvaluator.EvaluateAsync(messages, chatResponse, chatConfiguration);

        try
        {
            StringMetric wordCount = result.Get<StringMetric>(WordCountEvaluator.WordCountMetricName);
            StringMetric coherence = result.Get<StringMetric>(SreAgentCoherenceEvaluator.SreAgentCoherenceMetricName);
            StringMetric fluency = result.Get<StringMetric>(SreAgentFluencyEvaluator.SreAgentFluencyMetricName);
            StringMetric equivalence = result.Get<StringMetric>(SreAgentEquivalenceEvaluator.SreAgentEquivalenceMetricName);
            StringMetric groundedness = result.Get<StringMetric>(SreAgentGroundednessEvaluator.SreAgentGroundednessMetricName);

            var evaluationResults = new EvaluationResults
            {
                WordCount = wordCount.GetEvaluationResult(),
                Coherence = coherence.GetEvaluationResult(),
                Fluency = fluency.GetEvaluationResult(),
                Equivalence = equivalence.GetEvaluationResult(),
                Groundedness = groundedness.GetEvaluationResult(),
                LLMDeploymentName = llmDeploymentName,
                UserInput = messages.Where(x => x.Role == ChatRole.User).Select(x => x.Text).FirstOrDefault(),
                ModelResponse = chatResponse.Text,
            };

            testContext.WriteLine(JsonConvert.SerializeObject(evaluationResults));
            return evaluationResults;
        }
        catch (Exception ex)
        {
            // Do not fail the whole test if we failed to calculate eval scores, just omit the eval result.
            testContext.WriteLine($"Error calculating eval scores: {ex.Message}");
            testContext.WriteLine(ex.ToString());
            return null;
        }
    }

    public static ChatResponse CombineAgentResponses(this List<ChatMessage>? chatMessages)
    {
        var combinedText = string.Join($"{Environment.NewLine}{Environment.NewLine}",
            (chatMessages ?? Enumerable.Empty<ChatMessage>())
            .Where(x => x.Role == ChatRole.Assistant)
            .Where(x => !string.IsNullOrEmpty(x.Text))
            .Select(x => x.Text)
        );

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, combinedText));
    }
}
