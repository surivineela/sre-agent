// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class ToolOutputRetrieverPluginDefinition
{
    private readonly IToolOutputRetrieverPlugin _toolOutputRetrieverPlugin;

    public ToolOutputRetrieverPluginDefinition(IToolOutputRetrieverPlugin toolOutputRetrieverPlugin)
    {
        _toolOutputRetrieverPlugin = toolOutputRetrieverPlugin;
    }

    [Description("""
        Access large stored tool outputs (text/json/yaml) by fileKey.

        Supports reading by line or byte offset, filtering JSON/YAML via JMESPath, summarizing using LLM, and searching with regex.

        Operations & Examples:

        1. read_by_line - Read specific line ranges
           Required: fileKey, operation='read_by_line', lineStart
           Optional: lineEnd (defaults to lineStart + 49, i.e., 50 lines total)
           Example: Read lines 100-200 from a log file
            { "fileKey": "tool-run-20251130-143025.txt", "operation": "read_by_line", "lineStart": 100, "lineEnd": 200 }

        2. read_by_offset - Read specific byte ranges
           Required: fileKey, operation='read_by_offset', offsetStart
           Optional: offsetEnd (defaults to offsetStart + 16KB, capped at file size)
           Example: Read 1KB starting from byte 5000
            { "fileKey": "data-export-20251130-143025.json", "operation": "read_by_offset", "offsetStart": 5000, "offsetEnd": 6024 }

        3. summarize - Summarizes or analyzes the full file using a natural-language prompt
           Required: fileKey, operation='summarize', summaryPrompt
           Example: Summarize the entire log file
             { "fileKey": "logs-20251130-143025.json", "operation": "summarize", "summaryPrompt": "Summarize the content" }

        4. filter_structured - Filter JSON/YAML with JMESPath
           Required: fileKey, operation='filter_structured', jmesPath
           Output format matches input (JSON→JSON, YAML→YAML)
           Example: Extract all error entries from logs
             { "fileKey": "logs-20251130-143025.json", "operation": "filter_structured", "jmesPath": "[?level=='ERROR']" }
           Example: Get specific fields
             { "fileKey": "data-20251130-143025.yaml", "operation": "filter_structured", "jmesPath": "items[*].{name:name, status:status}" }
           Example: Get specific fields
             { "fileKey": "data-20251130-143025.yaml", "operation": "filter_structured", "jmesPath": "foo[?age > `25`]" }

        5. search_regex - Search with regular expressions
           Required: fileKey, operation='search_regex', regexPattern
           Optional: regexFlags (i=case-insensitive, m=multiline, s=dot-matches-newline), regexMaxMatches (default: 100)
           Example: Find all error messages (case-insensitive)
             { "fileKey": "app-logs-20251130-143025.txt", "operation": "search_regex", "regexPattern": "error.*failed", "regexFlags": "i" }
           Example: Find email addresses
             { "fileKey": "output-20251130-143025.txt", "operation": "search_regex", "regexPattern": "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}" }

        Use this tool when you need to:
        - Access more content from a partial preview
        - Filter large JSON/YAML datasets
        - Search for specific patterns in large outputs
        - Read specific sections of large files
        - Summarize or analyze file content
     """)]
    public async Task<string> ToolOutputRetrieverAsync(
        [Description("Unique ID for the stored file (fileKey)")] string fileKey,
        [Description("Operation to perform: read_by_line, read_by_offset, summarize, filter_structured, search_regex")] string operation,
        [Description("Starting line number (1-based, for read_by_line and summarize with scope=lines)")] int? lineStart = null,
        [Description("Ending line number (1-based, optional, for read_by_line and summarize with scope=lines)")] int? lineEnd = null,
        [Description("Starting byte offset (0-based, for read_by_offset and summarize with scope=offset)")] long? offsetStart = null,
        [Description("Ending byte offset (0-based, optional, for read_by_offset and summarize with scope=offset)")] long? offsetEnd = null,
        [Description("Prompt for summarization (required for summarize operation)")] string? summaryPrompt = null,
        [Description("JMESPath expression for filtering (required for filter_structured operation). Example: '[?level==`ERROR`]' or 'items[*].{name:name, status:status} *Don't use unicode in JMESPath expressions*'")] string? jmesPath = null,
        [Description("Regex pattern to search for (required for search_regex operation)")] string? regexPattern = null,
        [Description("Regex flags: i=case-insensitive, m=multiline, s=dot-matches-newline (optional for search_regex)")] string? regexFlags = null,
        [Description("Maximum number of regex matches to return (default: 100)")] int? regexMaxMatches = null)
    {
        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = operation,
            LineStart = lineStart,
            LineEnd = lineEnd,
            OffsetStart = offsetStart,
            OffsetEnd = offsetEnd,
            SummaryPrompt = summaryPrompt,
            JmesPath = jmesPath,
            RegexPattern = regexPattern,
            RegexFlags = regexFlags,
            RegexMaxMatches = regexMaxMatches
        };

        return await _toolOutputRetrieverPlugin.RetrieveToolOutputAsync(options);
    }
}
