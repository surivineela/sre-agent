// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

public class AgentOutput
{
    [Description("Your output message to the user")]
    public required string OutputMessage { get; set; }

    [Description("A boolean indicating if you need input from the user to continue with your task")]
    public required bool IsUserInputRequired { get; set; }

    [Description("A boolean indicating if you are finished addressing the user's request. This should be 'true' if your workflow is complete, and 'false' there are still tasks to be completed.")]
    public required bool RequestCompleted { get; set; }

    [Description("Concise reasoning as to why you provided your chosen value to 'isUserInputRequired' and 'requestCompleted'")]
    public required string Reasoning { get; set; }
}
