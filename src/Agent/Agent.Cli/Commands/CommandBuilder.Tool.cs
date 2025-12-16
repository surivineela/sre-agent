// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

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

            // Add default action for tool command to show formatted help
            cmd.SetAction(pr => ShowFormattedToolHelp(cmd));

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
                ToolCommandOptions.Create.ParameterOption
            };

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
                }
            });

            cmd.SetAction(ToolCommandHandlers.HandleCreateCommand);
            return cmd;
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
                ToolCommandOptions.Delete.DryRunOption
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
