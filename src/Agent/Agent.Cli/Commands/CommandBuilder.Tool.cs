// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.CommandLine.Parsing;
using Agent.Cli.Helpers;
using Agent.Cli.Models;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ToolCommand
    {
        public static Command Build()
        {
            var cmd = new Command("tool", "Tool commands for managing SRE automation tools")
            {
                CreateToolCreateCommand(),
                CreateToolValidateCommand(),
                CreateToolApplyCommand(),
                CreateToolDeleteCommand(),
                CreateToolDiffCommand(),
                CreateToolMigrateCommand(),
                CreateToolShowTypesCommand(),
                CreateToolShowConnectorsCommand(),
                CreateToolListCommand()
            };

            return cmd;
        }

        private static Command CreateToolCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.Tool.CreateDescription)
            {
                ToolCommandOptions.Create.NameOption,
                ToolCommandOptions.Create.TypeOption,
                ToolCommandOptions.Create.PathOption,
                ToolCommandOptions.Create.ConnectorOption,
                ToolCommandOptions.Create.DatabaseOption,
                ToolCommandOptions.Create.DescriptionOption,
                ToolCommandOptions.Create.QueryOption,
                ToolCommandOptions.Create.TemplateOption,
                ToolCommandOptions.Create.FunctionCodeOption,
                ToolCommandOptions.Create.TimeoutSecondsOption,
                ToolCommandOptions.Create.DependenciesOption,
                ToolCommandOptions.Create.ParameterOption,
                ToolCommandOptions.Create.UrlOption,
                ToolCommandOptions.Create.MethodOption,
                ToolCommandOptions.Create.BodyOption,
                ToolCommandOptions.Create.HeaderOption,
                ToolCommandOptions.Create.AuthConnectorOption,
                ToolCommandOptions.Create.AuthScopeOption
            };

            // Define common options shared across all tool types
            var commonOptions = new Option[]
            {
                ToolCommandOptions.Create.NameOption,
                ToolCommandOptions.Create.TypeOption,
                ToolCommandOptions.Create.PathOption,
                ToolCommandOptions.Create.DescriptionOption,
                ToolCommandOptions.Create.ParameterOption
            };

            // Define tool-specific options
            var kustoToolOptions = new Option[]
            {
                ToolCommandOptions.Create.ConnectorOption,
                ToolCommandOptions.Create.DatabaseOption,
                ToolCommandOptions.Create.QueryOption
            };

            var linkToolOptions = new Option[]
            {
                ToolCommandOptions.Create.TemplateOption
            };

            var pythonToolOptions = new Option[]
            {
                ToolCommandOptions.Create.FunctionCodeOption,
                ToolCommandOptions.Create.TimeoutSecondsOption,
                ToolCommandOptions.Create.DependenciesOption
            };

            var httpClientToolOptions = new Option[]
            {
                ToolCommandOptions.Create.UrlOption,
                ToolCommandOptions.Create.MethodOption,
                ToolCommandOptions.Create.BodyOption,
                ToolCommandOptions.Create.HeaderOption,
                ToolCommandOptions.Create.AuthConnectorOption,
                ToolCommandOptions.Create.AuthScopeOption,
                ToolCommandOptions.Create.TimeoutSecondsOption
            };

            // Define tool type to specific options mapping (empty string = Common options)
            var toolTypeOptions = new Dictionary<string, Option[]>
            {
                [""] = commonOptions,
                [ToolName.KustoTool] = kustoToolOptions,
                [ToolName.LinkTool] = linkToolOptions,
                [ToolName.PythonTool] = pythonToolOptions,
                [ToolName.HttpClientTool] = httpClientToolOptions
            };

            // Add custom help option to override default behavior
            CustomizeToolCreateHelp(cmd, toolTypeOptions);

            cmd.Validators.Add(result =>
            {
                // Validate tool name
                var name = result.GetValue(ToolCommandOptions.Create.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");
                if (!isValid)
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }

                // Validate tool type
                var type = result.GetValue(ToolCommandOptions.Create.TypeOption);
                if (!string.IsNullOrEmpty(type))
                {
                    var availableTypes = ExtendedToolHelper.GetAvailableToolTypes();
                    var isValidType = availableTypes.Any(t => t.Name.Equals(type, StringComparison.OrdinalIgnoreCase));

                    if (!isValidType)
                    {
                        var typeNames = string.Join(", ", availableTypes.Select(t => $"'{t.Name}'"));
                        result.AddError(ErrorMessageHelper.InvalidParameter($"Invalid tool type '{type}'. Supported types: {typeNames}"));
                    }
                    else
                    {
                        // Tool type-specific validation
                        ValidateToolTypeSpecificOptions(result, type, toolTypeOptions);
                    }
                }
            });

            cmd.SetAction(ToolCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        /// <summary>
        /// Validates tool type-specific required and conflicting options.
        /// </summary>
        private static void ValidateToolTypeSpecificOptions(CommandResult result, string type, Dictionary<string, Option[]> toolTypeOptions)
        {
            // Get all tool-specific options (everything except common options)
            var allToolSpecificOptions = toolTypeOptions
                .Where(kvp => kvp.Key != "")
                .SelectMany(kvp => kvp.Value)
                .ToHashSet();

            // Get valid options for the specified tool type
            var validOptions = toolTypeOptions.TryGetValue(type, out var typeOptions)
                ? typeOptions.ToHashSet()
                : new HashSet<Option>();

            // Check each tool-specific option
            foreach (var option in allToolSpecificOptions)
            {
                // Skip if this option is valid for the current tool type
                if (validOptions.Contains(option))
                    continue;

                // Check if this option was provided
                var hasValue = option switch
                {
                    Option<string> strOpt => !string.IsNullOrEmpty(result.GetValue(strOpt)),
                    Option<int> intOpt => result.GetValue(intOpt) > 0,
                    Option<string[]> arrOpt => result.GetValue(arrOpt)?.Length > 0,
                    _ => false
                };

                if (hasValue)
                {
                    var optionName = option.Aliases.FirstOrDefault() ?? option.Name;
                    result.AddError(ErrorMessageHelper.InvalidParameter($"{optionName} is not valid for {type}"));
                }
            }
        }

        /// <summary>
        /// Customizes the help option for tool create command to show type-specific option groups.
        /// </summary>
        private static void CustomizeToolCreateHelp(Command cmd, Dictionary<string, Option[]> toolTypeOptions)
        {
            // Find and replace the default help option
            var defaultHelpOption = cmd.Options.FirstOrDefault(o => o is System.CommandLine.Help.HelpOption);
            if (defaultHelpOption != null)
            {
                cmd.Options.Remove(defaultHelpOption);
            }

            var customHelpOption = new System.CommandLine.Help.HelpOption();
            customHelpOption.Action = new ToolCreateCustomHelpAction(cmd, toolTypeOptions);
            cmd.Add(customHelpOption);
        }

        /// <summary>
        /// Custom help action for tool create command that groups options by tool type.
        /// </summary>
        private class ToolCreateCustomHelpAction : System.CommandLine.Invocation.SynchronousCommandLineAction
        {
            private readonly Command _command;
            private readonly Dictionary<string, Option[]> _toolTypeOptions;

            public ToolCreateCustomHelpAction(Command command, Dictionary<string, Option[]> toolTypeOptions)
            {
                _command = command;
                _toolTypeOptions = toolTypeOptions;
            }

            public override int Invoke(ParseResult parseResult)
            {
                ShowToolCreateHelp();
                return 0;
            }

            private void ShowToolCreateHelp()
            {
                // Show description
                ConsoleUI.Write("Description:");
                ConsoleUI.Write($"  {_command.Description}");
                ConsoleUI.Write("");

                // Show usage
                ConsoleUI.Write("Usage:");
                ConsoleUI.Write($"  srectl tool {_command.Name} [options]");
                ConsoleUI.Write("");

                // Show Common Options
                if (_toolTypeOptions.TryGetValue("", out var commonOptions))
                {
                    ConsoleUI.Write("Common Options:");
                    ConsoleUI.WriteOptions(commonOptions);
                    ConsoleUI.Write("");
                }

                // Show KustoTool Options with examples
                if (_toolTypeOptions.TryGetValue(ToolName.KustoTool, out var kustoOptions))
                {
                    ConsoleUI.Write("KustoTool Options:");
                    ConsoleUI.WriteOptions(kustoOptions);
                    ConsoleUI.Write("");
                    var kustoExamples = new (string Comment, string Command)[]
                    {
                        ("Create a KustoTool with all parameters", "srectl tool create --name QueryMetrics --type KustoTool --connector analytics-cluster --database LogsDB --query \"MyTable | take 10\" --parameter limit"),
                        ("Create a KustoTool with minimal options", "srectl tool create --name GetLogs --type KustoTool --connector logs-cluster --database LogsDB")
                    };
                    ConsoleUI.WriteExamples(kustoExamples, indent: 2, pad: 2);
                }

                // Show LinkTool Options with examples
                if (_toolTypeOptions.TryGetValue(ToolName.LinkTool, out var linkOptions))
                {
                    ConsoleUI.Write("LinkTool Options:");
                    ConsoleUI.WriteOptions(linkOptions);
                    ConsoleUI.Write("");
                    var linkExamples = new (string Comment, string Command)[]
                    {
                        ("Create a LinkTool with template", "srectl tool create --name ServiceDashboard --type LinkTool --template \"https://dashboard.example.com/{serviceId}\" --parameter serviceId"),
                        ("Create a LinkTool with minimal options", "srectl tool create --name DocLink --type LinkTool --description \"Link to documentation\"")
                    };
                    ConsoleUI.WriteExamples(linkExamples, indent: 2, pad: 2);
                }

                // Show PythonTool Options with examples
                if (_toolTypeOptions.TryGetValue(ToolName.PythonTool, out var pythonOptions))
                {
                    ConsoleUI.Write("PythonTool Options:");
                    ConsoleUI.WriteOptions(pythonOptions);
                    ConsoleUI.Write("");
                    var pythonExamples = new (string Comment, string Command)[]
                    {
                        ("Create a PythonTool with dependencies", "srectl tool create --name ProcessData --type PythonTool --function-code \"def run(params): return params\" --dependency requests --timeout 60"),
                        ("Create a PythonTool with custom path", "srectl tool create --name DataProcessor --type PythonTool --path \"Data/Processing\" --dependency pandas")
                    };
                    ConsoleUI.WriteExamples(pythonExamples, indent: 2, pad: 2);
                }

                // Show HttpClientTool Options with examples
                if (_toolTypeOptions.TryGetValue(ToolName.HttpClientTool, out var httpClientOptions))
                {
                    ConsoleUI.Write("HttpClientTool Options:");
                    ConsoleUI.WriteOptions(httpClientOptions);
                    ConsoleUI.Write("");
                    var httpClientExamples = new (string Comment, string Command)[]
                    {
                        ("Create a simple GET request tool", "srectl tool create --name GetUserInfo --type HttpClientTool --url \"https://api.example.com/users/{{userId}}\" --method GET --parameter userId:string:User ID"),
                        ("Create a POST request tool with body", "srectl tool create --name CreateTicket --type HttpClientTool --url \"https://api.example.com/tickets\" --method POST --body \"{\\\"title\\\": \\\"{{title}}\\\"}\" --header \"Content-Type:application/json\""),
                        ("Create an authenticated API call", "srectl tool create --name CallSecureApi --type HttpClientTool --url \"https://api.example.com/data\" --method GET --auth-connector my-oauth-connector --auth-scope \"api://example/.default\"")
                    };
                    ConsoleUI.WriteExamples(httpClientExamples, indent: 2, pad: 2);
                }
            }

        }

        private static Command CreateToolValidateCommand()
        {
            var cmd = new Command("validate", CommandExamples.Tool.ValidateDescription)
            {
                ToolCommandOptions.Validate.NameOption,
                ToolCommandOptions.Validate.AllOption
            };

            // Validate mutually exclusive options
            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(ToolCommandOptions.Validate.NameOption);
                var all = result.GetValue(ToolCommandOptions.Validate.AllOption);

                if (all && !string.IsNullOrWhiteSpace(name))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --all together"));
                }
                else if (!all && string.IsNullOrWhiteSpace(name))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Must specify either --name or --all"));
                }
            });

            cmd.SetAction((parseResult, cancellationToken) => ToolCommandHandlers.HandleValidateCommand(parseResult));
            return cmd;
        }

        private static Command CreateToolApplyCommand()
        {
            var cmd = new Command("apply", CommandExamples.Tool.ApplyDescription)
            {
                ToolCommandOptions.Apply.NameOption,
                ToolCommandOptions.Apply.DryRunOption
            };

            cmd.Validators.Add(result =>
            {
                // Validate tool name
                var name = result.GetValue(ToolCommandOptions.Apply.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");
                if (!isValid)
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(ToolCommandHandlers.HandleApplyCommand);
            return cmd;
        }

        private static Command CreateToolDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Tool.DeleteDescription)
            {
                ToolCommandOptions.Delete.NameOption,
                ToolCommandOptions.Delete.DryRunOption,
                ToolCommandOptions.Delete.DeleteLocalFilesOption
            };

            // Add validator for name
            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(ToolCommandOptions.Delete.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");
                if (!isValid)
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(ToolCommandHandlers.HandleDeleteCommand);
            return cmd;
        }

        private static Command CreateToolDiffCommand()
        {
            var cmd = new Command("diff", CommandExamples.Tool.DiffDescription)
            {
                ToolCommandOptions.Diff.NameOption,
                ToolCommandOptions.Diff.ToolOption,
                ToolCommandOptions.Diff.RawOption
            };

            cmd.SetAction(ToolCommandHandlers.HandleDiffCommand);
            return cmd;
        }

        private static Command CreateToolMigrateCommand()
        {
            var cmd = new Command("migrate", CommandExamples.Tool.MigrateDescription)
            {
                ToolCommandOptions.Migrate.NameOption,
                ToolCommandOptions.Migrate.AllOption,
                ToolCommandOptions.Migrate.DryRunOption
            };

            // Validate mutually exclusive options
            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(ToolCommandOptions.Migrate.NameOption);
                var all = result.GetValue(ToolCommandOptions.Migrate.AllOption);

                if (all && !string.IsNullOrWhiteSpace(name))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --all together"));
                }
                else if (!all && string.IsNullOrWhiteSpace(name))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Must specify either --name or --all"));
                }
            });

            cmd.SetAction((parseResult, cancellationToken) => ToolCommandHandlers.HandleMigrateCommand(parseResult));
            return cmd;
        }

        private static Command CreateToolShowTypesCommand()
        {
            var cmd = new Command("show-types", CommandExamples.Tool.ShowTypesDescription)
            {
                ToolCommandOptions.ShowTypes.TypeOption
            };

            cmd.SetAction(ToolCommandHandlers.HandleShowTypesCommand);
            return cmd;
        }

        private static Command CreateToolShowConnectorsCommand()
        {
            var cmd = new Command("show-connectors", CommandExamples.Tool.ShowConnectorsDescription);

            cmd.SetAction(ToolCommandHandlers.HandleShowConnectorsCommand);
            return cmd;
        }

        private static Command CreateToolListCommand()
        {
            var cmd = new Command("list", CommandExamples.Tool.ListDescription)
            {
                ToolCommandOptions.List.SearchOption,
                ToolCommandOptions.List.NameOption,
                ToolCommandOptions.List.DetailOption
            };

            // Validate mutually exclusive options
            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(ToolCommandOptions.List.NameOption);
                var search = result.GetValue(ToolCommandOptions.List.SearchOption);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(search))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --search together"));
                }
            });

            cmd.SetAction(ToolCommandHandlers.HandleListCommand);
            return cmd;
        }
    }
}
