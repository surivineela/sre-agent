// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class IncidentHandlerCommand
    {
        public static Command Build()
        {
            var incidentHandler = new Command("incidenthandler", "Manage incident handlers and filters")
            {
                CreateMapAgentCommand(),
                CreateListCommand(),
                CreateCreateCommand()
            };

            return incidentHandler;
        }

        private static Command CreateMapAgentCommand()
        {
            var cmd = new Command("map-agent", CommandExamples.IncidentHandler.MapAgentDescription)
            {
                IncidentHandlerCommandOptions.MapAgent.NameOption,
                IncidentHandlerCommandOptions.MapAgent.HandlingAgentOption
            };

            cmd.SetAction(IncidentHandlerCommandHandlers.HandleMapAgentCommand);
            return cmd;
        }

        private static Command CreateListCommand()
        {
            var cmd = new Command("list", CommandExamples.IncidentHandler.ListDescription)
            {
                IncidentHandlerCommandOptions.List.VerboseOption
            };

            cmd.SetAction(IncidentHandlerCommandHandlers.HandleListCommand);
            return cmd;
        }

        private static Command CreateCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.IncidentHandler.CreateDescription)
            {
                IncidentHandlerCommandOptions.Create.IdOption,
                IncidentHandlerCommandOptions.Create.NameOption,
                IncidentHandlerCommandOptions.Create.ImpactedServiceOption,
                IncidentHandlerCommandOptions.Create.PriorityOption,
                IncidentHandlerCommandOptions.Create.IncidentTypeOption,
                IncidentHandlerCommandOptions.Create.AlertIdOption,
                IncidentHandlerCommandOptions.Create.TitleContainsOption,
                IncidentHandlerCommandOptions.Create.AgentModeOption,
                IncidentHandlerCommandOptions.Create.HandlingAgentOption,
                IncidentHandlerCommandOptions.Create.OwningTeamIdOption,
                IncidentHandlerCommandOptions.Create.MaxAttemptsOption
            };

            cmd.SetAction(IncidentHandlerCommandHandlers.HandleCreateCommand);
            return cmd;
        }
    }
}
