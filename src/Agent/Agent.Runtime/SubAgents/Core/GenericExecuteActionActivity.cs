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
        var aiFunctions = input.ToolSignatures.Select(_toolsRepository.FindAiFunction);
        var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == input.FunctionCallContent.Name);
        var invokeResult = await matchingTool.ToolFunction.InvokeAsync(input.FunctionCallContent.Arguments);
        var result = new FunctionResultContent(input.FunctionCallContent.CallId, invokeResult);

        return new ExecuteActionOutput(
            ChatMessage: new ChatMessage(ChatRole.Tool, [result]),
            Is202Submit: matchingTool is ToolFunction202);
    }
}
