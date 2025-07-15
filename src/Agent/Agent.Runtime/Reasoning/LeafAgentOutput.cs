// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

public sealed class LeafAgentOutput : IAgentOutput
{
    [Description(
        """
        Use this space to think step-by-step about the problem you're solving, formulate a plan, your current trajectory, reflecting on tool call outputs, and deciding next steps.
        You may mention handoffs in this field.
        """)]
    public required string ReasoningScratchPad { get; set; }

    [Description(
        """
        Presented to the user. Use this space to keep the user posted of your activity. It may be summary of your plan, tool results, ask for guidance, ask for option selection, or final answer to their query. It should be concise, to the point, and relevant to the user query investigation.
        You must NOT mention handoffs in this field.
        """)]

    public required string NotifyUserMessage { get; set; }

    [Description(
        """
        Current state of execution. Your internal evaluation of where you're at. The allowed values are:
        - Processing: Still doing my part. This should be followed by tool calls. They should NOT be HandOffBack tool call.
        - UserInputRequired: User needs to select from few options or give approval.
        - HandOff_Continue: I did my part of request processing and MY part is done but the overall user request still needs work. Continue with next relevant agent. This should **always** be followed by HandOffBack tool call.
        - HandOff_OutOfScope: I do not have any role in processing the request. Please find the right agent. This should be followed by HandOffBack tool call.
        - CompletedSuccessfully: User request completed successfully. This should be followed by a HandOffBack tool call so further agents may verify your work.
        """)]
    public required string State { get; set; }

    [Description(
        """
        1-2 sentence explanation of why you are in that state.
        If the state is HandOff_OutOfScope, it should also mention what is out of scope and what help is needed.
        If the state is HandOff_Continue, it should also mention what you did and what next step is needed.
        If the state is UserInputRequired, it should also mention what input is required.
        If the state is CompletedSuccessfully, it should also explain how the request was completely within our scope, and how we handled it.
        """)]
    public required string StateExplanation { get; set; }
}
