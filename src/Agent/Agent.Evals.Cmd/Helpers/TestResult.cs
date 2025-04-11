namespace Agent.Evals.Cmd.Helpers;

public class TestResult
{
    public string BuildId { get; set; }

    public string BuildNumber { get; set; }

    public string TestId { get; set; }

    public string TestMethod { get; set; }

    public string ClassName { get; set; }

    public int? CoherenceRating { get; set; }

    public string CoherenceReasoning { get; set; }

    public int? FluencyRating { get; set; }

    public string FluencyReasoning { get; set; }

    public int? EquivalenceRating { get; set; }

    public string EquivalenceReasoning { get; set; }

    public int? GroundednessRating { get; set; }

    public string GroundednessReasoning { get; set; }

    public string StartTime { get; set; }

    public string EndTime { get; set; }

    public string Duration { get; set; }

    public List<string> ErrorInfo { get; set; } = new List<string>();
}
