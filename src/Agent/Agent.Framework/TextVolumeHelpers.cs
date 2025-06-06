// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Framework;

public static partial class TextVolumeHelpers
{
    public static string ApplyWordTruncation(
        string input,
        int maxWords = 1000,
        bool addTruncationMessage = true)
    {
        if (string.IsNullOrEmpty(input) || maxWords <= 0)
        {
            return input;
        }

        // ⬇️  “\S+” = one word, “\s*” = any spaces/new‑lines/tabs that trail it
        var matches = WordRegex().Matches(input);

        // Nothing to truncate?
        if (matches.Count <= maxWords)
        {
            return input;
        }

        // Re‑assemble the first maxWords matches, which include their original spacing
        var sb = new StringBuilder();
        for (var i = 0; i < maxWords; i++)
        {
            sb.Append(matches[i].Value);
        }

        sb.Append("...");

        if (addTruncationMessage)
        {
            var remainingWords = matches.Count - maxWords;
            sb.Append(
                $"\n[TRUNCATED: Output limited to first {maxWords} words. " +
                $"{remainingWords} additional words were cut.]");
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"\S+\s*", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex WordRegex();
}

