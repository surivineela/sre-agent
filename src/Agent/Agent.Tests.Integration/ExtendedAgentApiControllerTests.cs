// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.Integration;

/// <summary>
/// Integration tests for ExtendedAgentApiController.
/// These tests make real HTTP requests to a running server at https://localhost:7023.
/// </summary>
public class ExtendedAgentApiControllerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string BaseUrl = "https://localhost:7023";

    // Set to null to enable all tests, or set to a message to skip them
    private const string? SkipReason = "Only for local testing purpose";

    public ExtendedAgentApiControllerTests(ITestOutputHelper output)
    {
        _output = output;
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

    #region Agent Tests

    [Fact(Skip = SkipReason)]
    public async Task Agent_CreateThenGet_ReturnsCreatedAgent()
    {
        // Arrange
        var agentName = $"test-agent-{Guid.NewGuid():N}";
        var request = CreateAgentRequest(agentName, "Test instructions for agent");

        try
        {
            // Act - Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName}", request, _jsonOptions);
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/agents/{agentName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response: {getResponse.StatusCode}");
            _output.WriteLine($"Get response body: {getContent}");

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode, $"Get failed: {getContent}");
            Assert.Contains(agentName, getContent);
            Assert.Contains("Test instructions for agent", getContent);
        }
        finally
        {
            // Cleanup
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Agent_UpdatePutThenGet_ReturnsUpdatedAgent()
    {
        // Arrange
        var agentName = $"test-agent-{Guid.NewGuid():N}";
        var createRequest = CreateAgentRequest(agentName, "Initial instructions");

        try
        {
            // Create initial agent
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PUT
            var updateRequest = CreateAgentRequest(agentName, "Updated instructions via PUT");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName}", updateRequest, _jsonOptions);
            var updateContent = await updateResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Update response: {updateResponse.StatusCode}");
            _output.WriteLine($"Update response body: {updateContent}");

            Assert.True(updateResponse.IsSuccessStatusCode, $"Update failed: {updateContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/agents/{agentName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("Updated instructions via PUT", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Agent_UpdatePatchThenGet_ReturnsUpdatedAgent()
    {
        // Arrange
        var agentName = $"test-agent-{Guid.NewGuid():N}";
        var createRequest = CreateAgentRequest(agentName, "Initial instructions");

        try
        {
            // Create initial agent
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Update with PATCH (partial update)
            var patchRequest = new
            {
                name = agentName,
                type = "ExtendedAgent",
                properties = new
                {
                    instructions = "Patched instructions for testing the PATCH functionality in integration tests"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/agents/{agentName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");

            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed: {patchResponseContent}");

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/agents/{agentName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains("Patched instructions for testing the PATCH functionality", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Agent_Get_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var agentName = $"non-existent-agent-{Guid.NewGuid():N}";

        // Act
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/agents/{agentName}");
        _output.WriteLine($"Get response: {getResponse.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Agent_DeleteThenGet_ReturnsNotFound()
    {
        // Arrange
        var agentName = $"test-agent-{Guid.NewGuid():N}";
        var createRequest = CreateAgentRequest(agentName, "Agent to be deleted");

        try
        {
            // Create agent
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName}", createRequest, _jsonOptions);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act - Delete
            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName}");
            _output.WriteLine($"Delete response: {deleteResponse.StatusCode}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent);

            // Wait a bit for deletion to propagate
            await Task.Delay(500);

            // Act - Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/agents/{agentName}");
            _output.WriteLine($"Get after delete response: {getResponse.StatusCode}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Agent_List_ReturnsAgentsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/agents");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"List agents response: {getResponse.StatusCode}");
        _output.WriteLine($"List agents body: {getContent}");

        Assert.True(getResponse.IsSuccessStatusCode);
        Assert.Contains("value", getContent);
    }

    [Fact(Skip = SkipReason)]
    public async Task Agent_ListAfterCreateTwo_ReturnsBothAgents()
    {
        var agentName1 = $"test-agent-list-{Guid.NewGuid():N}";
        var agentName2 = $"test-agent-list-{Guid.NewGuid():N}";

        try
        {
            // Create two agents
            var createRequest1 = CreateAgentRequest(agentName1, "First agent for list test");
            var createRequest2 = CreateAgentRequest(agentName2, "Second agent for list test");

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/agents/{agentName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            // List all agents
            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/agents");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(agentName1, listContent);
            Assert.Contains(agentName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/agents/{agentName2}");
        }
    }

    #endregion

    #region Tool Tests

    [Fact(Skip = SkipReason)]
    public async Task Tool_CreateThenGet_ReturnsCreatedTool()
    {
        var toolName = $"test-tool-{Guid.NewGuid():N}";
        var request = CreateKustoToolRequest(toolName, "Test tool description");

        try
        {
            // Create
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName}", request, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            // Get
            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/tools/{toolName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains(toolName, getContent);
            Assert.Contains("Test tool description", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Tool_UpdatePutThenGet_ReturnsUpdatedTool()
    {
        var toolName = $"test-tool-{Guid.NewGuid():N}";
        var createRequest = CreateKustoToolRequest(toolName, "Initial description");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName}", createRequest, _jsonOptions);

            var updateRequest = CreateKustoToolRequest(toolName, "Updated description via PUT");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName}", updateRequest, _jsonOptions);
            Assert.True(updateResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/tools/{toolName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("Updated description via PUT", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Tool_UpdatePatchThenGet_ReturnsUpdatedTool()
    {
        var toolName = $"test-tool-{Guid.NewGuid():N}";
        var createRequest = CreateKustoToolRequest(toolName, "Initial description");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName}", createRequest, _jsonOptions);

            var patchRequest = new
            {
                name = toolName,
                type = "ExtendedAgentTool",
                properties = new
                {
                    type = "KustoTool",
                    description = "Patched description"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/tools/{toolName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");
            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed with {patchResponse.StatusCode}: {patchResponseContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/tools/{toolName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            Assert.Contains("Patched description", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Tool_Get_NonExistent_ReturnsNotFound()
    {
        var toolName = $"non-existent-tool-{Guid.NewGuid():N}";
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/tools/{toolName}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Tool_DeleteThenGet_ReturnsNotFound()
    {
        var toolName = $"test-tool-{Guid.NewGuid():N}";
        var createRequest = CreateKustoToolRequest(toolName, "Tool to be deleted");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName}", createRequest, _jsonOptions);

            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent);

            await Task.Delay(500);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/tools/{toolName}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Tool_List_ReturnsToolsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/tools");
        Assert.True(getResponse.IsSuccessStatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Tool_ListAfterCreateTwo_ReturnsBothTools()
    {
        var toolName1 = $"test-tool-list-{Guid.NewGuid():N}";
        var toolName2 = $"test-tool-list-{Guid.NewGuid():N}";

        try
        {
            var createRequest1 = CreateKustoToolRequest(toolName1, "First tool for list test");
            var createRequest2 = CreateKustoToolRequest(toolName2, "Second tool for list test");

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/tools/{toolName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/tools");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(toolName1, listContent);
            Assert.Contains(toolName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/tools/{toolName2}");
        }
    }

    #endregion

    #region Connector Tests

    [Fact(Skip = SkipReason)]
    public async Task Connector_CreateThenGet_ReturnsCreatedConnector()
    {
        var connectorName = $"test-connector-{Guid.NewGuid():N}";
        var request = CreateKustoConnectorRequest(connectorName, "Test connector description");

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName}", request, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains(connectorName, getContent);
            Assert.Contains("Test connector description", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Connector_UpdatePutThenGet_ReturnsUpdatedConnector()
    {
        var connectorName = $"test-connector-{Guid.NewGuid():N}";
        var createRequest = CreateKustoConnectorRequest(connectorName, "Initial description");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName}", createRequest, _jsonOptions);

            var updateRequest = CreateKustoConnectorRequest(connectorName, "Updated description via PUT");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName}", updateRequest, _jsonOptions);
            Assert.True(updateResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("Updated description via PUT", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Connector_UpdatePatchThenGet_ReturnsUpdatedConnector()
    {
        var connectorName = $"test-connector-{Guid.NewGuid():N}";
        var createRequest = CreateKustoConnectorRequest(connectorName, "Initial description");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName}", createRequest, _jsonOptions);

            var patchRequest = new
            {
                name = connectorName,
                type = "ExtendedAgentConnector",
                properties = new
                {
                    type = "Kusto",
                    description = "Patched description"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/connectors/{connectorName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");
            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed with {patchResponse.StatusCode}: {patchResponseContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            Assert.Contains("Patched description", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Connector_Get_NonExistent_ReturnsNotFound()
    {
        var connectorName = $"non-existent-connector-{Guid.NewGuid():N}";
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Connector_DeleteThenGet_ReturnsNotFound()
    {
        var connectorName = $"test-connector-{Guid.NewGuid():N}";
        var createRequest = CreateKustoConnectorRequest(connectorName, "Connector to be deleted");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName}", createRequest, _jsonOptions);

            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent);

            await Task.Delay(500);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Connector_List_ReturnsConnectorsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/connectors");
        Assert.True(getResponse.IsSuccessStatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Connector_ListAfterCreateTwo_ReturnsBothConnectors()
    {
        var connectorName1 = $"test-connector-list-{Guid.NewGuid():N}";
        var connectorName2 = $"test-connector-list-{Guid.NewGuid():N}";

        try
        {
            var createRequest1 = CreateKustoConnectorRequest(connectorName1, "First connector for list test");
            var createRequest2 = CreateKustoConnectorRequest(connectorName2, "Second connector for list test");

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/connectors/{connectorName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/connectors");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(connectorName1, listContent);
            Assert.Contains(connectorName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/connectors/{connectorName2}");
        }
    }

    #endregion

    #region Plugin Tests

    [Fact(Skip = SkipReason)]
    public async Task Plugin_CreateThenGet_ReturnsCreatedPlugin()
    {
        var pluginName = $"test-plugin-{Guid.NewGuid():N}";
        var request = CreatePluginRequest(pluginName);

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName}", request, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response: {getResponse.StatusCode}");
            _output.WriteLine($"Get response body: {getContent}");

            Assert.True(getResponse.IsSuccessStatusCode, $"Get failed with {getResponse.StatusCode}: {getContent}");
            Assert.Contains(pluginName, getContent);
            Assert.Contains("testKey", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Plugin_UpdatePutThenGet_ReturnsUpdatedPlugin()
    {
        var pluginName = $"test-plugin-{Guid.NewGuid():N}";
        var createRequest = CreatePluginRequest(pluginName);

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName}", createRequest, _jsonOptions);

            var updateRequest = CreatePluginRequest(pluginName, "updatedValue");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName}", updateRequest, _jsonOptions);
            Assert.True(updateResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("updatedValue", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Plugin_UpdatePatchThenGet_ReturnsUpdatedPlugin()
    {
        var pluginName = $"test-plugin-{Guid.NewGuid():N}";
        var createRequest = CreatePluginRequest(pluginName);

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName}", createRequest, _jsonOptions);

            var patchRequest = new
            {
                name = pluginName,
                type = "PluginConfig",
                properties = new
                {
                    config = new Dictionary<string, object>
                    {
                        { "testKey", "patchedValue" },
                        { "newKey", "newValue" }
                    }
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/plugins/{pluginName}", patchContent);
            var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Patch response: {patchResponse.StatusCode}");
            _output.WriteLine($"Patch response body: {patchResponseContent}");
            Assert.True(patchResponse.IsSuccessStatusCode, $"Patch failed with {patchResponse.StatusCode}: {patchResponseContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get response body: {getContent}");

            Assert.Contains("patchedValue", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Plugin_Get_NonExistent_ReturnsNotFound()
    {
        var pluginName = $"non-existent-plugin-{Guid.NewGuid():N}";
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Plugin_DeleteThenGet_ReturnsNotFound()
    {
        var pluginName = $"test-plugin-{Guid.NewGuid():N}";
        var createRequest = CreatePluginRequest(pluginName);

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName}", createRequest, _jsonOptions);

            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent);

            await Task.Delay(500);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Plugin_List_ReturnsPluginsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/plugins");
        Assert.True(getResponse.IsSuccessStatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Plugin_ListAfterCreateTwo_ReturnsBothPlugins()
    {
        var pluginName1 = $"test-plugin-list-{Guid.NewGuid():N}";
        var pluginName2 = $"test-plugin-list-{Guid.NewGuid():N}";

        try
        {
            var createRequest1 = CreatePluginRequest(pluginName1, "value1");
            var createRequest2 = CreatePluginRequest(pluginName2, "value2");

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/plugins/{pluginName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/plugins");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(pluginName1, listContent);
            Assert.Contains(pluginName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/plugins/{pluginName2}");
        }
    }

    #endregion

    #region CommonPrompt Tests

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_CreateThenGet_ReturnsCreatedPrompt()
    {
        var promptName = $"test-prompt-{Guid.NewGuid():N}";
        var request = CreateCommonPromptRequest(promptName, "Test prompt content");

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName}", request, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains(promptName, getContent);
            Assert.Contains("Test prompt content", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_UpdatePutThenGet_ReturnsUpdatedPrompt()
    {
        var promptName = $"test-prompt-{Guid.NewGuid():N}";
        var createRequest = CreateCommonPromptRequest(promptName, "Initial prompt");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName}", createRequest, _jsonOptions);

            var updateRequest = CreateCommonPromptRequest(promptName, "Updated prompt via PUT");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName}", updateRequest, _jsonOptions);
            Assert.True(updateResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("Updated prompt via PUT", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_UpdatePatchThenGet_ReturnsUpdatedPrompt()
    {
        var promptName = $"test-prompt-{Guid.NewGuid():N}";
        var createRequest = CreateCommonPromptRequest(promptName, "Initial prompt");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName}", createRequest, _jsonOptions);

            var patchRequest = new
            {
                name = promptName,
                type = "CommonPrompt",
                properties = new
                {
                    prompt = "Patched prompt content"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/commonprompts/{promptName}", patchContent);
            Assert.True(patchResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("Patched prompt content", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_Get_NonExistent_ReturnsNotFound()
    {
        var promptName = $"non-existent-prompt-{Guid.NewGuid():N}";
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_DeleteThenGet_ReturnsNotFound()
    {
        var promptName = $"test-prompt-{Guid.NewGuid():N}";
        var createRequest = CreateCommonPromptRequest(promptName, "Prompt to be deleted");

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName}", createRequest, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");
            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
            var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete response: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete response body: {deleteContent}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent,
                $"Delete failed with {deleteResponse.StatusCode}: {deleteContent}");

            await Task.Delay(1000);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get after delete response: {getResponse.StatusCode}");
            _output.WriteLine($"Get after delete body: {getContent}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_List_ReturnsPromptsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/commonprompts");
        Assert.True(getResponse.IsSuccessStatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonPrompt_ListAfterCreateTwo_ReturnsBothPrompts()
    {
        var promptName1 = $"test-prompt-list-{Guid.NewGuid():N}";
        var promptName2 = $"test-prompt-list-{Guid.NewGuid():N}";

        try
        {
            var createRequest1 = CreateCommonPromptRequest(promptName1, "First prompt for list test");
            var createRequest2 = CreateCommonPromptRequest(promptName2, "Second prompt for list test");

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commonprompts/{promptName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/commonprompts");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(promptName1, listContent);
            Assert.Contains(promptName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commonprompts/{promptName2}");
        }
    }

    #endregion

    #region CommonToolList Tests

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_CreateThenGet_ReturnsCreatedList()
    {
        var listName = $"test-toollist-{Guid.NewGuid():N}";
        var request = CreateCommonToolListRequest(listName, new[] { "tool1", "tool2" });

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName}", request, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains(listName, getContent);
            Assert.Contains("tool1", getContent);
            Assert.Contains("tool2", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_UpdatePutThenGet_ReturnsUpdatedList()
    {
        var listName = $"test-toollist-{Guid.NewGuid():N}";
        var createRequest = CreateCommonToolListRequest(listName, new[] { "tool1" });

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName}", createRequest, _jsonOptions);

            var updateRequest = CreateCommonToolListRequest(listName, new[] { "updatedTool1", "updatedTool2" });
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName}", updateRequest, _jsonOptions);
            Assert.True(updateResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("updatedTool1", getContent);
            Assert.Contains("updatedTool2", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_UpdatePatchThenGet_ReturnsUpdatedList()
    {
        var listName = $"test-toollist-{Guid.NewGuid():N}";
        var createRequest = CreateCommonToolListRequest(listName, new[] { "tool1" });

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName}", createRequest, _jsonOptions);

            var patchRequest = new
            {
                name = listName,
                type = "CommonToolsList",
                properties = new
                {
                    commonToolsList = new[] { "patchedTool1", "patchedTool2", "patchedTool3" }
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/commontoolslists/{listName}", patchContent);
            Assert.True(patchResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("patchedTool1", getContent);
            Assert.Contains("patchedTool2", getContent);
            Assert.Contains("patchedTool3", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_Get_NonExistent_ReturnsNotFound()
    {
        var listName = $"non-existent-toollist-{Guid.NewGuid():N}";
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_DeleteThenGet_ReturnsNotFound()
    {
        var listName = $"test-toollist-{Guid.NewGuid():N}";
        var createRequest = CreateCommonToolListRequest(listName, new[] { "tool1" });

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName}", createRequest, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");
            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
            var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Delete response: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete response body: {deleteContent}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent,
                $"Delete failed with {deleteResponse.StatusCode}: {deleteContent}");

            await Task.Delay(1000);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Get after delete response: {getResponse.StatusCode}");
            _output.WriteLine($"Get after delete body: {getContent}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_List_ReturnsToolListsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/commontoolslists");
        Assert.True(getResponse.IsSuccessStatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task CommonToolList_ListAfterCreateTwo_ReturnsBothLists()
    {
        var listName1 = $"test-toollist-list-{Guid.NewGuid():N}";
        var listName2 = $"test-toollist-list-{Guid.NewGuid():N}";

        try
        {
            var createRequest1 = CreateCommonToolListRequest(listName1, new[] { "tool1", "tool2" });
            var createRequest2 = CreateCommonToolListRequest(listName2, new[] { "tool3", "tool4" });

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/commontoolslists/{listName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/commontoolslists");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(listName1, listContent);
            Assert.Contains(listName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/commontoolslists/{listName2}");
        }
    }

    #endregion

    #region Skill Tests

    [Fact(Skip = SkipReason)]
    public async Task Skill_CreateThenGet_ReturnsCreatedSkill()
    {
        var skillName = $"test-skill-{Guid.NewGuid():N}";
        var request = CreateSkillRequest(skillName, "Test skill description");

        try
        {
            var createResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName}", request, _jsonOptions);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Create response: {createResponse.StatusCode}");
            _output.WriteLine($"Create response body: {createContent}");

            Assert.True(createResponse.IsSuccessStatusCode, $"Create failed: {createContent}");

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/skills/{skillName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.True(getResponse.IsSuccessStatusCode);
            Assert.Contains(skillName, getContent);
            Assert.Contains("Test skill description", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Skill_UpdatePutThenGet_ReturnsUpdatedSkill()
    {
        var skillName = $"test-skill-{Guid.NewGuid():N}";
        var createRequest = CreateSkillRequest(skillName, "Initial description");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName}", createRequest, _jsonOptions);

            var updateRequest = CreateSkillRequest(skillName, "Updated description via PUT");
            var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName}", updateRequest, _jsonOptions);
            Assert.True(updateResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/skills/{skillName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("Updated description via PUT", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Skill_UpdatePatchThenGet_ReturnsUpdatedSkill()
    {
        var skillName = $"test-skill-{Guid.NewGuid():N}";
        var createRequest = CreateSkillRequest(skillName, "Initial description");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName}", createRequest, _jsonOptions);

            var patchRequest = new
            {
                name = skillName,
                type = "Skill",
                properties = new
                {
                    description = "Patched skill description"
                }
            };

            var patchContent = new StringContent(
                JsonSerializer.Serialize(patchRequest, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var patchResponse = await _httpClient.PatchAsync($"/api/v2/extendedAgent/skills/{skillName}", patchContent);
            Assert.True(patchResponse.IsSuccessStatusCode);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/skills/{skillName}");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            Assert.Contains("Patched skill description", getContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Skill_Get_NonExistent_ReturnsNotFound()
    {
        var skillName = $"non-existent-skill-{Guid.NewGuid():N}";
        var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/skills/{skillName}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Skill_DeleteThenGet_ReturnsNotFound()
    {
        var skillName = $"test-skill-{Guid.NewGuid():N}";
        var createRequest = CreateSkillRequest(skillName, "Skill to be deleted");

        try
        {
            await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName}", createRequest, _jsonOptions);

            var deleteResponse = await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName}");
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.Accepted || deleteResponse.StatusCode == HttpStatusCode.NoContent);

            await Task.Delay(500);

            var getResponse = await _httpClient.GetAsync($"/api/v2/extendedAgent/skills/{skillName}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Cleanup - ensure resource is deleted even if test fails
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName}");
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task Skill_List_ReturnsSkillsList()
    {
        var getResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/skills");
        Assert.True(getResponse.IsSuccessStatusCode);
    }

    [Fact(Skip = SkipReason)]
    public async Task Skill_ListAfterCreateTwo_ReturnsBothSkills()
    {
        var skillName1 = $"test-skill-list-{Guid.NewGuid():N}";
        var skillName2 = $"test-skill-list-{Guid.NewGuid():N}";

        try
        {
            var createRequest1 = CreateSkillRequest(skillName1, "First skill for list test");
            var createRequest2 = CreateSkillRequest(skillName2, "Second skill for list test");

            var createResponse1 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName1}", createRequest1, _jsonOptions);
            Assert.True(createResponse1.IsSuccessStatusCode);

            var createResponse2 = await _httpClient.PutAsJsonAsync($"/api/v2/extendedAgent/skills/{skillName2}", createRequest2, _jsonOptions);
            Assert.True(createResponse2.IsSuccessStatusCode);

            var listResponse = await _httpClient.GetAsync("/api/v2/extendedAgent/skills");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"List response: {listResponse.StatusCode}");
            _output.WriteLine($"List body: {listContent}");

            Assert.True(listResponse.IsSuccessStatusCode);
            Assert.Contains(skillName1, listContent);
            Assert.Contains(skillName2, listContent);
        }
        finally
        {
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName1}");
            await _httpClient.DeleteAsync($"/api/v2/extendedAgent/skills/{skillName2}");
        }
    }

    #endregion

    #region Helper Methods

    private static object CreateAgentRequest(string name, string instructions)
    {
        // Ensure instructions meet the minimum 50 character requirement
        var paddedInstructions = instructions.Length < 50
            ? instructions + new string(' ', 50 - instructions.Length) + " (padding for validation)"
            : instructions;

        return new
        {
            name,
            type = "ExtendedAgent",
            properties = new
            {
                instructions = paddedInstructions,
                handoffDescription = "Test handoff description",
                handoffs = new List<string>(),
                tools = new List<string>(),
                connectors = new List<string>(),
                allowParallelToolCalls = true
            }
        };
    }

    private static object CreateKustoToolRequest(string name, string description)
    {
        return new
        {
            name,
            type = "ExtendedAgentTool",
            properties = new
            {
                type = "KustoTool",
                description,
                connector = "test-connector",
                database = "TestDatabase",
                query = "TestTable | take 10"
            }
        };
    }

    private static object CreateKustoConnectorRequest(string name, string description)
    {
        return new
        {
            name,
            type = "ExtendedAgentConnector",
            properties = new
            {
                type = "Kusto",
                description,
                clusterUrl = "https://test.kusto.windows.net",
                database = "TestDatabase",
                enabled = true
            }
        };
    }

    private static object CreatePluginRequest(string name, string testValue = "testValue")
    {
        return new
        {
            name,
            type = "PluginConfig",
            properties = new
            {
                config = new Dictionary<string, object>
                {
                    { "testKey", testValue }
                }
            }
        };
    }

    private static object CreateCommonPromptRequest(string name, string prompt)
    {
        return new
        {
            name,
            type = "CommonPrompt",
            properties = new
            {
                prompt
            }
        };
    }

    private static object CreateCommonToolListRequest(string name, string[] tools)
    {
        return new
        {
            name,
            type = "CommonToolsList",
            properties = new
            {
                commonToolsList = tools.ToList()
            }
        };
    }

    private static object CreateSkillRequest(string name, string description)
    {
        return new
        {
            name,
            type = "Skill",
            properties = new
            {
                description,
                tools = new List<string>(),
                skillMdContent = "# Test Skill\n\nThis is a test skill."
            }
        };
    }

    #endregion
}
