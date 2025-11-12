// ------------------------------------------------------------
// Token type constants for token consumption logging
// ------------------------------------------------------------

namespace Agent.Logging;

public static class AgentActionEvents
{
    public const string CreateAgentMessage = nameof(CreateAgentMessage);
    public const string CreateThread = nameof(CreateThread);
    public const string CreateUserInitiatedThread = nameof(CreateUserInitiatedThread);
    public const string CreateUserMessage = nameof(CreateUserMessage);
    public const string CriticEvaluation = nameof(CriticEvaluation);
    public const string GenerateModelResponse = nameof(GenerateModelResponse);
    public const string InvokeAgent = nameof(InvokeAgent);
    public const string InvokeTool = nameof(InvokeTool);
    public const string MarkThreadAsRead = nameof(MarkThreadAsRead);
    public const string ThumbsDown = nameof(ThumbsDown);
    public const string ThumbsUp = nameof(ThumbsUp);
    public const string ToolExecution = nameof(ToolExecution);
    public const string Heartbeat = nameof(Heartbeat);
    public const string LLMHttpRequest = nameof(LLMHttpRequest);

    // LLM-as-Judge Evals
    public const string EvaluateHandoffs = "evaluate.handoffs";
    public const string EvaluateRag = "evaluate.rag";
    public const string EvaluateTask = "evaluate.task";
    public const string EvaluateThread = "evaluate.thread";

    // Incident Management
    public const string ResolveIncident = nameof(ResolveIncident);
}

public static class AgentActionStatus
{
    public const string Success = "Success";
    public const string Fail = "Fail";
}
