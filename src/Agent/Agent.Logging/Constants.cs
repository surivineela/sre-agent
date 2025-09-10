// ------------------------------------------------------------
// Token type constants for token consumption logging
// ------------------------------------------------------------

namespace Agent.Logging
{
    /// <summary>
    /// Token type constants used for token consumption logging
    /// </summary>
    public static class TokenType
    {
        public const string Input = "input";
        public const string Output = "output";
    }

    public static class AgentActionEvents
    {
        public const string GenerateModelResponse = "GenerateModelResponse";
        public const string InvokeAgent = "InvokeAgent";
        public const string InvokeTool = "InvokeTool";
        public const string ToolExecution = "ToolExecution";
        public const string CriticEvaluation = "CriticEvaluation";
    public const string CreateUserMessage = "CreateUserMessage";
    public const string CreateThread = "CreateThread";
    }

    public static class AgentActionStatus
    {
        public const string Success = "Success";
    }
}
