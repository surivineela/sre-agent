// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Agent.Runtime.Reasoning;

internal static class ModeCommandParser
{
    private static readonly Regex ModeRegex = new(
        pattern: "^\\s*/mode\\s+(conversation|workflow)(?:\\s+(.*))?$",
        options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static bool TryParse(string text, out string targetMode, out string? initialUserUtterance)
    {
        targetMode = string.Empty;
        initialUserUtterance = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = ModeRegex.Match(text);
        if (!match.Success) return false;
        targetMode = match.Groups[1].Value.ToLowerInvariant();
        if (match.Groups.Count > 2 && match.Groups[2].Success)
        {
            initialUserUtterance = match.Groups[2].Value;
        }
        return true;
    }
}
