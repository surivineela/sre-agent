// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Core;

namespace Agent.Core.Models;

public sealed class ProcessedTrajectoryOutput_v3
{
    public required string ReasoningScratchPad { get; set; }

    public required bool IsInvestigationThread { get; set; }

    public required string ClassificationReason { get; set; }

    public required string Title { get; set; }

    public required string IncidentTitle { get; set; }

    public required string IncidentId { get; set; }

    public required string IncidentTime { get; set; }

    public required string SystemDesignKnowledge { get; set; }

    public required string InitialSymptoms { get; set; }

    public required string StepsFollowed { get; set; }

    public required string SymptomsObserved { get; set; }

    public required string Pitfalls { get; set; }

    public required string RootCause { get; set; }

    public required string ResourcesInvolved { get; set; }

    public required string ResourceTypesInvolved { get; set; }

    public required int InvestigationCompleteness { get; set; }

    public required string InvestigationOutcome { get; set; }

    public static ProcessedTrajectoryOutput_v3 FromTrajectoryOutput(TrajectoryOutput_v3 trajectoryOutput)
    {
        var resourceTypes = trajectoryOutput.ResourcesInvolved
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(GetResourceType)
            .Where(p => p is not null)
            .ToHashSet(); // de-duplicate types

        return new ProcessedTrajectoryOutput_v3
        {
            ReasoningScratchPad = trajectoryOutput.ReasoningScratchPad,
            IsInvestigationThread = trajectoryOutput.IsInvestigationThread,
            ClassificationReason = trajectoryOutput.ClassificationReason,
            Title = trajectoryOutput.Title,
            IncidentId = trajectoryOutput.IncidentId,
            IncidentTitle = trajectoryOutput.IncidentTitle,
            IncidentTime = trajectoryOutput.IncidentTime,
            SystemDesignKnowledge = trajectoryOutput.SystemDesignKnowledge,
            InitialSymptoms = trajectoryOutput.InitialSymptoms,
            StepsFollowed = trajectoryOutput.StepsFollowed,
            SymptomsObserved = trajectoryOutput.SymptomsObserved,
            Pitfalls = trajectoryOutput.Pitfalls,
            RootCause = trajectoryOutput.RootCause,
            ResourcesInvolved = trajectoryOutput.ResourcesInvolved,
            ResourceTypesInvolved = string.Join(';', resourceTypes),
            InvestigationCompleteness = trajectoryOutput.InvestigationCompleteness,
            InvestigationOutcome = trajectoryOutput.InvestigationOutcome,
        };
    }

    private static string? GetResourceType(string resourceId)
    {
        var trimmed = resourceId.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (ResourceIdentifier.TryParse(trimmed, out var resourceIdentifier))
        {
            return resourceIdentifier?.ResourceType.ToString();
        }

        return null;
    }
}
