// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Common.ApiModels;

/// <summary>
/// Represents a single replacement operation for multi_replace_string_in_file.
/// </summary>
public class ReplaceOperation
{
    /// <summary>
    /// Brief explanation of what this replacement accomplishes.
    /// </summary>
    [Description("A brief explanation of this specific replacement operation.")]
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the file to edit.
    /// </summary>
    [Description("An absolute path to the file to edit.")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The exact literal text to replace.
    /// </summary>
    [Description("The exact literal text to replace, preferably unescaped. Include at least 3 lines of context BEFORE and AFTER the target text, matching whitespace and indentation precisely. If this string is not the exact literal text or does not match exactly, this replacement will fail.")]
    public string OldString { get; set; } = string.Empty;

    /// <summary>
    /// The exact literal text to replace oldString with.
    /// </summary>
    [Description("The exact literal text to replace `oldString` with, preferably unescaped. Provide the EXACT text. Ensure the resulting code is correct and idiomatic.")]
    public string NewString { get; set; } = string.Empty;
}
