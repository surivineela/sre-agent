// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Agent.Data.AgentMemory;

public class AgentMemory
{
    [SearchableField(IsKey = true, IsFilterable = true, AnalyzerName = LexicalAnalyzerName.Values.Keyword)]
    public string Id { get; set; } = string.Empty;

    // The type of content this represents (e.g., "document", "trajectory")
    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public string Type { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsFacetable = true, IsSortable = true, AnalyzerName = LexicalAnalyzerName.Values.EnLucene)]
    public string Title { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Values.Keyword)]
    public string ChunkId { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string ParentId { get; set; } = string.Empty;

    // document or trajectory content
    [SearchableField]
    public string Chunk { get; set; } = string.Empty;

    [VectorSearchField(VectorSearchDimensions = Constants.VectorDimension, VectorSearchProfileName = Constants.VectorSearchHnswProfile, IsHidden = true)]
    public List<float> Vector { get; set; } = [];

    // Fields for trajectory-specific metadata
    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public List<string> ResourceTypes { get; set; } = [];

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public List<string> ResourceIds { get; set; } = [];

    [SearchableField]
    public string RootCause { get; set; } = string.Empty;

    [SearchableField]
    public string Pitfalls { get; set; } = string.Empty;

    [SearchableField]
    public string IncidentId { get; set; } = string.Empty;

    [SearchableField]
    public string IncidentTitle { get; set; } = string.Empty;

    [SimpleField(IsSortable = true)]
    public DateTimeOffset? IncidentTime { get; set; }

    [SearchableField]
    public string InitialSymptoms { get; set; } = string.Empty;

    [SearchableField]
    public string StepsFollowed { get; set; } = string.Empty;

    [SearchableField]
    public string SymptomsObserved { get; set; } = string.Empty;

    // Common metadata field for all indexable content
    [SimpleField(IsSortable = true)]
    public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

    public static AgentMemory FromTrajectory(
        string id,
        TrajectoryOutput trajectoryData,
        List<float> embedding)
    {
        return new AgentMemory
        {
            Id = id,
            Type = "trajectory",
            Title = trajectoryData.Title,
            ChunkId = string.Empty,
            ParentId = id,
            Chunk = JsonSerializer.Serialize(trajectoryData),
            ResourceTypes = trajectoryData.ResourceTypesInvolved?.ToLowerInvariant().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
            ResourceIds = trajectoryData.ResourcesInvolved?.ToLowerInvariant().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
            RootCause = trajectoryData.RootCause ?? string.Empty,
            InitialSymptoms = trajectoryData.InitialSymptoms ?? string.Empty,
            StepsFollowed = trajectoryData.StepsFollowed ?? string.Empty,
            SymptomsObserved = trajectoryData.SymptomsObserved ?? string.Empty,
            IndexedAt = DateTimeOffset.UtcNow,
            Pitfalls = trajectoryData.Pitfalls ?? string.Empty,
            Vector = embedding
        };
    }

    public static AgentMemory FromTrajectory(
        string id,
        ProcessedTrajectoryOutput_v3 trajectoryData,
        List<float> embedding)
    {
        return new AgentMemory
        {
            Id = id,
            Type = "trajectory",
            Title = trajectoryData.Title,
            ChunkId = string.Empty,
            ParentId = id,
            Chunk = JsonSerializer.Serialize(trajectoryData),
            ResourceTypes = trajectoryData.ResourceTypesInvolved?.ToLowerInvariant().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
            ResourceIds = trajectoryData.ResourcesInvolved?.ToLowerInvariant().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
            RootCause = trajectoryData.RootCause ?? string.Empty,
            InitialSymptoms = trajectoryData.InitialSymptoms ?? string.Empty,
            IncidentId = trajectoryData.IncidentId ?? string.Empty,
            IncidentTitle = trajectoryData.IncidentTitle ?? string.Empty,
            IncidentTime = ParseIncidentTime(trajectoryData.IncidentTime),
            StepsFollowed = trajectoryData.StepsFollowed ?? string.Empty,
            SymptomsObserved = trajectoryData.SymptomsObserved ?? string.Empty,
            IndexedAt = DateTimeOffset.UtcNow,
            Pitfalls = trajectoryData.Pitfalls ?? string.Empty,
            Vector = embedding
        };
    }

    public static AgentMemory FromUserMemory(
        string id,
        string memoryContent,
        List<float> embedding)
    {
        // Generate simple title based on content length or first sentence
        var title = memoryContent.Length <= 50 ? memoryContent : memoryContent.Substring(0, 47) + "...";
        var firstSentence = memoryContent.Split('.', '!', '?')[0].Trim();
        if (firstSentence.Length <= 50 && firstSentence.Length > 0)
            title = firstSentence;

        return new AgentMemory
        {
            Id = id,
            Type = "usermemory",
            Title = title,
            ChunkId = string.Empty,
            ParentId = id,
            Chunk = memoryContent, // Store the user's memory content
            ResourceTypes = [],
            ResourceIds = [],
            RootCause = string.Empty,
            InitialSymptoms = string.Empty,
            StepsFollowed = string.Empty,
            SymptomsObserved = string.Empty,
            Pitfalls = string.Empty,
            Vector = embedding,
            IndexedAt = DateTimeOffset.UtcNow
        };
    }

    private static DateTimeOffset? ParseIncidentTime(string? incidentTimeString)
    {
        return !string.IsNullOrEmpty(incidentTimeString) && !incidentTimeString.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.TryParse(incidentTimeString, out var parsedTime) ? parsedTime : null;
    }
}

