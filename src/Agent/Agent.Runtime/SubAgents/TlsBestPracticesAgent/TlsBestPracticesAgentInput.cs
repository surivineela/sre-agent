using Agent.Core.Models;

namespace Agent.Runtime.SubAgents.TlsBestPractices;

public sealed record TlsBestPracticesAgentInput(
    TlsBestPracticesInput Input,
    IReadOnlyList<string> ToolSignatures,
    string ThreadId);
