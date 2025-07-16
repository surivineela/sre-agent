// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Azure.Search.Documents.Models;

namespace Agent.Data.AgentMemory;

/// <summary>
/// Represents an agent trajectory in the search index
/// </summary>
public class TrajectoryContent : BaseIndexableContent
{
    public override string Type => "trajectory";

    public TrajectoryContent(
        string conversationId,
        TrajectoryOutput trajectoryData,
        Dictionary<string, object>? additionalMetadata = null)
        : base(
            id: conversationId,
            content: JsonSerializer.Serialize(trajectoryData, new JsonSerializerOptions { WriteIndented = true }),
            title: trajectoryData.Title,
            metadata: CreateMetadata(trajectoryData, additionalMetadata))
    {
    }

    private static Dictionary<string, object> CreateMetadata(
        TrajectoryOutput trajectory,
        Dictionary<string, object>? additionalMetadata)
    {
        var metadata = additionalMetadata ?? new Dictionary<string, object>();

        // Add trajectory-specific searchable fields
        if (!string.IsNullOrEmpty(trajectory.ResourceTypesInvolved))
            metadata["resource_types"] = trajectory.ResourceTypesInvolved;

        if (!string.IsNullOrEmpty(trajectory.ResourcesInvolved))
            metadata["resource_ids"] = trajectory.ResourcesInvolved;

        if (!string.IsNullOrEmpty(trajectory.RootCause))
            metadata["root_cause"] = trajectory.RootCause;

        metadata["initial_symptoms"] = trajectory.InitialSymptoms;
        metadata["symptoms_observed"] = trajectory.SymptomsObserved;
        metadata["indexed_at"] = DateTime.UtcNow;

        return metadata;
    }

    protected override void AddContentSpecificFields(SearchDocument doc)
    {
        if (Metadata.TryGetValue("resource_types", out var resourceTypes))
            doc["resource_types"] = resourceTypes;

        if (Metadata.TryGetValue("resource_ids", out var resourceIds))
            doc["resource_ids"] = resourceIds;

        if (Metadata.TryGetValue("root_cause", out var rootCause))
            doc["root_cause"] = rootCause;

        if (Metadata.TryGetValue("initial_symptoms", out var initialSymptoms))
            doc["initial_symptoms"] = initialSymptoms;

        if (Metadata.TryGetValue("symptoms_observed", out var symptomsObserved))
            doc["symptoms_observed"] = symptomsObserved;

        if (Metadata.TryGetValue("pitfalls", out var pitfalls))
            doc["pitfalls"] = pitfalls;
    }
}
