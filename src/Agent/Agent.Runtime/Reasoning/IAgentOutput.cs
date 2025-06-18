// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Reasoning;

public interface IAgentOutput
{
    public string ReasoningScratchPad { get; }

    public string OutputMessage { get; }

    public string State { get; }

    public string StateExplanation { get; }
}
