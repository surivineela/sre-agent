// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.ScheduledTasks.Services;
using Agent.Web.Controllers.v1;
using Agent.Web.Models.ScheduledTasks.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Controllers.v1
{
    public class ScheduledTasksControllerTests
    {
        private readonly Mock<IScheduledTaskManagementService> _mockScheduledTaskService;
        private readonly Mock<ILogger<ScheduledTasksController>> _mockLogger;
        private readonly Mock<IChatClientProvider> _mockChatClientProvider;
        private readonly ScheduledTasksController _controller;

        public ScheduledTasksControllerTests()
        {
            _mockScheduledTaskService = new Mock<IScheduledTaskManagementService>();
            _mockLogger = new Mock<ILogger<ScheduledTasksController>>();
            _mockChatClientProvider = new Mock<IChatClientProvider>();

            _controller = new ScheduledTasksController(
                _mockScheduledTaskService.Object,
                _mockLogger.Object,
                _mockChatClientProvider.Object);
        }

        #region ExecuteTaskNow Tests

        [Fact]
        public async Task ExecuteTaskNow_TaskExists_ReturnsOkWithExecution()
        {
            // Arrange
            var taskId = "task-123";
            var executionTime = DateTime.UtcNow;
            var execution = new ScheduledTaskExecution(
                ExecutionTime: executionTime,
                ThreadId: "thread-456",
                Success: true,
                ErrorMessage: null,
                ExecutionMetadata: new Dictionary<string, object>
                {
                    ["ManualExecution"] = true,
                    ["TaskId"] = taskId,
                    ["TaskName"] = "Test Task"
                }
            );

            _mockScheduledTaskService
                .Setup(x => x.ExecuteTaskNow(taskId))
                .ReturnsAsync(execution);

            // Act
            var result = await _controller.ExecuteTaskNow(taskId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            _mockScheduledTaskService.Verify(
                x => x.ExecuteTaskNow(taskId),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteTaskNow_TaskNotFound_ReturnsNotFound()
        {
            // Arrange
            var taskId = "task-123";

            _mockScheduledTaskService
                .Setup(x => x.ExecuteTaskNow(taskId))
                .ReturnsAsync((ScheduledTaskExecution?)null);

            // Act
            var result = await _controller.ExecuteTaskNow(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.NotNull(notFoundResult.Value);
        }

        [Fact]
        public async Task ExecuteTaskNow_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var taskId = "task-123";

            _mockScheduledTaskService
                .Setup(x => x.ExecuteTaskNow(taskId))
                .ThrowsAsync(new Exception("Execution error"));

            // Act
            var result = await _controller.ExecuteTaskNow(taskId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        #endregion

        #region GetAllExecutions Tests

        [Fact]
        public async Task GetAllExecutions_NoFilters_ReturnsAllExecutions()
        {
            // Arrange
            var executions = new List<TaskExecutionSummary>
            {
                new TaskExecutionSummary(
                    "task-1",
                    "Task 1",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-1),
                        "thread-1",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                ),
                new TaskExecutionSummary(
                    "task-2",
                    "Task 2",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-2),
                        "thread-2",
                        false,
                        "Error occurred",
                        new Dictionary<string, object>()
                    )
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetAllExecutions(null, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            _mockScheduledTaskService.Verify(
                x => x.GetAllExecutions(),
                Times.Once);
        }

        [Fact]
        public async Task GetAllExecutions_WithSuccessFilter_ReturnsFilteredExecutions()
        {
            // Arrange
            var executions = new List<TaskExecutionSummary>
            {
                new TaskExecutionSummary(
                    "task-1",
                    "Task 1",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-1),
                        "thread-1",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                ),
                new TaskExecutionSummary(
                    "task-2",
                    "Task 2",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-2),
                        "thread-2",
                        false,
                        "Error occurred",
                        new Dictionary<string, object>()
                    )
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetAllExecutions(null, true, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<List<ScheduledTaskExecutionWithTaskInfoResponse>>(okResult.Value);
            Assert.Single(resultValue); // Only successful executions
        }

        [Fact]
        public async Task GetAllExecutions_WithSinceFilter_ReturnsFilteredExecutions()
        {
            // Arrange
            var since = DateTime.UtcNow.AddHours(-1.5);
            var executions = new List<TaskExecutionSummary>
            {
                new TaskExecutionSummary(
                    "task-1",
                    "Task 1",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-1),
                        "thread-1",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                ),
                new TaskExecutionSummary(
                    "task-2",
                    "Task 2",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-2),
                        "thread-2",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetAllExecutions(null, null, since);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<List<ScheduledTaskExecutionWithTaskInfoResponse>>(okResult.Value);
            Assert.Single(resultValue); // Only recent executions
        }

        [Fact]
        public async Task GetAllExecutions_WithLimit_ReturnsLimitedExecutions()
        {
            // Arrange
            var executions = new List<TaskExecutionSummary>
            {
                new TaskExecutionSummary(
                    "task-1",
                    "Task 1",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-1),
                        "thread-1",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                ),
                new TaskExecutionSummary(
                    "task-2",
                    "Task 2",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-2),
                        "thread-2",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                ),
                new TaskExecutionSummary(
                    "task-3",
                    "Task 3",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow.AddHours(-3),
                        "thread-3",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetAllExecutions(2, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<List<ScheduledTaskExecutionWithTaskInfoResponse>>(okResult.Value);
            Assert.Equal(2, resultValue.Count); // Limited to 2
        }

        [Fact]
        public async Task GetAllExecutions_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllExecutions(null, null, null);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GetAllExecutions_EmptyList_ReturnsEmptyList()
        {
            // Arrange
            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(new List<TaskExecutionSummary>());

            // Act
            var result = await _controller.GetAllExecutions(null, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<List<ScheduledTaskExecutionWithTaskInfoResponse>>(okResult.Value);
            Assert.Empty(resultValue);
        }

        #endregion

        #region Input Validation Tests

        [Fact]
        public async Task ExecuteTaskNow_WithEmptyTaskId_ReturnsBadRequest()
        {
            // Arrange
            var taskId = "";

            // Act
            var result = await _controller.ExecuteTaskNow(taskId);

            // Assert - Should return BadRequest for empty ID
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task ExecuteTaskNow_WithWhitespaceTaskId_ReturnsBadRequest()
        {
            // Arrange
            var taskId = "   ";

            // Act
            var result = await _controller.ExecuteTaskNow(taskId);

            // Assert - Should return BadRequest for whitespace ID
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task GetAllExecutions_WithNegativeLimit_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetAllExecutions(-1, null, null);

            // Assert - Negative limit should return BadRequest
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task GetAllExecutions_WithExcessiveLimit_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetAllExecutions(10001, null, null);

            // Assert - Excessive limit should return BadRequest
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task GetAllExecutions_WithZeroLimit_ReturnsAllExecutions()
        {
            // Arrange
            var executions = new List<TaskExecutionSummary>
            {
                new TaskExecutionSummary(
                    "task-1",
                    "Task 1",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow,
                        "thread-1",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetAllExecutions(0, null, null);

            // Assert - Zero limit is ignored, all results returned
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<List<ScheduledTaskExecutionWithTaskInfoResponse>>(okResult.Value);
            Assert.Single(resultValue);
        }

        [Fact]
        public async Task GetAllExecutions_WithFutureDate_ReturnsEmptyList()
        {
            // Arrange
            var futureDate = DateTime.UtcNow.AddDays(1);
            var executions = new List<TaskExecutionSummary>
            {
                new TaskExecutionSummary(
                    "task-1",
                    "Task 1",
                    new ScheduledTaskExecution(
                        DateTime.UtcNow,
                        "thread-1",
                        true,
                        null,
                        new Dictionary<string, object>()
                    )
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetAllExecutions())
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetAllExecutions(null, null, futureDate);

            // Assert - Future date filters out all executions
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<List<ScheduledTaskExecutionWithTaskInfoResponse>>(okResult.Value);
            Assert.Empty(resultValue);
        }

        #endregion

        #region GetTaskExecutionHistory Tests

        [Fact]
        public async Task GetTaskExecutionHistory_TaskExists_ReturnsExecutionHistory()
        {
            // Arrange
            var taskId = "task-123";
            var executions = new List<ScheduledTaskExecution>
            {
                new ScheduledTaskExecution(
                    DateTime.UtcNow.AddHours(-1),
                    "thread-1",
                    true,
                    null,
                    new Dictionary<string, object>()
                ),
                new ScheduledTaskExecution(
                    DateTime.UtcNow.AddHours(-2),
                    "thread-2",
                    false,
                    "Error occurred",
                    new Dictionary<string, object>()
                )
            };

            _mockScheduledTaskService
                .Setup(x => x.GetTaskExecutionHistory(taskId))
                .ReturnsAsync(executions);

            // Act
            var result = await _controller.GetTaskExecutionHistory(taskId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            _mockScheduledTaskService.Verify(
                x => x.GetTaskExecutionHistory(taskId),
                Times.Once);
        }

        [Fact]
        public async Task GetTaskExecutionHistory_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var taskId = "task-123";

            _mockScheduledTaskService
                .Setup(x => x.GetTaskExecutionHistory(taskId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetTaskExecutionHistory(taskId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        #endregion

        #region CreateScheduledTask AgentMode Tests

        [Fact]
        public async Task CreateScheduledTask_WithValidAgentMode_CreatesTaskWithSpecifiedMode()
        {
            // Arrange
            var request = new CreateScheduledTaskApiRequest(
                Name: "Test Task",
                Description: "Test Description",
                CronExpression: "0 0 * * *",
                AgentPrompt: "Test prompt",
                AgentMode: "readonly"
            );

            var createdTask = new ScheduledTaskDocument(
                Id: "task-123",
                Name: request.Name,
                Description: request.Description,
                CronExpression: request.CronExpression,
                StartTime: DateTime.UtcNow,
                EndTime: null,
                AgentPrompt: request.AgentPrompt,
                Agent: null,
                ThreadId: null,
                CreatedBy: "api",
                CreatedAt: DateTime.UtcNow,
                LastExecutionTime: null,
                Status: ScheduledTaskStatus.Active,
                ExecutionContext: null,
                ExecutionHistory: new List<ScheduledTaskExecution>(),
                MaxExecutions: null,
                ExecutionCount: 0,
                NotificationChannel: null,
                AgentMode: "readonly"
            );

            _mockScheduledTaskService
                .Setup(x => x.CreateScheduledTask(It.Is<CreateScheduledTaskRequest>(r => r.AgentMode == "readonly")))
                .ReturnsAsync(createdTask);

            // Act
            var result = await _controller.CreateScheduledTask(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdAtActionResult.StatusCode);

            _mockScheduledTaskService.Verify(
                x => x.CreateScheduledTask(It.Is<CreateScheduledTaskRequest>(r => r.AgentMode == "readonly")),
                Times.Once);
        }

        [Fact]
        public async Task CreateScheduledTask_WithoutAgentMode_DefaultsToAutonomous()
        {
            // Arrange
            var request = new CreateScheduledTaskApiRequest(
                Name: "Test Task",
                Description: "Test Description",
                CronExpression: "0 0 * * *",
                AgentPrompt: "Test prompt"
            );

            var createdTask = new ScheduledTaskDocument(
                Id: "task-123",
                Name: request.Name,
                Description: request.Description,
                CronExpression: request.CronExpression,
                StartTime: DateTime.UtcNow,
                EndTime: null,
                AgentPrompt: request.AgentPrompt,
                Agent: null,
                ThreadId: null,
                CreatedBy: "api",
                CreatedAt: DateTime.UtcNow,
                LastExecutionTime: null,
                Status: ScheduledTaskStatus.Active,
                ExecutionContext: null,
                ExecutionHistory: new List<ScheduledTaskExecution>(),
                MaxExecutions: null,
                ExecutionCount: 0,
                NotificationChannel: null,
                AgentMode: AgentModes.Autonomous.ToLowerInvariant()
            );

            _mockScheduledTaskService
                .Setup(x => x.CreateScheduledTask(It.Is<CreateScheduledTaskRequest>(
                    r => r.AgentMode == AgentModes.Autonomous.ToLowerInvariant())))
                .ReturnsAsync(createdTask);

            // Act
            var result = await _controller.CreateScheduledTask(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdAtActionResult.StatusCode);

            _mockScheduledTaskService.Verify(
                x => x.CreateScheduledTask(It.Is<CreateScheduledTaskRequest>(
                    r => r.AgentMode == AgentModes.Autonomous.ToLowerInvariant())),
                Times.Once);
        }

        [Fact]
        public async Task CreateScheduledTask_WithInvalidAgentMode_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateScheduledTaskApiRequest(
                Name: "Test Task",
                Description: "Test Description",
                CronExpression: "0 0 * * *",
                AgentPrompt: "Test prompt",
                AgentMode: "invalid_mode"
            );

            // Act
            var result = await _controller.CreateScheduledTask(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var errorResponse = badRequestResult.Value;
            Assert.NotNull(errorResponse);

            // Verify service was never called
            _mockScheduledTaskService.Verify(
                x => x.CreateScheduledTask(It.IsAny<CreateScheduledTaskRequest>()),
                Times.Never);
        }

        [Theory]
        [InlineData("readonly")]
        [InlineData("review")]
        [InlineData("autonomous")]
        [InlineData("ReadOnly")]
        [InlineData("REVIEW")]
        [InlineData("Autonomous")]
        public async Task CreateScheduledTask_WithValidAgentModes_CaseInsensitive_Succeeds(string agentMode)
        {
            // Arrange
            var request = new CreateScheduledTaskApiRequest(
                Name: "Test Task",
                Description: "Test Description",
                CronExpression: "0 0 * * *",
                AgentPrompt: "Test prompt",
                AgentMode: agentMode
            );

            var createdTask = new ScheduledTaskDocument(
                Id: "task-123",
                Name: request.Name,
                Description: request.Description,
                CronExpression: request.CronExpression,
                StartTime: DateTime.UtcNow,
                EndTime: null,
                AgentPrompt: request.AgentPrompt,
                Agent: null,
                ThreadId: null,
                CreatedBy: "api",
                CreatedAt: DateTime.UtcNow,
                LastExecutionTime: null,
                Status: ScheduledTaskStatus.Active,
                ExecutionContext: null,
                ExecutionHistory: new List<ScheduledTaskExecution>(),
                MaxExecutions: null,
                ExecutionCount: 0,
                NotificationChannel: null,
                AgentMode: agentMode
            );

            _mockScheduledTaskService
                .Setup(x => x.CreateScheduledTask(It.IsAny<CreateScheduledTaskRequest>()))
                .ReturnsAsync(createdTask);

            // Act
            var result = await _controller.CreateScheduledTask(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdAtActionResult.StatusCode);

            _mockScheduledTaskService.Verify(
                x => x.CreateScheduledTask(It.IsAny<CreateScheduledTaskRequest>()),
                Times.Once);
        }

        #endregion

        #region UpdateScheduledTask AgentMode Tests

        [Fact]
        public async Task UpdateScheduledTask_WithValidAgentMode_UpdatesSuccessfully()
        {
            // Arrange
            var taskId = "task-123";
            var request = new UpdateScheduledTaskApiRequest(
                AgentMode: "review"
            );

            var updatedTask = new ScheduledTaskDocument(
                Id: taskId,
                Name: "Existing Task",
                Description: "Description",
                CronExpression: "0 0 * * *",
                StartTime: DateTime.UtcNow,
                EndTime: null,
                AgentPrompt: "Test prompt",
                Agent: null,
                ThreadId: null,
                CreatedBy: "api",
                CreatedAt: DateTime.UtcNow.AddDays(-1),
                LastExecutionTime: null,
                Status: ScheduledTaskStatus.Active,
                ExecutionContext: null,
                ExecutionHistory: new List<ScheduledTaskExecution>(),
                MaxExecutions: null,
                ExecutionCount: 0,
                NotificationChannel: null,
                AgentMode: "review"
            );

            _mockScheduledTaskService
                .Setup(x => x.UpdateScheduledTask(taskId, It.Is<UpdateScheduledTaskRequest>(r => r.AgentMode == "review")))
                .ReturnsAsync(updatedTask);

            // Act
            var result = await _controller.UpdateScheduledTask(taskId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            _mockScheduledTaskService.Verify(
                x => x.UpdateScheduledTask(taskId, It.Is<UpdateScheduledTaskRequest>(r => r.AgentMode == "review")),
                Times.Once);
        }

        [Fact]
        public async Task UpdateScheduledTask_WithInvalidAgentMode_ReturnsBadRequest()
        {
            // Arrange
            var taskId = "task-123";
            var request = new UpdateScheduledTaskApiRequest(
                AgentMode: "invalid_mode"
            );

            // Act
            var result = await _controller.UpdateScheduledTask(taskId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var errorResponse = badRequestResult.Value;
            Assert.NotNull(errorResponse);

            // Verify service was never called
            _mockScheduledTaskService.Verify(
                x => x.UpdateScheduledTask(It.IsAny<string>(), It.IsAny<UpdateScheduledTaskRequest>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateScheduledTask_WithNullAgentMode_DoesNotUpdateAgentMode()
        {
            // Arrange
            var taskId = "task-123";
            var request = new UpdateScheduledTaskApiRequest(
                Name: "Updated Name"
            // AgentMode is null
            );

            var updatedTask = new ScheduledTaskDocument(
                Id: taskId,
                Name: "Updated Name",
                Description: "Description",
                CronExpression: "0 0 * * *",
                StartTime: DateTime.UtcNow,
                EndTime: null,
                AgentPrompt: "Test prompt",
                Agent: null,
                ThreadId: null,
                CreatedBy: "api",
                CreatedAt: DateTime.UtcNow.AddDays(-1),
                LastExecutionTime: null,
                Status: ScheduledTaskStatus.Active,
                ExecutionContext: null,
                ExecutionHistory: new List<ScheduledTaskExecution>(),
                MaxExecutions: null,
                ExecutionCount: 0,
                NotificationChannel: null,
                AgentMode: "autonomous" // Keeps existing mode
            );

            _mockScheduledTaskService
                .Setup(x => x.UpdateScheduledTask(taskId, It.Is<UpdateScheduledTaskRequest>(r => r.AgentMode == null)))
                .ReturnsAsync(updatedTask);

            // Act
            var result = await _controller.UpdateScheduledTask(taskId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            _mockScheduledTaskService.Verify(
                x => x.UpdateScheduledTask(taskId, It.Is<UpdateScheduledTaskRequest>(r => r.AgentMode == null)),
                Times.Once);
        }

        #endregion
    }
}
