//// ------------------------------------------------------------
////  Copyright (c) Microsoft Corporation.  All rights reserved.
//// ------------------------------------------------------------

//using System.Text;
//using Agent.Core.Models.Api.v1;
//using Agent.Runtime.Interfaces;
//using Agent.Runtime.Models.ExtendedAgents;
//using Agent.Runtime.Services;
//using Agent.Web.Controllers.v1;
//using Agent.Web.Models.ExtendedAgents;
//using Agent.Web.Models.ExtendedAgents.Response;
//using Agent.Web.Services;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using Moq;

//namespace Agent.Tests.Unit.Controllers.v1;

//public class ExtendedAgentControllerTests
//{
//    private readonly Mock<IExtendedAgentService> _mockExtendedAgentService;
//    private readonly Mock<ILogger<ExtendedAgentController>> _mockLogger;
//    private readonly Mock<IResourceDeploymentService> _mockResourceDeploymentService;
//    private readonly ExtendedAgentController _controller;

//    public ExtendedAgentControllerTests()
//    {
//        _mockExtendedAgentService = new Mock<IExtendedAgentService>();
//        _mockLogger = new Mock<ILogger<ExtendedAgentController>>();
//        _mockResourceDeploymentService = new Mock<IResourceDeploymentService>();

//        _controller = new ExtendedAgentController(_mockExtendedAgentService.Object, _mockLogger.Object, _mockResourceDeploymentService.Object);
//    }

//    #region ApplyAgentConfiguration Tests

//    [Fact]
//    public async Task ApplyAgentConfiguration_ValidYaml_ReturnsAcceptedResult()
//    {
//        // Arrange
//        var validYaml = """
//            agent:
//              name: test_agent
//              system_prompt: This is a test agent for validation purposes
//              tools:
//                - test_tool
//            tools:
//              - name: test_tool
//                type: KustoFunction
//                description: A test tool for validation
//            """;

//        var expectedRuntimeResponse = new Runtime.Models.ExtendedAgents.ExtendedAgentApply
//        {
//            Status = Runtime.Models.ExtendedAgents.ExtendedAgentApplyStatus.Accepted,
//            Message = "Agent and tools deployment initiated",
//            OperationId = "test-operation-id",
//            Timestamp = DateTime.UtcNow,
//            Details = new Runtime.Models.ExtendedAgents.ExtendedAgentApplyDetails
//            {
//                AgentName = "test_agent",
//                ToolsCount = 1,
//                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(2)
//            }
//        };

//        //_mockResourceDeploymentService
//        //    .Setup(s => s.ApplyAsync(It.IsAny<AgentDeploymentModel>()))
//        //    .ReturnsAsync(expectedRuntimeResponse);

//        SetupRequestBody(validYaml);

//        // Act
//        var result = await _controller.ApplyAgentConfiguration(validYaml);

//        // Assert
//        var acceptedResult = Assert.IsType<AcceptedResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentApplyResponse>(acceptedResult.Value);

//        Assert.Equal("Accepted", response.Status);
//        Assert.Equal("Agent and tools deployment initiated", response.Message);
//        Assert.Equal("test-operation-id", response.OperationId);
//        Assert.NotNull(response.Details);
//        Assert.Equal("test_agent", response.Details.AgentName);
//        Assert.Equal(1, response.Details.ToolsCount);

//        // verify service was invoked once
//        Assert.Single(_mockExtendedAgentService.Invocations);
//    }

//    [Fact]
//    public async Task ApplyAgentConfiguration_EmptyBody_ReturnsBadRequest()
//    {
 

//        // Act
//        var result = await _controller.ApplyAgentConfiguration("");

//        // Assert
//        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
//        var errorResponse = Assert.IsType<ExtendedAgentErrorResponse>(badRequestResult.Value);

//        Assert.Equal("error", errorResponse.Status);
//        Assert.Equal("VALIDATION_FAILED", errorResponse.ErrorCode);
//        Assert.Equal("YAML content cannot be empty", errorResponse.Message);
//        Assert.NotNull(errorResponse.Details);
//        Assert.Single(errorResponse.Details.Errors);
//        Assert.Equal("yamlContent", errorResponse.Details.Errors[0].Field);
//        Assert.Equal("YAML content is required", errorResponse.Details.Errors[0].Message);

//        //_mockExtendedAgentService.Verify(
//        //    s => s.ApplyExtendedConfigurationAsync(It.IsAny<AgentConfiguration>()),
//        //    Times.Never);
//    }

//    [Fact]
//    public async Task ApplyAgentConfiguration_InvalidYaml_ReturnsBadRequest()
//    {
//        // Arrange
//        var invalidYaml = """
//            agent:
//              name: test_agent
//              system_prompt: [invalid yaml structure
//            """;

        

//        // Act
//        var result = await _controller.ApplyAgentConfiguration(invalidYaml);

//        // Assert
//        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
//        var errorResponse = Assert.IsType<ExtendedAgentErrorResponse>(badRequestResult.Value);

//        Assert.Equal("error", errorResponse.Status);
//        Assert.Equal("YAML_PARSE_ERROR", errorResponse.ErrorCode);
//        Assert.Equal("Invalid YAML format", errorResponse.Message);

//        //_mockExtendedAgentService.Verify(
//        //    s => s.ApplyExtendedConfigurationAsync(It.IsAny<AgentConfiguration>()),
//        //    Times.Never);
//    }

//    [Fact]
//    public async Task ApplyAgentConfiguration_ServiceThrowsArgumentException_ReturnsBadRequest()
//    {
//        // Arrange
//        var validYaml = """
//            agent:
//              name: test_agent
//              system_prompt: This is a test agent for validation purposes
//              tools:
//                - test_tool
//            tools:
//              - name: test_tool
//                type: KustoFunction
//                description: A test tool for validation
//            """;

//        //_mockExtendedAgentService
//        //    .Setup(s => s.ApplyExtendedConfigurationAsync(It.IsAny<AgentConfiguration>()))
//        //    .ThrowsAsync(new ArgumentException("Invalid configuration"));

        

//        // Act
//        var result = await _controller.ApplyAgentConfiguration(validYaml);

//        // Assert
//        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
//        var errorResponse = Assert.IsType<ExtendedAgentErrorResponse>(badRequestResult.Value);

//        Assert.Equal("error", errorResponse.Status);
//        Assert.Equal("VALIDATION_FAILED", errorResponse.ErrorCode);
//        Assert.Equal("Invalid configuration", errorResponse.Message);
//    }

//    [Fact]
//    public async Task ApplyAgentConfiguration_ServiceThrowsException_ReturnsInternalServerError()
//    {
//        // Arrange
//        var validYaml = """
//            agent:
//              name: test_agent
//              system_prompt: This is a test agent for validation purposes
//              tools:
//                - test_tool
//            tools:
//              - name: test_tool
//                type: KustoFunction
//                description: A test tool for validation
//            """;

//       // _mockExtendedAgentService
//            //.Setup(s => s.ApplyExtendedConfigurationAsync(It.IsAny<AgentConfiguration>()))
//            //.ThrowsAsync(new Exception("Database connection failed"));

//        //SetupRequestBody(validYaml);

//        // Act
//        var result = await _controller.ApplyAgentConfiguration(validYaml);

//        // Assert
//        var internalServerErrorResult = Assert.IsType<ObjectResult>(result.Result);
//        Assert.Equal(500, internalServerErrorResult.StatusCode);

//        var errorResponse = Assert.IsType<ExtendedAgentErrorResponse>(internalServerErrorResult.Value);
//        Assert.Equal("error", errorResponse.Status);
//        Assert.Equal("INTERNAL_ERROR", errorResponse.ErrorCode);
//        Assert.Equal("An internal error occurred while processing the request", errorResponse.Message);
//    }

//    #endregion

//    #region ListAgents Tests

//    [Fact]
//    public async Task ListAgents_DefaultParameters_ReturnsOkResult()
//    {
//        // Arrange
//        var expectedRuntimeResponse = new Runtime.Models.ExtendedAgents.ExtendedAgentsList
//        {
//            Data = new Runtime.Models.ExtendedAgents.ExtendedAgentsListData
//            {
//                Agents = [
//                    new ExtendedAgent
//                {
//                    Name = "agent1",
//                    Instructions = "Test agent 1",
//                    CreatedAt = DateTime.UtcNow
//                },
//                new ExtendedAgent
//                {
//                    Name = "agent2",
//                    Instructions = "Test agent 2",
//                    CreatedAt = DateTime.UtcNow
//                }
//                ],
//                Pagination = new Runtime.Models.ExtendedAgents.Pagination
//                {
//                    TotalCount = 2,
//                    Limit = 50,
//                    Offset = 0,
//                    HasMore = false
//                }
//            },
//            Timestamp = DateTime.UtcNow
//        };

//        _mockExtendedAgentService
//            .Setup(s => s.GetAgentsAsync(50, null))
//            .ReturnsAsync(expectedRuntimeResponse);

//        // Act
//        var result = await _controller.ListAgents();

//        // Assert
//        var okResult = Assert.IsType<OkObjectResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentsListResponse>(okResult.Value);

//        Assert.Equal(2, response.Data.Agents.Count);
//        Assert.Equal("agent1", response.Data.Agents[0].Name);
//        Assert.Equal("agent2", response.Data.Agents[1].Name);
//        Assert.Equal(2, response.Data.Pagination.TotalCount);
//        Assert.False(response.Data.Pagination.HasMore);

//        _mockExtendedAgentService.Verify(s => s.GetAgentsAsync(50, null), Times.Once);
//    }

//    [Fact]
//    public async Task ListAgents_WithCustomParameters_ReturnsOkResult()
//    {
//        // Arrange
//        var expectedRuntimeResponse = new PaginatedList<Agent.Framework.YamlAgentDescriptor>()
//        {
//            Data = new Runtime.Models.ExtendedAgents.ExtendedAgentsListData
//            {
//                Agents = [
//                    new ExtendedAgent
//                    {
//                        Name = "custom_agent",
//                        Instructions = "Custom agent for testing",
//                        CreatedAt = DateTime.UtcNow
//                    }
//                ],
//                Pagination = new Runtime.Models.ExtendedAgents.Pagination
//                {
//                    TotalCount = 1,
//                    Limit = 10,
//                    Offset = 0,
//                    HasMore = false
//                }
//            },
//            Timestamp = DateTime.UtcNow
//        };
//        {
//            Data = new Runtime.Models.ExtendedAgents.ExtendedAgentsListData
//            {
//                Agents = [
//                    new ExtendedAgent
//                    {
//                        Name = "search_agent",
//                        Instructions = "Search-related agent",
//                        CreatedAt = DateTime.UtcNow
//                    }
//                ],
//                Pagination = new Runtime.Models.ExtendedAgents.Pagination
//                {
//                    TotalCount = 1,
//                    Limit = 10,
//                    Offset = 0,
//                    HasMore = false
//                }
//            },
//            Timestamp = DateTime.UtcNow
//        };

//        _mockExtendedAgentService
//            .Setup(s => s.GetAgentsAsync(10, "search"))
//            .ReturnsAsync(expectedRuntimeResponse);

//        // Act
//        var result = await _controller.ListAgents(10, "search");

//        // Assert
//        var okResult = Assert.IsType<OkObjectResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentsListResponse>(okResult.Value);

//        Assert.Single(response.Data.Agents);
//        Assert.Equal("search_agent", response.Data.Agents[0].Name);
//        Assert.Equal(1, response.Data.Pagination.TotalCount);

//        _mockExtendedAgentService.Verify(s => s.GetAgentsAsync(10, "search"), Times.Once);
//    }

//    [Fact]
//    public async Task ListAgents_ServiceThrowsException_ReturnsInternalServerError()
//    {
//        // Arrange
//        _mockExtendedAgentService
//            .Setup(s => s.GetAgentsAsync(It.IsAny<int>(), It.IsAny<string>()))
//            .ThrowsAsync(new Exception("Database connection failed"));

//        // Act
//        var result = await _controller.ListAgents();

//        // Assert
//        var internalServerErrorResult = Assert.IsType<ObjectResult>(result.Result);
//        Assert.Equal(500, internalServerErrorResult.StatusCode);

//        var errorResponse = Assert.IsType<ExtendedAgentErrorResponse>(internalServerErrorResult.Value);
//        Assert.Equal("error", errorResponse.Status);
//        Assert.Equal("INTERNAL_ERROR", errorResponse.ErrorCode);
//        Assert.Equal("An internal error occurred while retrieving agents", errorResponse.Message);
//    }

//    [Fact]
//    public async Task ListAgents_EmptyResult_ReturnsOkResult()
//    {
//        // Arrange
//        var expectedRuntimeResponse = new ExtendedAgentsList
//        {
//            Data = new Runtime.Models.ExtendedAgents.ExtendedAgentsListData
//            {
//                Agents = [],
//                Pagination = new Runtime.Models.ExtendedAgents.Pagination
//                {
//                    TotalCount = 0,
//                    Limit = 50,
//                    Offset = 0,
//                    HasMore = false
//                }
//            },
//            Timestamp = DateTime.UtcNow
//        };

//        _mockExtendedAgentService
//            .Setup(s => s.GetAgentsAsync(50, null))
//            .ReturnsAsync(expectedRuntimeResponse);

//        // Act
//        var result = await _controller.ListAgents();

//        // Assert
//        var okResult = Assert.IsType<OkObjectResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentsListResponse>(okResult.Value);

//        Assert.Empty(response.Data.Agents);
//        Assert.Equal(0, response.Data.Pagination.TotalCount);
//        Assert.False(response.Data.Pagination.HasMore);
//    }

//    #endregion

//    #region ListTools Tests

//    [Fact]
//    public async Task ListTools_DefaultParameters_ReturnsOkResult()
//    {
//        // Arrange
//        var expectedRuntimeResponse = new ExtendedAgentToolsList
//        {
//            Data = new ExtendedAgentToolsListData
//            {
//                Tools = [
//                    new ExtendedAgentTool
//                    {
//                        Name = "tool1",
//                        Type = "KustoFunction",
//                        Description = "Test tool 1",
//                        CreatedAt = DateTime.UtcNow
//                    },
//                    new ExtendedAgentTool
//                    {
//                        Name = "tool2",
//                        Type = "HttpRequest",
//                        Description = "Test tool 2",
//                        CreatedAt = DateTime.UtcNow
//                    }
//                ]
//            },
//            Timestamp = DateTime.UtcNow
//        };

//        _mockExtendedAgentService
//            .Setup(s => s.GetToolsAsync(50, null))
//            .ReturnsAsync(expectedRuntimeResponse);

//        // Act
//        var result = await _controller.ListTools();

//        // Assert
//        var okResult = Assert.IsType<OkObjectResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentToolsResponse>(okResult.Value);

//        Assert.Equal(2, response.Data.Tools.Count);
//        Assert.Equal("tool1", response.Data.Tools[0].Name);
//        Assert.Equal("KustoFunction", response.Data.Tools[0].Type);
//        Assert.Equal("tool2", response.Data.Tools[1].Name);
//        Assert.Equal("HttpRequest", response.Data.Tools[1].Type);

//        _mockExtendedAgentService.Verify(s => s.GetToolsAsync(50, null), Times.Once);
//    }

//    [Fact]
//    public async Task ListTools_WithCustomParameters_ReturnsOkResult()
//    {
//        // Arrange
//        var expectedRuntimeResponse = new Runtime.Models.ExtendedAgents.ExtendedAgentToolsList
//        {
//            Data = new Runtime.Models.ExtendedAgents.ExtendedAgentToolsListData
//            {
//                Tools = [
//                    new ExtendedAgentTool
//                    {
//                        Name = "kusto_tool",
//                        Type = "KustoFunction",
//                        Description = "Kusto-related tool",
//                        CreatedAt = DateTime.UtcNow
//                    }
//                ]
//            },
//            Timestamp = DateTime.UtcNow
//        };

//        _mockExtendedAgentService
//            .Setup(s => s.GetToolsAsync(20, "kusto"))
//            .ReturnsAsync(expectedRuntimeResponse);

//        // Act
//        var result = await _controller.ListTools(20, "kusto");

//        // Assert
//        var okResult = Assert.IsType<OkObjectResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentToolsResponse>(okResult.Value);

//        Assert.Single(response.Data.Tools);
//        Assert.Equal("kusto_tool", response.Data.Tools[0].Name);
//        Assert.Equal("KustoFunction", response.Data.Tools[0].Type);

//        _mockExtendedAgentService.Verify(s => s.GetToolsAsync(20, "kusto"), Times.Once);
//    }

//    [Fact]
//    public async Task ListTools_ServiceThrowsException_ReturnsInternalServerError()
//    {
//        // Arrange
//        _mockExtendedAgentService
//            .Setup(s => s.GetToolsAsync(It.IsAny<int>(), It.IsAny<string>()))
//            .ThrowsAsync(new Exception("Database connection failed"));

//        // Act
//        var result = await _controller.ListTools();

//        // Assert
//        var internalServerErrorResult = Assert.IsType<ObjectResult>(result.Result);
//        Assert.Equal(500, internalServerErrorResult.StatusCode);

//        var errorResponse = Assert.IsType<ExtendedAgentErrorResponse>(internalServerErrorResult.Value);
//        Assert.Equal("error", errorResponse.Status);
//        Assert.Equal("INTERNAL_ERROR", errorResponse.ErrorCode);
//        Assert.Equal("An internal error occurred while retrieving tools", errorResponse.Message);
//    }

//    [Fact]
//    public async Task ListTools_EmptyResult_ReturnsOkResult()
//    {
//        // Arrange
//        var expectedRuntimeResponse = new Runtime.Models.ExtendedAgents.ExtendedAgentToolsList
//        {
//            Data = new Runtime.Models.ExtendedAgents.ExtendedAgentToolsListData
//            {
//                Tools = []
//            },
//            Timestamp = DateTime.UtcNow
//        };

//        _mockExtendedAgentService
//            .Setup(s => s.GetToolsAsync(50, null))
//            .ReturnsAsync(expectedRuntimeResponse);

//        // Act
//        var result = await _controller.ListTools();

//        // Assert
//        var okResult = Assert.IsType<OkObjectResult>(result.Result);
//        var response = Assert.IsType<ExtendedAgentToolsResponse>(okResult.Value);

//        Assert.Empty(response.Data.Tools);
//    }

//    #endregion

//    #region Helper Methods

//    private void SetupRequestBody(string content)
//    {
//        var bytes = Encoding.UTF8.GetBytes(content);
//        var stream = new MemoryStream(bytes);
//        var httpContext = new DefaultHttpContext();
//        httpContext.Request.Body = stream;
//        httpContext.Request.ContentType = "application/yaml";

//        _controller.ControllerContext = new ControllerContext
//        {
//            HttpContext = httpContext
//        };
//    }

//    #endregion
//}
