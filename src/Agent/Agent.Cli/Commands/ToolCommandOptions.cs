// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for tool commands.
/// </summary>
public static class ToolCommandOptions
{
    // ============================================================
    // Tool Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the tool",
            Required = true
        };

        public static readonly Option<string> TypeOption = new("--type")
        {
            Description = $"Type of the tool ({string.Join(", ", ExtendedToolHelper.GetAvailableToolTypes().Select(t => t.Name))})",
            Required = true
        };

        public static readonly Option<string> PathOption = new("--path")
        {
            Description = "Custom path under tools directory (e.g., 'StorageOperations')"
        };

        public static readonly Option<string> DescriptionOption = new("--description")
        {
            Description = "Description of the tool"
        };

        public static readonly Option<string> ConnectorOption = new("--connector")
        {
            Description = "Connector name for the tool"
        };

        public static readonly Option<string> DatabaseOption = new("--database")
        {
            Description = "Database name for KustoTool"
        };

        public static readonly Option<string> QueryOption = new("--query")
        {
            Description = "Query for KustoTool"
        };

        public static readonly Option<string> TemplateOption = new("--template")
        {
            Description = "URL template for LinkTool"
        };

        public static readonly Option<string> FunctionCodeOption = new("--function-code")
        {
            Description = "Python function code for PythonTool"
        };

        public static readonly Option<int?> TimeoutSecondsOption = new("--timeout")
        {
            Description = "Timeout in seconds (default: 30)"
        };

        public static readonly Option<string[]> DependenciesOption = new("--dependency")
        {
            Description = "Python package dependency for PythonTool (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        public static readonly Option<string[]> ParameterOption = new("--parameter")
        {
            Description = "Tool parameter in format 'name:type:description' (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        // HttpClientTool-specific options
        public static readonly Option<string> UrlOption = new("--url")
        {
            Description = "URL template with optional {{param}} placeholders for HttpClientTool"
        };

        public static readonly Option<string> MethodOption = new("--method")
        {
            Description = "HTTP method (GET, POST, PUT, DELETE, PATCH) for HttpClientTool"
        };

        public static readonly Option<string> BodyOption = new("--body")
        {
            Description = "Request body template with {{param}} placeholders for HttpClientTool"
        };

        public static readonly Option<string[]> HeaderOption = new("--header")
        {
            Description = "HTTP header in format 'key:value' (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        public static readonly Option<string> AuthConnectorOption = new("--auth-connector")
        {
            Description = "Data connector name for authentication"
        };

        public static readonly Option<string> AuthScopeOption = new("--auth-scope")
        {
            Description = "OAuth scope to request for authentication"
        };
    }

    // ============================================================
    // Tool Validate Command Options
    // ============================================================

    public static class Validate
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the tool to validate"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Validate all YAML files in the tools directory"
        };
    }

    // ============================================================
    // Tool Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the tool to apply",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Show what would be applied without making changes"
        };
    }

    // ============================================================
    // Tool Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the tool to delete",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Show what would be deleted without making changes"
        };
    }

    // ============================================================
    // Tool Show-Types Command Options
    // ============================================================

    public static class ShowTypes
    {
        public static readonly Option<string> TypeOption = new("--type")
        {
            Description = "Show details for a specific tool type"
        };
    }

    // ============================================================
    // Tool Diff Command Options
    // ============================================================

    public static class Diff
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the tool to diff",
            Required = true
        };

        public static readonly Option<string> ToolOption = new("--tool")
        {
            Description = "Diff tool to use: git, vim, code (default: git)"
        };

        public static readonly Option<bool> RawOption = new("--raw")
        {
            Description = "Show inline diff instead of launching external tool"
        };
    }

    // ============================================================
    // Tool List Command Options
    // ============================================================

    public static class List
    {
        public static readonly Option<string?> SearchOption = new("--search")
        {
            Description = "Search filter for tool names or descriptions"
        };

        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Get a specific tool by name and output the full YAML"
        };

        public static readonly Option<bool> DetailOption = new("--detail")
        {
            Description = "Output the full YAML for all tools in the list"
        };
    }

    // ============================================================
    // Tool Migrate Command Options
    // ============================================================

    public static class Migrate
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the tool to migrate from V1 to V2"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Migrate all V1 tools to V2 format"
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Preview migration changes without modifying files"
        };
    }
}
