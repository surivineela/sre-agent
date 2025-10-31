// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using Agent.ScheduledTasks.Services;
using Agent.Web.Controllers.v1;
using Agent.Web.Models.ScheduledTasks.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

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
    }
}
