using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericExecute202ActionActivity : TaskActivity<ExecuteActionInput, ChatMessage>
{
    private readonly IChatClient _chatClient;
    private readonly ToolsRepository _toolsRepository;
    public GenericExecute202ActionActivity(
        IChatClient chatClient,
        ToolsRepository toolsRepository)
    {
        _chatClient = chatClient;
        _toolsRepository = toolsRepository;
    }

    public async override Task<ChatMessage> RunAsync(
    TaskActivityContext context,
    ExecuteActionInput input)
    {
        try
        {
            var aiFunctions = _toolsRepository.GetAllTools(input.ToolSignatures).Select(_toolsRepository.FindAiFunction);
            var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == input.FunctionCallContent.Name) as ToolFunction202;

            if (matchingTool is null)
            {
                throw new InvalidOperationException($"ToolFunction is not 202 kind function: {input.FunctionCallContent.Name}");
            }

            var invokeResult = await matchingTool.ExecueFunction.InvokeAsync(input.FunctionCallContent.Arguments);

            // Success case - return formatted result
            return new ChatMessage(
                ChatRole.System,
                $"Operation {input.FunctionCallContent.Name} finished for input: {JsonSerializer.Serialize(input.FunctionCallContent.Arguments)}, the result is {JsonSerializer.Serialize(invokeResult)}");
        }
        catch (Exception ex)
        {
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
