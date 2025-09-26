using System.Net;
using System.Text;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace Agent.Cli.UnitTests.Services;

// Extended Agents Tests
public partial class ApiServiceTests
{
    [Fact]
    public async Task ListAgentsAsync_WithPaginatedResponse_ShouldReturnFormattedAgentList()
    {
        // Arrange - Use correct format that matches API's expected structure
        var jsonResponse = @"{
            ""data"": [
                {
                    ""name"": ""SRE Incident Handler"",
                    ""handoffDescription"": ""Handles incident management workflows"",
                    ""system_prompt"": ""You are an SRE agent responsible for incident handling"",
                    ""created_at"": ""2024-01-15T10:30:00Z"",
                    ""tools"": [""kusto"", ""azure-monitor""],
                    ""handoffs"": [""escalation""]
                },
                {
                    ""name"": ""Performance Monitor"",
                    ""handoffDescription"": ""Monitors system performance and alerts on anomalies"",
                    ""system_prompt"": ""You are a performance monitoring agent"",
                    ""created_at"": ""2024-01-10T08:15:00Z"",
                    ""tools"": [""metrics"", ""alerting""],
                    ""handoffs"": [""investigation""]
                }
            ],
            ""pagination"": {
                ""total"": 2,
                ""page"": 1,
                ""limit"": 10
            }
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.ListAgentsAsync();

        // Assert
        Success.ShouldBeTrue();
        Response.ShouldNotBeNullOrEmpty();
        Response.ShouldContain("SRE Incident Handler");
        Response.ShouldContain("Performance Monitor");
        Response.ShouldContain("Handles incident management workflows");
        Response.ShouldContain("Monitors system performance");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, @"{""error"": ""Agents not found"", ""success"": false}")]
    [InlineData(HttpStatusCode.InternalServerError, @"{""error"": ""Internal server error"", ""success"": false}")]
    [InlineData(HttpStatusCode.Unauthorized, @"{""error"": ""Authentication required"", ""success"": false}")]
    public async Task ListAgentsAsync_WithErrorResponses_ShouldHandleGracefully(HttpStatusCode statusCode, string errorResponse)
    {
        // Arrange
        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(statusCode, errorResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.ListAgentsAsync();

        // Assert
        Success.ShouldBeFalse();
        Response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAgentNamesAsync_WithSuccessfulResponse_ShouldReturnAgentNames()
    {
        // Arrange - Use correct format that matches API's PaginatedResponse<AgentListItem> structure
        var jsonResponse = @"{
            ""data"": [
                {
                    ""name"": ""SRE Incident Handler"",
                    ""description"": ""Handles incident management workflows""
                },
                {
                    ""name"": ""Performance Monitor"",
                    ""description"": ""Monitors system performance""
                },
                {
                    ""name"": ""Security Auditor"",
                    ""description"": ""Audits security compliance""
                },
                {
                    ""name"": ""Cost Optimizer"",
                    ""description"": ""Optimizes cloud costs""
                },
                {
                    ""name"": ""Backup Manager"",
                    ""description"": ""Manages backup operations""
                }
            ],
            ""pagination"": {
                ""total"": 5,
                ""page"": 1,
                ""limit"": 10
            }
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var result = await apiService.GetAgentNamesAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Names.ShouldNotBeNull();
        result.Names.Count.ShouldBe(5);
        result.Names.ShouldContain("SRE Incident Handler");
        result.Names.ShouldContain("Performance Monitor");
        result.Names.ShouldContain("Security Auditor");
        result.Error.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAgentNamesAsync_WithEmptyResponse_ShouldReturnEmptyList()
    {
        // Arrange - Use correct format that matches API's PaginatedResponse<AgentListItem> structure
        var jsonResponse = @"{
            ""data"": [],
            ""pagination"": {
                ""total"": 0,
                ""page"": 1,
                ""limit"": 10
            }
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Names, Error) = await apiService.GetAgentNamesAsync();

        // Assert
        Success.ShouldBeTrue();
        Names.ShouldNotBeNull();
        Names.Count.ShouldBe(0);
        Error.ShouldBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApplyAgentAsync_WithInvalidAgentName_ReturnsError(string agentName)
    {
        // Arrange - Create a temporary test environment
        var testDirectory = Path.Combine(Path.GetTempPath(), $"ApiServiceTest_{Guid.NewGuid():N}");
        var agentsDirectory = Path.Combine(testDirectory, "agents");

        // TODO: Convert this to Mock.
        Directory.CreateDirectory(testDirectory);
        Directory.CreateDirectory(agentsDirectory);

        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            // Change to test directory
            Directory.SetCurrentDirectory(testDirectory);

            // Act
            var (Success, Response) = await _apiService.ApplyAgentAsync(agentName);

            // Assert
            Assert.False(Success);
            Assert.Contains("Agent file not found", Response);
        }
        finally
        {
            // Cleanup
            Directory.SetCurrentDirectory(originalDirectory);
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    [Fact]
    public async Task ApplyAgentAsync_WithValidAgentFile_ShouldProcessFile()
    {
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"ApiServiceTest_{Guid.NewGuid():N}");
        var agentsDirectory = Path.Combine(testDirectory, "agents");
        var agentName = "test-agent";
        var agentSubDirectory = Path.Combine(agentsDirectory, agentName);
        var agentFile = Path.Combine(agentSubDirectory, $"{agentName}.yaml");

        var agentContent = @"
api_version: azuresre.ai/v1
kind: AgentConfiguration
spec:
  name: test-agent
  system_prompt: You are a helpful assistant designed to assist with various tasks and provide accurate information to users in a friendly and professional manner.
  tools:
    - kusto
    - azure-monitor
  handoffs:
    - meta_agent
";

        Directory.CreateDirectory(testDirectory);
        Directory.CreateDirectory(agentsDirectory);
        Directory.CreateDirectory(agentSubDirectory);
        await File.WriteAllTextAsync(agentFile, agentContent);

        var originalDirectory = Directory.GetCurrentDirectory();

        // Setup HTTP mock for the API call that happens after file is found
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""success"": true, ""message"": ""Agent applied successfully""}", Encoding.UTF8, "application/json")
            });

        try
        {
            // Change to test directory so ApiService can find the agent file
            Directory.SetCurrentDirectory(testDirectory);

            // Act
            var (Success, Response) = await _apiService.ApplyAgentAsync(agentName);

            // Assert
            // The method should find the file and proceed to make HTTP call
            Success.ShouldBeTrue();
            Response.ShouldBe("✅ Agent 'test-agent' applied successfully!");
        }
        finally
        {
            // Cleanup
            Directory.SetCurrentDirectory(originalDirectory);
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteAgentAsync_WithInvalidAgentName_ReturnsError(string agentName)
    {
        // Act
        var (Success, Response) = await _apiService.DeleteAgentAsync(agentName);

        // Assert
        Assert.False(Success);
    }

    [Fact]
    public async Task DeleteAgentAsync_WithValidAgentName_ShouldReturnSuccess()
    {
        // Arrange
        var agentName = "SRE Incident Handler";
        var jsonResponse = @"{
            ""message"": ""Agent 'SRE Incident Handler' deleted successfully"",
            ""agent_id"": ""agent-123"",
            ""deleted_at"": ""2024-01-25T12:00:00Z"",
            ""success"": true
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.DeleteAgentAsync(agentName);

        // Assert
        Success.ShouldBeTrue();
        Response.ShouldContain("deleted successfully");
        Response.ShouldContain(agentName);
    }

    [Fact]
    public async Task DeleteAgentAsync_WithNonExistentAgent_ShouldReturnError()
    {
        // Arrange
        var agentName = "NonExistentAgent";
        var jsonResponse = @"{
            ""error"": ""Agent 'NonExistentAgent' not found"",
            ""error_code"": ""AGENT_NOT_FOUND"",
            ""success"": false,
            ""timestamp"": ""2024-01-25T12:00:00Z""
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.NotFound, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.DeleteAgentAsync(agentName);

        // Assert
        Success.ShouldBeFalse();
        Response.ShouldContain("not found");
        Response.ShouldContain(agentName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAgentConfigurationAsync_WithInvalidAgentName_ReturnsError(string agentName)
    {
        // Act
        var (Success, YamlContent, ErrorMessage) = await _apiService.GetAgentConfigurationAsync(agentName);

        // Assert
        Assert.False(Success);
    }

    [Fact]
    public async Task GetAgentConfigurationAsync_WithValidAgent_ShouldReturnConfiguration()
    {
        // Arrange
        var agentName = "SRE Incident Handler";

        var jsonResponse = @"{
            ""agents"": [
                {
                    ""name"": ""SRE Incident Handler"",
                    ""instructions"": ""You are an SRE incident handler responsible for managing and resolving system incidents efficiently and effectively."",
                    ""handoffDescription"": ""Handles incident management workflows"",
                    ""tools"": [""kusto"", ""azure-monitor""],
                    ""handoffs"": [""escalation""],
                    ""mcpTools"": [],
                    ""connectors"": [],
                    ""allowParallelToolCalls"": false,
                    ""agentsAsTools"": [],
                    ""maxReflectionCount"": 0,
                    ""commonPrompts"": [],
                    ""commonTools"": [],
                    ""agentType"": 0,
                    ""orchestrationStartAgents"": [],
                    ""nextAgentMappings"": []
                }
            ]
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, YamlContent, ErrorMessage) = await apiService.GetAgentConfigurationAsync(agentName);

        _output.WriteLine($"YAML Content: {YamlContent}");
        _output.WriteLine($"Error: {ErrorMessage}");

        // Assert
        Success.ShouldBeTrue();
        YamlContent.ShouldNotBeNullOrEmpty();
        YamlContent.ShouldContain("name: SRE Incident Handler");
        YamlContent.ShouldContain("tools:");
        YamlContent.ShouldContain("- kusto");
        ErrorMessage.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAgentConfigurationAsync_WithAgentsFormatResponse_ShouldReturnConfiguration()
    {
        // Arrange
        var agentName = "Performance Monitor";

        // Test the "agents" format that GetAgentConfigurationAsync also supports
        var jsonResponse = @"{
            ""agents"": [
                {
                    ""name"": ""Performance Monitor"",
                    ""instructions"": ""You are a performance monitoring agent responsible for tracking system metrics and alerting on anomalies."",
                    ""handoffDescription"": ""Monitors system performance and alerts on anomalies"",
                    ""tools"": [""metrics"", ""alerting""],
                    ""handoffs"": [""investigation""],
                    ""mcpTools"": [],
                    ""connectors"": [],
                    ""allowParallelToolCalls"": false,
                    ""agentsAsTools"": [],
                    ""maxReflectionCount"": 0,
                    ""commonPrompts"": [],
                    ""commonTools"": [],
                    ""agentType"": 0,
                    ""orchestrationStartAgents"": [],
                    ""nextAgentMappings"": []
                }
            ]
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, YamlContent, ErrorMessage) = await apiService.GetAgentConfigurationAsync(agentName);

        _output.WriteLine($"YAML Content: {YamlContent}");
        _output.WriteLine($"Error: {ErrorMessage}");

        // Assert
        Success.ShouldBeTrue();
        YamlContent.ShouldNotBeNullOrEmpty();
        YamlContent.ShouldContain("name: Performance Monitor");
        YamlContent.ShouldContain("tools:");
        YamlContent.ShouldContain("- metrics");
        YamlContent.ShouldContain("- alerting");
        ErrorMessage.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAgentConfigurationAsync_WithDataFormatResponse_ShouldReturnConfiguration()
    {
        // Arrange
        var agentName = "Performance Monitor";

        // Create JSON that matches what the controller actually returns: PaginatedResponse<ExtendedAgentApiModel>
        // The controller converts PaginatedList to PaginatedResponse and returns that
        var jsonResponse = @"{
            ""data"": [
                {
                    ""name"": ""Performance Monitor"",
                    ""instructions"": ""You are a performance monitoring agent responsible for tracking system metrics and alerting on anomalies."",
                    ""handoffDescription"": ""Monitors system performance and alerts on anomalies"",
                    ""tools"": [""metrics"", ""alerting""],
                    ""handoffs"": [""investigation""],
                    ""mcpTools"": [],
                    ""connectors"": [],
                    ""allowParallelToolCalls"": false,
                    ""agentsAsTools"": [],
                    ""maxReflectionCount"": 0,
                    ""commonPrompts"": [],
                    ""commonTools"": [],
                    ""agentType"": 0,
                    ""orchestrationStartAgents"": [],
                    ""nextAgentMappings"": []
                }
            ],
            ""page_index"": 0,
            ""total_pages"": 1,
            ""page_size"": 50,
            ""total_count"": 1,
            ""has_previous_page"": false,
            ""has_next_page"": false
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, YamlContent, ErrorMessage) = await apiService.GetAgentConfigurationAsync(agentName);

        _output.WriteLine($"YAML Content: {YamlContent}");
        _output.WriteLine($"Error: {ErrorMessage}");

        // Assert
        Success.ShouldBeTrue();
        YamlContent.ShouldNotBeNullOrEmpty();
        YamlContent.ShouldContain("name: Performance Monitor");
        YamlContent.ShouldContain("tools:");
        YamlContent.ShouldContain("- metrics");
        YamlContent.ShouldContain("- alerting");
        ErrorMessage.ShouldBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApplyToolAsync_WithInvalidToolName_ReturnsError(string toolName)
    {
        // Act
        var (Success, Response) = await _apiService.ApplyToolAsync(toolName);

        // Assert
        Assert.False(Success);
        Assert.Contains("Tool file not found", Response);
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApplyYamlFileAsync_WithNullOrEmptyPath_ReturnsError(string filePath)
    {
        // Act
        var (Success, _) = await _apiService.ApplyYamlFileAsync(filePath);

        // Assert
        Assert.False(Success);
    }

    [Fact]
    public async Task ApplyYamlFileAsync_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        var yamlContent = @"
spec:
  agent:
    name: Test Agent
    instructions: You are a test agent
    tools:
      - kusto
      - metrics
    handoffs:
      - escalation";

        await File.WriteAllTextAsync(tempFilePath, yamlContent);

        var expectedResponse = "Agent applied successfully";
        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        try
        {
            // Act
            var (Success, Response) = await apiService.ApplyYamlFileAsync(tempFilePath);

            // Assert
            Success.ShouldBeTrue();
            Response.ShouldContain("applied successfully");

            // Verify the request was made correctly
            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put &&
                    req.RequestUri!.ToString().Contains("/api/v1/extendedAgent/apply") &&
                    req.Content!.Headers.ContentType!.MediaType == "application/yaml"
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task ApplyYamlFileAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentFilePath = "non-existent-file.yaml";
        var mockHandler = new Mock<HttpMessageHandler>();
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.ApplyYamlFileAsync(nonExistentFilePath);

        // Assert
        Success.ShouldBeFalse();
        Response.ShouldContain("YAML file not found");
        Response.ShouldContain(nonExistentFilePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("   \n\t   ")]
    public async Task ApplyYamlFileAsync_WithEmptyFile_ShouldReturnFailure(string fileContent)
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, fileContent);

        var mockHandler = new Mock<HttpMessageHandler>();
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        try
        {
            // Act
            var (Success, Response) = await apiService.ApplyYamlFileAsync(tempFilePath);

            // Assert
            Success.ShouldBeFalse();
            Response.ShouldContain("YAML file is empty");
            Response.ShouldContain(tempFilePath);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task ApplyYamlFileAsync_WithServerError_ShouldReturnFailure()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        var yamlContent = @"
spec:
  agent:
    name: Invalid Agent
    invalid_field: bad_value";

        await File.WriteAllTextAsync(tempFilePath, yamlContent);

        var errorResponse = "Validation failed: Invalid agent configuration";
        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.BadRequest, errorResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        try
        {
            // Act
            var (Success, Response) = await apiService.ApplyYamlFileAsync(tempFilePath);

            // Assert
            Success.ShouldBeFalse();
            Response.ShouldContain("Failed to apply YAML file");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApplyYamlFileAsync_WithInvalidFilePath_ShouldReturnFailure(string filePath)
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.ApplyYamlFileAsync(filePath);

        // Assert
        Success.ShouldBeFalse();
        Response.ShouldContain("YAML file not found");
    }

    [Fact]
    public async Task ListToolsAsync_WithSuccessfulResponse_ShouldReturnFormattedToolList()
    {
        // Arrange - Use format that ToolResponseParser can understand
        var jsonResponse = @"{
            ""tools"": [
                {
                    ""id"": ""tool-001"",
                    ""name"": ""Azure Resource Monitor"",
                    ""description"": ""Monitors Azure resource health and performance metrics"",
                    ""version"": ""2.1.0"",
                    ""category"": ""monitoring"",
                    ""status"": ""active"",
                    ""created_at"": ""2024-01-15T10:30:00Z"",
                    ""updated_at"": ""2024-01-20T14:45:00Z"",
                    ""capabilities"": [""resource_health"", ""performance_metrics"", ""alerting""],
                    ""supported_resources"": [""virtual_machines"", ""storage_accounts"", ""databases""],
                    ""configuration"": {
                        ""polling_interval"": 60,
                        ""retention_days"": 30,
                        ""alert_thresholds"": {
                            ""cpu_usage"": 80,
                            ""memory_usage"": 85,
                            ""disk_usage"": 90
                        }
                    }
                },
                {
                    ""id"": ""tool-002"",
                    ""name"": ""Kusto Query Executor"",
                    ""description"": ""Executes KQL queries against Azure Data Explorer and Log Analytics"",
                    ""version"": ""1.5.3"",
                    ""category"": ""data_analysis"",
                    ""status"": ""active"",
                    ""created_at"": ""2024-01-10T08:15:00Z"",
                    ""updated_at"": ""2024-01-18T16:22:00Z"",
                    ""capabilities"": [""kql_execution"", ""data_visualization"", ""export""],
                    ""supported_sources"": [""azure_data_explorer"", ""log_analytics"", ""application_insights""]
                }
            ],
            ""pagination"": {
                ""total"": 2,
                ""page"": 1,
                ""limit"": 10,
                ""has_next"": false
            },
            ""success"": true,
            ""timestamp"": ""2024-01-25T12:00:00Z""
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.ListToolsAsync();

        // Assert
        Success.ShouldBeTrue();
        Response.ShouldNotBeNullOrEmpty();
        Response.ShouldContain("Azure Resource Monitor");
        Response.ShouldContain("Kusto Query Executor");
        // Note: Test expects formatted output, not raw JSON IDs
    }

    [Fact]
    public async Task GetToolNamesAsync_WithSuccessfulResponse_ShouldReturnToolNames()
    {
        // Arrange - Use correct format that matches API's PaginatedResponse<ToolListItem> structure
        var jsonResponse = @"{
            ""data"": [
                {
                    ""name"": ""Tool 1"",
                    ""description"": ""Tool 1""
                },
                {
                    ""name"": ""Tool 2"",
                    ""description"": ""Tool 2""
                }
            ],
            ""pagination"": {
                ""total"": 2,
                ""page"": 1,
                ""limit"": 10
            }
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Names, Error) = await apiService.GetToolNamesAsync();

        // Assert
        Success.ShouldBeTrue();
        Names.ShouldNotBeNull();
        Names.Count.ShouldBe(2);
        Names.ShouldContain("Tool 1");
        Names.ShouldContain("Tool 2");
        Error.ShouldBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteToolAsync_WithInvalidToolName_ReturnsError(string toolName)
    {
        // Act
        var (Success, Response) = await _apiService.DeleteToolAsync(toolName);

        // Assert
        Assert.False(Success);
    }

    [Fact]
    public async Task DeleteToolAsync_WithValidToolName_ShouldReturnSuccess()
    {
        // Arrange
        var toolName = "Azure Resource Monitor";
        var jsonResponse = @"{
            ""message"": ""Tool 'Azure Resource Monitor' deleted successfully"",
            ""tool_id"": ""tool-001"",
            ""deleted_at"": ""2024-01-25T12:00:00Z"",
            ""success"": true
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.DeleteToolAsync(toolName);

        // Assert
        Success.ShouldBeTrue();
        Response.ShouldContain("deleted successfully");
        Response.ShouldContain(toolName);
    }

    [Fact]
    public async Task DeleteToolAsync_WithToolInUse_ShouldReturnError()
    {
        // Arrange
        var toolName = "Azure Resource Monitor";
        // Use the correct property name that the API expects: "dependentAgents"
        var jsonResponse = @"{
            ""error"": ""Cannot delete tool 'Azure Resource Monitor': it is being used by agents"",
            ""error_code"": ""TOOL_IN_USE"",
            ""dependentAgents"": [
                ""SRE Incident Handler"",
                ""Performance Monitor""
            ],
            ""success"": false,
            ""timestamp"": ""2024-01-25T12:00:00Z""
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.Conflict, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, Response) = await apiService.DeleteToolAsync(toolName);

        // Assert
        Success.ShouldBeFalse();
        Response.ShouldContain("used by the following agents");
        Response.ShouldContain("SRE Incident Handler");
        Response.ShouldContain("Performance Monitor");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetToolConfigurationAsync_WithInvalidToolName_ReturnsError(string toolName)
    {
        // Act
        var (Success, YamlContent, ErrorMessage) = await _apiService.GetToolConfigurationAsync(toolName);

        // Assert
        Assert.False(Success);
    }

    [Fact]
    public async Task GetToolConfigurationAsync_WithValidTool_ShouldReturnConfiguration()
    {
        // Arrange
        var toolName = "Azure Resource Monitor";
        // Use correct format that matches API's expectations - array with tool objects containing 'name' property
        var jsonResponse = @"{
            ""data"": [
                {
                    ""name"": ""Azure Resource Monitor"",
                    ""version"": ""2.1.0"",
                    ""description"": ""Monitors Azure resource health and performance metrics"",
                    ""category"": ""monitoring"",
                    ""settings"": {
                        ""polling_interval_seconds"": 60,
                        ""retention_days"": 30,
                        ""max_concurrent_queries"": 50,
                        ""timeout_seconds"": 300,
                        ""alert_thresholds"": {
                            ""cpu_usage_percent"": 80,
                            ""memory_usage_percent"": 85,
                            ""disk_usage_percent"": 90,
                            ""network_latency_ms"": 1000
                        }
                    },
                    ""capabilities"": [""resource_health"", ""performance_metrics"", ""alerting""],
                    ""supported_resources"": [
                        ""Microsoft.Compute/virtualMachines"",
                        ""Microsoft.Storage/storageAccounts""
                    ],
                    ""dependencies"": [
                        {
                            ""name"": ""Azure Monitor API"",
                            ""version"": "">=2023-01-01""
                        }
                    ]
                }
            ],
            ""pagination"": {
                ""total"": 1,
                ""page"": 1,
                ""limit"": 10
            }
        }";

        var mockHandler = TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(mockHandler);

        // Act
        var (Success, YamlContent, ErrorMessage) = await apiService.GetToolConfigurationAsync(toolName);

        // Assert
        Success.ShouldBeTrue();
        YamlContent.ShouldNotBeNullOrEmpty();
        ErrorMessage.ShouldBeNullOrEmpty();
    }
}
