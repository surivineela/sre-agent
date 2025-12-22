// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class SkillCommand
    {
        public static Command Build()
        {
            var cmd = new Command("skill", "Skill management commands. Upload and manage custom skills for agents to use, or convert an existing agent into a skill.")
            {
                CreateSkillCreateCommand(),
                CreateSkillUploadCommand(),
                CreateSkillConvertCommand(),
                CreateSkillListCommand(),
                CreateSkillDownloadCommand(),
                CreateSkillDeleteCommand()
            };

            return cmd;
        }

        private static Command CreateSkillCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.Skill.CreateDescription)
            {
                SkillCommandOptions.CreateNameOption,
                SkillCommandOptions.CreateOutputPathOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateSkillUploadCommand()
        {
            var cmd = new Command("upload", CommandExamples.Skill.UploadDescription)
            {
                SkillCommandOptions.UploadPathOption,
                SkillCommandOptions.UploadFolderOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleUploadCommand);
            return cmd;
        }

        private static Command CreateSkillConvertCommand()
        {
            var cmd = new Command("convert", CommandExamples.Skill.ConvertDescription)
            {
                SkillCommandOptions.ConvertAgentNameOption,
                SkillCommandOptions.ConvertTopLevelAgentsOption,
                SkillCommandOptions.ConvertOutputPathOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleConvertCommand);
            return cmd;
        }

        private static Command CreateSkillListCommand()
        {
            var cmd = new Command("list", CommandExamples.Skill.ListDescription)
            {
                SkillCommandOptions.ListLimitOption,
                SkillCommandOptions.ListPageOption,
                SkillCommandOptions.ListSearchOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleListCommand);
            return cmd;
        }

        private static Command CreateSkillDownloadCommand()
        {
            var cmd = new Command("download", CommandExamples.Skill.DownloadDescription)
            {
                SkillCommandOptions.DownloadNameOption,
                SkillCommandOptions.DownloadOutputPathOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleDownloadCommand);
            return cmd;
        }

        private static Command CreateSkillDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Skill.DeleteDescription)
            {
                SkillCommandOptions.DeleteNameOption
            };

            cmd.SetAction(SkillCommandHandlers.HandleDeleteCommand);
            return cmd;
        }
    }
}
