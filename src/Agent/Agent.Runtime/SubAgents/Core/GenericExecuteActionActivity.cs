using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericExecuteActionActivity : TaskActivity<ExecuteActionInput, ExecuteActionOutput>
{
    private readonly ToolsRepository _toolsRepository;

    public GenericExecuteActionActivity(
        ToolsRepository toolsRepository)
    {
        _toolsRepository = toolsRepository;
    }

    public async override Task<ExecuteActionOutput> RunAsync(
    TaskActivityContext context,
    ExecuteActionInput input)
    {
        try
        {
            // Get all tools and find matching tool
            var aiFunctions = _toolsRepository.GetAllTools(input.ToolSignatures).Select(_toolsRepository.FindAiFunction);
            var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == input.FunctionCallContent.Name);

            // Invoke the function
            var invokeResult = await matchingTool.ToolFunction.InvokeAsync(input.FunctionCallContent.Arguments);
            var result = new FunctionResultContent(input.FunctionCallContent.CallId, invokeResult);

            // Return successful result
            return new ExecuteActionOutput(
                ChatMessage: new ChatMessage(ChatRole.Tool, [result]),
                Is202Submit: matchingTool is ToolFunction202);
        }
        catch (Exception ex)
        {
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
