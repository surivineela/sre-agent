// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Logging;
using Agent.Runtime.HelperAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class StartHelperAgentActivity(
    ILogger<StartHelperAgentActivity> logger,
    IServiceProvider serviceProvider
) : TaskActivity<ExecuteHelperAgentInput, ExecuteHelperAgentOutput>
{
    public override async Task<ExecuteHelperAgentOutput> RunAsync(TaskActivityContext context, ExecuteHelperAgentInput input)
    {
        var helperAgentInput = input.HelperAgentInput;
        var helperAgent = serviceProvider.GetRequiredService(helperAgentInput.AgentType) as HelperAgent
            ?? throw new InvalidOperationException($"Failed to resolve helper agent of type {helperAgentInput.GetType()}");

        helperAgent.Initialize(helperAgentInput, input.ThreadId);

        var startMethod = helperAgent.GetEntryPointMethod()
            ?? throw new InvalidOperationException($"Entry point method for helper agent type {helperAgent.GetType()} not found");

        try
        {
            var invokeResult = await AIFunctionFactory.Create(startMethod, helperAgent).InvokeAsync(new AIFunctionArguments(input.FunctionCall.Arguments));
            var result = new FunctionResultContent(input.FunctionCall.CallId, invokeResult);
            var message = new ChatMessage(ChatRole.Tool, [result]);

            return new ExecuteHelperAgentOutput(message, true, helperAgent.IsAsync());
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Helper Agent invocation failed");

            // Handle all errors with a single catch
            string errorMessage = $"Error executing {input.FunctionCall.Name}: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Details: {ex.InnerException.Message}";
            }

            // Return error as function result so it appears in chat
            var errorResult = new FunctionResultContent(
                input.FunctionCall.CallId,
                errorMessage);

            var message = new ChatMessage(ChatRole.Tool, [errorResult]);

            return new ExecuteHelperAgentOutput(message, false, false);
        }
    }
}

[DurableTask]
public class RunHelperAgentLongRunningActivity(
    ILogger<StartHelperAgentActivity> logger,
    IServiceProvider serviceProvider
) : TaskActivity<ExecuteHelperAgentInput, ChatMessage>
{
    public override async Task<ChatMessage> RunAsync(TaskActivityContext context, ExecuteHelperAgentInput input)
    {
        var helperAgentInput = input.HelperAgentInput;

        using var scope = serviceProvider.CreateScope();

        var helperAgent = scope.ServiceProvider.GetRequiredService(helperAgentInput.AgentType) as HelperAgent
            ?? throw new InvalidOperationException($"Failed to resolve helper agent of type {helperAgentInput.GetType()}");

        helperAgent.Initialize(helperAgentInput, input.ThreadId);

        var longRunningMethod = helperAgent.GetLongRunningMethod()
            ?? throw new InvalidOperationException($"Long running method not defined for helper agent type {helperAgent.GetType()}");

        try
        {
            var invokeResult = await AIFunctionFactory.Create(longRunningMethod, helperAgent).InvokeAsync(new AIFunctionArguments(input.FunctionCall.Arguments));

            return new ChatMessage(
                ChatRole.System,
                $"Operation {input.FunctionCall.Name} finished for input: {JsonSerializer.Serialize(input.FunctionCall.Arguments)}, the result is {JsonSerializer.Serialize(invokeResult)}");
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Helper agent invocation failed.");

            // Handle all errors with a single catch
            string operationName = input.FunctionCall.Name;
            string errorMessage = $"❌ The long-running operation '{operationName}' failed: {ex.Message}";

            if (ex.InnerException != null)
            {
                errorMessage += $"\nAdditional details: {ex.InnerException.Message}";
            }

            return new ChatMessage(ChatRole.Tool, errorMessage);
        }
    }
}

public sealed record ExecuteHelperAgentInput(
    Guid ThreadId,
    FunctionCallContent FunctionCall,
    HelperAgentInput HelperAgentInput
);

public sealed record ExecuteHelperAgentOutput(
    ChatMessage ChatMessage,
    bool Succeeded,
    bool RunAsync
);
