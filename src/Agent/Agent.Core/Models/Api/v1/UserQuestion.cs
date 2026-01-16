// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents a question presented to the user for interactive response.
/// Similar to Claude Code's AskUserQuestion tool.
/// </summary>
public class UserQuestion
{
    /// <summary>
    /// Unique identifier for this question, used to match responses.
    /// </summary>
    public Guid QuestionId { get; set; }

    /// <summary>
    /// The question text to display to the user.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// A short header/label for the question (max 12 characters).
    /// Displayed as a badge above the question.
    /// </summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// Available options for the user to choose from.
    /// </summary>
    public List<UserQuestionOption> Options { get; set; } = new();

    /// <summary>
    /// Whether to allow free text input in addition to options.
    /// </summary>
    public bool AllowFreeText { get; set; } = true;

    /// <summary>
    /// Current status of the question.
    /// </summary>
    public UserQuestionStatus Status { get; set; } = UserQuestionStatus.Pending;

    /// <summary>
    /// The label of the option the user selected (if any).
    /// </summary>
    public string? SelectedOptionLabel { get; set; }

    /// <summary>
    /// The free text response from the user (if any).
    /// </summary>
    public string? FreeTextResponse { get; set; }

    /// <summary>
    /// When the question was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user answered the question (if answered).
    /// </summary>
    public DateTime? AnsweredAt { get; set; }
}

/// <summary>
/// An option presented to the user in a question.
/// </summary>
public class UserQuestionOption
{
    /// <summary>
    /// Short display text for the option (1-5 words).
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Longer description explaining what this option means.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Status of a user question.
/// </summary>
public enum UserQuestionStatus
{
    /// <summary>
    /// Question is awaiting user response.
    /// </summary>
    Pending,

    /// <summary>
    /// User has answered the question.
    /// </summary>
    Answered,

    /// <summary>
    /// Question was cancelled (e.g., thread was cancelled).
    /// </summary>
    Cancelled
}

/// <summary>
/// Response from the user to a question.
/// </summary>
public class UserQuestionResponse
{
    /// <summary>
    /// The label of the selected option (if user clicked an option).
    /// </summary>
    [JsonPropertyName("selectedLabel")]
    public string? SelectedLabel { get; set; }

    /// <summary>
    /// Free text response (if user typed a custom response).
    /// </summary>
    [JsonPropertyName("freeText")]
    public string? FreeText { get; set; }
}
