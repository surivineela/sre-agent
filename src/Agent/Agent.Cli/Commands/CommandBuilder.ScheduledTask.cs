// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ScheduledTaskCommand
    {
        public static Command Build()
        {
            var scheduledTask = new Command("scheduledtask", "Manage scheduled tasks for automated agent operations")
            {
                CreateCreateCommand(),
                CreateListCommand(),
                CreateGetCommand(),
                CreatePauseCommand(),
                CreateResumeCommand(),
                CreateDeleteCommand(),
                CreateQuickstartCommand(),
                CreateApplyCommand()
            };

            return scheduledTask;
        }

        private static Command CreateCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.ScheduledTask.CreateDescription)
            {
                ScheduledTaskCommandOptions.Create.NameOption,
                ScheduledTaskCommandOptions.Create.DescriptionOption,
                ScheduledTaskCommandOptions.Create.CronExpressionOption,
                ScheduledTaskCommandOptions.Create.AgentPromptOption,
                ScheduledTaskCommandOptions.Create.AgentOption,
                ScheduledTaskCommandOptions.Create.StartTimeOption,
                ScheduledTaskCommandOptions.Create.EndTimeOption,
                ScheduledTaskCommandOptions.Create.ThreadIdOption,
                ScheduledTaskCommandOptions.Create.MaxExecutionsOption,
                ScheduledTaskCommandOptions.Create.NotificationChannelOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateListCommand()
        {
            var cmd = new Command("list", CommandExamples.ScheduledTask.ListDescription)
            {
                ScheduledTaskCommandOptions.List.VerboseOption,
                ScheduledTaskCommandOptions.List.FilterThreadIdOption,
                ScheduledTaskCommandOptions.List.FilterStatusOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleListCommand);
            return cmd;
        }

        private static Command CreateGetCommand()
        {
            var cmd = new Command("get", CommandExamples.ScheduledTask.GetDescription)
            {
                ScheduledTaskCommandOptions.Get.TaskIdOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleGetCommand);
            return cmd;
        }

        private static Command CreatePauseCommand()
        {
            var cmd = new Command("pause", CommandExamples.ScheduledTask.PauseDescription)
            {
                ScheduledTaskCommandOptions.Pause.TaskIdOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandlePauseCommand);
            return cmd;
        }

        private static Command CreateResumeCommand()
        {
            var cmd = new Command("resume", CommandExamples.ScheduledTask.ResumeDescription)
            {
                ScheduledTaskCommandOptions.Resume.TaskIdOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleResumeCommand);
            return cmd;
        }

        private static Command CreateDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.ScheduledTask.DeleteDescription)
            {
                ScheduledTaskCommandOptions.Delete.TaskIdOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleDeleteCommand);
            return cmd;
        }

        private static Command CreateQuickstartCommand()
        {
            var cmd = new Command("quickstart", "Interactive hello-world scheduled task wizard")
            {
                ScheduledTaskCommandOptions.Quickstart.NameOption,
                ScheduledTaskCommandOptions.Quickstart.CronOption,
                ScheduledTaskCommandOptions.Quickstart.DurationHoursOption,
                ScheduledTaskCommandOptions.Quickstart.AgentOption,
                ScheduledTaskCommandOptions.Quickstart.ApplyOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleQuickstartCommand);
            return cmd;
        }

        private static Command CreateApplyCommand()
        {
            var cmd = new Command("apply", "Apply a ScheduledTask YAML manifest (apiVersion/kind/spec)")
            {
                ScheduledTaskCommandOptions.Apply.FileOption
            };

            cmd.SetAction(ScheduledTaskCommandHandlers.HandleApplyYamlCommand);
            return cmd;
        }
    }
}
