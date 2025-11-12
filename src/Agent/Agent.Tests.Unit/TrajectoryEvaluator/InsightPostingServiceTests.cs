// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Runtime.TrajectoryEvaluator;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.TrajectoryEvaluator;

public class InsightPostingServiceTests
{
    private Mock<IAgentOutboundCommunicationService> _mockOutboundService;
    private Mock<IThreadRepository> _mockThreadRepository;
    private Mock<IChatClientProvider> _mockChatClientProvider;
    private Mock<ILogger<InsightPostingService>> _mockLogger;
    private Mock<ISessionInsightRepository> _mockSessionInsightRepository;
    private AgentMemorySettings _agentMemorySettings;
    private InsightPostingService _service;

    public InsightPostingServiceTests()
    {
        _mockOutboundService = new Mock<IAgentOutboundCommunicationService>();
        _mockThreadRepository = new Mock<IThreadRepository>();
        _mockChatClientProvider = new Mock<IChatClientProvider>();
        _mockLogger = new Mock<ILogger<InsightPostingService>>();
        _mockSessionInsightRepository = new Mock<ISessionInsightRepository>();
        _agentMemorySettings = new AgentMemorySettings
        {
            Enabled = true,
            EnableInsightPosting = true
        };

        _service = new InsightPostingService(
            _mockOutboundService.Object,
            _mockThreadRepository.Object,
            _mockChatClientProvider.Object,
            _mockLogger.Object,
            _mockSessionInsightRepository.Object,
            _agentMemorySettings
        );
    }

    // Note: The enablement check has been moved to service registration level in Program.cs
    // InsightPostingService is only registered when SessionInsightsEnabled() returns true
    // Therefore, these tests assume the service is only used when enabled

    // Note: The enablement check has been moved to service registration level in Program.cs
    // InsightPostingService is only registered when SessionInsightsEnabled() returns true
    // Therefore, these tests assume the service is only used when enabled

    // Note: Full integration tests with LLM mocking are complex
    // This test verifies the configuration logic works correctly

    [Fact]
    public async Task PostTrajectoryInsightsAsync_NoPitfallsAndHighQuality_DoesNotPost()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var trajectory = CreateMockTrajectory(
            isInvestigation: true,
            hasPitfalls: false,
            investigationQuality: 5
        );

        // Act
        await _service.PostTrajectoryInsightsAsync(threadId, trajectory, string.Empty, CancellationToken.None);

        // Assert
        _mockOutboundService.Verify(
            x => x.UpdateThreadWithAgentMessageAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<Guid?>(),
                It.IsAny<StreamMessageType?>()
            ),
            Times.Never
        );
    }

    private ProcessedTrajectoryOutput_v3 CreateMockTrajectory(
        bool isInvestigation,
        bool hasPitfalls,
        int investigationQuality = 3,
        string? pitfalls = null,
        string? classificationReason = null)
    {
        return new ProcessedTrajectoryOutput_v3
        {
            IsInvestigationThread = isInvestigation,
            ReasoningScratchPad = "Test reasoning",
            ClassificationReason = classificationReason ?? (isInvestigation
                ? "User reported production issue"
                : "Routine resource query"),
            Title = "Test Thread",
            IncidentTitle = isInvestigation ? "Test Incident" : "N/A",
            IncidentId = isInvestigation ? "INC-123" : "N/A",
            IncidentTime = isInvestigation ? DateTime.UtcNow.ToString() : "N/A",
            SystemDesignKnowledge = "Test system",
            InitialSymptoms = isInvestigation ? "Service unavailable" : "N/A",
            StepsFollowed = isInvestigation ? "1. Check logs\n2. Analyze metrics" : "N/A",
            SymptomsObserved = isInvestigation ? "High error rate" : "N/A",
            Pitfalls = hasPitfalls
                ? (pitfalls ?? "Did: Something incorrect. Should: Do it correctly.")
                : "N/A",
            RootCause = isInvestigation ? "Configuration error" : "N/A",
            ResourcesInvolved = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Test/test",
            ResourceTypesInvolved = "Microsoft.Test/test",
            InvestigationCompleteness = investigationQuality,
            InvestigationOutcome = isInvestigation ? "Issue resolved" : "N/A"
        };
    }
}
