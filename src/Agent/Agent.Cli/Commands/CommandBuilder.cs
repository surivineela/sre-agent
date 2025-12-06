// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

public static class CommandBuilder
{
    public static RootCommand BuildCommands()
    {
        // ----- Build subcommand trees
        var agent = new Command("agent", "Agent commands for managing SRE automation agents")
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

        // Add default action for agent command to show formatted help
        agent.SetAction(pr => ShowFormattedAgentHelp(agent));

        var tool = new Command("tool", "Tool commands for managing SRE automation tools")
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
        tool.SetAction(pr => ShowFormattedToolHelp(tool));

        var doc = new Command("doc", "Document management commands. Upload and manage documents like TSGs, architecture docs, runbooks, and other reference materials for agents to use")
        {
            CreateDocumentUploadCommand(),
            CreateDocumentSearchCommand(),
            CreateDocumentReindexCommand()
        };

        // Add default action for doc command to show formatted help
        doc.SetAction(pr => ShowFormattedDocHelp(doc));

        var profile = new Command("profile", "Profile management commands. Profiles store connection settings for different SRE Agent instances (local or remote)")
        {
            CreateProfileListCommand(),
            CreateProfileGetCommand(),
            CreateProfileCreateCommand(),
            CreateProfileSetCommand(),
            CreateProfileDeleteCommand()
        };

        var skill = new Command("skill", "Skill management commands. Upload and manage custom skills for agents to use, or convert an existing agent into a skill.")
        {
            CreateSkillCreateCommand(),
            CreateSkillUploadCommand(),
            CreateSkillConvertCommand(),
            CreateSkillListCommand(),
            CreateSkillDownloadCommand(),
            CreateSkillDeleteCommand()
        };

        // Add default action for skill command to show formatted help
        skill.SetAction(pr => ShowFormattedSkillHelp(skill));

        // Add default action for profile command to show formatted help
        profile.SetAction(pr => ShowFormattedProfileHelp(profile));

        var initCommand = CreateInitCommand();
        var syncCommand = CreateSyncCommand();
        var listCommand = CreateListCommand();

        // Add default action for list command to show formatted help
        listCommand.SetAction(pr => ShowFormattedListHelp(listCommand));
        var applyYamlCommand = CreateApplyYamlCommand();
        var threadCommand = CreateThreadCommand();
        var scheduledTask = BuildScheduledTaskCommand();

        // Add default action for thread command to show formatted help
        threadCommand.SetAction(pr => ShowFormattedThreadHelp(threadCommand));
        var chatCommand = CreateChatCommand();
        var welcomeCommand = CreateWelcomeCommand();
        var helpCommand = CreateEnhancedHelpCommand();
        var statusCommand = CreateStatusCommand();
        var interactiveCommand = CreateInteractiveCommand();
        var versionCommand = CreateVersionCommand();

        var incidentHandler = BuildIncidentHandlerCommand();

        // Add default action for incident handler command to show formatted help
        incidentHandler.SetAction(pr => ShowFormattedIncidentHandlerHelp(incidentHandler));

        var extension = BuildExtensionCommand();

        // Add default action for extension command to show formatted help
        extension.SetAction(pr => ShowFormattedExtensionHelp(extension));

        // ----- Root
        var root = new RootCommand(
            "SRE Agent CLI - Your intelligent assistant for managing SRE agents and automating incident response\n\n" +
            "Incident Handler Commands: create, map-agent, list (run 'srectl incidenthandler --help' for details)")
        {
            welcomeCommand, helpCommand, statusCommand, interactiveCommand, versionCommand,
            initCommand, syncCommand, listCommand, applyYamlCommand, threadCommand, chatCommand,
            agent, tool, doc, profile, skill, incidentHandler, scheduledTask, extension
        };

        // Single root action (runs when no verb provided)
        root.SetAction(HandleRootCommand);

        // Simulate global options on all commands (older System.CommandLine lacks AddGlobalOption)
        root.AddGlobalOptionsCompat(GlobalOptions.Debug, GlobalOptions.Quiet);

        return root;
    }

    private static Command CreateSkillCreateCommand()
    {
        var cmd = new Command("create", CommandExamples.Skill.CreateDescription)
        {
            SkillCommandOptions.CreateNameOption,
            SkillCommandOptions.CreateOutputPathOption,
            SkillCommandOptions.DebugOption
        };

        cmd.SetAction(SkillCommandHandlers.HandleCreateCommand);
        return cmd;
    }

    private static Command CreateSkillUploadCommand()
    {
        var cmd = new Command("upload", CommandExamples.Skill.UploadDescription)
        {
            SkillCommandOptions.UploadPathOption,
            SkillCommandOptions.UploadFolderOption,
            SkillCommandOptions.DebugOption
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
            SkillCommandOptions.ConvertOutputPathOption,
            SkillCommandOptions.DebugOption
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
            SkillCommandOptions.ListSearchOption,
            SkillCommandOptions.DebugOption
        };

        cmd.SetAction(SkillCommandHandlers.HandleListCommand);
        return cmd;
    }

    private static Command CreateSkillDownloadCommand()
    {
        var cmd = new Command("download", CommandExamples.Skill.DownloadDescription)
        {
            SkillCommandOptions.DownloadNameOption,
            SkillCommandOptions.DownloadOutputPathOption,
            SkillCommandOptions.DebugOption
        };

        cmd.SetAction(SkillCommandHandlers.HandleDownloadCommand);
        return cmd;
    }

    private static Command CreateSkillDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Skill.DeleteDescription)
        {
            SkillCommandOptions.DeleteNameOption,
            SkillCommandOptions.DebugOption
        };

        cmd.SetAction(SkillCommandHandlers.HandleDeleteCommand);
        return cmd;
    }

    private static Command CreateSyncCommand()
    {
        var cmd = new Command("sync", CommandExamples.General.SyncDescription);

        cmd.SetAction(GeneralCommandHandlers.HandleSyncCommand);
        return cmd;
    }

    private static Command CreateAgentCreateCommand()
    {
        var cmd = new Command("create", CommandExamples.Agent.CreateDescription)
        {
            AgentCommandOptions.NameOptionCreate,
            AgentCommandOptions.InstructionsOptionCreate,
            AgentCommandOptions.ToolsOptionCreate,
            AgentCommandOptions.HandoffDescriptionOption,
            AgentCommandOptions.HandoffsOption,
            AgentCommandOptions.AllowParallelToolCallsOption,
            AgentCommandOptions.MaxReflectionCountOption,
            AgentCommandOptions.CriticPromptPathOption,
            AgentCommandOptions.CriticOnHandoffOption,
            AgentCommandOptions.CustomReflectionNoteOption,
            AgentCommandOptions.CommonPromptsOption,
            AgentCommandOptions.TemperatureOption,
            AgentCommandOptions.OutputTypeOption,
            AgentCommandOptions.VanillaModeOption,
            AgentCommandOptions.SmartOption,
            AgentCommandOptions.EnableSkillsOption,
            AgentCommandOptions.AddSystemSkillsOption
        };

        cmd.SetAction(AgentCommandHandlers.HandleCreateCommand);
        return cmd;
    }

    private static Command CreateAgentValidateCommand()
    {
        var cmd = new Command("validate", CommandExamples.Agent.ValidateDescription)
        {
            AgentCommandOptions.NameOptionValidate,
            AgentCommandOptions.FileOptionValidate,
            AgentCommandOptions.AllOption,
            AgentCommandOptions.CheckToolsOption
        };

        cmd.SetAction(AgentCommandHandlers.HandleValidateCommand);
        return cmd;
    }

    private static Command CreateAgentApplyCommand()
    {
        var cmd = new Command("apply", CommandExamples.Agent.ApplyDescription)
        {
            AgentCommandOptions.ApplyNameOption,
            AgentCommandOptions.ApplyDryRunOption
        };

        cmd.SetAction(AgentCommandHandlers.HandleApplyCommand);
        return cmd;
    }

    private static Command CreateAgentDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Agent.DeleteDescription)
        {
            AgentCommandOptions.DeleteNameOption
        };

        cmd.SetAction(AgentCommandHandlers.HandleDeleteCommand);
        return cmd;
    }

    private static Command CreateAgentTestCommand()
    {
        var cmd = new Command("test", CommandExamples.Agent.TestDescription)
        {
            AgentCommandOptions.TestNameOption,
            AgentCommandOptions.TestMessageOption,
            AgentCommandOptions.TestUserIdOption,
            AgentCommandOptions.TestDisplayNameOption,
            AgentCommandOptions.ThreadWaitOption,
            AgentCommandOptions.ThreadNoWaitOption
        };

        cmd.Validators.Add(result =>
        {
            var w = result.GetValue(AgentCommandOptions.ThreadWaitOption);
            var nw = result.GetValue(AgentCommandOptions.ThreadNoWaitOption);
            if (w && nw) result.AddError("Specify either --wait or --no-wait, not both.");
        });

        cmd.SetAction(AgentCommandHandlers.HandleTestCommand);
        return cmd;
    }

    private static Command CreateAgentDiffCommand()
    {
        var cmd = new Command("diff", CommandExamples.Agent.DiffDescription)
        {
            AgentCommandOptions.DiffNameOption,
            AgentCommandOptions.DiffToolOption,
            AgentCommandOptions.DiffRawOption
        };

        cmd.SetAction(AgentCommandHandlers.HandleDiffCommand);
        return cmd;
    }

    private static Command CreateAgentMigrateCommand()
    {
        var cmd = new Command("migrate", CommandExamples.Agent.MigrateDescription)
        {
            AgentCommandOptions.MigrateNameOption,
            AgentCommandOptions.MigrateAllOption,
            AgentCommandOptions.MigrateDryRunOption
        };

        cmd.SetAction(AgentCommandHandlers.HandleMigrateCommand);
        return cmd;
    }

    private static Command CreateAgentListCommand()
    {
        var cmd = new Command("list", CommandExamples.Agent.ListDescription)
        {
            AgentCommandOptions.ListSearchOption,
            AgentCommandOptions.ListNameOption,
            AgentCommandOptions.ListDetailOption
        };

        // Validate mutually exclusive options
        cmd.Validators.Add(result =>
        {
            var name = result.GetValue(AgentCommandOptions.ListNameOption);
            var search = result.GetValue(AgentCommandOptions.ListSearchOption);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(search))
            {
                result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --search together"));
            }
        });

        cmd.SetAction(AgentCommandHandlers.HandleListCommand);
        return cmd;
    }

    private static Command CreateToolCreateCommand()
    {
        var cmd = new Command("create", CommandExamples.Tool.CreateDescription)
        {
            ToolCommandOptions.NameOption,
            ToolCommandOptions.TypeOption,
            ToolCommandOptions.PathOption,
            ToolCommandOptions.ConnectorOption,
            ToolCommandOptions.DatabaseOption,
            ToolCommandOptions.DescriptionOption,
            ToolCommandOptions.QueryOption,
            ToolCommandOptions.TemplateOption,
            ToolCommandOptions.ParameterOption
        };

        cmd.SetAction(ToolCommandHandlers.HandleCreateCommand);
        return cmd;
    }

    private static Command CreateToolValidateCommand()
    {
        var cmd = new Command("validate", CommandExamples.Tool.ValidateDescription)
        {
            ToolCommandOptions.ValidateNameOption,
            ToolCommandOptions.ValidateAllOption
        };

        // Validate mutually exclusive options
        cmd.Validators.Add(result =>
        {
            var name = result.GetValue(ToolCommandOptions.ValidateNameOption);
            var all = result.GetValue(ToolCommandOptions.ValidateAllOption);

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
            ToolCommandOptions.ApplyNameOption,
            ToolCommandOptions.ApplyDryRunOption
        };

        cmd.SetAction(ToolCommandHandlers.HandleApplyCommand);
        return cmd;
    }

    private static Command CreateToolDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Tool.DeleteDescription)
        {
            ToolCommandOptions.DeleteNameOption,
            ToolCommandOptions.DeleteDryRunOption
        };

        cmd.SetAction(ToolCommandHandlers.HandleDeleteCommand);
        return cmd;
    }

    private static Command CreateToolDiffCommand()
    {
        var cmd = new Command("diff", CommandExamples.Tool.DiffDescription)
        {
            ToolCommandOptions.DiffNameOption,
            ToolCommandOptions.DiffToolOption,
            ToolCommandOptions.DiffRawOption
        };

        cmd.SetAction(ToolCommandHandlers.HandleDiffCommand);
        return cmd;
    }

    private static Command CreateInitCommand()
    {
        var resourceUrl = new Option<string>("--resource-url")
        {
            Required = true,
            Description = "Base URL of the SRE Agent server"
        };

        var cmd = new Command("init", CommandExamples.General.InitDescription) { resourceUrl };

        // Guard so flow analysis knows it's enforced at runtime
        cmd.Validators.Add(r =>
        {
            var v = r.GetValue(resourceUrl);
            if (string.IsNullOrWhiteSpace(v))
                r.AddError("--resource-url must be provided and non-empty.");
        });

        // Now it's safe to use ! (or do an explicit check)
        cmd.SetAction(pr => GeneralCommandHandlers.HandleInitCommandWithResourceUrl(pr.GetValue(resourceUrl)!));

        // If you prefer an explicit check instead of '!'
        // cmd.SetAction((ParseResult pr) =>
        // {
        //     var url = pr.GetValue(resourceUrl);
        //     if (string.IsNullOrWhiteSpace(url))
        //         throw new ArgumentException("--resource-url must be provided and non-empty.");
        //     return GeneralCommandHandlers.HandleInitCommandWithResourceUrl(url);
        // });

        return cmd;
    }

    private static Command CreateListCommand()
    {
        var listAgents = new Command("agents", "List remote extended agents from the server")
        {
            AgentCommandOptions.DebugOption,
            AgentCommandOptions.AllOption
        };
        listAgents.SetAction(AgentCommandHandlers.HandleListCommand);

        var listExtendedTools = new Command("extended-tools", "List all extended tools added to the server through apply command")
        {
            ToolCommandOptions.DebugOption
        };
        listExtendedTools.SetAction(GeneralCommandHandlers.HandleListExtendedToolsCommand);

        var listDataConnectors = new Command("data-connectors", "List all data connectors configured on the server")
        {
            ToolCommandOptions.DebugOption
        };
        listDataConnectors.SetAction(GeneralCommandHandlers.HandleListDataConnectorsCommand);

        // List incident handlers subcommand
        var listIncidentHandlersCommand = new Command("incidenthandlers", "List all incident handlers from the remote server")
        {
            IncidentHandlerCommandOptions.VerboseOption
        };
        listIncidentHandlersCommand.SetAction(IncidentHandlerCommandHandlers.HandleListCommand);

        var cmd = new Command("list", CommandExamples.General.ListDescription)
        {
            listAgents, listExtendedTools, listDataConnectors, listIncidentHandlersCommand
        };
        return cmd;
    }

    private static Command CreateToolShowTypesCommand()
    {
        var cmd = new Command("show-types", CommandExamples.Tool.ShowTypesDescription)
        {
            ToolCommandOptions.ShowTypesTypeOption
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
            ToolCommandOptions.ListSearchOption,
            ToolCommandOptions.ListNameOption,
            ToolCommandOptions.ListDetailOption
        };

        // Validate mutually exclusive options
        cmd.Validators.Add(result =>
        {
            var name = result.GetValue(ToolCommandOptions.ListNameOption);
            var search = result.GetValue(ToolCommandOptions.ListSearchOption);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(search))
            {
                result.AddError(ErrorMessageHelper.InvalidParameter("Cannot use both --name and --search together"));
            }
        });

        cmd.SetAction(ToolCommandHandlers.HandleListCommand);
        return cmd;
    }

    private static Command CreateToolMigrateCommand()
    {
        var cmd = new Command("migrate", CommandExamples.Tool.MigrateDescription)
        {
            ToolCommandOptions.MigrateNameOption,
            ToolCommandOptions.MigrateAllOption,
            ToolCommandOptions.MigrateDryRunOption
        };

        // Validate mutually exclusive options
        cmd.Validators.Add(result =>
        {
            var name = result.GetValue(ToolCommandOptions.MigrateNameOption);
            var all = result.GetValue(ToolCommandOptions.MigrateAllOption);

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

    private static Command CreateApplyYamlCommand()
    {
        var cmd = new Command("apply-yaml", CommandExamples.General.ApplyYamlDescription)
        {
            AgentCommandOptions.ApplyYamlFileOption
        };

        cmd.SetAction(pr =>

        {
            // If no file option provided, show formatted help
            var filePath = pr.GetValue(AgentCommandOptions.ApplyYamlFileOption);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ShowFormattedApplyYamlHelp(cmd);
            }

            return GeneralCommandHandlers.HandleApplyYamlCommand(pr);
        });

        return cmd;
    }

    private static Command CreateThreadCommand()
    {
        var threadNew = new Command("new", CommandExamples.Thread.NewDescription)
        {
            AgentCommandOptions.ThreadMessageOption,
            AgentCommandOptions.ThreadUserIdOption,
            AgentCommandOptions.ThreadDisplayNameOption,
            AgentCommandOptions.ChatAgentNameOption,
            AgentCommandOptions.ThreadWaitOption,
            AgentCommandOptions.ThreadNoWaitOption
        };
        threadNew.Validators.Add(result =>
        {
            var w = result.GetValue(AgentCommandOptions.ThreadWaitOption);
            var nw = result.GetValue(AgentCommandOptions.ThreadNoWaitOption);
            if (w && nw) result.AddError("Specify either --wait or --no-wait, not both.");
        });
        threadNew.SetAction(ThreadCommandHandlers.HandleThreadNewCommand);

        var threadContinue = new Command("continue", CommandExamples.Thread.ContinueDescription)
        {
            AgentCommandOptions.ThreadIdOption,
            AgentCommandOptions.ThreadMessageOptionalOption,
            AgentCommandOptions.ThreadUserIdOption,
            AgentCommandOptions.ThreadDisplayNameOption,
            AgentCommandOptions.ThreadWaitOption,
            AgentCommandOptions.ThreadNoWaitOption
        };
        threadContinue.Validators.Add(result =>
        {
            var w = result.GetValue(AgentCommandOptions.ThreadWaitOption);
            var nw = result.GetValue(AgentCommandOptions.ThreadNoWaitOption);
            if (w && nw) result.AddError("Specify either --wait or --no-wait, not both.");
        });
        threadContinue.SetAction(ThreadCommandHandlers.HandleThreadContinueCommand);

        var threadList = new Command("list", "List all threads");
        threadList.SetAction(ThreadCommandHandlers.HandleThreadListCommand);

        var threadDelete = new Command("delete", CommandExamples.Thread.DeleteDescription)
        {
            AgentCommandOptions.ThreadIdRequiredOption
        };
        threadDelete.SetAction(ThreadCommandHandlers.HandleThreadDeleteCommand);

        var threadTrack = new Command("track", "Track an existing thread for new messages")
        {
            AgentCommandOptions.ThreadIdRequiredOption
        };
        threadTrack.SetAction(ThreadCommandHandlers.HandleThreadTrackCommand);

        // Thread apply from YAML manifest
        var threadApply = new Command("apply", "Create a new thread from a YAML manifest (supports starting agent)")
        {
            AgentCommandOptions.ApplyYamlFileOption
        };
        threadApply.SetAction(ThreadCommandHandlers.HandleThreadApplyCommand);

        var thread = new Command("thread", "Thread management commands")
        {
            threadNew, threadContinue, threadList, threadDelete, threadTrack, threadApply
        };

        return thread;
    }

    private static Command CreateDocumentUploadCommand()
    {
        var cmd = new Command("upload", CommandExamples.Document.UploadDescription)
        {
            DocumentCommandOptions.FileOption,
            DocumentCommandOptions.FolderOption,
            DocumentCommandOptions.TriggerIndexingOption,
            DocumentCommandOptions.NoIndexingOption,
            DocumentCommandOptions.RecursiveOption
        };

        cmd.SetAction(DocumentCommandHandlers.HandleUploadCommand);
        return cmd;
    }

    private static Command CreateDocumentSearchCommand()
    {
        var cmd = new Command("search", CommandExamples.Document.SearchDescription)
        {
            DocumentCommandOptions.QueryOption
        };

        cmd.SetAction(DocumentCommandHandlers.HandleSearchCommand);
        return cmd;
    }

    private static Command CreateDocumentReindexCommand()
    {
        var cmd = new Command("reindex", CommandExamples.Document.ReindexDescription);
        cmd.SetAction(DocumentCommandHandlers.HandleReindexCommand);
        return cmd;
    }

    private static Command CreateChatCommand()
    {
        var cmd = new Command("chat", CommandExamples.General.ChatDescription)
        {
            AgentCommandOptions.ChatAgentNameOption
        };
        cmd.SetAction(GeneralCommandHandlers.HandleChatCommand);
        return cmd;
    }

    private static Command CreateProfileListCommand()
    {
        var cmd = new Command("list", CommandExamples.Profile.ListDescription);
        cmd.SetAction(ProfileCommandHandlers.HandleListCommand);
        return cmd;
    }

    private static Command CreateProfileGetCommand()
    {
        var cmd = new Command("get", CommandExamples.Profile.GetDescription)
        {
            ProfileCommandOptions.ProfileNameOption
        };
        cmd.SetAction(ProfileCommandHandlers.HandleGetCommand);
        return cmd;
    }

    private static Command CreateProfileCreateCommand()
    {
        var cmd = new Command("create", CommandExamples.Profile.CreateDescription)
        {
            ProfileCommandOptions.ProfileNameRequiredOption,
            ProfileCommandOptions.ResourceUrlOption,
            ProfileCommandOptions.SetCurrentOption
        };
        cmd.SetAction(ProfileCommandHandlers.HandleCreateCommand);
        return cmd;
    }

    private static Command CreateProfileSetCommand()
    {
        var cmd = new Command("set", CommandExamples.Profile.SetDescription)
        {
            ProfileCommandOptions.ProfileNameRequiredOption
        };
        cmd.SetAction(ProfileCommandHandlers.HandleSetCommand);
        return cmd;
    }

    private static Command CreateProfileDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Profile.DeleteDescription)
        {
            ProfileCommandOptions.ProfileNameRequiredOption
        };
        cmd.SetAction(ProfileCommandHandlers.HandleDeleteCommand);
        return cmd;
    }

    private static Command CreateWelcomeCommand()
    {
        var cmd = new Command("welcome", "Show welcome screen and getting started guide");
        cmd.SetAction(async _ =>

        {
            WelcomeService.ShowWelcomeBanner();
            await WelcomeService.ShowContextualGuidance();
        });
        return cmd;
    }

    private static Command CreateEnhancedHelpCommand()
    {
        var topic = new Argument<string>("topic")
        {
            Description = "Help topic to display",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("help", "Interactive help system with examples and troubleshooting") { topic };

        // Bind the positional arg via ParseResult
        cmd.SetAction(async pr =>

        {
            string? t = pr.GetValue(topic);
            await InteractiveHelpService.ShowInteractiveHelp(t);
        });
        return cmd;
    }

    private static Command CreateStatusCommand()
    {
        var cmd = new Command("status", "Show workspace status and health check")
        {
            GlobalOptions.Debug
        };
        cmd.SetAction(GeneralCommandHandlers.HandleStatusCommand);
        return cmd;
    }

    private static Command CreateInteractiveCommand()
    {
        var cmd = new Command("interactive", "Start interactive guided mode for step-by-step assistance");
        cmd.SetAction(InteractiveCommandHandlers.HandleInteractiveMode);
        return cmd;
    }

    private static Command CreateVersionCommand()
    {
        var cmd = new Command("version", "Show version information and build details");
        cmd.SetAction(GeneralCommandHandlers.HandleVersionCommand);
        return cmd;
    }

    // -------- Root action & status --------

    private static Task HandleRootCommand(ParseResult parseResult)
    {
        ConsoleUI.WriteSection("SRE Agent CLI (srectl)");
        return ShowRootAfterWelcome();
    }


    private static async Task ShowRootAfterWelcome()
    {
        await WelcomeService.ShowContextualGuidance();
        ConsoleUI.WriteSection("Quick commands");
        ConsoleUI.WriteCommand("Interactive conversation", "srectl chat");
        ConsoleUI.WriteCommand("Comprehensive help", "srectl help");
        ConsoleUI.WriteCommand("Workspace status", "srectl status");
        Console.WriteLine();
    }

    public static async Task ShowWorkspaceStatus()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ConsoleUI.WriteSection("Workspace Status Check");
        ConsoleUI.DrawLine();

        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        var configStatus = hasConfig ? "Configured" : "Not configured";
        ConsoleUI.WriteStatus(hasConfig, $"Configuration: {configStatus}");

        if (hasConfig)
        {
            try
            {
                var config = await configService.LoadConfigurationAsync();
                ConsoleUI.WriteBullet($"Server URL: {config?.ResourceUrl ?? "Unknown"}");
                ConsoleUI.WriteBullet($"Auth Required: {config?.AuthRequired ?? false}");
            }
            catch
            {
                ConsoleUI.WriteStatus(false, "Configuration file corrupted");
            }
        }

        var agentCount = Directory.Exists("agents") ? Directory.GetDirectories("agents").Length : 0;
        var toolCount = Directory.Exists("tools") ? Directory.GetDirectories("tools").Length : 0;

        ConsoleUI.WriteInfo($"Agents: {agentCount} configured");
        ConsoleUI.WriteInfo($"Tools: {toolCount} configured");

        bool serverConnected = false;
        bool remoteAgentsAvailable = false;
        bool remoteToolsAvailable = false;

        if (hasConfig)
        {
            ProgressService.AnimatedSpinner.Start("Testing server connection");

            try
            {
                var config = await configService.LoadConfigurationAsync();
                if (config == null)
                {
                    ProgressService.AnimatedSpinner.Stop();
                    ProgressService.ShowError("No configuration found. Please run 'srectl init' first.");
                    return;
                }

                using var apiService = new ApiService();
                var (success, response) = await apiService.TestConnectionAsync(config.ResourceUrl);

                ProgressService.AnimatedSpinner.Stop();
                if (success)
                {
                    serverConnected = true;
                    ConsoleUI.WriteStatus(true, "Server Connection: Connected");

                    var (agentsSuccess, _, _) = await apiService.ListAgentsAsync();
                    var (toolsSuccess, _) = await apiService.ListToolsAsync();

                    remoteAgentsAvailable = agentsSuccess;
                    remoteToolsAvailable = toolsSuccess;

                    if (agentsSuccess) ConsoleUI.WriteBullet("Remote Agents: Available");
                    if (toolsSuccess) ConsoleUI.WriteBullet("Remote Tools: Available");
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Server Connection: Failed");
                    ConsoleUI.WriteBullet($"Error: {response}");
                }
            }
            catch (Exception ex)
            {
                ProgressService.AnimatedSpinner.Stop();
                ConsoleUI.WriteStatus(false, "Server Connection: Failed");
                ConsoleUI.WriteBullet($"Error: {ex.Message}");
            }
        }

        stopwatch.Stop();
        ProgressService.ShowTiming("Status check", stopwatch.Elapsed);

        Console.WriteLine();
        ConsoleUI.WriteSection("Suggested next steps");

        if (!hasConfig)
        {
            ConsoleUI.WriteBullet("srectl init --resource-url <your-server>");
        }
        else if (agentCount == 0)
        {
            ConsoleUI.WriteBullet("srectl agent create --smart");
            ConsoleUI.WriteBullet("srectl help quickstart");
        }
        else
        {
            ConsoleUI.WriteBullet("srectl chat");
            ConsoleUI.WriteBullet("srectl agent test --name <agent>");

            // Provide appropriate list commands based on connection status
            if (serverConnected && remoteAgentsAvailable)
            {
                ConsoleUI.WriteBullet("srectl list agents  # List remote agents from server");
            }
            else if (serverConnected)
            {
                ConsoleUI.WriteBullet("srectl list agents  # Check remote agents (server connected)");
            }
            else
            {
                ConsoleUI.WriteBullet("srectl agent list  # List local agent configurations");
                if (hasConfig)
                {
                    ConsoleUI.WriteBullet("srectl list agents  # List remote agents (requires server connection)");
                }
            }
        }
    }

    private static Task ShowFormattedAgentHelp(Command agentCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Agent Lifecycle"] = ["create", "validate", "apply", "delete"],
            ["Agent Testing"] = ["test"],
            ["Agent Management"] = ["list", "diff"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Agent Lifecycle"] = "Create, validate, deploy, and remove agents",
            ["Agent Testing"] = "Test and validate agent functionality",
            ["Agent Management"] = "List, discover, and compare deployed agents"
        };

        var examples = new[]
        {
            "srectl agent create --name DevOpsAgent --smart",
            "srectl agent validate --all",
            "srectl agent test --name MyAgent --message \"Hello\"",
            "srectl agent diff --name MyAgent",
            "srectl agent list"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Agent Commands",
            "Manage SRE automation agents",
            ConsoleColor.Green,
            agentCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedToolHelp(Command toolCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Tool Lifecycle"] = ["create", "validate", "apply", "delete"],
            ["Tool Discovery"] = ["show-types", "show-connectors"],
            ["Tool Management"] = ["list", "diff"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Tool Lifecycle"] = "Create, validate, deploy, and remove automation tools",
            ["Tool Discovery"] = "Explore available tool types and data connectors",
            ["Tool Management"] = "List, discover, and compare deployed tools"
        };

        var examples = new[]
        {
            "srectl tool create --name QueryMetrics --type KustoTool --connector my-connector --database MyDB --parameter limit",
            "srectl tool create --name GenerateLink --type LinkTool --template \"https://example.com/{id}\" --parameter id",
            "srectl tool validate --all",
            "srectl tool diff --name QueryMetrics",
            "srectl tool show-types",
            "srectl tool list"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Tool Commands",
            "Manage SRE automation tools",
            ConsoleColor.Blue,
            toolCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedDocHelp(Command docCommand)
    {
        var examples = new[]
        {
            "srectl doc upload --file runbook.md",
            "srectl doc search --query \"restart web services\"",
            "srectl doc reindex"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Document Commands",
            "Manage documentation and knowledge base",
            ConsoleColor.Magenta,
            docCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedProfileHelp(Command profileCommand)
    {
        var examples = new[]
        {
            "srectl profile list",
            "srectl profile create --name prod --resource-url https://prod.example.com",
            "srectl profile set --name local-dev"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Profile Commands",
            "Manage connection profiles for different SRE Agent instances",
            ConsoleColor.Cyan,
            profileCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedSkillHelp(Command skillCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Skill Creation"] = ["create", "convert"],
            ["Skill Lifecycle"] = ["upload", "download", "delete"],
            ["Skill Discovery"] = ["list"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Skill Creation"] = "Create new skills or convert existing agents to skills",
            ["Skill Lifecycle"] = "Upload, download, and remove custom skills",
            ["Skill Discovery"] = "List and discover available skills"
        };

        var examples = new[]
        {
            "srectl skill create --name my-skill",
            "srectl skill convert --agent-name my-agent",
            "srectl skill upload --path skills/my-skill",
            "srectl skill upload --folder skills",
            "srectl skill list",
            "srectl skill list --search database",
            "srectl skill download --name my-skill",
            "srectl skill delete --name my-skill"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Skill Commands",
            "Manage custom skills for agents",
            ConsoleColor.Magenta,
            skillCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedListHelp(Command listCommand)
    {
        var examples = new[]
        {
            "srectl list agents",
            "srectl list extended-tools",
            "srectl list data-connectors",
            "srectl list incidenthandlers",
            "srectl agent list  # Alternative agent listing",
            "srectl tool list   # Alternative tool listing"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "List Commands",
            "List resources from the remote server",
            ConsoleColor.DarkYellow,
            listCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedThreadHelp(Command threadCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Thread Management"] = ["new", "continue", "list", "delete", "track"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Thread Management"] = "Create, manage, and track conversation threads with agents"
        };

        var examples = new[]
        {
            "srectl thread new --message \"Help me debug an issue\"",
            "srectl thread continue --thread-id abc123 --message \"More details\"",
            "srectl thread list",
            "srectl thread track --thread-id abc123"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Thread Commands",
            "Manage conversation threads with SRE Agent",
            ConsoleColor.DarkCyan,
            threadCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedIncidentHandlerHelp(Command incidentHandlerCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Incident Management"] = ["create", "map-agent", "list"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Incident Management"] = "Create filters, map agents, and manage incident handlers"
        };

        var examples = new[]
        {
            "srectl incidenthandler create --id MyFilter --name \"Production Issues\" --title-contains \"production\"",
            "srectl incidenthandler map-agent --name MyFilter --handling-agent ProductionAgent",
            "srectl incidenthandler list --verbose"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Incident Handler Commands",
            "Manage incident handlers and filters for automated incident response",
            ConsoleColor.Red,
            incidentHandlerCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Command BuildScheduledTaskCommand()
    {
        var scheduledTaskCommand = new Command("scheduledtask", "Manage scheduled tasks for automated agent operations");

        var createCommand = new Command("create", CommandExamples.ScheduledTask.CreateDescription)
        {
            ScheduledTaskCommandOptions.CreateNameOption,
            ScheduledTaskCommandOptions.DescriptionOption,
            ScheduledTaskCommandOptions.CronExpressionOption,
            ScheduledTaskCommandOptions.AgentPromptOption,
            ScheduledTaskCommandOptions.AgentOption,
            ScheduledTaskCommandOptions.StartTimeOption,
            ScheduledTaskCommandOptions.EndTimeOption,
            ScheduledTaskCommandOptions.ThreadIdOption,
            ScheduledTaskCommandOptions.MaxExecutionsOption,
            ScheduledTaskCommandOptions.NotificationChannelOption
        };
        createCommand.SetAction(ScheduledTaskCommandHandlers.HandleCreateCommand);

        var listCommand = new Command("list", CommandExamples.ScheduledTask.ListDescription)
        {
            ScheduledTaskCommandOptions.VerboseOption,
            ScheduledTaskCommandOptions.FilterThreadIdOption,
            ScheduledTaskCommandOptions.FilterStatusOption
        };
        listCommand.SetAction(ScheduledTaskCommandHandlers.HandleListCommand);

        var getCommand = new Command("get", CommandExamples.ScheduledTask.GetDescription)
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        getCommand.SetAction(ScheduledTaskCommandHandlers.HandleGetCommand);

        var pauseCommand = new Command("pause", CommandExamples.ScheduledTask.PauseDescription)
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        pauseCommand.SetAction(ScheduledTaskCommandHandlers.HandlePauseCommand);

        var resumeCommand = new Command("resume", CommandExamples.ScheduledTask.ResumeDescription)
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        resumeCommand.SetAction(ScheduledTaskCommandHandlers.HandleResumeCommand);

        var deleteCommand = new Command("delete", CommandExamples.ScheduledTask.DeleteDescription)
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        deleteCommand.SetAction(ScheduledTaskCommandHandlers.HandleDeleteCommand);

        // Quickstart creates a minimal hello-world manifest interactively and applies it
        var quickstart = new Command("quickstart", "Interactive hello-world scheduled task wizard")
        {
            ScheduledTaskCommandOptions.QuickstartNameOption,
            ScheduledTaskCommandOptions.QuickstartCronOption,
            ScheduledTaskCommandOptions.QuickstartDurationHoursOption,
            ScheduledTaskCommandOptions.QuickstartAgentOption,
            ScheduledTaskCommandOptions.QuickstartApplyOption
        };
        quickstart.SetAction(ScheduledTaskCommandHandlers.HandleQuickstartCommand);

        // Apply a YAML manifest for a scheduled task
        var apply = new Command("apply", "Apply a ScheduledTask YAML manifest (apiVersion/kind/spec)")
        {
            AgentCommandOptions.ApplyYamlFileOption
        };
        apply.SetAction(ScheduledTaskCommandHandlers.HandleApplyYamlCommand);

        scheduledTaskCommand.Add(createCommand);
        scheduledTaskCommand.Add(listCommand);
        scheduledTaskCommand.Add(getCommand);
        scheduledTaskCommand.Add(pauseCommand);
        scheduledTaskCommand.Add(resumeCommand);
        scheduledTaskCommand.Add(deleteCommand);
        scheduledTaskCommand.Add(quickstart);
        scheduledTaskCommand.Add(apply);

        // Add default action for scheduled task command to show formatted help
        scheduledTaskCommand.SetAction(pr => ShowFormattedScheduledTaskHelp(scheduledTaskCommand));

        return scheduledTaskCommand;
    }

    private static Task ShowFormattedScheduledTaskHelp(Command scheduledTaskCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Task Management"] = ["create", "list", "get"],
            ["Task Control"] = ["pause", "resume", "delete"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Task Management"] = "Create and view scheduled tasks",
            ["Task Control"] = "Control execution and lifecycle of scheduled tasks"
        };

        var examples = new[]
        {
            "srectl scheduledtask create --name \"Daily Check\" --cron \"0 9 * * *\" --prompt \"Check system status\"",
            "srectl scheduledtask create --name \"Agent Check\" --cron \"0 10 * * *\" --prompt \"Run checks\" --agent \"MyAgent\"",
            "srectl scheduledtask list --verbose",
            "srectl scheduledtask pause --id task-123",
            "srectl scheduledtask resume --id task-123"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Scheduled Task Commands",
            "Manage scheduled tasks for automated agent operations and monitoring",
            ConsoleColor.Blue,
            scheduledTaskCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedApplyYamlHelp(Command applyYamlCommand)
    {
        var examples = new[]
        {
            "srectl apply-yaml --file agents/MyAgent/MyAgent.yaml",
            "srectl apply-yaml --file tools/CustomTool/CustomTool.yaml",
            "srectl apply-yaml --file configs/my-config.yaml"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Apply YAML Commands",
            "Apply YAML configuration files to the server",
            ConsoleColor.Green,
            applyYamlCommand,
            null, // Single command
            null, // No group descriptions for single command
            examples);

        return Task.CompletedTask;
    }

    private static Command BuildIncidentHandlerCommand()
    {
        var incidentHandlerCommand = new Command("incidenthandler", "Manage incident handlers and filters");

        var mapAgentCommand = new Command("map-agent", CommandExamples.IncidentHandler.MapAgentDescription)
        {
            IncidentHandlerCommandOptions.FilterNameOption,
            IncidentHandlerCommandOptions.HandlingAgentOption
        };
        mapAgentCommand.SetAction(IncidentHandlerCommandHandlers.HandleMapAgentCommand);

        var listCommand = new Command("list", CommandExamples.IncidentHandler.ListDescription)
        {
            IncidentHandlerCommandOptions.VerboseOption
        };
        listCommand.SetAction(IncidentHandlerCommandHandlers.HandleListCommand);

        var createCommand = new Command("create", CommandExamples.IncidentHandler.CreateDescription)
        {
            IncidentHandlerCommandOptions.CreateIdOption,
            IncidentHandlerCommandOptions.CreateNameOption,
            IncidentHandlerCommandOptions.ImpactedServiceOption,
            IncidentHandlerCommandOptions.PriorityOption,
            IncidentHandlerCommandOptions.IncidentTypeOption,
            IncidentHandlerCommandOptions.AlertIdOption,
            IncidentHandlerCommandOptions.TitleContainsOption,
            IncidentHandlerCommandOptions.AgentModeOption,
            IncidentHandlerCommandOptions.CreateHandlingAgentOption,
            IncidentHandlerCommandOptions.OwningTeamIdOption,
            IncidentHandlerCommandOptions.MaxAttemptsOption
        };
        createCommand.SetAction(IncidentHandlerCommandHandlers.HandleCreateCommand);

        incidentHandlerCommand.Add(mapAgentCommand);
        incidentHandlerCommand.Add(listCommand);
        incidentHandlerCommand.Add(createCommand);
        return incidentHandlerCommand;
    }

    private static Command BuildExtensionCommand()
    {
        var extensionCommand = new Command("extension", "Extension commands for generating deployment files and configurations");

        var generateEv2Command = new Command("generate-ev2", CommandExamples.Extension.GenerateEv2Description)
        {
            ExtensionCommandOptions.ToolsFolderOption,
            ExtensionCommandOptions.AgentFolderOption,
            ExtensionCommandOptions.OutputOption,
            ExtensionCommandOptions.DebugOption
        };

        generateEv2Command.SetAction(ExtensionCommandHandlers.HandleGenerateEv2Command);

        extensionCommand.Add(generateEv2Command);
        return extensionCommand;
    }

    private static Task ShowFormattedExtensionHelp(Command extensionCommand)
    {
        var examples = new[]
        {
            "srectl extension generate-ev2 --tools-folder ./tools --agent-folder ./agents --output ./ev2-output",
            "srectl extension generate-ev2 --tools-folder ./tools --agent-folder ./agents --output ./deployment --debug"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Extension Commands",
            "Generate deployment files and configurations for SRE Agent deployments",
            ConsoleColor.DarkGreen,
            extensionCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }
}
