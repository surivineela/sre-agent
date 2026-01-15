// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

/// <summary>
/// Plugin definition for asking users questions during agent execution.
/// Similar to Claude Code's AskUserQuestion tool.
/// </summary>
[AgentToolPlugin(Category = ToolCategories.System)]
public class AskUserQuestionPluginDefinition
{
    private readonly IUserQuestionService _questionService;

    public AskUserQuestionPluginDefinition(IUserQuestionService questionService)
    {
        _questionService = questionService;
    }

    [Description(@"Ask the user a pointed question and wait for their response.

## How It Works
Displays an interactive question UI. The user sees clickable options and/or a free text input.
BLOCKS execution until the user responds. Once answered, you receive their choice and continue.

## Ask POINTED Questions, Not Abstract Ones

GOOD (specific, actionable):
- 'Which subscription should I query?' with actual subscription names as options
- 'The query references table X which doesn't exist. Should I update it to use table Y?'
- 'I found 3 failing pods. Which should I investigate?' with pod names as options
- 'What time range should I analyze?' with options: 'Last 1 hour', 'Last 24 hours', 'Last 7 days'

BAD (vague, unhelpful):
- 'What would you like me to do?'
- 'How should I proceed?'
- 'What approach do you prefer?'

PRINCIPLES:
- Ask about CONCRETE values: resource names, time ranges, file paths, thresholds
- Include options you discovered (actual subscription/cluster/pod names)
- State what you know and what specific piece is missing
- If you can infer or discover the answer yourself, do that instead of asking

## Parameters
- question: Specific question ending with '?'. State context and what you need.
- header: Short label (max 12 chars): 'Resource', 'TimeRange', 'Confirm', 'Fix'
- options: Concrete choices with labels and descriptions. Use actual values you found.
- allowFreeText: Allow custom input (default true).

## Return Value
- 'User selected: {label}' - option clicked
- 'User responded: {text}' - free text entered

## Example
  question: 'Which cluster should I check for the failing deployments?'
  header: 'Cluster'
  options: [
    { label: 'prod-eastus-01', description: '47 deployments, 3 failing' },
    { label: 'prod-westus-02', description: '23 deployments, 1 failing' }
  ]")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> AskUserQuestion(
        [Description("The question to ask the user. Should be clear, specific, and end with a question mark.")]
        string question,
        [Description("A short header/label for the question (max 12 characters). Examples: 'Approach', 'Library', 'Format', 'Action'.")]
        string header,
        [Description("Array of options for the user to choose from. Each option should have a 'label' (1-5 words, concise display text) and a 'description' (explanation of what this option means). Provide 2-4 options when applicable.")]
        UserQuestionOption[]? options = null,
        [Description("Whether to allow free text input in addition to options. Default is true.")]
        bool allowFreeText = true,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Tool call failed: Question cannot be empty.";
        }

        if (string.IsNullOrWhiteSpace(header))
        {
            header = "Question";
        }
        else if (header.Length > 12)
        {
            header = header[..12];
        }

        // Build the question object
        var userQuestion = new UserQuestion
        {
            QuestionId = Guid.NewGuid(),
            Question = question,
            Header = header,
            Options = options?.ToList() ?? new List<UserQuestionOption>(),
            AllowFreeText = allowFreeText,
            Status = UserQuestionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Get thread ID from context
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
        if (threadId == Guid.Empty)
        {
            return "Tool call failed: No thread context available.";
        }

        try
        {
            // Ask question and wait for response (blocks until user responds)
            var response = await _questionService.AskQuestionAsync(threadId, userQuestion, cancellationToken);

            // Format response for the agent
            if (!string.IsNullOrEmpty(response.SelectedLabel))
            {
                return $"User selected: {response.SelectedLabel}";
            }
            else if (!string.IsNullOrEmpty(response.FreeText))
            {
                return $"User responded: {response.FreeText}";
            }
            else
            {
                return "User did not provide a response.";
            }
        }
        catch (OperationCanceledException)
        {
            return "Question was cancelled.";
        }
        catch (Exception ex)
        {
            return $"Tool call failed: {ex.Message}";
        }
    }
}
