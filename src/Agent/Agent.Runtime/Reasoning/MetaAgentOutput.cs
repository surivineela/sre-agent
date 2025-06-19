// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

public sealed class MetaAgentOutput : IAgentOutput
{
    [Description(
        """
        Use this space to think step-by-step about the problem you're solving, formulate a plan, your current trajectory, reflecting on agent handoff outputs, and deciding next steps.
        You may mention other agents and handoffs etc in this field.
        """)]
    public required string ReasoningScratchPad { get; set; }

    [Description(
        """
        Presented to the user. Use this space to keep the user posted of your activity. It may be summary of your plan, handoffs, ask for guidance, ask for option selection, or final answer to their query.
        While processing the query, it should be concise, to the point, and relevant to the user query investigation, within 2-3 sentences.
        When state is CompletedSuccessfully or RequestFailed, it should scale with the investigation. For short ones 2-3 sentence summary is fine. For more involved investigations it may be upto 7-8 sentences long explaining in detail the actions taken and result gathered and any help / guidance needed.
        You must NOT mention other agents or the flow of control or handoffs in this field.
        """)]
    public required string NotifyUserMessage { get; set; }

    [Description(
        """
        Current state of execution. Your internal evaluation of where you're at. The allowed values are:
        - UserInputRequired: User needs to select from few options or give approval.
        - HandOff_Continue: Continue with next relevant agent. This should be followed by transfer_to_* tool call clearly specifying the subtask for the next agent.
        - CompletedSuccessfully: User request completed successfully. Clearly state the summary of your actions and final result in notifyUserMessage.
        - RequestFailed: You failed to solve the user query, or only solved partially. Clearly state the summary of your actions, why you failed and what help / guidance you need from user in notifyUserMessage.
        """)]
    public required string State { get; set; }

    [Description(
        """
        1-2 sentence explanation of why you are in that state.
        If the state is HandOff_Continue, it should also mention what the previoud agent did and what next step is expected of the next agent.
        If the state is UserInputRequired, it should also mention what input is required.
        If the state is RequestFailed, it should have clear explanation of what failed and what guidance is needed from the user.
        """)]
    public required string StateExplanation { get; set; }
}
