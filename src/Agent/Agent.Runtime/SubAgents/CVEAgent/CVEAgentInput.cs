using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.CVEAgent;

public sealed record CVEAgentInput(
    [Description("This object contains a list of GitHub repos that need to be scanned for security threats. ONLY the repo urls in this Input object will be fed to the GitHub issue plugin. No other repos will be scanned.")]
    CVEInput Input,
    [Description("The list of tools that the agent can use to perform its tasks")]
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);
