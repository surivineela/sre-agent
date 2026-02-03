// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Common.ApiModels.Session;
using Agent.Core.Interfaces;
using Agent.Framework.Hooks;
using Agent.Runtime.Hooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Agent.Tests.Unit.Framework.Hooks;

/// <summary>
/// Tests for CommandHookExecutor. Uses mocked ISessionPoolService to simulate
/// command execution without requiring actual shell access.
/// </summary>
public class CommandHookExecutorTests
{
    private readonly Mock<ISessionPoolService> _mockSessionPoolService;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly ILogger<CommandHookExecutor> _logger;

    public CommandHookExecutorTests()
    {
        _mockSessionPoolService = new Mock<ISessionPoolService>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        _logger = NullLogger<CommandHookExecutor>.Instance;

        // Default setup: development environment (so AgentNameHelper returns "test-agent")
        _mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        // Default setup: build session identifier
        _mockSessionPoolService
            .Setup(s => s.BuildSessionIdentifier(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns<string, string, bool>((agent, thread, _) => $"{agent ?? "agent"}-{thread ?? "thread"}");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenCommandIsEmpty()
    {
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenCommandIsWhitespace()
    {
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "   " };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenThreadIdIsEmpty()
    {
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "echo test" };
        var context = new StopHookContext { ThreadId = Guid.Empty };

        var result = await executor.ExecuteAsync(hook, context);

        // Should return error (fail-open by default)
        Assert.True(result.Ok);
        Assert.Contains("No thread ID", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenCommandReturnsOkTrue()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRejection_WhenCommandReturnsOkFalse()
    {
        SetupMockResponse(0, "{\"ok\": false, \"reason\": \"Task incomplete\"}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Task incomplete", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ParsesHookSpecificOutput_WithAdditionalContext()
    {
        var jsonOutput = """
        {
            "ok": true,
            "hookSpecificOutput": {
                "additionalContext": "Extra info for agent"
            }
        }
        """;
        SetupMockResponse(0, jsonOutput, "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        Assert.Equal("Extra info for agent", result.GetAdditionalContext());
    }

    [Fact]
    public async Task ExecuteAsync_ParsesHookSpecificOutput_OnRejectionWithContext()
    {
        var jsonOutput = """
        {
            "ok": false,
            "reason": "Needs more work",
            "hookSpecificOutput": {
                "additionalContext": "Consider adding tests"
            }
        }
        """;
        SetupMockResponse(0, jsonOutput, "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Needs more work", result.Reason);
        Assert.Equal("Consider adding tests", result.GetAdditionalContext());
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNonZeroExitCode_WithFailModeAllow()
    {
        SetupMockResponse(1, "", "Command failed");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Allow
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // FailMode.Allow means error returns success with error info
        Assert.True(result.Ok);
        Assert.Contains("Command exited with code 1", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNonZeroExitCode_WithFailModeBlock()
    {
        SetupMockResponse(1, "", "Command failed");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Block
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // FailMode.Block means error returns rejection
        Assert.False(result.Ok);
        Assert.Contains("Command exited with code 1", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMarkdownCodeBlock()
    {
        SetupMockResponse(0, "```json\n{\"ok\": false, \"reason\": \"Keep working\"}\n```", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Keep working", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesPlainCodeBlock()
    {
        SetupMockResponse(0, "```\n{\"ok\": true}\n```", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_TreatsEmptyOutput_AsSuccess()
    {
        SetupMockResponse(0, "", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // Empty output = no objection = success
        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesInvalidJson_WithFailModeAllow()
    {
        SetupMockResponse(0, "This is not valid JSON", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Allow
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // FailMode.Allow means parse error returns success with error info
        Assert.True(result.Ok);
        Assert.Contains("Failed to parse", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesInvalidJson_WithFailModeBlock()
    {
        SetupMockResponse(0, "This is not valid JSON", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Block
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // FailMode.Block means parse error returns rejection
        Assert.False(result.Ok);
        Assert.Contains("Failed to parse", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_WorksWithStopHookContext()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            CurrentTurn = 5,
            MaxTurns = 10,
            ThreadId = Guid.NewGuid(),
            FinalOutput = "Task done",
            ExecutionSummary = "Summary"
        };

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.Is<string>(cmd => cmd.Contains("test-agent")),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WorksWithPostToolUseHookContext()
    {
        SetupMockResponse(0, "{\"ok\": false, \"reason\": \"Tool result invalid\"}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = new PostToolUseHookContext
        {
            AgentName = "test-agent",
            CurrentTurn = 3,
            MaxTurns = 50,
            ThreadId = Guid.NewGuid(),
            ToolName = "Edit",
            ToolInput = new { file = "test.py" },
            ToolResult = "Success",
            ToolSucceeded = true
        };

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Tool result invalid", result.Reason);

        // Verify command was called with tool context in stdin
        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.Is<string>(cmd => cmd.Contains("Edit") && cmd.Contains("test.py")),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UsesConfiguredTimeout()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            Timeout = 60
        };
        var context = CreateStopContext();

        await executor.ExecuteAsync(hook, context);

        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                60), // Verify timeout is passed
            Times.Once);
    }

    [Fact]
    public void SupportedType_ReturnsCommand()
    {
        var executor = CreateExecutor();
        Assert.Equal(HookType.Command, executor.SupportedType);
    }

    #region Script Support Tests

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenScriptIsEmpty()
    {
        var executor = CreateExecutor();
        var hook = new HookDefinition { Script = "" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UploadsAndExecutesScript()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        SetupMockUpload();
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Script = "#!/bin/bash\necho '{\"ok\": true}'"
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);

        // Verify script was uploaded (no directory parameter - uploads to default location)
        _mockSessionPoolService.Verify(
            s => s.UploadSessionFileAsync(
                It.IsAny<string>(),
                It.Is<string>(f => f.StartsWith("hook_") && f.EndsWith(".sh")),
                It.IsAny<byte[]>(),
                null),
            Times.Once);

        // Verify command changes to /mnt/data, makes script executable, and pipes context to it
        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.Is<string>(cmd => cmd.Contains("cd /mnt/data") && cmd.Contains("chmod +x hook_") && cmd.Contains(".sh")),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CachesScriptUpload_ForSameSession()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        SetupMockUpload();
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Script = "#!/bin/bash\necho '{\"ok\": true}'"
        };
        // Use context without execution summary to avoid transcript uploads
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            CurrentTurn = 5,
            MaxTurns = 10,
            ThreadId = Guid.NewGuid(),
            FinalOutput = "Done",
            ExecutionSummary = null  // No transcript upload
        };

        // Execute twice with the same context (same session)
        await executor.ExecuteAsync(hook, context);
        await executor.ExecuteAsync(hook, context);

        // Script should only be uploaded once due to caching (no transcript uploads since ExecutionSummary is null)
        _mockSessionPoolService.Verify(
            s => s.UploadSessionFileAsync(
                It.IsAny<string>(),
                It.Is<string>(f => f.StartsWith("hook_Stop_") && f.EndsWith(".sh")),
                It.IsAny<byte[]>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PrefersScript_WhenBothCommandAndScriptProvided()
    {
        // Note: Validation should prevent this, but executor uses script when both are present
        SetupMockResponse(0, "{\"ok\": true}", "");
        SetupMockUpload();
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/command.sh",
            Script = "#!/bin/bash\necho 'script'"
        };
        var context = CreateStopContext();

        await executor.ExecuteAsync(hook, context);

        // Verify it executes the script (cd /mnt/data && chmod +x ... | ./{script}) not the command
        _mockSessionPoolService.Verify(
            s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.Is<string>(cmd => cmd.Contains("cd /mnt/data") && cmd.Contains("chmod +x")),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.Once);
    }

    #endregion

    #region Claude-Style Exit Code Tests

    [Fact]
    public async Task ExecuteAsync_ExitCode2_AlwaysBlocking_IgnoresFailModeAllow()
    {
        SetupMockResponse(2, "", "Blocked by policy");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Allow // Even with Allow, exit 2 should block
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Blocked by policy", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ExitCode2_AlwaysBlocking_WithEmptyStderr()
    {
        SetupMockResponse(2, "", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Allow
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Hook exited with code 2", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ExitCode1_UsesFailModeAllow()
    {
        SetupMockResponse(1, "", "Some error");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Allow
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // With FailMode.Allow, exit code 1 should be non-blocking
        Assert.True(result.Ok);
        Assert.Contains("exit", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ExitCode1_UsesFailModeBlock()
    {
        SetupMockResponse(1, "", "Some error");
        var executor = CreateExecutor();
        var hook = new HookDefinition
        {
            Command = "/path/to/hook.sh",
            FailMode = HookFailMode.Block
        };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        // With FailMode.Block, exit code 1 should be blocking
        Assert.False(result.Ok);
        Assert.Contains("exit", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Decision Format Tests

    [Fact]
    public async Task ExecuteAsync_ParsesDecisionBlock()
    {
        SetupMockResponse(0, "{\"decision\": \"block\", \"reason\": \"Dangerous command\"}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Dangerous command", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ParsesDecisionAllow()
    {
        SetupMockResponse(0, "{\"decision\": \"allow\"}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_ParsesDecisionBlock_WithoutReason()
    {
        SetupMockResponse(0, "{\"decision\": \"block\"}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Blocked by hook", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ParsesDecisionAllow_WithHookSpecificOutput()
    {
        var jsonOutput = """
        {
            "decision": "allow",
            "hookSpecificOutput": {
                "additionalContext": "Command validated successfully"
            }
        }
        """;
        SetupMockResponse(0, jsonOutput, "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        Assert.Equal("Command validated successfully", result.GetAdditionalContext());
    }

    [Fact]
    public async Task ExecuteAsync_DecisionIsCaseInsensitive()
    {
        SetupMockResponse(0, "{\"decision\": \"BLOCK\", \"reason\": \"Blocked\"}", "");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = CreateStopContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Blocked", result.Reason);
    }

    #endregion

    private CommandHookExecutor CreateExecutor()
    {
        return new CommandHookExecutor(
            _mockSessionPoolService.Object,
            _mockHostEnvironment.Object,
            _logger);
    }

    private void SetupMockResponse(int exitCode, string stdout, string stderr)
    {
        _mockSessionPoolService
            .Setup(s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync(new SessionResponse
            {
                ExitCode = exitCode,
                Result = new CommandResult
                {
                    Stdout = stdout,
                    Stderr = stderr
                }
            });
    }

    private void SetupMockUpload()
    {
        _mockSessionPoolService
            .Setup(s => s.UploadSessionFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private static StopHookContext CreateStopContext()
    {
        return new StopHookContext
        {
            AgentName = "test-agent",
            CurrentTurn = 5,
            MaxTurns = 10,
            ThreadId = Guid.NewGuid(),
            FinalOutput = "Done",
            ExecutionSummary = "Summary"
        };
    }

    #region Transcript Upload Tests

    [Fact]
    public async Task ExecuteAsync_UploadsTranscript_WhenExecutionSummaryProvided()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        SetupMockUpload();

        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            CurrentTurn = 5,
            MaxTurns = 10,
            ThreadId = Guid.NewGuid(),
            FinalOutput = "Done",
            ExecutionSummary = "This is the execution summary content"
        };

        await executor.ExecuteAsync(hook, context);

        // Verify transcript was uploaded
        _mockSessionPoolService.Verify(
            s => s.UploadSessionFileAsync(
                It.IsAny<string>(),
                It.Is<string>(f => f.StartsWith("hook_transcript_") && f.EndsWith(".txt")),
                It.IsAny<byte[]>(),
                null),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ContextContainsFilePath_WhenTranscriptUploaded()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");
        SetupMockUpload();

        string? capturedCommand = null;
        _mockSessionPoolService
            .Setup(s => s.ExecuteShellCommandInCodeInterpreterPoolAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Callback<string, string, int>((cmd, _, _) => capturedCommand = cmd)
            .ReturnsAsync(new SessionResponse
            {
                ExitCode = 0,
                Result = new CommandResult { Stdout = "{\"ok\": true}", Stderr = "" }
            });

        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            ThreadId = Guid.NewGuid(),
            ExecutionSummary = "Summary content"
        };

        await executor.ExecuteAsync(hook, context);

        // Verify the command contains the file path instead of inline content
        Assert.NotNull(capturedCommand);
        Assert.Contains("/mnt/data/hook_transcript_", capturedCommand);
        Assert.DoesNotContain("Summary content", capturedCommand);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotUploadTranscript_WhenExecutionSummaryEmpty()
    {
        SetupMockResponse(0, "{\"ok\": true}", "");

        var executor = CreateExecutor();
        var hook = new HookDefinition { Command = "/path/to/hook.sh" };
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            ThreadId = Guid.NewGuid(),
            ExecutionSummary = null
        };

        await executor.ExecuteAsync(hook, context);

        // Verify transcript was NOT uploaded
        _mockSessionPoolService.Verify(
            s => s.UploadSessionFileAsync(
                It.IsAny<string>(),
                It.Is<string>(f => f.StartsWith("hook_transcript_")),
                It.IsAny<byte[]>(),
                It.IsAny<string>()),
            Times.Never);
    }

    #endregion
}
