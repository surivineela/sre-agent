// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;
using Anthropic.Models.Beta.Messages;

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
- Process spawning is blocked by static validation (no subprocess/os.system/etc)
File handling:
- Files saved to /mnt/data/ are automatically retrieved and returned as markdown links
- Images (matplotlib, seaborn, PIL outputs) return as ![filename](link) for inline display
- Data files, documents, and other outputs return as [Download filename](link)
- Supports comprehensive file types: images, data formats, documents, archives, scientific data
Input handling best practices:
- DON'T embed large data directly in Python code (e.g., hardcoded lists, dictionaries, or strings). 
- DO firstly upload data files, then read them in your code
Example: 
- Upload 'data.json' usig appropriate tool
- use `with open('/mnt/data/data.json') as f: data = json.load(f)` in code to retrieve the data

Returns stdout/stderr (truncated) plus auto-retrieved files as ready-to-use markdown links.
Examples:
- Calculate fibonacci numbers and save results to CSV
- Create matplotlib visualizations and save as PNG/SVG
- Generate data analysis reports in multiple formats
- Process datasets and export to Excel, JSON, or HDF5
- Fetching web content
Avoid: installing packages, spawning processes, embedding large data in code.")]
    [AgentTool(ToolMode.Manual, KeepOriginalReturnType = true)]
    public Task<CodeExecutionResponse> ExecutePythonCodeAsync(
        [Description("Python code to execute (<=20k chars)")] string pythonCode,
        [Description("Timeout in seconds (default 120, max 900)")] int timeoutSeconds = 120)
        => _plugin.ExecutePythonCodeAsync(pythonCode, Math.Clamp(timeoutSeconds, 5, 900));

    [Description(@"Generate a PDF report by executing Python code that writes a PDF file.
Safety & Workflow:
0. Script should save the file under /mnt/data (use relative paths, e.g., 'reports/output.pdf')
1. Your code MUST write the PDF to the provided expectedOutputFilename path (relative to /mnt/data)
2. No external network, package installation, or process spawning allowed.
3. Tool returns ONLY a status message with a markdown download link in format [Link Text](/api/files/<filename>).
Examples:
- Produce a simple PDF summary with text.
Tip: Use reportlab if available, otherwise craft a minimal PDF manually.
Return format: Returns success message with markdown link like: '✅ PDF report generated successfully. Download: [Download report.pdf](/api/files/report.pdf)'")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> GeneratePdfReportAsync(
        [Description("Python code producing the PDF (no network/process operations)")] string pythonCode,
        [Description("Path inside session for produced PDF, e.g. 'report.pdf'")] string expectedOutputFilename,
        [Description("Local filename to save as (e.g. 'daily_summary.pdf')")] string saveAsFilename,
        [Description("Timeout in seconds (default 180, max 900)")] int timeoutSeconds = 180)
        => _plugin.GeneratePdfReportAsync(pythonCode, expectedOutputFilename, saveAsFilename, Math.Clamp(timeoutSeconds, 5, 900));

    [Description(@"Execute a POSIX shell command (bash) inside the code interpreter sandbox.
Rules:
- Commands run from /mnt/data (relative paths only).
- Chain commands with ';' instead of '&&'.
- Background jobs are not supported; keep isBackground=false.
- Timeout capped at 240 seconds.
Returns exit code plus truncated STDOUT/STDERR.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> RunShellCommand(
        [Description("Shell command to execute relative to /mnt/data (e.g., 'bash script.sh' or 'python main.py').")] string command,
        [Description("One-line explanation logged with the command (optional).")] string explanation = "",
        [Description("Background execution flag (must remain false in this sandbox).")] bool isBackground = false,
        [Description("Timeout in seconds (default 120, max 240).")] int timeoutSeconds = 120)
        => _plugin.ExecuteShellCommandAsync(command, explanation, isBackground, Math.Clamp(timeoutSeconds, 5, 240));

    [Description(@"Read the contents of a text file stored in /mnt/data with simple paging support.
Use to inspect artifacts produced by earlier commands or Python executions.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> ReadSessionFile(
        [Description("File path relative to /mnt/data (e.g., 'logs/output.txt').")] string filePath,
        [Description("1-based line number to start from (default 1).")] int offset = 1,
        [Description("Maximum lines to return (default 200, max 2000).")] int limit = 200)
        => _plugin.ReadSessionFileAsync(filePath, offset, limit);

    [Description(@"Search for text within files under /mnt/data using grep-style semantics.
- Set isRegexp=true for regex searches, false for fixed-string matches.
- Use includePattern (glob) to scope files, e.g., '*.log'.
- Results are capped to avoid flooding the conversation context.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> SearchSessionFiles(
        [Description("Text or pattern to search for.")] string query,
        [Description("Treat the query as a regular expression when true; fixed string when false.")] bool isRegexp,
        [Description("Optional glob (e.g., '*.log'). Leave empty to search all files.")] string includePattern = "",
        [Description("Maximum number of matches to return (default 50, max 500).")] int maxResults = 50,
        [Description("Timeout in seconds (default 120, max 900).")] int timeoutSeconds = 120)
        => _plugin.GrepSessionFilesAsync(query, isRegexp, includePattern, maxResults, Math.Clamp(timeoutSeconds, 5, 900));

    [Description(@"List all files in the current code interpreter session's /mnt/data directory.
Useful for:
- Discovering what files were generated by previous Python executions
- Checking available data files before processing
- Verifying output files were created successfully
Returns JSON array with file metadata (name, size, modified timestamp).")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> ListSessionFilesAsync()
        => _plugin.ListSessionFilesAsync();

    [Description(@"Download and retrieve a file from the code interpreter session's /mnt/data directory.
Use cases:
- Retrieve generated visualizations (matplotlib, seaborn, plotly outputs) for inline display
- Download data analysis results (CSV, Excel, JSON, HDF5, pickle files)
- Get reports and documents (PDF, HTML, Markdown, Office documents)
- Access code files, notebooks, and scientific data formats
- Retrieve any file type commonly produced by Python data science libraries
Return format:
- For images: Returns markdown for inline rendering (![alt](link)) plus download link ([Link Text](link))
- For other files: Returns markdown download link in format [Link Text](/api/files/<filename>)
Supported file types: Images (PNG, JPG, GIF, SVG, WebP, BMP, TIFF, EPS), Data (CSV, TSV, Excel, JSON, XML, YAML, HDF5, NetCDF, pickle), Documents (PDF, HTML, MD, Office formats), Code (PY, IPYNB, R, SQL), Archives (ZIP, TAR, GZ), and more.
The file is saved locally and made available via /api/files endpoint. All responses include ready-to-use markdown links.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> GetSessionFileAsync(
        [Description("Filename in session /mnt/data (e.g. 'chart.png', 'output.csv')")] string filename,
        [Description("Local filename to save as (e.g. 'analysis_chart.png')")] string saveAsFilename)
        => _plugin.GetSessionFileAsync(filename, saveAsFilename);

    [Description(@"Upload a file to the code interpreter session's /mnt/data directory.

The filePath should be a path relative to the sandbox root directory.
- For tool output files (from truncated output), use the file path shown in the truncation message (e.g., 'tmp/ToolOutputs/{threadId}/tool_xyz.json')
- For other sandbox files, use the relative path from sandbox root (e.g., 'path/to/data.csv')

Workflow:
1. Call UploadFileToSessionAsync with the relative file path
2. The file will be stored at /mnt/data/<filename> in the session
3. In your Python code, read the file from /mnt/data/<filename>

Use cases:
- Upload previously generated outputs or data files to the session for Python processing and analysis
- Make files available for further processing within the code interpreter session
- Avoid embedding large data directly in Python code - upload as a file instead.
Returns: The file path in the session (e.g., '/mnt/data/filename.json') on success, or error message if upload fails.")]
    [AgentTool(ToolMode.Manual)]
    public Task<string> UploadFileToSessionAsync(
        [Description("File path relative to sandbox root (e.g., 'tmp/ToolOutputs/{threadId}/file.json' or 'data/input.csv')")] string filePath)
        => _plugin.UploadFileToSessionAsync(filePath);
}
