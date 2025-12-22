// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ExtensionCommand
    {
        public static Command Build()
        {
            var extension = new Command("extension", "Extension commands for generating deployment files and configurations")
            {
                CreateGenerateEv2Command()
            };

            return extension;
        }

        private static Command CreateGenerateEv2Command()
        {
            var cmd = new Command("generate-ev2", CommandExamples.Extension.GenerateEv2Description)
            {
                ExtensionCommandOptions.GenerateEv2.ToolsFolderOption,
                ExtensionCommandOptions.GenerateEv2.AgentFolderOption,
                ExtensionCommandOptions.GenerateEv2.SkillsFolderOption,
                ExtensionCommandOptions.GenerateEv2.OutputOption,
                ExtensionCommandOptions.GenerateEv2.ServiceIdentifierOption,
                ExtensionCommandOptions.GenerateEv2.ServiceGroupOption,
                ExtensionCommandOptions.GenerateEv2.EnvironmentOption,
                ExtensionCommandOptions.GenerateEv2.TenantIdOption,
                ExtensionCommandOptions.GenerateEv2.SubscriptionKeyOption,
                ExtensionCommandOptions.GenerateEv2.SubscriptionIdOption,
                ExtensionCommandOptions.GenerateEv2.ResourceGroupOption,
                ExtensionCommandOptions.GenerateEv2.AgentNameOption
            };

            // Validate that EV2 options are either all provided or all omitted
            cmd.AddValidator(result =>
            {
                var serviceIdentifier = result.GetValue(ExtensionCommandOptions.GenerateEv2.ServiceIdentifierOption);
                var serviceGroup = result.GetValue(ExtensionCommandOptions.GenerateEv2.ServiceGroupOption);
                var environment = result.GetValue(ExtensionCommandOptions.GenerateEv2.EnvironmentOption);
                var tenantId = result.GetValue(ExtensionCommandOptions.GenerateEv2.TenantIdOption);
                var subscriptionKey = result.GetValue(ExtensionCommandOptions.GenerateEv2.SubscriptionKeyOption);
                var subscriptionId = result.GetValue(ExtensionCommandOptions.GenerateEv2.SubscriptionIdOption);
                var resourceGroup = result.GetValue(ExtensionCommandOptions.GenerateEv2.ResourceGroupOption);
                var agentName = result.GetValue(ExtensionCommandOptions.GenerateEv2.AgentNameOption);

                var ev2Options = new[]
                {
                    ("--service-identifier", serviceIdentifier),
                    ("--service-group", serviceGroup),
                    ("--environment", environment),
                    ("--tenant-id", tenantId),
                    ("--subscription-key", subscriptionKey),
                    ("--subscription-id", subscriptionId),
                    ("--resource-group", resourceGroup),
                    ("--agent-name", agentName)
                };

                var providedOptions = ev2Options.Where(opt => !string.IsNullOrWhiteSpace(opt.Item2)).ToList();
                var missingOptions = ev2Options.Where(opt => string.IsNullOrWhiteSpace(opt.Item2)).ToList();

                // If some but not all EV2 options are provided, show error
                if (providedOptions.Any() && missingOptions.Any())
                {
                    var missingNames = string.Join(", ", missingOptions.Select(opt => opt.Item1));
                    result.AddError(ErrorMessageHelper.InvalidParameter(
                        $"EV2 deployment options must be provided together. Missing: {missingNames}"));
                }

                // Validate environment value if provided
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    var validEnvironments = new[] { "Test", "Prod" };
                    if (!validEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
                    {
                        result.AddError(ErrorMessageHelper.InvalidParameter(
                            $"Invalid environment value '{environment}'. Must be 'Test' or 'Prod'."));
                    }
                }

                // Validate GUID fields if provided
                if (!string.IsNullOrWhiteSpace(serviceIdentifier) && !Guid.TryParse(serviceIdentifier, out _))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(
                        $"Invalid --service-identifier '{serviceIdentifier}'. Must be a valid service tree GUID."));
                }

                if (!string.IsNullOrWhiteSpace(tenantId) && !Guid.TryParse(tenantId, out _))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(
                        $"Invalid --tenant-id '{tenantId}'. Must be a valid Azure tenant ID (GUID)."));
                }

                if (!string.IsNullOrWhiteSpace(subscriptionId) && !Guid.TryParse(subscriptionId, out _))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter(
                        $"Invalid --subscription-id '{subscriptionId}'. Must be a valid Azure subscription ID (GUID)."));
                }
            });

            cmd.SetAction(ExtensionCommandHandlers.HandleGenerateEv2Command);
            return cmd;
        }
    }
}
