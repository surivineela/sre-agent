// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Runtime.Services.IncidentTriggerDetection;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Services;

public class IncidentEventDetectorTests
{
    private readonly Mock<ILogger<IncidentEventDetector>> _mockLogger;
    private readonly IncidentEventDetector _detector;

    public IncidentEventDetectorTests()
    {
        _mockLogger = new Mock<ILogger<IncidentEventDetector>>();
        _detector = new IncidentEventDetector(_mockLogger.Object);
    }

    #region HitCountIncreased Tests

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenNewIncident_DoesNotTrigger()
    {
        // Arrange - New incident with HitCount = 2
        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow,
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 2);

        // Act - No previous state (new incident)
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState: null,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenHitCountIsOne_DoesNotTrigger()
    {
        // Arrange - Previous HitCount = 1, Current HitCount = 1
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 1);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 1);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenHitCountDecreases_DoesNotTrigger()
    {
        // Arrange - HitCount decreased (unusual but possible edge case)
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 5);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 3);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenHitCountUnchanged_DoesNotTrigger()
    {
        // Arrange - HitCount same
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 3);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 3);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenHitCountIncreasesFromOneToTwo_Triggers()
    {
        // Arrange - HitCount increases from 1 to 2 (first correlation)
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 1);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 2);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.Contains(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.NotNull(result.HitCountChange);
        Assert.Equal(1, result.HitCountChange.PreviousHitCount);
        Assert.Equal(2, result.HitCountChange.CurrentHitCount);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenHitCountIncreasesFromTwoToFive_Triggers()
    {
        // Arrange - HitCount increases from 2 to 5 (multiple correlations)
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 2);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 5);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.Contains(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.NotNull(result.HitCountChange);
        Assert.Equal(2, result.HitCountChange.PreviousHitCount);
        Assert.Equal(5, result.HitCountChange.CurrentHitCount);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenCurrentHitCountNull_DoesNotTrigger()
    {
        // Arrange - Current HitCount is null
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 2);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: null);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_WhenPreviousHitCountNull_DoesNotTrigger()
    {
        // Arrange - Previous HitCount is null
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: null);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 3);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_CombinedWithOtherEvents_BothDetected()
    {
        // Arrange - HitCount increases AND state changes to Mitigated
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 2);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Mitigated",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 5);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert - Both events should be detected
        Assert.Contains(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Contains(IcmIncidentTriggerEvent.IncidentMitigated, result.DetectedEvents);
        Assert.NotNull(result.HitCountChange);
        Assert.Equal(2, result.HitCountChange.PreviousHitCount);
        Assert.Equal(5, result.HitCountChange.CurrentHitCount);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void DetectOccurredEvents_NewIncident_OnlyTriggersIncidentCreatedOrTransferred()
    {
        // Arrange - New incident with HitCount already > 1
        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow,
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 5);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState: null,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert - Should only detect new incident, not HitCountIncreased
        Assert.Contains(IcmIncidentTriggerEvent.IncidentCreatedOrTransferred, result.DetectedEvents);
        Assert.DoesNotContain(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.Null(result.HitCountChange);
    }

    [Fact]
    public void DetectOccurredEvents_HitCountIncreased_LargeIncrement_Triggers()
    {
        // Arrange - HitCount increases by a large amount
        var previousState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow.AddMinutes(-1),
            HitCount: 10);

        var currentState = new IncidentStateSnapshot(
            IncidentId: "12345",
            State: "Active",
            CreatedDate: DateTime.UtcNow.AddMinutes(-5),
            LastModifiedDate: DateTime.UtcNow,
            HitCount: 100);

        // Act
        var result = _detector.DetectOccurredEvents(
            currentState,
            previousState,
            discussionEntries: new List<DescriptionEntry>(),
            processedEntryIds: null,
            onCallAliases: null);

        // Assert
        Assert.Contains(IcmIncidentTriggerEvent.HitCountIncreased, result.DetectedEvents);
        Assert.NotNull(result.HitCountChange);
        Assert.Equal(10, result.HitCountChange.PreviousHitCount);
        Assert.Equal(100, result.HitCountChange.CurrentHitCount);
    }

    #endregion
}
