// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

public class AgentOutput
{
    [Description("Concise reasoning as to why you responded this way, and why you chose the values provided for each field in the response")]
    public required string Reasoning { get; set; }

    [Description("Your output message to the user")]
    public required string OutputMessage { get; set; }

    [Description("A boolean indicating if you are finished addressing the user's request. " +
        "This should be 'true' if the entire workflow is complete, and 'false' there are still tasks to be completed. " +
        "If any part of the user's request has not been fulfilled, this should be 'false'")]
    public required bool RequestCompleted { get; set; }

    [Description("A boolean indicating if the next step is out of your scope needs to be handled by another agent," +
        "but you cannot find a suitable agent to handoff to.")]
    public required bool CannotHandleNextStep { get; set; }

    [Description("A boolean indicating if you need input from the user to continue with your task")]
    public required bool IsUserInputRequired { get; set; }
}
