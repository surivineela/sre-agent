using Agent.Runtime.HelperAgents;

namespace Agent.Runtime.SubAgents.WebAppDownAgent;


public sealed record WebAppDownAgentInput(
   string Input,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId,
    IReadOnlyList<HelperAgentInput> HelperAgentsInputs);
