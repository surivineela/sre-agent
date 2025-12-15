// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class CommonPromptCommand
    {
        public static Command Build()
        {
            var cmd = new Command("common-prompt", "Common prompt commands for managing shared prompts")
            {
                CreateCreateCommand(),
                CreateGetCommand(),
                CreateApplyCommand(),
                CreateDeleteCommand()
            };

            return cmd;
        }

        private static Command CreateCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.CommonPrompt.CreateDescription)
            {
                CommonPromptCommandOptions.Create.NameOption,
                CommonPromptCommandOptions.Create.PathOption,
                CommonPromptCommandOptions.Create.PromptOption,
                CommonPromptCommandOptions.Create.OwnerOption,
                CommonPromptCommandOptions.Create.TagOption
            };

            cmd.Validators.Add(result =>
            {
                // Validate prompt name
                var name = result.GetValue(CommonPromptCommandOptions.Create.NameOption);
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Common prompt name must not be empty."));
                }
                else if (name.Any(char.IsWhiteSpace))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Common prompt name must not contain whitespace."));
                }
            });

            cmd.SetAction(CommonPromptCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateGetCommand()
        {
            var cmd = new Command("get", CommandExamples.CommonPrompt.GetDescription)
            {
                CommonPromptCommandOptions.Get.SearchOption,
                CommonPromptCommandOptions.Get.NameOption,
                CommonPromptCommandOptions.Get.DetailOption
            };

            // Validate mutually exclusive options
            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(CommonPromptCommandOptions.Get.NameOption);
                var search = result.GetValue(CommonPromptCommandOptions.Get.SearchOption);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(search))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --search together"));
                }
            });

            cmd.SetAction(CommonPromptCommandHandlers.HandleGetCommand);
            return cmd;
        }

        private static Command CreateApplyCommand()
        {
            var cmd = new Command("apply", CommandExamples.CommonPrompt.ApplyDescription)
            {
                CommonPromptCommandOptions.Apply.NameOption,
                CommonPromptCommandOptions.Apply.DryRunOption
            };

            cmd.SetAction(CommonPromptCommandHandlers.HandleApplyCommand);
            return cmd;
        }

        private static Command CreateDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.CommonPrompt.DeleteDescription)
            {
                CommonPromptCommandOptions.Delete.NameOption,
                CommonPromptCommandOptions.Delete.DryRunOption
            };

            cmd.SetAction(CommonPromptCommandHandlers.HandleDeleteCommand);
            return cmd;
        }
    }
}
