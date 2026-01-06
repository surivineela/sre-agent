// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ThreadCommand
    {
        public static Command Build()
        {
            var thread = new Command("thread", "Thread management commands")
            {
                CreateNewCommand(),
                CreateContinueCommand(),
                CreateListCommand(),
                CreateDeleteCommand(),
                CreateTrackCommand(),
                CreateApplyCommand()
            };

            return thread;
        }

        private static Command CreateNewCommand()
        {
            var cmd = new Command("new", CommandExamples.Thread.NewDescription)
            {
                ThreadCommandOptions.New.MessageOption,
                ThreadCommandOptions.New.UserIdOption,
                ThreadCommandOptions.New.DisplayNameOption,
                ThreadCommandOptions.New.AgentNameOption,
                ThreadCommandOptions.New.WaitOption,
                ThreadCommandOptions.New.NoWaitOption
            };

            cmd.Validators.Add(result =>
            {
                var w = result.GetValue(ThreadCommandOptions.New.WaitOption);
                var nw = result.GetValue(ThreadCommandOptions.New.NoWaitOption);
                if (w && nw) result.AddError("Specify either --wait or --no-wait, not both.");

                var message = result.GetValue(ThreadCommandOptions.New.MessageOption);
                if (nw && string.IsNullOrWhiteSpace(message))
                {
                    result.AddError("--no-wait requires --message to be specified");
                }
            });

            cmd.SetAction(ThreadCommandHandlers.HandleThreadNewCommand);
            return cmd;
        }

        private static Command CreateContinueCommand()
        {
            var cmd = new Command("continue", CommandExamples.Thread.ContinueDescription)
            {
                ThreadCommandOptions.Continue.ThreadIdOption,
                ThreadCommandOptions.Continue.MessageOption,
                ThreadCommandOptions.Continue.UserIdOption,
                ThreadCommandOptions.Continue.DisplayNameOption,
                ThreadCommandOptions.Continue.WaitOption,
                ThreadCommandOptions.Continue.NoWaitOption
            };

            cmd.Validators.Add(result =>
            {
                var w = result.GetValue(ThreadCommandOptions.Continue.WaitOption);
                var nw = result.GetValue(ThreadCommandOptions.Continue.NoWaitOption);
                if (w && nw) result.AddError("Specify either --wait or --no-wait, not both.");

                var message = result.GetValue(ThreadCommandOptions.Continue.MessageOption);
                if (nw && string.IsNullOrWhiteSpace(message))
                {
                    result.AddError("--no-wait requires --message to be specified");
                }
            });

            cmd.SetAction(ThreadCommandHandlers.HandleThreadContinueCommand);
            return cmd;
        }

        private static Command CreateListCommand()
        {
            var cmd = new Command("list", CommandExamples.Thread.ListDescription);
            cmd.SetAction(ThreadCommandHandlers.HandleThreadListCommand);
            return cmd;
        }

        private static Command CreateDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Thread.DeleteDescription)
            {
                ThreadCommandOptions.Delete.ThreadIdOption
            };

            cmd.SetAction(ThreadCommandHandlers.HandleThreadDeleteCommand);
            return cmd;
        }

        private static Command CreateTrackCommand()
        {
            var cmd = new Command("track", "Track an existing thread for new messages")
            {
                ThreadCommandOptions.Track.ThreadIdOption
            };

            cmd.SetAction(ThreadCommandHandlers.HandleThreadTrackCommand);
            return cmd;
        }

        private static Command CreateApplyCommand()
        {
            var cmd = new Command("apply", "Create a new thread from a YAML manifest (supports starting agent)")
            {
                ThreadCommandOptions.Apply.FileOption
            };

            cmd.SetAction(ThreadCommandHandlers.HandleThreadApplyCommand);
            return cmd;
        }
    }
}
