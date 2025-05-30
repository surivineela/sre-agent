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

        if (HasFormattingGuidelines)
        {
            builder.AppendLine(PromptTextConstants.FormattingGuidelines);
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

    public PromptText WithFormattingGuidelines()
    {
        HasFormattingGuidelines = true;
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
        """;

        public static string FormattingGuidelines = """
        # Formatting Guidelines
        Your messages will be sent via Microsoft Teams, without using adaptive cards.
        Note that the below guidelines use backticks to be clear about the referenced text. The only scenario you should put backticks in your response is for the code block case outlined below.
        Follow these guidelines:

        - Allowed Markdown Syntax:
            1. Bold: **bold text**
                - Use `**` around the text for bold (example: **This is bold**).
                - In these guidelines, backticks around `**bold text**` are just for illustration; do not include backticks in your final output when generating bold text.
            2. Italics: *italic text* or _italic text_
                - Use `*` or `_` around the text for italics (example: *This is italics* or _This is italics_).
                - In these guidelines, backticks around `*italic text*` or `_italic text_` are for illustration only.
            3. Underline: __underlined text__
            4. Strikethrough: ~~strikethrough text~~
            5. Markdown Links: [link text](URL)
                - Use `[link text](URL)` for links (example: [Microsoft](https://www.microsoft.com)).
            6. Markdown Tables:
                - Use `|` to separate columns and `-` to create headers (example below).
                ```
                | Header 1 | Header 2 |
                |----------|----------|
                | Row 1   | Data 1   |
                | Row 2   | Data 2   |
                ```
            7. Headings:
                - ## Heading 2
                - ### Heading 3
                (Note: Teams applies limited styling to headings.)
            8. Bulleted Lists:
                - Use `- ` or `* ` at the start of each line (example: `- Item 1`).
            9. Numbered Lists:
                - Use `1. `, `2. `, etc. (example: `1. First`, `2. Second`).
            10. Blockquotes:
                - Begin a line with `> ` for quoted text.
            11. Code Blocks:
                - Use triple backticks to start and end the block (example below).
                ```
                Your code here
                ```

        - Disallowed or Unreliable Markdown:
            1. HTML Tags: `<b>some text</b>`, `<br/>`, etc.
            2. Images: `![alt text](imageURL)`

        - Additional Requirements:
            1. No HTML, no JSON, and no Adaptive Cards in the output—Markdown text only.
        """;
    }
}
