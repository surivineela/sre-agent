// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models.WorkspaceTools;

namespace Agent.Plugins.Definitions;

/// <summary>
/// Plugin definition for VS Code-like agent tools.
/// Provides file operations, terminal execution, search, and task management capabilities.
/// </summary>
[AgentToolPlugin(Category = ToolCategories.WorkspaceOperation)]
public class WorkspacePluginDefinition
{
    private readonly IWorkspaceToolsPlugin _plugin;
    private readonly IAgentOutboundCommunicationService _communicationService;

    public WorkspacePluginDefinition(IWorkspaceToolsPlugin plugin, IAgentOutboundCommunicationService communicationService)
    {
        _plugin = plugin;
        _communicationService = communicationService;
    }

    #region File Operations

    [Description("Read the contents of a file.\n\nYou must specify the line range you're interested in. Line numbers are 1-indexed. If the file contents returned are insufficient for your task, you may call this tool again to retrieve more content. Prefer reading larger ranges over doing many small reads.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> ReadFile(
        [Description("The absolute path of the file to read.")] string filePath,
        [Description("The line number to start reading from, 1-based.")] int startLine,
        [Description("The inclusive line number to end reading at, 1-based.")] int endLine)
    {
        var textResult = await _plugin.ReadFileAsync(filePath, startLine, endLine);

        // Parse the result and stream to frontend for rich rendering
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
        if (threadId != Guid.Empty)
        {
            var readFileResult = ParseReadFileResult(textResult, filePath, startLine, endLine);
            await _communicationService.AppendAgentReadFileMessage(threadId, readFileResult);
        }

        return textResult;
    }

    /// <summary>
    /// Parses the text output from ReadFileAsync into a structured ReadFileResult.
    /// </summary>
    private static ReadFileResult ParseReadFileResult(string textResult, string filePath, int startLine, int endLine)
    {
        // Check for errors
        if (textResult.StartsWith("Tool call failed:"))
        {
            return new ReadFileResult
            {
                FilePath = filePath,
                StartLine = startLine,
                EndLine = endLine,
                TotalLines = 0,
                Content = string.Empty,
                IsTruncated = false,
                Error = textResult.Replace("Tool call failed: ", "")
            };
        }

        // Parse header: "File: `{filePath}`. Lines {start} to {end} ({total} lines total):"
        var lines = textResult.Split('\n');
        var totalLines = 0;
        var actualStartLine = startLine;
        var actualEndLine = endLine;
        var isTruncated = textResult.Contains("Output truncated");

        // Try to extract metadata from the header line
        if (lines.Length > 0 && lines[0].StartsWith("File:"))
        {
            var headerMatch = System.Text.RegularExpressions.Regex.Match(
                lines[0],
                @"Lines (\d+) to (\d+) \((\d+) lines total\)");
            if (headerMatch.Success)
            {
                actualStartLine = int.Parse(headerMatch.Groups[1].Value);
                actualEndLine = int.Parse(headerMatch.Groups[2].Value);
                totalLines = int.Parse(headerMatch.Groups[3].Value);
            }
        }

        // Extract content (skip header line)
        var contentLines = lines.Length > 1 ? lines.Skip(1) : Array.Empty<string>();
        // Remove truncation notice if present
        var content = string.Join("\n", contentLines.Where(l => !l.StartsWith("(Output truncated")));

        return new ReadFileResult
        {
            FilePath = filePath,
            StartLine = actualStartLine,
            EndLine = actualEndLine,
            TotalLines = totalLines,
            Content = content,
            IsTruncated = isTruncated,
            Error = null
        };
    }

    [Description("This is a tool for creating a new file in the workspace. The file will be created with the specified content. The directory will be created if it does not already exist. Never use this tool to edit a file that already exists.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> CreateFile(
        [Description("The absolute path to the file to create.")] string filePath,
        [Description("The content to write to the file.")] string content)
    {
        return await _plugin.CreateFileAsync(filePath, content);
    }

    [Description("Create a new directory structure in the workspace. Will recursively create all directories in the path, like mkdir -p. You do not need to use this tool before using create_file, that tool will automatically create the needed directories.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> CreateDirectory(
        [Description("The absolute path to the directory to create.")] string dirPath)
    {
        return await _plugin.CreateDirectoryAsync(dirPath);
    }

    [Description("List the contents of a directory. Result will have the name of the child. If the name ends in /, it's a folder, otherwise a file.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> ListDir(
        [Description("The absolute path to the directory to list.")] string path)
    {
        return await _plugin.ListDirectoryAsync(path);
    }

    #endregion

    #region Edit Operations

    [Description("This is a tool for making edits in an existing file in the workspace. For moving or renaming files, use run in terminal tool with the 'mv' command instead. For larger edits, split them into smaller edits and call the edit tool multiple times to ensure accuracy. Before editing, always ensure you have the context to understand the file's contents and context. To edit a file, provide: 1) filePath (absolute path), 2) oldString (MUST be the exact literal text to replace including all whitespace, indentation, newlines, and surrounding code etc), and 3) newString (MUST be the exact literal text to replace `oldString` with (also including all whitespace, indentation, newlines, and surrounding code etc.). Ensure the resulting code is correct and idiomatic.). Each use of this tool replaces exactly ONE occurrence of oldString.\n\nCRITICAL for `oldString`: Must uniquely identify the single instance to change. Include at least 3 lines of context BEFORE and AFTER the target text, matching whitespace and indentation precisely. If this string matches multiple locations, or does not match exactly, the tool will fail. Never use 'Lines 123-456 omitted' from summarized documents or ...existing code... comments in the oldString or newString.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> ReplaceStringInFile(
        [Description("An absolute path to the file to edit.")] string filePath,
        [Description("The exact literal text to replace, preferably unescaped. For single replacements (default), include at least 3 lines of context BEFORE and AFTER the target text, matching whitespace and indentation precisely. For multiple replacements, specify expected_replacements parameter. If this string is not the exact literal text (i.e. you escaped it) or does not match exactly, the tool will fail.")] string oldString,
        [Description("The exact literal text to replace `old_string` with, preferably unescaped. Provide the EXACT text. Ensure the resulting code is correct and idiomatic.")] string newString)
    {
        return await _plugin.ReplaceStringInFileAsync(filePath, oldString, newString);
    }

    [Description("This tool allows you to apply multiple replace_string_in_file operations in a single call, which is more efficient than calling replace_string_in_file multiple times. It takes an array of replacement operations and applies them sequentially. Each replacement operation has the same parameters as replace_string_in_file: filePath, oldString, newString, and explanation. This tool is ideal when you need to make multiple edits across different files or multiple edits in the same file. The tool will provide a summary of successful and failed operations.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> MultiReplaceStringInFile(
        [Description("A brief explanation of what the multi-replace operation will accomplish.")] string explanation,
        [Description("An array of replacement operations to apply sequentially.")] ReplaceOperation[] replacements)
    {
        return await _plugin.MultiReplaceStringInFileAsync(explanation, replacements);
    }

    #endregion

    #region Search Operations

    [Description("Search for files in the workspace by glob pattern. This only returns the paths of matching files. Use this tool when you know the exact filename pattern of the files you're searching for. Glob patterns match from the root of the workspace folder. Examples:\n- **/*.{js,ts} to match all js/ts files in the workspace.\n- src/** to match all files under the top-level src folder.\n- **/foo/**/*.js to match all js files under any foo folder in the workspace.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> FileSearch(
        [Description("Search for files with names or paths matching this glob pattern.")] string query,
        [Description("The maximum number of results to return. Do not use this unless necessary, it can slow things down. By default, only some matches are returned. If you use this and don't see what you're looking for, you can try again with a more specific query or a larger maxResults.")] int? maxResults = null)
    {
        return await _plugin.FileSearchAsync(query, maxResults);
    }

    [Description("Do a fast text search in the workspace. Use this tool when you want to search with an exact string or regex. If you are not sure what words will appear in the workspace, prefer using regex patterns with alternation (|) or character classes to search for multiple potential words at once instead of making separate searches. For example, use 'function|method|procedure' to look for all of those words at once. Use includePattern to search within files matching a specific pattern, or in a specific file, using a relative path. Use 'includeIgnoredFiles' to include files normally ignored by .gitignore, other ignore files, and `files.exclude` and `search.exclude` settings. Warning: using this may cause the search to be slower, only set it when you want to search in ignored folders like node_modules or build outputs. Use this tool when you want to see an overview of a particular file, instead of using read_file many times to look for code within a file.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> GrepSearch(
        [Description("The pattern to search for in files in the workspace. Use regex with alternation (e.g., 'word1|word2|word3') or character classes to find multiple potential words in a single search. Be sure to set the isRegexp property properly to declare whether it's a regex or plain text pattern. Is case-insensitive.")] string query,
        [Description("Whether the pattern is a regex.")] bool isRegexp,
        [Description("Search files matching this glob pattern. Will be applied to the relative path of files within the workspace. To search recursively inside a folder, use a proper glob pattern like \"src/folder/**\". Do not use | in includePattern.")] string? includePattern = null,
        [Description("The maximum number of results to return. Do not use this unless necessary, it can slow things down. By default, only some matches are returned. If you use this and don't see what you're looking for, you can try again with a more specific query or a larger maxResults.")] int? maxResults = null,
        [Description("Whether to include files that would normally be ignored according to .gitignore, other ignore files and `files.exclude` and `search.exclude` settings. Warning: using this may cause the search to be slower. Only set it when you want to search in ignored folders like node_modules or build outputs.")] bool includeIgnoredFiles = false)
    {
        var jsonResult = await _plugin.GrepSearchAsync(query, isRegexp, includePattern, maxResults, includeIgnoredFiles);

        // Parse the structured result and stream to frontend
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
        try
        {
            var grepResult = JsonSerializer.Deserialize<GrepSearchResult>(jsonResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (grepResult != null && threadId != Guid.Empty)
            {
                // Stream the structured result to the frontend for rich rendering
                await _communicationService.AppendAgentGrepSearchMessage(threadId, grepResult);
            }
        }
        catch (JsonException)
        {
            // If parsing fails, fall back to returning the raw result
            return jsonResult;
        }

        // Return the JSON result to the LLM for context
        return jsonResult;
    }

    #endregion

    #region Terminal Operations

    [Description("This tool allows you to execute shell commands in a persistent bash terminal session, preserving environment variables, working directory, and other context across multiple commands.\n\nCommand Execution:\n- Use && to chain simple commands on one line\n- Prefer pipelines | over temporary files for data flow\n- Never create a sub-shell (eg. bash -c \"command\") unless explicitly asked\n\nDirectory Management:\n- Must use absolute paths to avoid navigation issues\n- Use $PWD for current directory references\n- Consider using pushd/popd for directory stack management\n- Supports directory shortcuts like ~ and -\n\nProgram Execution:\n- Supports Python, Node.js, and other executables\n- Install packages via package managers (brew, apt, etc.)\n- Use which or command -v to verify command availability\n\nBackground Processes:\n- For long-running tasks (e.g., servers), set isBackground=true\n- Output is redirected to a file, use read_file to check it later\n\nOutput Management:\n- Output is automatically truncated if too long (first 500 + last 1000 chars kept)\n- Use head, tail, grep, awk to filter and limit output size\n- For pager commands, disable paging: git --no-pager or add | cat\n- Use wc -l to count lines before displaying large outputs\n\nBest Practices:\n- Quote variables: \"$var\" instead of $var to handle spaces\n- Use find with -exec or xargs for file operations\n- Be specific with commands to avoid excessive output\n- Use [[ ]] for conditional tests instead of [ ]\n- Prefer $() over backticks for command substitution\n- Use set -e at start of complex commands to exit on errors")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> RunInTerminal(
        [Description("The command to run in the terminal.")] string command,
        [Description("A one-sentence description of what the command does. This will be shown to the user before the command is run.")] string explanation,
        [Description("Whether the command starts a background process. If true, the command will run in the background and output will be redirected to a file. If false, the tool call will block on the command finishing, and then you will get the output with exit code. Examples of background processes: building in watch mode, starting a server.")] bool isBackground)
    {
        var textResult = await _plugin.RunInTerminalAsync(command, explanation, isBackground);

        // Parse the result and stream to frontend for rich rendering
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
        if (threadId != Guid.Empty)
        {
            var terminalResult = ParseTerminalResult(textResult, command, explanation, isBackground);
            await _communicationService.AppendAgentTerminalMessage(threadId, terminalResult);
        }

        return textResult;
    }

    /// <summary>
    /// Parses the text output from RunInTerminalAsync into a structured TerminalExecutionResult.
    /// </summary>
    private static TerminalExecutionResult ParseTerminalResult(string textResult, string command, string explanation, bool isBackground)
    {
        // Check for errors
        if (textResult.StartsWith("Tool call failed:"))
        {
            return new TerminalExecutionResult
            {
                Command = command,
                Explanation = explanation,
                IsBackground = isBackground,
                Status = TerminalStatus.Failed,
                Error = textResult.Replace("Tool call failed: ", "")
            };
        }

        // Background process
        if (isBackground)
        {
            // Extract session ID if present in the result
            string? sessionId = null;
            var sessionMatch = System.Text.RegularExpressions.Regex.Match(textResult, @"session[:\s]+([a-zA-Z0-9-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (sessionMatch.Success)
            {
                sessionId = sessionMatch.Groups[1].Value;
            }

            return new TerminalExecutionResult
            {
                Command = command,
                Explanation = explanation,
                IsBackground = true,
                SessionId = sessionId,
                Status = TerminalStatus.Background,
                Output = textResult
            };
        }

        // Foreground process - parse exit code from the result
        // Format: "{output}\n\nExit code: {code}" or "Command completed with exit code {code}"
        int? exitCode = null;
        string? output = textResult;
        string? error = null;

        var exitCodeMatch = System.Text.RegularExpressions.Regex.Match(textResult, @"[Ee]xit code[:\s]+(-?\d+)");
        if (exitCodeMatch.Success)
        {
            exitCode = int.Parse(exitCodeMatch.Groups[1].Value);

            // Extract output (everything before "Exit code:")
            var exitCodeIndex = textResult.LastIndexOf("Exit code:", StringComparison.OrdinalIgnoreCase);
            if (exitCodeIndex == -1)
            {
                exitCodeIndex = textResult.LastIndexOf("exit code", StringComparison.OrdinalIgnoreCase);
            }
            if (exitCodeIndex > 0)
            {
                output = textResult.Substring(0, exitCodeIndex).TrimEnd('\n', '\r', ' ');
            }
        }

        // Determine status based on exit code
        var status = (exitCode.HasValue && exitCode.Value != 0)
            ? TerminalStatus.Failed
            : TerminalStatus.Completed;

        return new TerminalExecutionResult
        {
            Command = command,
            Explanation = explanation,
            IsBackground = false,
            ExitCode = exitCode ?? 0,
            Output = output,
            Error = error,
            Status = status
        };
    }

    [Description("Get the last command run in the active terminal.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> TerminalLastCommand()
    {
        return await _plugin.GetTerminalLastCommandAsync();
    }

    #endregion

    #region Task Management

    [Description("Manage a structured todo list to track progress and plan tasks throughout your coding session. Use this tool VERY frequently to ensure task visibility and proper planning.\n\nWhen to use this tool:\n- Complex multi-step work requiring planning and tracking\n- When user provides multiple tasks or requests (numbered/comma-separated)\n- After receiving new instructions that require multiple steps\n- BEFORE starting work on any todo (mark as in-progress)\n- IMMEDIATELY after completing each todo (mark completed individually)\n- When breaking down larger tasks into smaller actionable steps\n- To give users visibility into your progress and planning\n\nWhen NOT to use:\n- Single, trivial tasks that can be completed in one step\n- Purely conversational/informational requests\n- When just reading files or performing simple searches\n\nCRITICAL workflow:\n1. Plan tasks by writing todo list with specific, actionable items\n2. Mark ONE todo as in-progress before starting work\n3. Complete the work for that specific todo\n4. Mark that todo as completed IMMEDIATELY\n5. Move to next todo and repeat\n\nTodo states:\n- not-started: Todo not yet begun\n- in-progress: Currently working (limit ONE at a time)\n- completed: Finished successfully\n\nIMPORTANT: Mark todos completed as soon as they are done. Do not batch completions.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> ManageTodoList(
        [Description("write: Replace entire todo list with new content. read: Retrieve current todo list. ALWAYS provide complete list when writing - partial updates not supported.")] string operation,
        [Description("Complete array of all todo items (required for write operation, ignored for read). Must include ALL items - both existing and new.")] WorkspaceTodoItem[]? todoList = null)
    {
        return await _plugin.ManageTodoListAsync(operation, todoList);
    }

    #endregion

    #region Web Operations

    [Description("Fetches the main content from a web page. This tool is useful for summarizing or analyzing the content of a webpage. You should use this tool when you think the user is looking for information from a specific webpage.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> FetchWebpage(
        [Description("An array of URLs to fetch content from.")] string[] urls,
        [Description("The query to search for in the web page's content. This should be a clear and concise description of the content you want to find.")] string query)
    {
        return await _plugin.FetchWebpageAsync(urls, query);
    }

    #endregion
}
