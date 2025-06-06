// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Helpers;

public class KernelFunctionHelpers
{
    public static TResult TryAction<TResult>(string className, Func<TResult> func, ILogger logger, [CallerMemberName] string caller = "Unknown")
    {
        try
        {
            logger.LogInternalInformation($"[{className}] Performing action '{caller}'...");
            TResult res = func();
            logger.LogInternalInformation($"[{className}] Completed action '{caller}'");
            return res;
        }
        catch (Exception e)
        {
            logger.LogInternalError(e, "Error occurred while executing action");
            throw;
        }
    }

    public static string ApplyGrepFiltering(string input, string? grepTerms, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(grepTerms))
            return input;

        var lines = input.Split('\n');
        var filteredLines = new List<string>();

        foreach (var line in lines)
        {
            if (LineMatchesGrep(line, grepTerms, caseSensitive))
            {
                filteredLines.Add(line);
            }
        }

        var result = string.Join("\n", filteredLines);

        // Add grep info if filtering was applied
        if (filteredLines.Count < lines.Length)
        {
            var totalLines = lines.Length;
            var matchedLines = filteredLines.Count;
            result = $"[GREP FILTERED: {matchedLines}/{totalLines} lines matched '{grepTerms}']\n\n{result}";
        }

        return result;
    }

    public static bool LineMatchesGrep(string line, string grepTerms, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (grepTerms.Contains(','))
        {
            // AND logic - all terms must be present
            var andTerms = grepTerms.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
            return andTerms.All(term => line.Contains(term, comparison));
        }
        else
        {
            // OR logic - any term can be present
            var orTerms = grepTerms.Split(' ').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
            return orTerms.Any(term => line.Contains(term, comparison));
        }
    }

    public static string ApplyWordTruncation(string input)
    {
        const int maxWords = 1000;

        var words = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= maxWords)
            return input;

        var truncatedWords = words.Take(maxWords);
        var truncatedText = string.Join(" ", truncatedWords);

        var remainingWords = words.Length - maxWords;
        truncatedText += $"\n\n[TRUNCATED: Output limited to first {maxWords} words. {remainingWords} additional words were cut.]";

        return truncatedText;
    }

    public static string ApplyEventLimit(string events)
    {
        const int maxEvents = 20;

        var lines = events.Split('\n');
        if (lines.Length <= maxEvents + 1) // +1 for header
            return events;

        var limitedLines = lines.Take(maxEvents + 1).ToArray(); // Keep header + limit events
        var result = string.Join("\n", limitedLines);

        var remainingEvents = lines.Length - maxEvents - 1;
        if (remainingEvents > 0)
        {
            result += $"\n\n[LIMITED: Showing first {maxEvents} events. {remainingEvents} additional events were cut.]";
        }

        return result;
    }
}

