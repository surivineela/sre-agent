using System.Linq.Expressions;
using System.Reflection;
using Agent.Runtime.Interfaces;
using Agent.Runtime.SubAgents.Core;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents;

public interface IToolsRepository : IMcpConnectable
{
    public IToolFunction FindAiFunction(string signature);
    public IReadOnlyList<string> GetAllTools(IReadOnlyList<string> localTools);
    public List<AITool> ResolveTools(IReadOnlyList<string> toolSignatures);
    public IEnumerable<ChatMessage> GetMCPServerInstructions();
    public string GetSignature(Expression<Func<Delegate>> actionSelector);
    public string GetSignature(MethodInfo method);
    public IToolFunction ResolveTool(ExecuteActionInput action);
}
