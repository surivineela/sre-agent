namespace Agent.Evals.Cmd.Helpers;

public class TestResult
{
    public string BuildId { get; set; }

    public string BuildNumber { get; set; }

    public string TestMethod { get; set; }

    public string ClassName { get; set; }

    public string Owner { get; set; }

    public int TotalRuns { get; set; }

    public int FailedRuns { get; set; }

    public int PassedRuns { get; set; }

    public string StartTime { get; set; }

    public string EndTime { get; set; }

    public string Duration { get; set; }

    public List<string> ErrorInfo { get; set; } = new List<string>();
}
