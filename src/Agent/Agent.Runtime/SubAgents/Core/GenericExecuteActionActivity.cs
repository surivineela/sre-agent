// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Reflection;
using Agent.Core.Attributes;
using Agent.Plugins.Attributes;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Core;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericExecuteActionActivity : TaskActivity<ExecuteActionInput, ExecuteActionOutput>
{
    private readonly IToolsRepository _toolsRepository;
    private readonly ILogger<GenericExecuteActionActivity> _logger;

    public GenericExecuteActionActivity(
        IToolsRepository toolsRepository,
        ILogger<GenericExecuteActionActivity> logger
        )
    {
        _toolsRepository = toolsRepository;
        _logger = logger;
    }

    public async override Task<ExecuteActionOutput> RunAsync(
        TaskActivityContext context,
        ExecuteActionInput input)
    {
        // Get all tools and find matching tool
        var aiFunctions = _toolsRepository.GetAllTools(input.ToolSignatures).Select(_toolsRepository.FindAiFunction);
        var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == input.FunctionCallContent.Name);

        try
        {
            // set the TLS ThreadId
            ToolStatic.AsyncLocalThreadId.Value = input.ThreadId;
            ToolStatic.AsyncLocalApprovalId.Value = input.ApprovalId;

            // Invoke the function
            var invokeResult = await matchingTool.ToolFunction.InvokeAsync(input.FunctionCallContent.Arguments);
            var result = new FunctionResultContent(input.FunctionCallContent.CallId, invokeResult);

            // Return successful result
            return new ExecuteActionOutput(
                ChatMessage: new ChatMessage(ChatRole.Tool, [result]),
                Is202Submit: matchingTool is IToolFunction202);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Function tool invocation failed.");

            // Handle all errors with a single catch
            string errorMessage = $"Error executing {input.FunctionCallContent?.Name ?? "function"}: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Details: {ex.InnerException.Message}";
            }

            // Return error as function result so it appears in chat
            var errorResult = new FunctionResultContent(
                input.FunctionCallContent?.CallId ?? "error",
                errorMessage);

            return new ExecuteActionOutput(
                ChatMessage: new ChatMessage(ChatRole.Tool, [errorResult]),
                Is202Submit: false);
        }
    }
}

