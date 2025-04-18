using Evaluation.Evaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Quality;
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
            Value = int.Parse(metric.Value),
            Reason = metric.Reason,
        };
    }

    public static async Task EvaluateAsync(
        this ChatResponse chatResponse,
        TestContext testContext,
        ChatConfiguration? chatConfiguration,
        List<ChatMessage> messages,
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
        };

        testContext.WriteLine(JsonConvert.SerializeObject(evaluationResults));
    }
}
