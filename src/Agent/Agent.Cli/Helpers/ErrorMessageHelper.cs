// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for creating consistent error messages across the CLI.
/// </summary>
public static class ErrorMessageHelper
{
    /// <summary>
    /// Error message categories for consistent formatting.
    /// </summary>
    public static class Category
    {
        public const string InvalidParameter = "Invalid parameter";
    }

    /// <summary>
    /// Creates a standardized invalid parameter error message.
    /// </summary>
    /// <param name="message">The specific error message describing the invalid parameter</param>
    /// <returns>Formatted error message with "Invalid parameter: " prefix</returns>
    public static string InvalidParameter(string message)
    {
        return $"{Category.InvalidParameter}: {message}";
    }
}
