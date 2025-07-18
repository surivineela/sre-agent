using System.Text.Json.Serialization;

namespace Agent.Core.Models;
public class AzureSupportCenterApolloResponsePayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("properties")]
    public ApolloProperties? Properties { get; set; }
}

public class ApolloProperties
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("replacementMaps" )]
    public ReplacementMaps? ReplacementMaps { get; set; }

    [JsonPropertyName("sections")]
    public List<Section>? Sections { get; set; }

    [JsonPropertyName("solutionId")]
    public string? SolutionId { get; set; }

    [JsonPropertyName("provisioningState")]
    public string? ProvisioningState { get; set; }
}

public class ReplacementMaps
{
    [JsonPropertyName("diagnostics")]
    public List<ApolloDiagnostic> Diagnostics { get; set; } = new List<ApolloDiagnostic>();

    [JsonPropertyName("troubleshooters")]
    public List<Troubleshooter> Troubleshooters { get; set; } = new List<Troubleshooter>();
}

public class Troubleshooter
{
    [JsonPropertyName("solutionId")]
    public string? SolutionId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("replacementKey")]
    public string? ReplacementKey { get; set; }
}

public class ApolloDiagnostic
{
    [JsonPropertyName("insights")]
    public List<ApolloInsight> Insights { get; set; } = new List<ApolloInsight>();

    [JsonPropertyName("solutionId")]
    public string? SolutionId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusDetails")]
    public string? StatusDetails { get; set; }

    [JsonPropertyName("steps")]
    public List<ApolloStep> Steps { get; set; } = new List<ApolloStep>();

    [JsonPropertyName("replacementKey")]
    public string? ReplacementKey { get; set; }
}

public class ApolloInsight
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("results")]
    public string? Results { get; set; }

    [JsonPropertyName("importanceLevel")]
    public string? ImportanceLevel { get; set; }
}

public class ApolloStep
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class Section
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("replacementMaps")]
    public SectionReplacementMaps ReplacementMaps { get; set; } = new SectionReplacementMaps();
}

public class SectionReplacementMaps
{
    [JsonPropertyName("diagnostics")]
    public List<ApolloDiagnostic> Diagnostics { get; set; } = new List<ApolloDiagnostic>();

    [JsonPropertyName("webResults")]
    public List<WebResult> WebResults { get; set; } = new List<WebResult>();
}

public class WebResult
{
    [JsonPropertyName("searchResults")]
    public List<SearchResult> SearchResults { get; set; } = new List<SearchResult>();

    [JsonPropertyName("replacementKey")]
    public string? ReplacementKey { get; set; }
}

public class SearchResult
{

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("resultType")]
    public string? ResultType { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }
}
