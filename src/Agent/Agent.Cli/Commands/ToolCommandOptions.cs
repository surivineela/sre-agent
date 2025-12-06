// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.CommandLine.Parsing;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for tool commands.
/// </summary>
public static class ToolCommandOptions
{
    // ============================================================
    // Helper Methods
    // ============================================================

    private static Option<T> CreateOption<T>(string name, string description, bool required = false, Action<Option<T>, OptionResult>? validator = null)
    {
        var option = new Option<T>(name) { Description = description, Required = required };
        if (validator != null)
        {
            option.Validators.Add(result => validator(option, result));
        }
        return option;
    }

    // ============================================================
    // Global Options (shared across multiple commands)
    // ============================================================

    public static readonly Option<bool> DebugOption = CreateOption<bool>(
        "--debug",
        "Enable verbose debug logging for network calls and operations"
    );

    // ============================================================
    // Tool Create Command Options
    // ============================================================

    public static readonly Option<string> NameOption = CreateOption<string>(
        "--name",
        "Name of the tool",
        required: true,
        validator: (opt, result) =>
        {
            var name = result.GetValue(opt);
            if (string.IsNullOrWhiteSpace(name))
            {
                result.AddError(ErrorMessageHelper.InvalidParameter("Tool name must not be empty."));
            }
            else if (name.Any(char.IsWhiteSpace))
            {
                result.AddError(ErrorMessageHelper.InvalidParameter("Tool name must not contain whitespace."));
            }
        }
    );

    public static readonly Option<string> TypeOption = CreateOption<string>(
        "--type",
        $"Type of the tool ({string.Join(", ", ExtendedToolHelper.GetAvailableToolTypes().Select(t => t.Name))})",
        required: true,
        validator: (opt, result) =>
        {
            var type = result.GetValue(opt);
            if (!string.IsNullOrEmpty(type))
            {
                var availableTypes = ExtendedToolHelper.GetAvailableToolTypes();
                var isValid = availableTypes.Any(t => t.Name.Equals(type, StringComparison.OrdinalIgnoreCase));

                if (!isValid)
                {
                    var typeNames = string.Join(", ", availableTypes.Select(t => $"'{t.Name}'"));
                    result.AddError(ErrorMessageHelper.InvalidParameter($"Invalid tool type '{type}'. Supported types: {typeNames}"));
                }
            }
        }
    );

    public static readonly Option<string> PathOption = CreateOption<string>(
        "--path",
        "Custom path under tools directory (e.g., 'StorageOperations')"
    );

    public static readonly Option<string> DescriptionOption = CreateOption<string>(
        "--description",
        "Description of the tool"
    );

    public static readonly Option<string> ConnectorOption = CreateOption<string>(
        "--connector",
        "Connector name for the tool"
    );

    public static readonly Option<string> DatabaseOption = CreateOption<string>(
        "--database",
        "Database name for KustoTool"
    );

    public static readonly Option<string> QueryOption = CreateOption<string>(
        "--query",
        "Query for KustoTool"
    );

    public static readonly Option<string> TemplateOption = CreateOption<string>(
        "--template",
        "URL template for LinkTool"
    );

    public static readonly Option<string[]> ParameterOption = new("--parameter")
    {
        Description = "Tool parameter in format 'name:type:description' (can be specified multiple times)",
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    // ============================================================
    // Tool Validate Command Options
    // ============================================================

    public static readonly Option<string> ValidateNameOption = CreateOption<string>(
        "--name",
        "Name of the tool to validate"
    );

    public static readonly Option<bool> ValidateAllOption = CreateOption<bool>(
        "--all",
        "Validate all YAML files in the tools directory"
    );

    // ============================================================
    // Tool Apply Command Options
    // ============================================================

    public static readonly Option<string> ApplyNameOption = CreateOption<string>(
        "--name",
        "Name of the tool to apply",
        required: true
    );

    public static readonly Option<bool> ApplyDryRunOption = CreateOption<bool>(
        "--dry-run",
        "Show what would be applied without making changes"
    );

    // ============================================================
    // Tool Delete Command Options
    // ============================================================

    public static readonly Option<string> DeleteNameOption = CreateOption<string>(
        "--name",
        "Name of the tool to delete",
        required: true
    );

    public static readonly Option<bool> DeleteDryRunOption = CreateOption<bool>(
        "--dry-run",
        "Show what would be deleted without making changes"
    );

    // ============================================================
    // Tool Show-Types Command Options
    // ============================================================

    public static readonly Option<string> ShowTypesTypeOption = CreateOption<string>(
        "--type",
        "Show details for a specific tool type"
    );

    // ============================================================
    // Tool Diff Command Options
    // ============================================================

    public static readonly Option<string> DiffNameOption = CreateOption<string>(
        "--name",
        "Name of the tool to diff",
        required: true
    );

    public static readonly Option<string> DiffToolOption = CreateOption<string>(
        "--tool",
        "Diff tool to use: git, vim, code (default: git)"
    );

    public static readonly Option<bool> DiffRawOption = CreateOption<bool>(
        "--raw",
        "Show inline diff instead of launching external tool"
    );

    // ============================================================
    // Tool List Command Options
    // ============================================================

    public static readonly Option<string?> ListSearchOption = CreateOption<string?>(
        "--search",
        "Search filter for tool names or descriptions"
    );

    public static readonly Option<string?> ListNameOption = CreateOption<string?>(
        "--name",
        "Get a specific tool by name and output the full YAML"
    );

    public static readonly Option<bool> ListDetailOption = CreateOption<bool>(
        "--detail",
        "Output the full YAML for all tools in the list"
    );

    // ============================================================
    // Tool Migrate Command Options
    // ============================================================

    public static readonly Option<string> MigrateNameOption = CreateOption<string>(
        "--name",
        "Name of the tool to migrate from V1 to V2"
    );

    public static readonly Option<bool> MigrateAllOption = CreateOption<bool>(
        "--all",
        "Migrate all V1 tools to V2 format"
    );

    public static readonly Option<bool> MigrateDryRunOption = CreateOption<bool>(
        "--dry-run",
        "Preview migration changes without modifying files"
    );
}
