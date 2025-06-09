// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Agent.Framework;

public class PromptText
{
    private readonly string _value;

    public PromptText(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();

        if (HasHandoffInstructions)
        {
            builder.AppendLine(PromptTextConstants.HandoffInstructions);
            builder.AppendLine();
            builder.AppendLine("# Instructions");
        }

        if (!string.IsNullOrEmpty(_value))
        {
            builder.AppendLine(_value);
            builder.AppendLine();
        }

        foreach (var commonPrompt in _commonPrompts)
        {
            builder.AppendLine(commonPrompt);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static implicit operator string(PromptText? promptText)
    {
        if (promptText is null)
        {
            return string.Empty;
        }

        return promptText.ToString();
    }

    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator PromptText?(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return new PromptText(value);
    }

    public bool HasHandoffInstructions { get; private set; } = false;

    public bool HasFormattingGuidelines { get; private set; } = false;

    private readonly List<string> _commonPrompts = [];

    public PromptText WithHandoffInstructions()
    {
        HasHandoffInstructions = true;
        return this;
    }

    public void AddCommonPrompt(string promptText)
    {
        _commonPrompts.Add(promptText);
    }

    private static class PromptTextConstants
    {
        public const string HandoffInstructions = """
        # System context
        You are part of a multi-agent system called the Agents SDK, designed to make agent
        coordination and execution easy. Agents uses two primary abstraction: **Agents** and
        **Handoffs**. An agent encompasses instructions and tools and can hand off a
        conversation to another agent when appropriate.
        Handoffs are achieved by calling a handoff function, generally named
        `transfer_to_<agent_name>`. Transfers between agents are handled seamlessly in the background;
         do not mention or draw attention to these transfers in your conversation with the user.
         perform the handoff automatically and do not ask the user if you can proceed.
        """;
    }
}
