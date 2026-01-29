// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class AgentCommand
    {
        public static Command Build()
        {
            var cmd = new Command("agent", "Agent commands for managing SRE automation agents")
            {
                CreateAgentCreateCommand(),
                CreateAgentValidateCommand(),
                CreateAgentApplyCommand(),
                CreateAgentDeleteCommand(),
                CreateAgentTestCommand(),
                CreateAgentDiffCommand(),
                CreateAgentMigrateCommand(),
                CreateAgentListCommand()
            };

            return cmd;
        }

        private static Command CreateAgentCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.Agent.CreateDescription)
            {
                AgentCommandOptions.Create.NameOption,
                AgentCommandOptions.Create.InstructionsOption,
                AgentCommandOptions.Create.ToolsOption,
                AgentCommandOptions.Create.HandoffDescriptionOption,
                AgentCommandOptions.Create.HandoffsOption,
                AgentCommandOptions.Create.AllowParallelToolCallsOption,
                AgentCommandOptions.Create.MaxReflectionCountOption,
                AgentCommandOptions.Create.CriticPromptPathOption,
                AgentCommandOptions.Create.CriticOnHandoffOption,
                AgentCommandOptions.Create.CustomReflectionNoteOption,
                AgentCommandOptions.Create.CommonPromptsOption,
                AgentCommandOptions.Create.TemperatureOption,
                AgentCommandOptions.Create.OutputTypeOption,
                AgentCommandOptions.Create.VanillaModeOption,
                AgentCommandOptions.Create.SmartOption,
                AgentCommandOptions.Create.EnableSkillsOption,
                AgentCommandOptions.Create.AddSystemSkillsOption
            };

            // Add name validation
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(AgentCommandOptions.Create.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "agent");

                if (!isValid)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(AgentCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateAgentValidateCommand()
        {
            var cmd = new Command("validate", CommandExamples.Agent.ValidateDescription)
            {
                AgentCommandOptions.Validate.NameOption,
                AgentCommandOptions.Validate.FileOption,
                AgentCommandOptions.Validate.AllOption,
                AgentCommandOptions.Validate.CheckToolsOption
            };

            cmd.SetAction(AgentCommandHandlers.HandleValidateCommand);
            return cmd;
        }

        private static Command CreateAgentApplyCommand()
        {
            var cmd = new Command("apply", CommandExamples.Agent.ApplyDescription)
            {
                AgentCommandOptions.Apply.NameOption,
                AgentCommandOptions.Apply.DryRunOption
            };

            // Add name validation
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(AgentCommandOptions.Apply.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "agent");

                if (!isValid)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(AgentCommandHandlers.HandleApplyCommand);
            return cmd;
        }

        private static Command CreateAgentDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Agent.DeleteDescription)
            {
                AgentCommandOptions.Delete.NameOption,
                AgentCommandOptions.Delete.DeleteLocalFilesOption
            };

            // Add name validation
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(AgentCommandOptions.Delete.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "agent");

                if (!isValid)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(AgentCommandHandlers.HandleDeleteCommand);
            return cmd;
        }

        private static Command CreateAgentTestCommand()
        {
            var cmd = new Command("test", CommandExamples.Agent.TestDescription)
            {
                AgentCommandOptions.Test.NameOption,
                AgentCommandOptions.Test.MessageOption,
                AgentCommandOptions.Test.UserIdOption,
                AgentCommandOptions.Test.DisplayNameOption,
                AgentCommandOptions.Test.WaitOption,
                AgentCommandOptions.Test.NoWaitOption
            };

            cmd.SetAction(AgentCommandHandlers.HandleTestCommand);
            return cmd;
        }

        private static Command CreateAgentDiffCommand()
        {
            var cmd = new Command("diff", CommandExamples.Agent.DiffDescription)
            {
                AgentCommandOptions.Diff.NameOption,
                AgentCommandOptions.Diff.ToolOption,
                AgentCommandOptions.Diff.RawOption
            };

            cmd.SetAction(AgentCommandHandlers.HandleDiffCommand);
            return cmd;
        }

        private static Command CreateAgentMigrateCommand()
        {
            var cmd = new Command("migrate", CommandExamples.Agent.MigrateDescription)
            {
                AgentCommandOptions.Migrate.NameOption,
                AgentCommandOptions.Migrate.AllOption,
                AgentCommandOptions.Migrate.DryRunOption
            };

            cmd.SetAction(AgentCommandHandlers.HandleMigrateCommand);
            return cmd;
        }

        private static Command CreateAgentListCommand()
        {
            var cmd = new Command("list", CommandExamples.Agent.ListDescription)
            {
                AgentCommandOptions.List.SearchOption,
                AgentCommandOptions.List.NameOption,
                AgentCommandOptions.List.DetailOption
            };

            // Validate mutually exclusive options
            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(AgentCommandOptions.List.NameOption);
                var search = result.GetValue(AgentCommandOptions.List.SearchOption);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(search))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --search together"));
                }
            });

            cmd.SetAction(AgentCommandHandlers.HandleListCommand);
            return cmd;
        }
    }
}
