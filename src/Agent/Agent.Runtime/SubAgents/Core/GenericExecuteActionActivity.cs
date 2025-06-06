// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Logging;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Core;
using Agent.Core.Models;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericExecuteActionActivity : TaskActivity<ExecuteActionInput, ExecuteActionOutput>
{
    private readonly IToolsRepository _toolsRepository;
    private readonly ILogger<GenericExecuteActionActivity> _logger;
    private readonly ActionSettings _actionSettings;

    public GenericExecuteActionActivity(
        IToolsRepository toolsRepository,
        ILogger<GenericExecuteActionActivity> logger,
        ActionSettings actionSettings)
    {
        _toolsRepository = toolsRepository;
        _logger = logger;
        _actionSettings = actionSettings;
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
            var approvalContext = ConstructApprovalContextForTool(matchingTool, input);
            ToolStatic.AsyncLocalApprovalContext.Value = approvalContext;
            _logger.LogInternalInformation($"[GenericExecuteActionActivity] The approval context for tool {matchingTool.ToolFunction.Name} is: ThreadId = {approvalContext.ThreadId}, ApprovalId = {approvalContext.ApprovalId}, UseOboToken = {approvalContext.UseOboToken}");

            // Invoke the function
            var invokeResult = await matchingTool.ToolFunction.InvokeAsync(input.FunctionCallContent.Arguments);
            var result = new FunctionResultContent(input.FunctionCallContent.CallId, invokeResult);

            // Return successful result
            return new ExecuteActionOutput(
                ChatMessage: new ChatMessage(ChatRole.Tool, [result]),
                Succeeded: true,
                Is202Submit: matchingTool is IToolFunction202);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Function tool invocation failed.");

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
                Succeeded: false,
                Is202Submit: false);
        }
    }

    private ApprovalContext ConstructApprovalContextForTool(IToolFunction toolFunction, ExecuteActionInput input)
    {
        var attribute = toolFunction.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();

        var useOboToken = (attribute?.UseOboToken ?? false) && (_actionSettings.Mode == ActionMode.Review);

        return new ApprovalContext(
            ThreadId: input.ThreadId,
            ApprovalId: input.ApprovalId,
            UseOboToken: useOboToken);
    }
}

