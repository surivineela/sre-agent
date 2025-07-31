using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for tool commands.
/// </summary>
public static class ToolCommandOptions
{
    // Tool options
    public static readonly Option<string> NameOption = new("--name", "ToolName") { Required = true };
    public static readonly Option<string> TypeOption = new("--type", "ToolType") { Required = true };
    public static readonly Option<string[]> ExtraOption = new("--extra", "AdditionArgumentsKeyValuePairs")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    // Tool validate options
    public static readonly Option<string> NameOptionValidate = new("--name", "ToolName");
    public static readonly Option<bool> AllOption = new("--all", "ValidateAllYAMLFilesInToolsDirectory");

    // Tool apply options
    public static readonly Option<string> ApplyNameOption = new("--name") { Required = true };

    // Tool show-types options
    public static readonly Option<bool> VerboseOption = new("--verbose", "ShowDetailedInformation");
    public static readonly Option<string> TypeFilterOption = new("--type", "ShowDetailsForSpecificToolType");
}
