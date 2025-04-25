// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericExecute202ActionActivity : TaskActivity<ExecuteActionInput, ChatMessage>
{
    private readonly IChatClient _chatClient;
    private readonly IToolsRepository _toolsRepository;
    private readonly ILogger<GenericExecute202ActionActivity> _logger;

    public GenericExecute202ActionActivity(
        IChatClient chatClient,
        IToolsRepository toolsRepository,
        ILogger<GenericExecute202ActionActivity> logger
        )
    {
        _chatClient = chatClient;
        _toolsRepository = toolsRepository;
        _logger = logger;
    }

    public async override Task<ChatMessage> RunAsync(
        TaskActivityContext context,
        ExecuteActionInput input)
    {
        var aiFunctions = _toolsRepository.GetAllTools(input.ToolSignatures).Select(_toolsRepository.FindAiFunction);
        var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == input.FunctionCallContent.Name) as IToolFunction202;

        if (matchingTool is null)
        {
            throw new InvalidOperationException($"ToolFunction is not 202 kind function: {input.FunctionCallContent.Name}");
        }

        try
        {
            var invokeResult = await matchingTool.ExecuteFunction.InvokeAsync(new AIFunctionArguments(input.FunctionCallContent.Arguments));

            // Success case - return formatted result
            return new ChatMessage(
                ChatRole.System,
                $"Operation {input.FunctionCallContent.Name} finished for input: {JsonSerializer.Serialize(input.FunctionCallContent.Arguments)}, the result is {JsonSerializer.Serialize(invokeResult)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Function tool invocation failed.");

            // Handle all errors with a single catch
            string operationName = input.FunctionCallContent?.Name ?? "unknown operation";
            string errorMessage = $"❌ The long-running operation '{operationName}' failed: {ex.Message}";

            if (ex.InnerException != null)
            {
                errorMessage += $"\nAdditional details: {ex.InnerException.Message}";
            }

            // For 202 activity, we return the error as a system message
            return new ChatMessage(ChatRole.Tool, errorMessage);
        }
    }
}

