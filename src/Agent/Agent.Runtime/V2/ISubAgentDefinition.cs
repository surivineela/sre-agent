// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.V2;

public interface ISubAgentDefinition<TInput>
{
    abstract static IReadOnlyList<string> ToolSignatures { get; }
    abstract static AgentTypeEnum AgentType { get; }
    abstract static string StartSubAgentMemberName { get; }

    abstract static string GetSystemPrompt(TInput? input = default);
}

public interface ISubAgentDefinition : ISubAgentDefinition<object?>
{
}
