// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Validation;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Runtime.Services;
using Agent.Web.Services;
using Agent.Web.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Services;

/// <summary>
/// Unit tests for IncidentFilterApiService.
/// Tests focus on business logic validation (type checking) and dry-run scenarios.
/// Note: Tests for database query operations (Get, List, Delete) require integration tests
/// because CosmosDB LINQ extension method ToFeedIterator() cannot be easily mocked.
/// </summary>
public class IncidentFilterApiServiceTests
{
    private readonly Mock<ILogger<IncidentFilterApiService>> _mockLogger;
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<Container> _mockContainer;
    private readonly Mock<IIncidentHandlerManagementService> _mockHandlerManagementService;
    private readonly Mock<IIncidentFilterValidator> _mockIncidentFilterValidator;
    private readonly CosmosDBSettings _cosmosDbSettings;

    public IncidentFilterApiServiceTests()
    {
        _mockLogger = new Mock<ILogger<IncidentFilterApiService>>();
        _mockCosmosClient = new Mock<CosmosClient>();
        _mockContainer = new Mock<Container>();
        _mockHandlerManagementService = new Mock<IIncidentHandlerManagementService>();
        _mockIncidentFilterValidator = new Mock<IIncidentFilterValidator>();

        _cosmosDbSettings = new CosmosDBSettings
        {
            Docs = new DocsSettings
            {
                Database = "TestDb"
            }
        };

        _mockCosmosClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_mockContainer.Object);

        // Default: validator returns success
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(new ApiValidationResult());
    }

    private IncidentFilterApiService CreateService(IncidentManagementType incidentManagementType)
    {
        var incidentManagementSettings = new IncidentManagementSettings
        {
            Type = incidentManagementType
        };

        return new IncidentFilterApiService(
            _mockLogger.Object,
            _mockCosmosClient.Object,
            _cosmosDbSettings,
            incidentManagementSettings,
            _mockHandlerManagementService.Object,
            _mockIncidentFilterValidator.Object
        );
    }

    #region CreateOrUpdateIncidentFilterAsync - Upsert Tests

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WithValidIcmFilter_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter-1";
        var filterDocument = CreateTestIcmFilter(filterId);

        SetupMockContainerForUpsert(filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WithAzMonitorFilter_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.AzMonitor);
        var filterId = "test-filter-azmonitor";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        SetupMockContainerForUpsert(filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WithPagerDutyFilter_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.PagerDuty);
        var filterId = "test-filter-pagerduty";
        var filterDocument = CreateTestPagerDutyFilter(filterId);

        SetupMockContainerForUpsert(filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WithServiceNowFilter_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.ServiceNow);
        var filterId = "test-filter-servicenow";
        var filterDocument = CreateTestServiceNowFilter(filterId);

        SetupMockContainerForUpsert(filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WithNullableFilter_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.None);
        var filterId = "test-filter-none";
        var filterDocument = CreateTestNullableFilter(filterId);

        SetupMockContainerForUpsert(filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);
    }

    [Theory]
    [InlineData(IncidentManagementType.Icm)]
    [InlineData(IncidentManagementType.AzMonitor)]
    [InlineData(IncidentManagementType.PagerDuty)]
    [InlineData(IncidentManagementType.ServiceNow)]
    [InlineData(IncidentManagementType.None)]
    public async Task CreateOrUpdateIncidentFilterAsync_WithMatchingType_Succeeds(IncidentManagementType type)
    {
        // Arrange
        var service = CreateService(type);
        var filterId = $"filter-{type}";
        var filterDocument = CreateFilterForType(type, filterId);

        SetupMockContainerForUpsertByType(type, filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
    }

    #endregion

    #region CreateOrUpdateIncidentFilterAsync - Type Mismatch Tests

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WithMismatchedType_ReturnsBadRequest()
    {
        // Arrange - Service configured for ICM but filter is AzMonitor
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter-1";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'AzMonitor' does not match configured incident management type 'Icm'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_AzMonitorFilterWithIcmService_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'AzMonitor' does not match configured incident management type 'Icm'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_IcmFilterWithAzMonitorService_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.AzMonitor);
        var filterId = "test-filter";
        var filterDocument = CreateTestIcmFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'Icm' does not match configured incident management type 'AzMonitor'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_PagerDutyFilterWithServiceNowService_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.ServiceNow);
        var filterId = "test-filter";
        var filterDocument = CreateTestPagerDutyFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'PagerDuty' does not match configured incident management type 'ServiceNow'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_ServiceNowFilterWithPagerDutyService_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.PagerDuty);
        var filterId = "test-filter";
        var filterDocument = CreateTestServiceNowFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'ServiceNow' does not match configured incident management type 'PagerDuty'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_NullableFilterWithIcmService_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter";
        var filterDocument = CreateTestNullableFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'None' does not match configured incident management type 'Icm'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    #endregion

    #region CreateOrUpdateIncidentFilterAsync - Dry Run Tests

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_DryRun_DoesNotSaveToDatabase()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter-1";
        var filterDocument = CreateTestIcmFilter(filterId);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument, dryRun: true);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);

        // Verify no upsert was called
        _mockContainer.Verify(
            c => c.UpsertItemAsync(It.IsAny<IcmIncidentFilterDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_DryRunWithAzMonitor_DoesNotSaveToDatabase()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.AzMonitor);
        var filterId = "test-filter-azmonitor";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument, dryRun: true);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);

        // Verify no upsert was called
        _mockContainer.Verify(
            c => c.UpsertItemAsync(It.IsAny<AzMonitorIncidentFilterDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_DryRunWithMismatchedType_StillReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        // Setup validator to return platform mismatch error
        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'AzMonitor' does not match configured incident management type 'Icm'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act - Even in dry-run mode, type mismatch should be caught
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument, dryRun: true);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Theory]
    [InlineData(IncidentManagementType.Icm)]
    [InlineData(IncidentManagementType.AzMonitor)]
    [InlineData(IncidentManagementType.PagerDuty)]
    [InlineData(IncidentManagementType.ServiceNow)]
    [InlineData(IncidentManagementType.None)]
    public async Task CreateOrUpdateIncidentFilterAsync_DryRun_ReturnsValidModelForAllTypes(IncidentManagementType type)
    {
        // Arrange
        var service = CreateService(type);
        var filterId = $"filter-{type}-dryrun";
        var filterDocument = CreateFilterForType(type, filterId);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument, dryRun: true);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);
        Assert.Equal(filterId, result.Response.Id);
    }

    #endregion

    #region CreateOrUpdateIncidentFilterAsync - Validation Error Tests

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidatorReturnsError_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter-1";
        var filterDocument = CreateTestIcmFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("HandlingAgent must be set");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidatorReturnsMultipleErrors_ReturnsBadRequestWithAllErrors()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.AzMonitor);
        var filterId = "test-filter-azmonitor";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("Id cannot be empty");
        validationResult.AddError("HandlingAgent must be set");
        validationResult.AddError("Priority 'Invalid' is not valid for AzMonitor");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidatorReturnsInvalidAgentMode_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.PagerDuty);
        var filterId = "test-filter-pagerduty";
        var filterDocument = CreateTestPagerDutyFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("AgentMode 'InvalidMode' is not valid. Allowed values are: ReadOnly, Review, Autonomous (case insensitive)");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidatorReturnsInvalidPriority_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.AzMonitor);
        var filterId = "test-filter-azmonitor";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("Priority 'P1' is not valid for AzMonitor. Allowed values are: Sev0, Sev1, Sev2, Sev3, Sev4 (case insensitive)");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidatorReturnsPlatformMismatch_ReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter";
        var filterDocument = CreateTestAzMonitorFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("Incident platform 'AzMonitor' does not match configured incident management type 'Icm'");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_DryRunWithValidationError_StillReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter";
        var filterDocument = CreateTestIcmFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("HandlingAgent must be set");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act - Even in dry-run mode, validation errors should be caught
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument, dryRun: true);

        // Assert
        Assert.True(result.IsStatusCodeResult);
        Assert.IsType<BadRequestObjectResult>(result.ActionResult);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidationPasses_SavesDocument()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter-1";
        var filterDocument = CreateTestIcmFilter(filterId);

        // Validator returns success (default setup in constructor)
        SetupMockContainerForUpsert(filterDocument);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsAsyncCreated);
        Assert.NotNull(result.Response);

        // Verify upsert was called
        _mockContainer.Verify(
            c => c.UpsertItemAsync(It.IsAny<IcmIncidentFilterDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateIncidentFilterAsync_WhenValidationFails_DoesNotSaveDocument()
    {
        // Arrange
        var service = CreateService(IncidentManagementType.Icm);
        var filterId = "test-filter-1";
        var filterDocument = CreateTestIcmFilter(filterId);

        var validationResult = new ApiValidationResult();
        validationResult.AddError("Some validation error");
        _mockIncidentFilterValidator.Setup(v => v.ValidateIncidentFilter(It.IsAny<IIncidentFilterDocument>()))
            .Returns(validationResult);

        // Act
        var result = await service.CreateOrUpdateIncidentFilterAsync(filterId, filterDocument);

        // Assert
        Assert.True(result.IsStatusCodeResult);

        // Verify upsert was NOT called
        _mockContainer.Verify(
            c => c.UpsertItemAsync(It.IsAny<IcmIncidentFilterDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helper Methods

    private IcmIncidentFilterDocument CreateTestIcmFilter(string filterId)
    {
        return new IcmIncidentFilterDocument
        {
            Id = filterId,
            Name = $"ICM Filter {filterId}",
            ImpactedService = "TestService",
            Priority = "Sev2",
            IncidentType = "Icm",
            TitleContains = "CPU",
            MonitorId = "monitor-123",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsEnabled = true
        };
    }

    private AzMonitorIncidentFilterDocument CreateTestAzMonitorFilter(string filterId)
    {
        return new AzMonitorIncidentFilterDocument
        {
            Id = filterId,
            Name = $"AzMonitor Filter {filterId}",
            ImpactedService = "TestService",
            Priority = "Sev2",
            IncidentType = "AzMonitor",
            TitleContains = "CPU",
            TargetResourceType = "microsoft.containerservice/managedclusters",
            TargetResource = "test-resource",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsEnabled = true
        };
    }

    private PagerDutyIncidentFilterDocument CreateTestPagerDutyFilter(string filterId)
    {
        return new PagerDutyIncidentFilterDocument
        {
            Id = filterId,
            Name = $"PagerDuty Filter {filterId}",
            ImpactedService = "TestService",
            Priority = "P2",
            IncidentType = "PagerDuty",
            TitleContains = "Alert",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsEnabled = true
        };
    }

    private ServiceNowIncidentFilterDocument CreateTestServiceNowFilter(string filterId)
    {
        return new ServiceNowIncidentFilterDocument
        {
            Id = filterId,
            Name = $"ServiceNow Filter {filterId}",
            ImpactedService = "TestService",
            Priority = "2",
            IncidentType = "ServiceNow",
            TitleContains = "Incident",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsEnabled = true
        };
    }

    private NullableIncidentFilterDocument CreateTestNullableFilter(string filterId)
    {
        return new NullableIncidentFilterDocument
        {
            Id = filterId,
            Name = $"Nullable Filter {filterId}",
            ImpactedService = "TestService",
            Priority = "Low",
            IncidentType = "None",
            TitleContains = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsEnabled = true
        };
    }

    private IIncidentFilterDocument CreateFilterForType(IncidentManagementType type, string filterId)
    {
        return type switch
        {
            IncidentManagementType.Icm => CreateTestIcmFilter(filterId),
            IncidentManagementType.AzMonitor => CreateTestAzMonitorFilter(filterId),
            IncidentManagementType.PagerDuty => CreateTestPagerDutyFilter(filterId),
            IncidentManagementType.ServiceNow => CreateTestServiceNowFilter(filterId),
            IncidentManagementType.None => CreateTestNullableFilter(filterId),
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };
    }

    private void SetupMockContainerForUpsert<T>(T document) where T : class, IIncidentFilterDocument
    {
        var mockResponse = new Mock<ItemResponse<T>>();
        mockResponse.Setup(r => r.Resource).Returns(document);
        mockResponse.Setup(r => r.StatusCode).Returns(System.Net.HttpStatusCode.OK);

        _mockContainer.Setup(c => c.UpsertItemAsync(
                It.IsAny<T>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);
    }

    private void SetupMockContainerForUpsertByType(IncidentManagementType type, IIncidentFilterDocument document)
    {
        switch (type)
        {
            case IncidentManagementType.Icm:
                SetupMockContainerForUpsert((IcmIncidentFilterDocument)document);
                break;
            case IncidentManagementType.AzMonitor:
                SetupMockContainerForUpsert((AzMonitorIncidentFilterDocument)document);
                break;
            case IncidentManagementType.PagerDuty:
                SetupMockContainerForUpsert((PagerDutyIncidentFilterDocument)document);
                break;
            case IncidentManagementType.ServiceNow:
                SetupMockContainerForUpsert((ServiceNowIncidentFilterDocument)document);
                break;
            case IncidentManagementType.None:
                SetupMockContainerForUpsert((NullableIncidentFilterDocument)document);
                break;
        }
    }

    #endregion
}
