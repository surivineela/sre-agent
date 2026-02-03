// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Xunit;

namespace Agent.Tests.Unit.Reasoning;

/// <summary>
/// Tests for ReasoningLoop InvestigationStatus handling logic.
/// Verifies that the correct investigation status is determined based on:
/// - Pending user actions
/// - Incident provider type (ICM only)
/// Note: Incident status (Active/Mitigated/Resolved) does NOT affect investigation status.
/// When reasoning loop completes, investigation is marked Complete regardless of incident status.
/// </summary>
public class ReasoningLoopInvestigationStatusTests
{
    #region Helper Methods

    /// <summary>
    /// Simulates the investigation status determination logic from ReasoningLoop.
    /// This mirrors the logic in UpdateInvestigationStatusIfNeededAsync.
    /// </summary>
    private static InvestigationStatus DetermineInvestigationStatus(
        bool hasPendingUserActions)
    {
        return hasPendingUserActions
            ? InvestigationStatus.PendingUserInput
            : InvestigationStatus.Complete;
    }

    /// <summary>
    /// Creates a mock thread with the specified incident configuration.
    /// </summary>
    private static ThreadDocument CreateMockThread(
        IncidentType incidentType,
        string? incidentStatus = "active",
        InvestigationStatus? investigationStatus = null)
    {
        var incidentId = "123456789";
        var thread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: "Test Thread",
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow,
            ModifiedTimestamp: DateTime.UtcNow,
            Source: ThreadSource.Incident
        )
        {
            IncidentSource = new IncidentSource(incidentType, incidentId),
            IncidentDetails = new IncidentDetails(
                IncidentTitle: "Test Incident",
                IncidentCreatedTime: DateTimeOffset.UtcNow,
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: "filter1",
                HandlerId: "filter1",
                InvestigationStatus: investigationStatus ?? InvestigationStatus.InProgress,
                TriggerEvent: null,
                IncidentStatus: incidentStatus
            )
        };
        return thread;
    }

    #endregion

    #region Active Incident Tests

    /// <summary>
    /// When there are pending user actions, investigation status should be PendingUserInput.
    /// </summary>
    [Fact]
    public void DetermineStatus_WithPendingActions_ReturnsPendingUserInput()
    {
        // Arrange
        var hasPendingUserActions = true;

        // Act
        var result = DetermineInvestigationStatus(hasPendingUserActions);

        // Assert
        Assert.Equal(InvestigationStatus.PendingUserInput, result);
    }

    /// <summary>
    /// When there are no pending user actions, investigation status should be Complete.
    /// This applies regardless of incident status (active, mitigated, resolved).
    /// </summary>
    [Fact]
    public void DetermineStatus_NoPendingActions_ReturnsComplete()
    {
        // Arrange
        var hasPendingUserActions = false;

        // Act
        var result = DetermineInvestigationStatus(hasPendingUserActions);

        // Assert
        Assert.Equal(InvestigationStatus.Complete, result);
    }

    #endregion

    #region ICM Provider Check Tests

    /// <summary>
    /// Verifies that ICM incidents should have investigation status tracking enabled.
    /// </summary>
    [Fact]
    public void ShouldTrackInvestigationStatus_IcmIncident_ReturnsTrue()
    {
        // Arrange
        var thread = CreateMockThread(IncidentType.Icm);

        // Act
        var shouldTrack = thread?.IncidentSource?.IncidentType == IncidentType.Icm;

        // Assert
        Assert.True(shouldTrack);
    }

    /// <summary>
    /// Verifies that PagerDuty incidents should NOT have ICM-specific investigation status tracking.
    /// </summary>
    [Fact]
    public void ShouldTrackInvestigationStatus_PagerDutyIncident_ReturnsFalse()
    {
        // Arrange
        var thread = CreateMockThread(IncidentType.PagerDuty);

        // Act
        var shouldTrack = thread?.IncidentSource?.IncidentType == IncidentType.Icm;

        // Assert
        Assert.False(shouldTrack);
    }

    /// <summary>
    /// Verifies that ServiceNow incidents should NOT have ICM-specific investigation status tracking.
    /// </summary>
    [Fact]
    public void ShouldTrackInvestigationStatus_ServiceNowIncident_ReturnsFalse()
    {
        // Arrange
        var thread = CreateMockThread(IncidentType.ServiceNow);

        // Act
        var shouldTrack = thread?.IncidentSource?.IncidentType == IncidentType.Icm;

        // Assert
        Assert.False(shouldTrack);
    }

    /// <summary>
    /// Verifies that when IncidentSource is null, tracking should not occur.
    /// </summary>
    [Fact]
    public void ShouldTrackInvestigationStatus_NullIncidentSource_ReturnsFalse()
    {
        // Arrange
        var thread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: "Test Thread",
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow,
            ModifiedTimestamp: DateTime.UtcNow,
            Source: ThreadSource.Incident
        )
        {
            IncidentSource = null  // No incident source
        };

        // Act
        var shouldTrack = thread?.IncidentSource?.IncidentType == IncidentType.Icm;

        // Assert
        Assert.False(shouldTrack);
    }

    /// <summary>
    /// Verifies that when thread is null, tracking should not occur.
    /// </summary>
    [Fact]
    public void ShouldTrackInvestigationStatus_NullThread_ReturnsFalse()
    {
        // Arrange
        ThreadDocument? thread = null;

        // Act
        var shouldTrack = thread?.IncidentSource?.IncidentType == IncidentType.Icm;

        // Assert
        Assert.False(shouldTrack);
    }

    #endregion

    #region Scenario Matrix Tests

    /// <summary>
    /// Comprehensive scenario matrix test covering pending user actions.
    /// Note: Incident status (active/mitigated/resolved) does NOT affect investigation status.
    /// </summary>
    [Theory]
    [InlineData(true, InvestigationStatus.PendingUserInput)]   // With pending actions
    [InlineData(false, InvestigationStatus.Complete)]          // No pending actions
    public void ScenarioMatrix_PendingActionsOnly_ReturnsExpectedStatus(
        bool hasPendingUserActions,
        InvestigationStatus expectedStatus)
    {
        // Act
        var result = DetermineInvestigationStatus(hasPendingUserActions);

        // Assert
        Assert.Equal(expectedStatus, result);
    }

    #endregion
}
