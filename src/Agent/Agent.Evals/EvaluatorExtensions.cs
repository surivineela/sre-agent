using Evaluation.Evaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation;
using Agent.Evals.Common.Evaluators;
using Agent.Evals.Common;

namespace Agent.Evals;

public static class EvaluatorExtensions
{
    public static async Task<Dictionary<string, EvalsResult>> GenerateEvaluationAsync(
        this ChatResponse chatResponse,
        ChatConfiguration? chatConfiguration,
        List<ChatMessage> messages,
        string groundedContext,
        string exampleResponse)
    {
        IEvaluator sreAgentCoherenceEvaluator = new SreAgentCoherenceEvaluator();
        IEvaluator sreAgentFluencyEvaluator = new SreAgentFluencyEvaluator();
        IEvaluator equivalenceEvaluator = new SreAgentEquivalenceEvaluator(groundedTruth: exampleResponse, question: messages.Last().Text, chatResponse.Text);
        IEvaluator groundednessEvaluator = new SreAgentGroundednessEvaluator(groundedContext: groundedContext, question: messages.Last().Text, chatResponse.Text);
        IEvaluator relevanceTrusthAndCompletenessEvaluator = new RelevanceTruthAndCompletenessEvaluator();
        IEvaluator wordCoundEvaluator = new WordCountEvaluator();
        IEvaluator markdownEvaluator = new MarkdownEvaluator();
        IEvaluator compositeEvaluator = new CompositeEvaluator(new[] { wordCoundEvaluator, markdownEvaluator, sreAgentCoherenceEvaluator, sreAgentFluencyEvaluator, equivalenceEvaluator, groundednessEvaluator });
        EvaluationResult result = await compositeEvaluator.EvaluateAsync(messages, chatResponse, chatConfiguration);
        NumericMetric wordCount = result.Get<NumericMetric>(WordCountEvaluator.WordCountMetricName);
        StringMetric markdown = result.Get<StringMetric>(MarkdownEvaluator.MarkdownMetricName);
        StringMetric coherence = result.Get<StringMetric>(SreAgentCoherenceEvaluator.SreAgentCoherenceMetricName);
        StringMetric fluency = result.Get<StringMetric>(SreAgentFluencyEvaluator.SreAgentFluencyMetricName);
        StringMetric equivalence = result.Get<StringMetric>(SreAgentEquivalenceEvaluator.SreAgentEquivalenceMetricName);
        StringMetric groundedness = result.Get<StringMetric>(SreAgentGroundednessEvaluator.SreAgentGroundednessMetricName);

        var evaluatorToResultsMap = new Dictionary<string, EvalsResult>
        {
            [SreAgentCoherenceEvaluator.SreAgentCoherenceMetricName] = new EvalsResult
            {
                Value = int.Parse(coherence.Value),
                Reason = coherence.Reason,
            },
            [SreAgentFluencyEvaluator.SreAgentFluencyMetricName] = new EvalsResult
            {
                Value = int.Parse(fluency.Value),
                Reason = fluency.Reason,
            },
            [SreAgentEquivalenceEvaluator.SreAgentEquivalenceMetricName] = new EvalsResult
            {
                Value = int.Parse(equivalence.Value),
                Reason = equivalence.Reason,
            },
            [SreAgentGroundednessEvaluator.SreAgentGroundednessMetricName] = new EvalsResult
            {
                Value = int.Parse(groundedness.Value),
                Reason = groundedness.Reason,
            },
        };

        return evaluatorToResultsMap;
    }
}
