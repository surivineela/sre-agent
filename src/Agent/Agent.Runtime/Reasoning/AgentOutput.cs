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

    [Description("A boolean indicating if you are finished addressing the user's request. " +
        "This should be 'true' if the entire workflow is complete, and 'false' there are still tasks to be completed. " +
        "If any part of the user's request has not been fulfilled, this should be 'false'")]
    public required bool RequestCompleted { get; set; }

    [Description("A boolean indicating if there is a part of the current task that is out of your scope because you cannot " +
        "find any proper tools to call and no proper agents to handoff to." +
        "This should be 'true' if any part of the task is out of your scope, and 'false' if the task is within your scope.")]
    public required bool CannotHandle { get; set; }

    [Description("Concise reasoning as to why you responded this way, and why you chose the values provided for each field in the response")]
    public required string Reasoning { get; set; }
}
