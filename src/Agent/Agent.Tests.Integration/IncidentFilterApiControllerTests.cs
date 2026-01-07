// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.Integration;

/// <summary>
/// Integration tests for IncidentFilterApiController.
/// These tests make real HTTP requests to a running server at https://localhost:7023.
/// </summary>
public class IncidentFilterApiControllerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string BaseUrl = "https://localhost:7023";

    // Set to null to enable all tests, or set to a message to skip them
    private const string? SkipReason = null; // "Only for local testing purpose";

    // The configured incident management platform from appsettings
    private static readonly string? ConfiguredPlatform;

    static IncidentFilterApiControllerTests()
    {
        // Load configuration from appsettings to determine the configured platform
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Agent.Web"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.development.json", optional: true)
            .Build();

        ConfiguredPlatform = configuration["AppSettings:Core:External:IncidentManagement:Type"];
    }

    public IncidentFilterApiControllerTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine($"Configured incident management platform: {ConfiguredPlatform ?? "None"}");

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// Checks if the specified platform is the configured platform.
    /// </summary>
    private static bool IsPlatformConfigured(string platform)
    {
        return string.Equals(ConfiguredPlatform, platform, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the skip reason for a platform-specific test if the platform is not configured.
    /// </summary>
    private static string? GetPlatformSkipReason(string platform)
    {
#pragma warning disable CS0162 // Unreachable code detected - SkipReason is a compile-time constant
        if (SkipReason != null)
        {
            return SkipReason;
        }
#pragma warning restore CS0162

        if (!IsPlatformConfigured(platform))
        {
            return $"Platform '{platform}' is not configured. Configured platform: '{ConfiguredPlatform ?? "None"}'";
        }

        return null;
    }

    #region ICM Filter Tests

    [Fact(Skip = SkipReason)]
    public async Task IcmFilter_CreateThenGet_ReturnsCreatedFilter()
    {
        // Skip if ICM is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("Icm");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-icm-filter-{Guid.NewGuid():N}";
        var request = CreateIcmFilterRequest(filterName, "TestMonitor123", "TestCreator");

        try
        {
            // Act - Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response: {getResponse.StatusCode}");
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode, $"Get failed: {getContent}");
            Assert.Contains(filterName, getContent);
            Assert.Contains("TestMonitor123", getContent);
            Assert.Contains("Icm", getContent);
        }
        finally
        {
            // Cleanup
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task IcmFilter_UpdatePutThenGet_ReturnsUpdatedFilter()
    {
        // Skip if ICM is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("Icm");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-icm-filter-{Guid.NewGuid():N}";
        var createRequest = CreateIcmFilterRequest(filterName, "InitialMonitor", "InitialCreator");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PUT
            var updateRequest = CreateIcmFilterRequest(filterName, "UpdatedMonitor", "UpdatedCreator");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", updateRequest, _jsonOptions);
            var updateContent = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update response: {updateResponse.StatusCode}");
            _output.WriteLine($"Update response body: {updateContent}");

            Assert.True(updateResponse.IsSuccessStatusCode, $"Update failed: {updateContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("UpdatedMonitor", getContent);
            Assert.Contains("UpdatedCreator", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task IcmFilter_UpdatePatchThenGet_ReturnsUpdatedFilter()
    {
        // Skip if ICM is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("Icm");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-icm-filter-{Guid.NewGuid():N}";
        var createRequest = CreateIcmFilterRequest(filterName, "InitialMonitor", "InitialCreator");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PATCH (partial update)
            var patchRequest = new
            {
                name = filterName,
                type = "IncidentFilter",
                properties = new
                {
                    incidentPlatform = "Icm",
                    icmFilterSettings = new
                    {
                        monitorId = "PatchedMonitor"
                    }
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");

            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed: {patchResponseContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("PatchedMonitor", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    #endregion

    #region AzMonitor Filter Tests

    [Fact(Skip = SkipReason)]
    public async Task AzMonitorFilter_CreateThenGet_ReturnsCreatedFilter()
    {
        // Skip if AzMonitor is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("AzMonitor");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-azmonitor-filter-{Guid.NewGuid():N}";
        var request = CreateAzMonitorFilterRequest(filterName, "Microsoft.Compute/virtualMachines", "/subscriptions/test-sub/resourceGroups/test-rg");

        try
        {
            // Act - Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response: {getResponse.StatusCode}");
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode, $"Get failed: {getContent}");
            Assert.Contains(filterName, getContent);
            Assert.Contains("Microsoft.Compute/virtualMachines", getContent);
            Assert.Contains("AzMonitor", getContent);
        }
        finally
        {
            // Cleanup
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task AzMonitorFilter_UpdatePutThenGet_ReturnsUpdatedFilter()
    {
        // Skip if AzMonitor is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("AzMonitor");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-azmonitor-filter-{Guid.NewGuid():N}";
        var createRequest = CreateAzMonitorFilterRequest(filterName, "Microsoft.Web/sites", "/subscriptions/sub1");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PUT
            var updateRequest = CreateAzMonitorFilterRequest(filterName, "Microsoft.Storage/storageAccounts", "/subscriptions/sub2");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", updateRequest, _jsonOptions);
            var updateContent = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update response: {updateResponse.StatusCode}");
            _output.WriteLine($"Update response body: {updateContent}");

            Assert.True(updateResponse.IsSuccessStatusCode, $"Update failed: {updateContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("Microsoft.Storage/storageAccounts", getContent);
            Assert.Contains("/subscriptions/sub2", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task AzMonitorFilter_UpdatePatchThenGet_ReturnsUpdatedFilter()
    {
        // Skip if AzMonitor is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("AzMonitor");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-azmonitor-filter-{Guid.NewGuid():N}";
        var createRequest = CreateAzMonitorFilterRequest(filterName, "Microsoft.Web/sites", "/subscriptions/initial");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PATCH (partial update)
            var patchRequest = new
            {
                name = filterName,
                type = "IncidentFilter",
                properties = new
                {
                    incidentPlatform = "AzMonitor",
                    azMonitorFilterSettings = new
                    {
                        targetResource = "/subscriptions/patched"
                    }
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");

            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed: {patchResponseContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("/subscriptions/patched", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    #endregion

    #region PagerDuty Filter Tests

    [Fact(Skip = SkipReason)]
    public async Task PagerDutyFilter_CreateThenGet_ReturnsCreatedFilter()
    {
        // Skip if PagerDuty is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("PagerDuty");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-pagerduty-filter-{Guid.NewGuid():N}";
        var request = CreatePagerDutyFilterRequest(filterName, "TestService", "P1");

        try
        {
            // Act - Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response: {getResponse.StatusCode}");
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode, $"Get failed: {getContent}");
            Assert.Contains(filterName, getContent);
            Assert.Contains("PagerDuty", getContent);
        }
        finally
        {
            // Cleanup
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task PagerDutyFilter_UpdatePutThenGet_ReturnsUpdatedFilter()
    {
        // Skip if PagerDuty is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("PagerDuty");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-pagerduty-filter-{Guid.NewGuid():N}";
        var createRequest = CreatePagerDutyFilterRequest(filterName, "InitialService", "P1");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PUT
            var updateRequest = CreatePagerDutyFilterRequest(filterName, "UpdatedService", "P2");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", updateRequest, _jsonOptions);
            var updateContent = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update response: {updateResponse.StatusCode}");
            _output.WriteLine($"Update response body: {updateContent}");

            Assert.True(updateResponse.IsSuccessStatusCode, $"Update failed: {updateContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("UpdatedService", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task PagerDutyFilter_UpdatePatchThenGet_ReturnsUpdatedFilter()
    {
        // Skip if PagerDuty is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("PagerDuty");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-pagerduty-filter-{Guid.NewGuid():N}";
        var createRequest = CreatePagerDutyFilterRequest(filterName, "InitialService", "P1");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PATCH (partial update)
            var patchRequest = new
            {
                name = filterName,
                type = "IncidentFilter",
                properties = new
                {
                    incidentPlatform = "PagerDuty",
                    impactedService = "PatchedService"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");

            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed: {patchResponseContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("PatchedService", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    #endregion

    #region ServiceNow Filter Tests

    [Fact(Skip = SkipReason)]
    public async Task ServiceNowFilter_CreateThenGet_ReturnsCreatedFilter()
    {
        // Skip if ServiceNow is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("ServiceNow");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-servicenow-filter-{Guid.NewGuid():N}";
        var request = CreateServiceNowFilterRequest(filterName, "TestService", "High");

        try
        {
            // Act - Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response: {getResponse.StatusCode}");
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode, $"Get failed: {getContent}");
            Assert.Contains(filterName, getContent);
            Assert.Contains("ServiceNow", getContent);
        }
        finally
        {
            // Cleanup
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task ServiceNowFilter_UpdatePutThenGet_ReturnsUpdatedFilter()
    {
        // Skip if ServiceNow is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("ServiceNow");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-servicenow-filter-{Guid.NewGuid():N}";
        var createRequest = CreateServiceNowFilterRequest(filterName, "InitialService", "High");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PUT
            var updateRequest = CreateServiceNowFilterRequest(filterName, "UpdatedService", "Critical");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", updateRequest, _jsonOptions);
            var updateContent = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update response: {updateResponse.StatusCode}");
            _output.WriteLine($"Update response body: {updateContent}");

            Assert.True(updateResponse.IsSuccessStatusCode, $"Update failed: {updateContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("UpdatedService", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task ServiceNowFilter_UpdatePatchThenGet_ReturnsUpdatedFilter()
    {
        // Skip if ServiceNow is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("ServiceNow");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange
        var filterName = $"test-servicenow-filter-{Guid.NewGuid():N}";
        var createRequest = CreateServiceNowFilterRequest(filterName, "InitialService", "High");

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PATCH (partial update)
            var patchRequest = new
            {
                name = filterName,
                type = "IncidentFilter",
                properties = new
                {
                    incidentPlatform = "ServiceNow",
                    impactedService = "PatchedService"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");

            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed: {patchResponseContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("PatchedService", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    #endregion

    #region Common Filter Operations Tests

    [Fact(Skip = SkipReason)]
    public async Task Filter_Get_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var filterName = $"non-existent-filter-{Guid.NewGuid():N}";

        // Act
        var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        _output.WriteLine($"Get response: {getResponse.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_DeleteThenGet_ReturnsNotFound()
    {
        // Arrange
        var filterName = $"test-filter-{Guid.NewGuid():N}";
        var createRequest = CreateFilterRequestForConfiguredPlatform(filterName);

        try
        {
            // Create filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Delete
            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            _output.WriteLine($"Delete response: {deleteResponse.StatusCode}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent);

            // Wait a bit for deletion to propagate
            await Task.Delay(500);

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            _output.WriteLine($"Get after delete response: {getResponse.StatusCode}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_List_ReturnsFiltersList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/IncidentFilter/filters");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"List filters response: {getResponse.StatusCode}");
        _output.WriteLine($"List filters body: {getContent}");

        Assert.True(getResponse.IsSuccessStatusCode);
        Assert.Contains("value", getContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_ListAfterCreateTwo_ReturnsBothFilters()
    {
        var filterName1 = $"test-filter-list-{Guid.NewGuid():N}";
        var filterName2 = $"test-filter-list-{Guid.NewGuid():N}";

        try
        {
            // Create two filters using the configured platform
            var createRequest1 = CreateFilterRequestForConfiguredPlatform(filterName1);
            var createRequest2 = CreateFilterRequestForConfiguredPlatform(filterName2);

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            // List all filters
            var listResponse = await _httpClient.GetAsync("/api/v2/IncidentFilter/filters");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(filterName1, listContent);
            Assert.Contains(filterName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName1}");
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName2}");
        }
    }

    #endregion

    #region Filter Properties Tests

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithAllSharedFields_ReturnsAllFields()
    {
        // Arrange
        var filterName = $"test-filter-full-{Guid.NewGuid():N}";
        // Use platform-appropriate priority (P1 for PagerDuty, 1 for others) and valid agentMode
        var priority = ConfiguredPlatform == "PagerDuty" ? "P1" : "1";
        var request = CreateFilterRequestWithAllFieldsForConfiguredPlatform(
            filterName,
            impactedService: "TestImpactedService",
            priority: priority,
            incidentType: "Sev1",
            alertId: "Alert123",
            titleContains: "Critical",
            agentMode: "Autonomous",
            handlingAgent: "TestAgent",
            owningTeamId: "Team123",
            maxAutomatedAttempts: 5,
            deepInvestigationEnabled: true,
            isEnabled: true
        );

        try
        {
            // Act - Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("TestImpactedService", getContent);
            Assert.Contains("\"priority\"", getContent);
            Assert.Contains("Sev1", getContent);
            Assert.Contains("Alert123", getContent);
            Assert.Contains("Critical", getContent);
            Assert.Contains("TestAgent", getContent);
            Assert.Contains("Team123", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_PatchIsEnabled_TogglesFilterState()
    {
        // Arrange
        var filterName = $"test-filter-toggle-{Guid.NewGuid():N}";
        var createRequest = CreateFilterRequestForConfiguredPlatform(filterName, isEnabled: true);

        try
        {
            // Create initial filter with isEnabled = true
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Patch to disable
            var patchRequest = CreatePatchRequestForConfiguredPlatform(filterName, isEnabled: false);

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            Assert.True(patchResponse.IsSuccessStatusCode);

            // Act - Get and verify disabled
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body after disable: {getContent}");

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("\"isEnabled\":false", getContent.Replace(" ", ""));
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_PatchMaxAutomatedInvestigationAttempts_UpdatesValue()
    {
        // Arrange
        var filterName = $"test-filter-attempts-{Guid.NewGuid():N}";
        var createRequest = CreateFilterRequestForConfiguredPlatform(filterName);

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Patch to update maxAutomatedInvestigationAttempts
            var patchRequest = CreatePatchRequestForConfiguredPlatform(filterName, maxAutomatedInvestigationAttempts: 10);

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            Assert.True(patchResponse.IsSuccessStatusCode);

            // Act - Get and verify
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("\"maxAutomatedInvestigationAttempts\":10", getContent.Replace(" ", ""));
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    #endregion

    #region Validation Tests

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithNameMismatch_ReturnsBadRequest()
    {
        // Arrange
        var urlFilterName = $"url-filter-{Guid.NewGuid():N}";
        var bodyFilterName = $"body-filter-{Guid.NewGuid():N}";
        var request = CreateFilterRequestForConfiguredPlatform(bodyFilterName);

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{urlFilterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithInvalidType_ReturnsBadRequest()
    {
        // Arrange
        var filterName = $"test-filter-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "InvalidType",
            properties = new
            {
                incidentPlatform = ConfiguredPlatform ?? "PagerDuty"
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_PatchWithNameMismatch_ReturnsBadRequest()
    {
        // Arrange
        var filterName = $"test-filter-{Guid.NewGuid():N}";
        var createRequest = CreateFilterRequestForConfiguredPlatform(filterName);

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Patch with mismatched name
            var patchRequest = new
            {
                name = "different-name",
                type = "IncidentFilter",
                properties = new
                {
                    incidentPlatform = ConfiguredPlatform ?? "PagerDuty"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_PatchNonExistent_ReturnsNotFound()
    {
        // Arrange
        var filterName = $"non-existent-filter-{Guid.NewGuid():N}";
        var patchRequest = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = ConfiguredPlatform ?? "PagerDuty"
            }
        };

        var patchContent = new StringContent(
            JsonSerializer.Serialize(patchRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}", patchContent);
        _output.WriteLine($"Patch response: {patchResponse.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithInvalidAgentMode_ReturnsBadRequest()
    {
        // Arrange
        var filterName = $"test-filter-invalid-mode-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = ConfiguredPlatform ?? "PagerDuty",
                impactedService = "TestService",
                priority = ConfiguredPlatform == "PagerDuty" ? "P1" : "High",
                handlingAgent = "test-agent",
                agentMode = "InvalidMode", // Invalid - should be ReadOnly, Review, or Autonomous
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Contains("AgentMode", createContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithAutoAgentMode_ReturnsBadRequest()
    {
        // Arrange - "Auto" is a common mistake, should be "Autonomous"
        var filterName = $"test-filter-auto-mode-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = ConfiguredPlatform ?? "PagerDuty",
                impactedService = "TestService",
                priority = ConfiguredPlatform == "PagerDuty" ? "P1" : "High",
                handlingAgent = "test-agent",
                agentMode = "Auto", // Invalid - should be "Autonomous"
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Contains("AgentMode", createContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithMissingHandlingAgent_ReturnsBadRequest()
    {
        // Arrange
        var filterName = $"test-filter-no-agent-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = ConfiguredPlatform ?? "PagerDuty",
                impactedService = "TestService",
                priority = ConfiguredPlatform == "PagerDuty" ? "P1" : "High",
                // handlingAgent is missing - should be required
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Contains("HandlingAgent", createContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithInvalidIncidentPlatform_ReturnsError()
    {
        // Arrange
        var filterName = $"test-filter-invalid-platform-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "InvalidPlatform", // Invalid - should be Icm, AzMonitor, PagerDuty, or ServiceNow
                impactedService = "TestService",
                handlingAgent = "test-agent",
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert - API returns InternalServerError for unrecognized platform (could be improved to BadRequest)
        Assert.False(createResponse.IsSuccessStatusCode, "Should reject invalid incident platform");
    }

    [Fact(Skip = SkipReason)]
    public async Task PagerDutyFilter_CreateWithInvalidPriority_ReturnsBadRequest()
    {
        // Skip if PagerDuty is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("PagerDuty");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange - PagerDuty requires P1-P5 format
        var filterName = $"test-pd-invalid-priority-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "PagerDuty",
                impactedService = "TestService",
                priority = "1", // Invalid for PagerDuty - should be P1, P2, P3, P4, or P5
                handlingAgent = "test-agent",
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Contains("Priority", createContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task PagerDutyFilter_CreateWithInvalidPriorityFormat_ReturnsBadRequest()
    {
        // Skip if PagerDuty is not the configured platform
        var platformSkipReason = GetPlatformSkipReason("PagerDuty");
        if (platformSkipReason != null)
        {
            _output.WriteLine($"Skipping: {platformSkipReason}");
            return;
        }

        // Arrange - PagerDuty requires P1-P5 format, not "High", "Low", etc.
        var filterName = $"test-pd-invalid-priority-format-{Guid.NewGuid():N}";
        var request = new
        {
            name = filterName,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "PagerDuty",
                impactedService = "TestService",
                priority = "High", // Invalid for PagerDuty - should be P1-P5
                handlingAgent = "test-agent",
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Contains("Priority", createContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            name = "",
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = ConfiguredPlatform ?? "PagerDuty",
                impactedService = "TestService",
                handlingAgent = "test-agent",
                isEnabled = true
            }
        };

        // Act
        var createResponse = await _httpClient.PutAsJsonAsync("/api/v2/IncidentFilter/filters/", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Create response: {createResponse.StatusCode}");
        _output.WriteLine($"Create response body: {createContent}");

        // Assert - Should return BadRequest or NotFound for empty name
        Assert.True(
            createResponse.StatusCode == HttpStatusCode.BadRequest ||
            createResponse.StatusCode == HttpStatusCode.NotFound ||
            createResponse.StatusCode == HttpStatusCode.MethodNotAllowed,
            $"Expected BadRequest, NotFound, or MethodNotAllowed but got {createResponse.StatusCode}");
    }

    #endregion

    #region DryRun Tests

    [Fact(Skip = SkipReason)]
    public async Task Filter_CreateWithDryRun_DoesNotPersist()
    {
        // Arrange
        var filterName = $"test-dryrun-filter-{Guid.NewGuid():N}";
        var request = CreateFilterRequestForConfiguredPlatform(filterName);

        // Act - Create with dryRun=true
        var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}?dryRun=true", request, _jsonOptions);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"DryRun Create response: {createResponse.StatusCode}");
        _output.WriteLine($"DryRun Create response body: {createContent}");

        Assert.True(createResponse.IsSuccessStatusCode, $"DryRun create failed: {createContent}");

        // Act - Get (should not find the filter)
        var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        _output.WriteLine($"Get response after dryRun: {getResponse.StatusCode}");

        // Assert - Filter should not exist
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_PatchWithDryRun_DoesNotPersistChanges()
    {
        // Arrange
        var filterName = $"test-dryrun-patch-{Guid.NewGuid():N}";
        var createRequest = CreateFilterRequestForConfiguredPlatform(filterName);

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Patch with dryRun=true
            var patchRequest = CreatePatchRequestForConfiguredPlatform(filterName, impactedService: "DryRunPatchedService");

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/IncidentFilter/filters/{filterName}?dryRun=true", patchContent);
            Assert.True(patchResponse.IsSuccessStatusCode);

            // Act - Get and verify original value is unchanged
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body after dryRun patch: {getContent}");

            // Assert - Patched value should not be persisted
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.DoesNotContain("DryRunPatchedService", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Filter_DeleteWithDryRun_DoesNotDelete()
    {
        // Arrange
        var filterName = $"test-dryrun-delete-{Guid.NewGuid():N}";
        var createRequest = CreateFilterRequestForConfiguredPlatform(filterName);

        try
        {
            // Create initial filter
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/IncidentFilter/filters/{filterName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Delete with dryRun=true
            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}?dryRun=true");
            _output.WriteLine($"DryRun Delete response: {deleteResponse.StatusCode}");

            // Act - Get (filter should still exist)
            var getResponse = await _httpClient.GetAsync($"/api/v2/IncidentFilter/filters/{filterName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response after dryRun delete: {getResponse.StatusCode}");

            // Assert - Filter should still exist
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains(filterName, getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/IncidentFilter/filters/{filterName}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a filter request for the configured incident platform.
    /// </summary>
    private static object CreateFilterRequestForConfiguredPlatform(string name, bool isEnabled = true)
    {
        return ConfiguredPlatform?.ToUpperInvariant() switch
        {
            "ICM" => CreateIcmFilterRequest(name, "TestMonitor", "TestCreator", isEnabled),
            "AZMONITOR" => CreateAzMonitorFilterRequest(name, "Microsoft.Web/sites", "/subscriptions/test", isEnabled),
            "PAGERDUTY" => CreatePagerDutyFilterRequest(name, "TestService", "P1", isEnabled),
            "SERVICENOW" => CreateServiceNowFilterRequest(name, "TestService", "High", isEnabled),
            _ => CreatePagerDutyFilterRequest(name, "TestService", "P1", isEnabled) // Default fallback
        };
    }

    /// <summary>
    /// Creates a filter request with all shared fields for the configured incident platform.
    /// </summary>
    private static object CreateFilterRequestWithAllFieldsForConfiguredPlatform(
        string name,
        string impactedService,
        string priority,
        string incidentType,
        string alertId,
        string titleContains,
        string agentMode,
        string handlingAgent,
        string owningTeamId,
        int maxAutomatedAttempts,
        bool deepInvestigationEnabled,
        bool isEnabled)
    {
        return ConfiguredPlatform?.ToUpperInvariant() switch
        {
            "ICM" => CreateIcmFilterRequestWithAllFields(name, "TestMonitor", "TestCreator", impactedService, priority, incidentType, alertId, titleContains, agentMode, handlingAgent, owningTeamId, maxAutomatedAttempts, deepInvestigationEnabled, isEnabled),
            "AZMONITOR" => CreateAzMonitorFilterRequestWithAllFields(name, "Microsoft.Web/sites", "/subscriptions/test", impactedService, priority, incidentType, alertId, titleContains, agentMode, handlingAgent, owningTeamId, maxAutomatedAttempts, deepInvestigationEnabled, isEnabled),
            "PAGERDUTY" => CreatePagerDutyFilterRequestWithAllFields(name, impactedService, priority, incidentType, alertId, titleContains, agentMode, handlingAgent, owningTeamId, maxAutomatedAttempts, deepInvestigationEnabled, isEnabled),
            "SERVICENOW" => CreateServiceNowFilterRequestWithAllFields(name, impactedService, priority, incidentType, alertId, titleContains, agentMode, handlingAgent, owningTeamId, maxAutomatedAttempts, deepInvestigationEnabled, isEnabled),
            _ => CreatePagerDutyFilterRequestWithAllFields(name, impactedService, priority, incidentType, alertId, titleContains, agentMode, handlingAgent, owningTeamId, maxAutomatedAttempts, deepInvestigationEnabled, isEnabled)
        };
    }

    /// <summary>
    /// Creates a PATCH request for the configured platform.
    /// </summary>
    private static object CreatePatchRequestForConfiguredPlatform(string name, string? impactedService = null, bool? isEnabled = null, int? maxAutomatedInvestigationAttempts = null)
    {
        var properties = new Dictionary<string, object>
        {
            { "incidentPlatform", ConfiguredPlatform ?? "PagerDuty" }
        };

        if (impactedService != null)
        {
            properties["impactedService"] = impactedService;
        }

        if (isEnabled.HasValue)
        {
            properties["isEnabled"] = isEnabled.Value;
        }

        if (maxAutomatedInvestigationAttempts.HasValue)
        {
            properties["maxAutomatedInvestigationAttempts"] = maxAutomatedInvestigationAttempts.Value;
        }

        return new
        {
            name,
            type = "IncidentFilter",
            properties
        };
    }

    private static object CreateIcmFilterRequest(string name, string monitorId, string createdBy, bool isEnabled = true)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "Icm",
                handlingAgent = "test-agent",
                isEnabled,
                icmFilterSettings = new
                {
                    monitorId,
                    createdBy
                }
            }
        };
    }

    private static object CreateIcmFilterRequestWithAllFields(
        string name,
        string monitorId,
        string createdBy,
        string impactedService,
        string priority,
        string incidentType,
        string alertId,
        string titleContains,
        string agentMode,
        string handlingAgent,
        string owningTeamId,
        int maxAutomatedAttempts,
        bool deepInvestigationEnabled,
        bool isEnabled)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "Icm",
                impactedService,
                priority,
                incidentType,
                alertId,
                titleContains,
                agentMode,
                handlingAgent,
                owningTeamId,
                maxAutomatedInvestigationAttempts = maxAutomatedAttempts,
                deepInvestigationEnabled,
                isEnabled,
                icmFilterSettings = new
                {
                    monitorId,
                    createdBy
                }
            }
        };
    }

    private static object CreateAzMonitorFilterRequest(string name, string targetResourceType, string targetResource, bool isEnabled = true)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "AzMonitor",
                handlingAgent = "test-agent",
                isEnabled,
                azMonitorFilterSettings = new
                {
                    targetResourceType,
                    targetResource
                }
            }
        };
    }

    private static object CreatePagerDutyFilterRequest(string name, string impactedService, string priority, bool isEnabled = true)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "PagerDuty",
                impactedService,
                priority,
                handlingAgent = "test-agent",
                isEnabled
            }
        };
    }

    private static object CreateServiceNowFilterRequest(string name, string impactedService, string priority, bool isEnabled = true)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "ServiceNow",
                impactedService,
                priority,
                handlingAgent = "test-agent",
                isEnabled
            }
        };
    }

    private static object CreateAzMonitorFilterRequestWithAllFields(
        string name,
        string targetResourceType,
        string targetResource,
        string impactedService,
        string priority,
        string incidentType,
        string alertId,
        string titleContains,
        string agentMode,
        string handlingAgent,
        string owningTeamId,
        int maxAutomatedAttempts,
        bool deepInvestigationEnabled,
        bool isEnabled)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "AzMonitor",
                impactedService,
                priority,
                incidentType,
                alertId,
                titleContains,
                agentMode,
                handlingAgent,
                owningTeamId,
                maxAutomatedInvestigationAttempts = maxAutomatedAttempts,
                deepInvestigationEnabled,
                isEnabled,
                azMonitorFilterSettings = new
                {
                    targetResourceType,
                    targetResource
                }
            }
        };
    }

    private static object CreatePagerDutyFilterRequestWithAllFields(
        string name,
        string impactedService,
        string priority,
        string incidentType,
        string alertId,
        string titleContains,
        string agentMode,
        string handlingAgent,
        string owningTeamId,
        int maxAutomatedAttempts,
        bool deepInvestigationEnabled,
        bool isEnabled)
    {
        // Use PagerDuty-compatible priority (P1-P5)
        var pagerDutyPriority = priority.StartsWith("P") ? priority : $"P{priority}";

        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "PagerDuty",
                impactedService,
                priority = pagerDutyPriority,
                incidentType,
                alertId,
                titleContains,
                agentMode,
                handlingAgent,
                owningTeamId,
                maxAutomatedInvestigationAttempts = maxAutomatedAttempts,
                deepInvestigationEnabled,
                isEnabled
            }
        };
    }

    private static object CreateServiceNowFilterRequestWithAllFields(
        string name,
        string impactedService,
        string priority,
        string incidentType,
        string alertId,
        string titleContains,
        string agentMode,
        string handlingAgent,
        string owningTeamId,
        int maxAutomatedAttempts,
        bool deepInvestigationEnabled,
        bool isEnabled)
    {
        return new
        {
            name,
            type = "IncidentFilter",
            properties = new
            {
                incidentPlatform = "ServiceNow",
                impactedService,
                priority,
                incidentType,
                alertId,
                titleContains,
                agentMode,
                handlingAgent,
                owningTeamId,
                maxAutomatedInvestigationAttempts = maxAutomatedAttempts,
                deepInvestigationEnabled,
                isEnabled
            }
        };
    }

    #endregion
}
