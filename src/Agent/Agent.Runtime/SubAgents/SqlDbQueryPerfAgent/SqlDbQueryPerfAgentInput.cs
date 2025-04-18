using System.ComponentModel;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;
public sealed record SqlDbQueryPerfAgentInput(
    [Description("Full azure resource id of the Azure sql database resource that needs to be investigated. Should restart with /subscriptions/...")]
        string AzSqlDbResourceId,
    [Description("Signature of a list of tools available for the agent to use")]
        IReadOnlyList<string> ToolSignatures,
    [Description("Thread Id")]
        Guid ThreadId);
