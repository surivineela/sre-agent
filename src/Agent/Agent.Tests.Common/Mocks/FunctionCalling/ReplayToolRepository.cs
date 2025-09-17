using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Agent.Runtime.Models;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;

namespace Agent.Tests.Common.Mocks.FunctionCalling;

public class ReplayToolRepository : IToolsRepository
{
    private readonly IToolsRepository _innerRepository;
    private readonly ReplayToolCore _replayCore;

    public IEnumerable<string> FunctionNames => _replayCore.FunctionNames;
    public HashSet<string> FunctionNamesEnabledForReplay => _replayCore.FunctionNamesEnabledForReplay;

    public ReplayToolRepository(IToolsRepository innerRepository, JsonSerializerOptions serializerOptions)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _replayCore = new ReplayToolCore(serializerOptions);
    }

    public void LoadLogFromString(string logContent)
    {
        _replayCore.LoadLogFromString(logContent);
    }

    public List<AITool> ResolveTools(IReadOnlyList<string> toolSignatures)
    {
        // TODO: decide whether we want to replay here too.
        // So far its not necessary, as the codepath that handles getting function results only resolves a single tool.
        return _innerRepository.ResolveTools(toolSignatures);
    }

    public IToolFunction FindAiFunction(string signature)
    {
        return _innerRepository.FindAiFunction(signature);
    }

    public IReadOnlyList<string> GetAllTools(IReadOnlyList<string> localTools)
    {
        return _innerRepository.GetAllTools(localTools);
    }

    public IEnumerable<ChatMessage> GetMCPServerInstructions()
    {
        return _innerRepository.GetMCPServerInstructions();
    }

    public string GetSignature(Expression<Func<Delegate>> actionSelector)
    {
        return _innerRepository.GetSignature(actionSelector);
    }

    public string GetSignature(MethodInfo method)
    {
        return _innerRepository.GetSignature(method);
    }

    public void TryAddServer(McpConnection connection)
    {
        _innerRepository.TryAddServer(connection);
    }

    public void TryRemoveServer(McpConnection connection)
    {
        _innerRepository.TryRemoveServer(connection);
    }

    public List<AIFunction> GetAllFunctions()
    {
        return _innerRepository.GetAllFunctions();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
