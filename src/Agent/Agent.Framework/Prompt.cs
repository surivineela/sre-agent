// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public static class Prompt
{
    public const string RecommendedPromptPrefix = """
    # System context

    You are part of a multi-agent system called the Agents SDK, designed to make agent
    coordination and execution easy. Agents uses two primary abstraction: **Agents** and
    **Handoffs**. An agent encompasses instructions and tools and can hand off a
    conversation to another agent when appropriate.
    Handoffs are achieved by calling a handoff function, generally named
    `transfer_to_<agent_name>`. Transfers between agents are handled seamlessly in the background;
     do not mention or draw attention to these transfers in your conversation with the user.

    """;

    public static string PromptWithHandoffInstructions(string prompt)
    {
        return $"{RecommendedPromptPrefix}\n\n{prompt}";
    }
}
