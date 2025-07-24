namespace Agent.Evals.Cmd.Helpers;

public class TestResult
{
    public DateTime PreciseTimeStamp => DateTime.UtcNow;

    public string BuildId { get; set; } = string.Empty;

    public string BuildNumber { get; set; } = string.Empty;

    public string TestId { get; set; } = string.Empty;

    public string TestMethod { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public int? WordCountRating { get; set; }

    public string? WordCountReasoning { get; set; }

    public int? CoherenceRating { get; set; }

    public string? CoherenceReasoning { get; set; }

    public int? FluencyRating { get; set; }

    public string? FluencyReasoning { get; set; }

    public int? EquivalenceRating { get; set; }

    public string? EquivalenceReasoning { get; set; }

    public int? GroundednessRating { get; set; }

    public string? GroundednessReasoning { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public string Duration { get; set; } = string.Empty;

    public List<string> ErrorInfo { get; set; } = new List<string>();

    public bool? HasPassed { get; set; }

    public string? LLMDeploymentName { get; set; }

    public string? AdditionalInfo { get; set; }
}
