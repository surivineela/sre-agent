// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Web.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Validation;

public class IncidentFilterValidatorTests
{
    private readonly Mock<ILogger<IncidentFilterValidator>> _mockLogger;

    public IncidentFilterValidatorTests()
    {
        _mockLogger = new Mock<ILogger<IncidentFilterValidator>>();
    }

    private IncidentFilterValidator CreateValidator(IncidentManagementType incidentManagementType)
    {
        var settings = new IncidentManagementSettings
        {
            Type = incidentManagementType
        };
        return new IncidentFilterValidator(_mockLogger.Object, settings);
    }

    #region Id Validation Tests

    [Fact]
    public void ValidateIncidentFilter_WithEmptyId_ReturnsError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = string.Empty,
            HandlingAgent = "test-agent"
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Id cannot be empty"));
    }

    [Fact]
    public void ValidateIncidentFilter_WithValidId_NoIdError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "valid-filter-id",
            HandlingAgent = "test-agent"
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("Id cannot be empty"));
    }

    #endregion

    #region HandlingAgent Validation Tests

    [Fact]
    public void ValidateIncidentFilter_WithEmptyHandlingAgent_ReturnsError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = string.Empty
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("HandlingAgent must be set"));
    }

    [Fact]
    public void ValidateIncidentFilter_WithValidHandlingAgent_NoHandlingAgentError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent"
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("HandlingAgent must be set"));
    }

    #endregion

    #region Platform Mismatch Tests

    [Theory]
    [InlineData(IncidentManagementType.Icm)]
    [InlineData(IncidentManagementType.AzMonitor)]
    [InlineData(IncidentManagementType.PagerDuty)]
    [InlineData(IncidentManagementType.ServiceNow)]
    [InlineData(IncidentManagementType.None)]
    public void ValidateIncidentFilter_WithMatchingPlatform_NoPlatformError(IncidentManagementType type)
    {
        // Arrange
        var validator = CreateValidator(type);
        var document = CreateFilterForType(type, "test-filter", "test-agent");

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("does not match configured incident management type"));
    }

    [Fact]
    public void ValidateIncidentFilter_IcmFilterWithAzMonitorConfig_ReturnsPlatformError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.AzMonitor);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent"
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Incident platform 'Icm' does not match configured incident management type 'AzMonitor'"));
    }

    [Fact]
    public void ValidateIncidentFilter_AzMonitorFilterWithIcmConfig_ReturnsPlatformError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new AzMonitorIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent"
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Incident platform 'AzMonitor' does not match configured incident management type 'Icm'"));
    }

    [Fact]
    public void ValidateIncidentFilter_PagerDutyFilterWithServiceNowConfig_ReturnsPlatformError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.ServiceNow);
        var document = new PagerDutyIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent"
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Incident platform 'PagerDuty' does not match configured incident management type 'ServiceNow'"));
    }

    #endregion

    #region AgentMode Validation Tests

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("Review")]
    [InlineData("Autonomous")]
    [InlineData("readonly")]
    [InlineData("REVIEW")]
    [InlineData("autonomous")]
    public void ValidateIncidentFilter_WithValidAgentMode_NoAgentModeError(string agentMode)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            AgentMode = agentMode
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("AgentMode"));
    }

    [Fact]
    public void ValidateIncidentFilter_WithEmptyAgentMode_NoAgentModeError()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            AgentMode = string.Empty
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("AgentMode"));
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("ReadWrite")]
    [InlineData("Manual")]
    [InlineData("Auto")]
    public void ValidateIncidentFilter_WithInvalidAgentMode_ReturnsError(string agentMode)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            AgentMode = agentMode
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains($"AgentMode '{agentMode}' is not valid"));
    }

    #endregion

    #region Priority Validation Tests - ICM

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("25")]
    [InlineData("3")]
    [InlineData("4")]
    public void ValidateIncidentFilter_IcmWithValidPriority_NoPriorityError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("Priority"));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("Sev1")]
    [InlineData("High")]
    [InlineData("5")]
    public void ValidateIncidentFilter_IcmWithInvalidPriority_ReturnsError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains($"Priority '{priority}' is not valid for Icm"));
    }

    #endregion

    #region Priority Validation Tests - AzMonitor

    [Theory]
    [InlineData("Sev0")]
    [InlineData("Sev1")]
    [InlineData("Sev2")]
    [InlineData("Sev3")]
    [InlineData("Sev4")]
    [InlineData("sev0")]
    [InlineData("SEV1")]
    public void ValidateIncidentFilter_AzMonitorWithValidPriority_NoPriorityError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.AzMonitor);
        var document = new AzMonitorIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("Priority"));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("1")]
    [InlineData("High")]
    [InlineData("Sev5")]
    public void ValidateIncidentFilter_AzMonitorWithInvalidPriority_ReturnsError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.AzMonitor);
        var document = new AzMonitorIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains($"Priority '{priority}' is not valid for AzMonitor"));
    }

    #endregion

    #region Priority Validation Tests - PagerDuty

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    [InlineData("P3")]
    [InlineData("P4")]
    [InlineData("P5")]
    [InlineData("p1")]
    [InlineData("p2")]
    public void ValidateIncidentFilter_PagerDutyWithValidPriority_NoPriorityError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.PagerDuty);
        var document = new PagerDutyIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("Priority"));
    }

    [Theory]
    [InlineData("Sev1")]
    [InlineData("1")]
    [InlineData("High")]
    [InlineData("P6")]
    public void ValidateIncidentFilter_PagerDutyWithInvalidPriority_ReturnsError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.PagerDuty);
        var document = new PagerDutyIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains($"Priority '{priority}' is not valid for PagerDuty"));
    }

    #endregion

    #region Priority Validation Tests - ServiceNow

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    public void ValidateIncidentFilter_ServiceNowWithValidPriority_NoPriorityError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.ServiceNow);
        var document = new ServiceNowIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("Priority"));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("Sev1")]
    [InlineData("High")]
    [InlineData("6")]
    public void ValidateIncidentFilter_ServiceNowWithInvalidPriority_ReturnsError(string priority)
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.ServiceNow);
        var document = new ServiceNowIncidentFilterDocument
        {
            Id = "test-filter",
            HandlingAgent = "test-agent",
            Priorities = new List<string> { priority }
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains($"Priority '{priority}' is not valid for ServiceNow"));
    }

    #endregion

    #region Empty Priority Tests

    [Theory]
    [InlineData(IncidentManagementType.Icm)]
    [InlineData(IncidentManagementType.AzMonitor)]
    [InlineData(IncidentManagementType.PagerDuty)]
    [InlineData(IncidentManagementType.ServiceNow)]
    public void ValidateIncidentFilter_WithEmptyPriority_NoPriorityError(IncidentManagementType type)
    {
        // Arrange
        var validator = CreateValidator(type);
        var document = CreateFilterForType(type, "test-filter", "test-agent");

        // Set Priority to empty
        if (document is IncidentFilterDocumentPayload payload)
        {
            payload.Priorities = [];
        }

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.DoesNotContain(result.Errors, e => e.Contains("Priority"));
    }

    #endregion

    #region Multiple Errors Tests

    [Fact]
    public void ValidateIncidentFilter_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var validator = CreateValidator(IncidentManagementType.Icm);
        var document = new IcmIncidentFilterDocument
        {
            Id = string.Empty,
            HandlingAgent = string.Empty,
            AgentMode = "InvalidMode",
            Priorities = ["InvalidPriority"]
        };

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 4);
        Assert.Contains(result.Errors, e => e.Contains("Id cannot be empty"));
        Assert.Contains(result.Errors, e => e.Contains("HandlingAgent must be set"));
        Assert.Contains(result.Errors, e => e.Contains("AgentMode"));
        Assert.Contains(result.Errors, e => e.Contains("Priority"));
    }

    #endregion

    #region Valid Document Tests

    [Theory]
    [InlineData(IncidentManagementType.Icm)]
    [InlineData(IncidentManagementType.AzMonitor)]
    [InlineData(IncidentManagementType.PagerDuty)]
    [InlineData(IncidentManagementType.ServiceNow)]
    public void ValidateIncidentFilter_WithValidDocument_ReturnsSuccess(IncidentManagementType type)
    {
        // Arrange
        var validator = CreateValidator(type);
        var document = CreateValidFilterForType(type);

        // Act
        var result = validator.ValidateIncidentFilter(document);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region Helper Methods

    private static IIncidentFilterDocument CreateFilterForType(IncidentManagementType type, string filterId, string handlingAgent)
    {
        return type switch
        {
            IncidentManagementType.Icm => new IcmIncidentFilterDocument { Id = filterId, HandlingAgent = handlingAgent },
            IncidentManagementType.AzMonitor => new AzMonitorIncidentFilterDocument { Id = filterId, HandlingAgent = handlingAgent },
            IncidentManagementType.PagerDuty => new PagerDutyIncidentFilterDocument { Id = filterId, HandlingAgent = handlingAgent },
            IncidentManagementType.ServiceNow => new ServiceNowIncidentFilterDocument { Id = filterId, HandlingAgent = handlingAgent },
            IncidentManagementType.None => new NullableIncidentFilterDocument { Id = filterId, HandlingAgent = handlingAgent },
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };
    }

    private static IIncidentFilterDocument CreateValidFilterForType(IncidentManagementType type)
    {
        return type switch
        {
            IncidentManagementType.Icm => new IcmIncidentFilterDocument
            {
                Id = "valid-filter",
                HandlingAgent = "test-agent",
                AgentMode = "Review",
                Priorities = ["2"]
            },
            IncidentManagementType.AzMonitor => new AzMonitorIncidentFilterDocument
            {
                Id = "valid-filter",
                HandlingAgent = "test-agent",
                AgentMode = "Review",
                Priorities = ["Sev2"]
            },
            IncidentManagementType.PagerDuty => new PagerDutyIncidentFilterDocument
            {
                Id = "valid-filter",
                HandlingAgent = "test-agent",
                AgentMode = "Review",
                Priorities = ["P2"]
            },
            IncidentManagementType.ServiceNow => new ServiceNowIncidentFilterDocument
            {
                Id = "valid-filter",
                HandlingAgent = "test-agent",
                AgentMode = "Review",
                Priorities = ["2"]
            },
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };
    }

    #endregion
}
