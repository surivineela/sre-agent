namespace Agent.Evals.Common;

public class EvaluationResults
{
    public EvaluationResult? WordCount { get; set; }

    public EvaluationResult? Coherence { get; set; }

    public EvaluationResult? Fluency { get; set; }

    public EvaluationResult? Equivalence { get; set; }

    public EvaluationResult? Groundedness { get; set; }
}
