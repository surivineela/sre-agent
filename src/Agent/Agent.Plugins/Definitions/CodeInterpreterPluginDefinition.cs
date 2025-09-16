// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins;

[AgentToolPlugin(Category = ToolCategories.Utility)]
public class CodeInterpreterPluginDefinition
{
    private readonly ICodeInterpreterPlugin _plugin;

    public CodeInterpreterPluginDefinition(ICodeInterpreterPlugin plugin)
    {
        _plugin = plugin;
    }

    [Description(@"Execute a safe Python snippet inside an isolated ACA code interpreter session.
Safety:
- Outbound network & process spawning are blocked by static validation (no requests/httpx/urllib/subprocess/os.system/etc)
- Use ONLY for local data transformation, math, simple charts (if libs preinstalled), text formatting.
Returns stdout/stderr (truncated). Do NOT attempt to install packages or access the internet.
Examples:
- Calculate fibonacci numbers
- Format tabular data into markdown
Avoid: web calls, installing packages, spawning processes.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> ExecutePythonSnippetAsync(
        [Description("Python code to execute (<=20k chars, no network/process operations)")] string pythonCode,
        [Description("Timeout in seconds (default 120, max 900)")] int timeoutSeconds = 120)
        => _plugin.ExecutePythonSnippetAsync(pythonCode, Math.Clamp(timeoutSeconds, 5, 900));

    [Description(@"Generate a PDF report by executing Python code that writes a PDF file.
Safety & Workflow:
0. Script should save file in /mnt/data/<filename>
1. Your code MUST write the PDF to the provided expectedOutputFilename path. Should refer to the same path as script: /mnt/data/<filename>
2. No external network, package installation, or process spawning allowed.
3. Tool returns ONLY a status + relative download link (no base64 in response).
Examples:
- Produce a simple PDF summary with text. 
Tip: Use reportlab if available, otherwise craft a minimal PDF manually.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> GeneratePdfReportAsync(
        [Description("Python code producing the PDF (no network/process operations)")] string pythonCode,
        [Description("Path inside session for produced PDF, e.g. 'report.pdf'")] string expectedOutputFilename,
        [Description("Local filename to save as (e.g. 'daily_summary.pdf')")] string saveAsFilename,
        [Description("Timeout in seconds (default 180, max 900)")] int timeoutSeconds = 180)
        => _plugin.GeneratePdfReportAsync(pythonCode, expectedOutputFilename, saveAsFilename, Math.Clamp(timeoutSeconds, 5, 900));
}
