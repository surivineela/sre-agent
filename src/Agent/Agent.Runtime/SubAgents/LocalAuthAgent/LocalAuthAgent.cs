// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents.LocalAuthAgent;

public record LocalAuthAgentInput(
        LocalAuthAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<LocalAuthAgentActivityInput>(Input, ToolSignatures, ThreadId)
{
    public LocalAuthAgentInput()
        : this(
              new LocalAuthAgentActivityInput(FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
              new List<string>(),
              Guid.Empty
              )
    {
    }
}

[DurableTask]
public class LocalAuthAgent : SimpleResourceSubAgentBase<LocalAuthAgentInput, LocalAuthAgentActivity, LocalAuthAgentActivityInput>
{
}
