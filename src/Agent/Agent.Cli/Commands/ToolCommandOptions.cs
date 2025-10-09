// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for tool commands.
/// </summary>
public static class ToolCommandOptions
{
    // Global debug option for all tool commands
    public static readonly Option<bool> DebugOption = new("--debug")
    {
        Description = "Enable verbose debug logging for network calls and operations"
    };

    // Tool options
    public static readonly Option<string> NameOption = new("--name") { Description = "ToolName", Required = true };

    public static readonly Option<string> TypeOption = new("--type")
    {
        Description = "ToolType",
        Required = true
    };

    public static readonly Option<string> PathOption = new("--path") { Description = "Custom path under tools directory (e.g., 'StorageOperations')" };
    public static readonly Option<string[]> ExtraOption = new("--extra")
    {
        Description = "AdditionArgumentsKeyValuePairs",
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    // Tool validate options
    public static readonly Option<string> NameOptionValidate = new("--name") { Description = "ToolName" };
    public static readonly Option<bool> AllOption = new("--all") { Description = "ValidateAllYAMLFilesInToolsDirectory" };

    // Tool apply options
    public static readonly Option<string> ApplyNameOption = new("--name") { Required = true };
    public static readonly Option<bool> DryRunOption = new("--dry-run") { Description = "Show what would be applied without making changes" };

    // Tool delete options
    public static readonly Option<string> DeleteNameOption = new("--name") { Required = true };
    public static readonly Option<bool> DeleteDryRunOption = new("--dry-run") { Description = "Show what would be deleted without making changes" };

    // Tool show-types options
    public static readonly Option<bool> VerboseOption = new("--verbose") { Description = "Show detailed information including assembly and namespace" };
    public static readonly Option<string> TypeFilterOption = new("--type") { Description = "Show details for a specific tool type" };

    // Tool diff command options
    public static readonly Option<string> DiffNameOption = new("--name") { Required = true };
    public static readonly Option<string> DiffToolOption = new("--tool")
    {
        Description = "Diff tool to use: git, vim, code (default: git)"
    };
    public static readonly Option<bool> DiffRawOption = new("--raw")
    {
        Description = "Show inline diff instead of launching external tool"
    };

    // Tool list command options
    public static readonly Option<bool> ListAllOption = new("--all")
    {
        Description = "List all tools (both extended tools and platform tools)"
    };
}
