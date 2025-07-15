// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

public sealed class DefaultAgentOutput : IAgentOutput
{
    [Description(
        """
        Use this space to think step-by-step about the problem you're solving, formulate a plan, your current trajectory, reflecting on tool call outputs, and deciding next steps.
        You may mention other agents and handoffs etc in this field.
        """)]

    public required string ReasoningScratchPad { get; set; }

    [Description(
        """
        Presented to the user. Use this space to keep the user posted of your activity. It may be summary of your plan, ask for guidance, ask for option selection, or final answer to their query.
        While processing the query, it should be concise, to the point, and relevant to the user query investigation, within 2-3 sentences.
        When state is CompletedSuccessfully or RequestFailed, it should scale with the investigation. For short ones 2-3 sentence summary is fine. For more involved investigations it may be upto 7-8 sentences long explaining in detail the actions taken and result gathered and any help / guidance needed.
        You must NOT mention other agents or the flow of control or handoffs in this field.
        """)]
    public required string NotifyUserMessage { get; set; }

    [Description(
        """
        Current state of execution. Your internal evaluation of where you're at. The allowed values are:
        - Processing: Still doing my part. This should be followed by tool calls. They should NOT be handoffs ie transfer_to_* or HandOffBack.
        - UserInputRequired: User needs to select from few options or give approval.
        - HandOff_Continue: I did my part of request processing and MY part is done but the overall user request still needs work. Continue with next relevant agent. This should **always** be followed by transfer_to_* tool call.
        - HandOff_OutOfScope: The request is out of my scope. Please find the right agent. This should be followed by HandOffBack tool call.
        - CompletedSuccessfully: User request completed successfully. This should be followed by a transfer_to_meta_agent to verify your work.
        - RequestFailed: Failed to process user request. This should be followed by a transfer_to_meta_agent with clear explanation of what failed and what guidance is needed.
        """)]
    public required string State { get; set; }

    [Description(
        """
        1-2 sentence explanation of why you are in that state.
        HandOff_OutOfScope, it should also mention what is out of scope and what help is needed.
        If the state is UserInputRequired, it should also mention what input is required.
        If the state is RequestFailed, it should have clear explanation of what failed and what guidance is needed.
        """)]
    public required string StateExplanation { get; set; }
}
