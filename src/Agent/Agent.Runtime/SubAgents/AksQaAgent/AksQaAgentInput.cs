using System.ComponentModel;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.AksQaAgent;
public sealed record AksQaAgentInput(
    string Input,
    [Description("Signature of a list of tools available for the agent to use")]
        IReadOnlyList<string> ToolSignatures,
    [Description("Thread Id")]
        Guid ThreadId);
