// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class IncidentFilterCommand
    {
        public static Command Build()
        {
            var cmd = new Command("incident-filter", "Incident filter commands for managing incident routing rules")
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
            var cmd = new Command("create", CommandExamples.IncidentFilter.CreateDescription)
            {
                // Required options
                IncidentFilterCommandOptions.Create.NameOption,
                IncidentFilterCommandOptions.Create.PlatformOption,

                // Common filter options
                IncidentFilterCommandOptions.Create.HandlingAgentOption,
                IncidentFilterCommandOptions.Create.ImpactedServiceOption,
                IncidentFilterCommandOptions.Create.PriorityOption,
                IncidentFilterCommandOptions.Create.IncidentTypeOption,
                IncidentFilterCommandOptions.Create.AlertIdOption,
                IncidentFilterCommandOptions.Create.TitleContainsOption,
                IncidentFilterCommandOptions.Create.AgentModeOption,
                IncidentFilterCommandOptions.Create.OwningTeamIdOption,
                IncidentFilterCommandOptions.Create.MaxAutomatedInvestigationAttemptsOption,
                IncidentFilterCommandOptions.Create.DeepInvestigationEnabledOption,
                IncidentFilterCommandOptions.Create.DisabledOption,

                // IcM-specific options
                IncidentFilterCommandOptions.Create.MonitorIdOption,
                IncidentFilterCommandOptions.Create.CreatedByOption,

                // AzMonitor-specific options
                IncidentFilterCommandOptions.Create.TargetResourceTypeOption,
                IncidentFilterCommandOptions.Create.TargetResourceOption
            };

            cmd.Validators.Add(result =>
            {
                // Validate filter name
                var name = result.GetValue(IncidentFilterCommandOptions.Create.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "incident filter");
                if (!isValid)
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }

                // Validate platform
                var platform = result.GetValue(IncidentFilterCommandOptions.Create.PlatformOption);
                if (!string.IsNullOrWhiteSpace(platform) && !IncidentFilterHelper.IsValidPlatform(platform))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter($"Invalid platform '{platform}'. Valid platforms are: {string.Join(", ", IncidentFilterHelper.Platform.All)}"));
                }

                // Validate IcM-specific options are only used with IcM platform
                var monitorId = result.GetValue(IncidentFilterCommandOptions.Create.MonitorIdOption);
                var createdBy = result.GetValue(IncidentFilterCommandOptions.Create.CreatedByOption);
                var hasIcmOptions = !string.IsNullOrWhiteSpace(monitorId) || !string.IsNullOrWhiteSpace(createdBy);

                if (hasIcmOptions && !string.Equals(platform, IncidentFilterHelper.Platform.IcM, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter($"Options --monitor-id and --created-by can only be used with --platform {IncidentFilterHelper.Platform.IcM}"));
                }

                // Validate AzMonitor-specific options are only used with AzMonitor platform
                var targetResourceType = result.GetValue(IncidentFilterCommandOptions.Create.TargetResourceTypeOption);
                var targetResource = result.GetValue(IncidentFilterCommandOptions.Create.TargetResourceOption);
                var hasAzMonitorOptions = !string.IsNullOrWhiteSpace(targetResourceType) || !string.IsNullOrWhiteSpace(targetResource);

                if (hasAzMonitorOptions && !string.Equals(platform, IncidentFilterHelper.Platform.AzMonitor, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter($"Options --target-resource-type and --target-resource can only be used with --platform {IncidentFilterHelper.Platform.AzMonitor}"));
                }
            });

            cmd.SetAction(IncidentFilterCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateGetCommand()
        {
            var cmd = new Command("get", CommandExamples.IncidentFilter.GetDescription)
            {
                IncidentFilterCommandOptions.Get.NameOption,
                IncidentFilterCommandOptions.Get.DetailOption
            };

            cmd.SetAction(IncidentFilterCommandHandlers.HandleGetCommand);
            return cmd;
        }

        private static Command CreateApplyCommand()
        {
            var cmd = new Command("apply", CommandExamples.IncidentFilter.ApplyDescription)
            {
                IncidentFilterCommandOptions.Apply.NameOption,
                IncidentFilterCommandOptions.Apply.DryRunOption
            };

            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(IncidentFilterCommandOptions.Apply.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "incident filter");
                if (!isValid)
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(IncidentFilterCommandHandlers.HandleApplyCommand);
            return cmd;
        }

        private static Command CreateDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.IncidentFilter.DeleteDescription)
            {
                IncidentFilterCommandOptions.Delete.NameOption,
                IncidentFilterCommandOptions.Delete.DryRunOption,
                IncidentFilterCommandOptions.Delete.DeleteLocalFilesOption
            };

            cmd.Validators.Add(result =>
            {
                var name = result.GetValue(IncidentFilterCommandOptions.Delete.NameOption);
                var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "incident filter");
                if (!isValid)
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(errorMessage!));
                }
            });

            cmd.SetAction(IncidentFilterCommandHandlers.HandleDeleteCommand);
            return cmd;
        }
    }
}
