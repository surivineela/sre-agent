// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class SkillCommand
    {
        public static Command Build()
        {
            var cmd = new Command("skill", "Skill management commands. Apply and manage custom skills for agents to use, or convert an existing agent into a skill.")
            {
                CreateSkillCreateCommand(),
                CreateSkillApplyCommand(),
                CreateSkillConvertCommand(),
                CreateSkillListCommand(),
                CreateSkillSyncCommand(),
                CreateSkillDeleteCommand(),
                CreateSkillMigrateCommand()
            };

            return cmd;
        }

        private static Command CreateSkillCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.Skill.CreateDescription)
            {
                SkillCommandOptions.Create.NameOption,
                SkillCommandOptions.Create.DescriptionOption
            };

            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(SkillCommandOptions.Create.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "skill");

                if (!isValid)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(SkillCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateSkillApplyCommand()
        {
            var cmd = new Command("apply", CommandExamples.Skill.ApplyDescription)
            {
                SkillCommandOptions.Apply.NameOption,
                SkillCommandOptions.Apply.DryRunOption
            };

            cmd.Aliases.Add("upload");

            // Add name validation
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(SkillCommandOptions.Apply.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "skill");

                if (!isValid)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(SkillCommandHandlers.HandleApplyCommand);
            return cmd;
        }

        private static Command CreateSkillConvertCommand()
        {
            var cmd = new Command("convert", CommandExamples.Skill.ConvertDescription)
            {
                SkillCommandOptions.Convert.AgentNameOption,
                SkillCommandOptions.Convert.TopLevelAgentsOption,
                SkillCommandOptions.Convert.OutputPathOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleConvertCommand);
            return cmd;
        }

        private static Command CreateSkillListCommand()
        {
            var cmd = new Command("list", CommandExamples.Skill.ListDescription)
            {
                SkillCommandOptions.List.SearchOption,
                SkillCommandOptions.List.NameOption,
                SkillCommandOptions.List.DetailOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleListCommand);
            return cmd;
        }

        private static Command CreateSkillSyncCommand()
        {
            var cmd = new Command("sync", CommandExamples.Skill.SyncDescription)
            {
                SkillCommandOptions.Sync.NameOption,
                SkillCommandOptions.Sync.AllOption,
                SkillCommandOptions.Sync.PathOption
            };

            cmd.Aliases.Add("download");

            // Add validation for mutually exclusive options
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(SkillCommandOptions.Sync.NameOption);
                var all = pr.GetValue(SkillCommandOptions.Sync.AllOption);

                // Require either --name or --all
                if (string.IsNullOrWhiteSpace(name) && !all)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter("Either --name or --all must be specified"));
                }

                // Cannot use both --name and --all
                if (!string.IsNullOrWhiteSpace(name) && all)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --all options"));
                }

                // Validate name if provided
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "skill");
                    if (!isValid)
                    {
                        pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                    }
                }
            });

            cmd.SetAction(SkillCommandHandlers.HandleSyncCommand);
            return cmd;
        }

        private static Command CreateSkillDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Skill.DeleteDescription)
            {
                SkillCommandOptions.Delete.NameOption,
                SkillCommandOptions.Delete.DryRunOption
            };

            // Add name validation
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(SkillCommandOptions.Delete.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "skill");

                if (!isValid)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(SkillCommandHandlers.HandleDeleteCommand);
            return cmd;
        }

        private static Command CreateSkillMigrateCommand()
        {
            var cmd = new Command("migrate", CommandExamples.Skill.MigrateDescription)
            {
                SkillCommandOptions.Migrate.NameOption,
                SkillCommandOptions.Migrate.AllOption,
                SkillCommandOptions.Migrate.DryRunOption
            };

            // Add validator for mutually exclusive options
            cmd.AddValidator(pr =>
            {
                var name = pr.GetValue(SkillCommandOptions.Migrate.NameOption);
                var all = pr.GetValue(SkillCommandOptions.Migrate.AllOption);

                if (string.IsNullOrWhiteSpace(name) && !all)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter("Either --name or --all must be specified"));
                }

                if (!string.IsNullOrWhiteSpace(name) && all)
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter("Cannot specify both --name and --all"));
                }
            });

            cmd.SetAction(SkillCommandHandlers.HandleMigrateCommand);
            return cmd;
        }
    }
}
