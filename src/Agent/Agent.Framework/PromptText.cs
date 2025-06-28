// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Agent.Framework;

public class PromptText
{
    private readonly string _value;

    private string? _text;

    private readonly List<string> _commonPrompts = [];

    private readonly List<string> _promptStarters = [];

    private readonly List<string> _promptEnders = [];

    // Add mode-specific prompt storage
    private string? _modeSpecificPrompt;

    public PromptText(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString()
    {
        if (_text is null)
        {
            StringBuilder builder = new StringBuilder();

            foreach (var promptStarter in _promptStarters)
            {
                builder.AppendLine(promptStarter);
                builder.AppendLine();
            }

            if (HasHandoffInstructions)
            {
                builder.AppendLine(HandoffInstructions);
                builder.AppendLine();
            }

            builder.AppendLine(_value);
            builder.AppendLine();

            // Add mode-specific prompt before common prompts
            if (!string.IsNullOrEmpty(_modeSpecificPrompt))
            {
                builder.AppendLine(_modeSpecificPrompt);
                builder.AppendLine();
            }

            foreach (var commonPrompt in _commonPrompts)
            {
                builder.AppendLine(commonPrompt);
                builder.AppendLine();
            }

            _text = builder.ToString();
        }

        return _text;
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

    public PromptText WithHandoffInstructions()
    {
        HasHandoffInstructions = true;
        return this;
    }

    public void AddModeSpecificPrompt(string promptText)
    {
        _modeSpecificPrompt = promptText;
        _text = null; // Reset cached text
    }

    public void ClearModeSpecificPrompt()
    {
        _modeSpecificPrompt = null;
        _text = null; // Reset cached text
    }

    public void AddCommonPrompt(string promptText)
    {
        _commonPrompts.Add(promptText);
        _text = null; // Reset cached text
    }

    public void AddPromptStarter(string promptStarter)
    {
        _promptStarters.Add(promptStarter);
        _text = null; // Reset cached text
    }

    public void AddPromptEnder(string promptEnder)
    {
        _promptEnders.Add(promptEnder);
        _text = null; // Reset cached text
    }

    private const string HandoffInstructions = """
        # Handoff System Context

        You are part of a multi-agent framework called the **Agents Framework**, designed to make agent
        coordination and execution easy. The framework uses two primary abstraction: **Agents** and
        **Handoffs**. An agent encompasses instructions and tools and can hand off a
        conversation to another agent when appropriate. You are **one** agent in this system.
        Handoffs are achieved by calling a handoff tool, generally named
        `transfer_to_<agent_name>` or `HandOffBack`. Transfers between agents are handled seamlessly in the background by the framework.
        Perform the handoff automatically and do not ask the user if you can proceed.
        """;
}
