// CommandBuilder.cs
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
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
            CreateAgentListCommand()
        };

        // Add default action for agent command to show formatted help
        agent.SetAction((ParseResult pr) => ShowFormattedAgentHelp(agent));

        var tool = new Command("tool", "Tool commands for managing SRE automation tools")
        {
            CreateToolCreateCommand(),
            CreateToolValidateCommand(),
            CreateToolApplyCommand(),
            CreateToolDeleteCommand(),
            CreateToolDiffCommand(),
            CreateToolShowTypesCommand(),
            CreateToolShowConnectorsCommand(),
            CreateToolListCommand()
        };

        // Add default action for tool command to show formatted help
        tool.SetAction((ParseResult pr) => ShowFormattedToolHelp(tool));

        var doc = new Command("doc", "Document management commands. Upload and manage documents like TSGs, architecture docs, runbooks, and other reference materials for agents to use")
        {
            CreateDocumentUploadCommand(),
            CreateDocumentSearchCommand(),
            CreateDocumentReindexCommand()
        };

        // Add default action for doc command to show formatted help
        doc.SetAction((ParseResult pr) => ShowFormattedDocHelp(doc));

        var profile = new Command("profile", "Profile management commands. Profiles store connection settings for different SRE Agent instances (local or remote)")
        {
            CreateProfileListCommand(),
            CreateProfileGetCommand(),
            CreateProfileCreateCommand(),
            CreateProfileSetCommand(),
            CreateProfileDeleteCommand()
        };

        // Add default action for profile command to show formatted help
        profile.SetAction((ParseResult pr) => ShowFormattedProfileHelp(profile));

        var initCommand = CreateInitCommand();
        var syncCommand = CreateSyncCommand();
        var listCommand = CreateListCommand();

        // Add default action for list command to show formatted help
        listCommand.SetAction((ParseResult pr) => ShowFormattedListHelp(listCommand));
        var applyYamlCommand = CreateApplyYamlCommand();
    var threadCommand = CreateThreadCommand();
    var scheduledTask = BuildScheduledTaskCommand();

        // Add default action for thread command to show formatted help
        threadCommand.SetAction((ParseResult pr) => ShowFormattedThreadHelp(threadCommand));
        var chatCommand = CreateChatCommand();
        var welcomeCommand = CreateWelcomeCommand();
        var helpCommand = CreateEnhancedHelpCommand();
        var statusCommand = CreateStatusCommand();
        var interactiveCommand = CreateInteractiveCommand();
        var versionCommand = CreateVersionCommand();

        var incidentHandler = BuildIncidentHandlerCommand();

        // Add default action for incident handler command to show formatted help
        incidentHandler.SetAction((ParseResult pr) => ShowFormattedIncidentHandlerHelp(incidentHandler));

        // ----- Root
        var root = new RootCommand(
            "SRE Agent CLI - Your intelligent assistant for managing SRE agents and automating incident response\n\n" +
            "Incident Handler Commands: create, map-agent, list (run 'srectl incidenthandler --help' for details)")
        {
            welcomeCommand, helpCommand, statusCommand, interactiveCommand, versionCommand,
            initCommand, syncCommand, listCommand, applyYamlCommand, threadCommand, chatCommand,
            agent, tool, doc, profile, incidentHandler, scheduledTask
        };

        // Single root action (runs when no verb provided)
        root.SetAction((ParseResult pr) => HandleRootCommand(pr));

        // Simulate global options on all commands (older System.CommandLine lacks AddGlobalOption)
        root.AddGlobalOptionsCompat(GlobalOptions.Debug, GlobalOptions.Quiet);

        return root;
    }

    private static Command CreateSyncCommand()
    {
        var cmd = new Command("sync", "Sync agents and tools YAML from the remote server into the local workspace (agents/, tools/)");

        cmd.SetAction((ParseResult pr) =>
        {
            // Show a nice formatted help if user asked for it (for consistency with others)
            if (IsHelpRequested(pr))
            {
                return ShowFormattedSubcommandHelp(
                    "Sync",
                    "Download all remote agent and tool YAMLs and populate the local 'agents/' and 'tools/' folders",
                    cmd,
                    new[]
                    {
                        "srectl sync",
                        "# Requires prior 'srectl init --resource-url <url>'",
                    }
                );
            }

            return GeneralCommandHandlers.HandleSyncCommand(pr);
        });

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
            AgentCommandOptions.SmartOption
        };

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent Create", "Create a new agent YAML configuration file", cmd,
                    new[] {
                        "srectl agent create --name DevOpsAgent --instructions \"Help with DevOps tasks\"",
                        "srectl agent create --name KustoAgent --tools QueryKusto AnalyzeMetrics",
                        "srectl agent create --name StorageAgent --smart --instructions \"Help troubleshoot Azure Storage issues\""
                    });
            return AgentCommandHandlers.HandleCreateCommand(pr);
        });
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

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent Validate", "Validate agent YAML configuration files", cmd,
                    new[] {
                        "srectl agent validate --name MyAgent",
                        "srectl agent validate --file agents/MyAgent/MyAgent.yaml",
                        "srectl agent validate --all",
                        "srectl agent validate --all --check-tools"
                    });
            return AgentCommandHandlers.HandleValidateCommand(pr);
        });
        return cmd;
    }

    private static Command CreateAgentApplyCommand()
    {
        var cmd = new Command("apply", CommandExamples.Agent.ApplyDescription)
        {
            AgentCommandOptions.ApplyNameOption,
            AgentCommandOptions.ApplyDryRunOption
        };

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent Apply", "Apply an agent configuration to the remote server", cmd,
                    new[] {
                        "srectl agent apply --name DevOpsAgent",
                        "srectl agent apply --name KustoAgent --dry-run",
                        "srectl agent apply --name MyAgent --debug"
                    });
            return AgentCommandHandlers.HandleApplyCommand(pr);
        });
        return cmd;
    }

    private static Command CreateAgentDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Agent.DeleteDescription)
        {
            AgentCommandOptions.DeleteNameOption
        };

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent Delete", "Delete an agent from the remote server", cmd,
                    new[] {
                        "srectl agent delete --name OldAgent",
                        "srectl agent delete --name TestAgent --debug"
                    });
            return AgentCommandHandlers.HandleDeleteCommand(pr);
        });
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

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent Test", "Test an agent with a specific message", cmd,
                    new[] {
                        "srectl agent test --name DevOpsAgent --message \"Check pod status in namespace production\"",
                        "srectl agent test --name KustoAgent --message \"Query memory usage\" --no-wait",
                        "srectl agent test --name MyAgent --message \"Help me\" --user-id john.doe --display-name \"John Doe\""
                    });
            return AgentCommandHandlers.HandleTestCommand(pr);
        });
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

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent Diff", "Compare local and remote agent configurations", cmd,
                    new[] {
                        "srectl agent diff --name DevOpsAgent",
                        "srectl agent diff --name KustoAgent --tool code",
                        "srectl agent diff --name MyAgent --raw"
                    });
            return AgentCommandHandlers.HandleDiffCommand(pr);
        });
        return cmd;
    }

    private static Command CreateAgentListCommand()
    {
        var cmd = new Command("list", "List remote extended agents from the server")
        {
            AgentCommandOptions.DebugOption,
            AgentCommandOptions.AllOption
        };
        cmd.SetAction((ParseResult pr) =>
        {
            // Initialize context early to surface debug logs in this path
            Agent.Cli.Helpers.CommandExecutionContext.Initialize(pr);
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Agent List", "List remote extended agents from the server", cmd,
                    new[] { "srectl agent list", "srectl agent list --all", "srectl agent list --debug" });
            return GeneralCommandHandlers.HandleListAgentsCommand(pr);
        });
        return cmd;
    }

    private static Command CreateToolCreateCommand()
    {
        var cmd = new Command("create", CommandExamples.Tool.CreateDescription)
        {
            ToolCommandOptions.NameOption,
            ToolCommandOptions.TypeOption,
            ToolCommandOptions.PathOption,
            ToolCommandOptions.ExtraOption
        };

        cmd.SetAction((ParseResult pr) => ToolCommandHandlers.HandleCreateCommand(pr));
        return cmd;
    }

    private static Command CreateToolValidateCommand()
    {
        var cmd = new Command("validate", CommandExamples.Tool.ValidateDescription)
        {
            ToolCommandOptions.NameOptionValidate,
            ToolCommandOptions.AllOption
        };

        cmd.SetAction((ParseResult pr) => ToolCommandHandlers.HandleValidateCommand(pr));
        return cmd;
    }

    private static Command CreateToolApplyCommand()
    {
        var cmd = new Command("apply", CommandExamples.Tool.ApplyDescription)
        {
            ToolCommandOptions.ApplyNameOption,
            ToolCommandOptions.DryRunOption
        };

        cmd.SetAction((ParseResult pr) => ToolCommandHandlers.HandleApplyCommand(pr));
        return cmd;
    }

    private static Command CreateToolDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Tool.DeleteDescription)
        {
            ToolCommandOptions.DeleteNameOption,
            ToolCommandOptions.DeleteDryRunOption
        };

        cmd.SetAction((ParseResult pr) => ToolCommandHandlers.HandleDeleteCommand(pr));
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

        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Tool Diff", "Compare local and remote tool configurations", cmd,
                    new[] {
                        "srectl tool diff --name QueryMetrics",
                        "srectl tool diff --name KustoTool --tool code",
                        "srectl tool diff --name MyTool --raw"
                    });
            return ToolCommandHandlers.HandleDiffCommand(pr);
        });
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
        cmd.SetAction((ParseResult pr) =>
            GeneralCommandHandlers.HandleInitCommandWithResourceUrl(pr.GetValue(resourceUrl)!));

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
        listAgents.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleListAgentsCommand(pr));

        var listExtendedTools = new Command("extended-tools", "List all extended tools added to the server through apply command")
        {
            ToolCommandOptions.DebugOption
        };
        listExtendedTools.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleListExtendedToolsCommand(pr));

        var listDataConnectors = new Command("data-connectors", "List all data connectors configured on the server")
        {
            ToolCommandOptions.DebugOption
        };
        listDataConnectors.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleListDataConnectorsCommand(pr));

        // List incident handlers subcommand
        var listIncidentHandlersCommand = new Command("incidenthandlers", "List all incident handlers from the remote server")
        {
            IncidentHandlerCommandOptions.VerboseOption
        };
        listIncidentHandlersCommand.SetAction(async parseResult =>
        {
            await IncidentHandlerCommandHandlers.HandleListCommand(parseResult);
        });

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
            ToolCommandOptions.VerboseOption,
            ToolCommandOptions.TypeFilterOption
        };

        cmd.SetAction((ParseResult pr) => ToolCommandHandlers.HandleShowTypesCommand(pr));
        return cmd;
    }

    private static Command CreateToolShowConnectorsCommand()
    {
        var cmd = new Command("show-connectors", CommandExamples.Tool.ShowConnectorsDescription)
        {
            ToolCommandOptions.VerboseOption
        };

        cmd.SetAction((ParseResult pr) => ToolCommandHandlers.HandleShowConnectorsCommand(pr));
        return cmd;
    }

    private static Command CreateToolListCommand()
    {
        var cmd = new Command("list", "List all tools from the remote server")
        {
            ToolCommandOptions.DebugOption
        };
        cmd.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleListToolsCommand(pr));
        return cmd;
    }

    private static Command CreateApplyYamlCommand()
    {
        var cmd = new Command("apply-yaml", CommandExamples.General.ApplyYamlDescription)
        {
            AgentCommandOptions.ApplyYamlFileOption
        };

        cmd.SetAction((ParseResult pr) =>
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
        threadNew.SetAction((ParseResult pr) => ThreadCommandHandlers.HandleThreadNewCommand(pr));

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
        threadContinue.SetAction((ParseResult pr) => ThreadCommandHandlers.HandleThreadContinueCommand(pr));

        var threadList = new Command("list", "List all threads");
        threadList.SetAction((ParseResult pr) => ThreadCommandHandlers.HandleThreadListCommand(pr));

        var threadDelete = new Command("delete", CommandExamples.Thread.DeleteDescription)
        {
            AgentCommandOptions.ThreadIdRequiredOption
        };
        threadDelete.SetAction((ParseResult pr) => ThreadCommandHandlers.HandleThreadDeleteCommand(pr));

        var threadTrack = new Command("track", "Track an existing thread for new messages")
        {
            AgentCommandOptions.ThreadIdRequiredOption
        };
        threadTrack.SetAction((ParseResult pr) => ThreadCommandHandlers.HandleThreadTrackCommand(pr));

        // Thread apply from YAML manifest
        var threadApply = new Command("apply", "Create a new thread from a YAML manifest (supports starting agent)")
        {
            AgentCommandOptions.ApplyYamlFileOption
        };
        threadApply.SetAction((ParseResult pr) => ThreadCommandHandlers.HandleThreadApplyCommand(pr));

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

        cmd.SetAction((ParseResult pr) => DocumentCommandHandlers.HandleUploadCommand(pr));
        return cmd;
    }

    private static Command CreateDocumentSearchCommand()
    {
        var cmd = new Command("search", CommandExamples.Document.SearchDescription)
        {
            DocumentCommandOptions.QueryOption
        };

        cmd.SetAction((ParseResult pr) => DocumentCommandHandlers.HandleSearchCommand(pr));
        return cmd;
    }

    private static Command CreateDocumentReindexCommand()
    {
        var cmd = new Command("reindex", CommandExamples.Document.ReindexDescription);
        cmd.SetAction((ParseResult pr) => DocumentCommandHandlers.HandleReindexCommand(pr));
        return cmd;
    }

    private static Command CreateChatCommand()
    {
        var cmd = new Command("chat", CommandExamples.General.ChatDescription)
        {
            AgentCommandOptions.ChatAgentNameOption
        };
        cmd.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleChatCommand(pr));
        return cmd;
    }

    private static Command CreateProfileListCommand()
    {
        var cmd = new Command("list", CommandExamples.Profile.ListDescription);
        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Profile List", "List all available profiles and show which one is currently active", cmd,
                    new[] { "srectl profile list", "srectl profile list --verbose" });
            return ProfileCommandHandlers.HandleListCommand(pr);
        });

        // Help is already handled by IsHelpRequested check in the command action
        return cmd;
    }

    private static Command CreateProfileGetCommand()
    {
        var cmd = new Command("get", CommandExamples.Profile.GetDescription)
        {
            ProfileCommandOptions.ProfileNameOption
        };
        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
            {
                return ShowFormattedSubcommandHelp("Profile Get", "Get details of a specific profile or the current active profile", cmd,
                    new[] { "srectl profile get", "srectl profile get --name production", "srectl profile get --name local --debug" });
            }
            return ProfileCommandHandlers.HandleGetCommand(pr);
        });

        // Help is already handled by IsHelpRequested check in the command action
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
        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Profile Create", "Create a new connection profile", cmd,
                    new[] {
                        "srectl profile create --name local --url https://localhost:7023",
                        "srectl profile create --name production --url https://prod-sreagent.company.com",
                        "srectl profile create --name staging --url https://staging.company.com --set-current"
                    });
            return ProfileCommandHandlers.HandleCreateCommand(pr);
        });

        // Help is already handled by IsHelpRequested check in the command action
        return cmd;
    }

    private static Command CreateProfileSetCommand()
    {
        var cmd = new Command("set", CommandExamples.Profile.SetDescription)
        {
            ProfileCommandOptions.ProfileNameRequiredOption
        };
        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Profile Set", "Set the active profile", cmd,
                    new[] { "srectl profile set --name local", "srectl profile set --name production", "srectl profile set --name staging --debug" });
            return ProfileCommandHandlers.HandleSetCommand(pr);
        });

        // Help is already handled by IsHelpRequested check in the command action
        return cmd;
    }

    private static Command CreateProfileDeleteCommand()
    {
        var cmd = new Command("delete", CommandExamples.Profile.DeleteDescription)
        {
            ProfileCommandOptions.ProfileNameRequiredOption
        };
        cmd.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Profile Delete", "Delete a profile", cmd,
                    new[] { "srectl profile delete --name old-environment", "srectl profile delete --name test --debug" });
            return ProfileCommandHandlers.HandleDeleteCommand(pr);
        });

        // Help is already handled by IsHelpRequested check in the command action
        return cmd;
    }

    private static Command CreateWelcomeCommand()
    {
        var cmd = new Command("welcome", "Show welcome screen and getting started guide");
        cmd.SetAction(async (ParseResult _) =>
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
        cmd.SetAction(async (ParseResult pr) =>
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
        cmd.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleStatusCommand(pr));
        return cmd;
    }

    private static Command CreateInteractiveCommand()
    {
        var cmd = new Command("interactive", "Start interactive guided mode for step-by-step assistance");
        cmd.SetAction((ParseResult pr) => InteractiveCommandHandlers.HandleInteractiveMode(pr));
        return cmd;
    }

    private static Command CreateVersionCommand()
    {
        var cmd = new Command("version", "Show version information and build details");
        cmd.SetAction((ParseResult pr) => GeneralCommandHandlers.HandleVersionCommand(pr));
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

                    var (agentsSuccess, _) = await apiService.ListAgentsAsync();
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
            ["Agent Lifecycle"] = new[] { "create", "validate", "apply", "delete" },
            ["Agent Testing"] = new[] { "test" },
            ["Agent Management"] = new[] { "list", "diff" }
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
            ["Tool Lifecycle"] = new[] { "create", "validate", "apply", "delete" },
            ["Tool Discovery"] = new[] { "show-types", "show-connectors" },
            ["Tool Management"] = new[] { "list", "diff" }
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Tool Lifecycle"] = "Create, validate, deploy, and remove automation tools",
            ["Tool Discovery"] = "Explore available tool types and data connectors",
            ["Tool Management"] = "List, discover, and compare deployed tools"
        };

        var examples = new[]
        {
            "srectl tool create --name QueryMetrics --type KustoTool",
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
            ["Thread Management"] = new[] { "new", "continue", "list", "delete", "track" }
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
            ["Incident Management"] = new[] { "create", "map-agent", "list" }
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

        var createCommand = new Command("create", "Create a new scheduled task")
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
        createCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Scheduled Task Create", "Create a new scheduled task for automated agent operations", createCommand,
                    new[] {
                        "srectl scheduledtask create --name \"Daily Health Check\" --cron \"0 9 * * *\" --prompt \"Check system health\"",
                        "srectl scheduledtask create --name \"Weekly Report\" --cron \"0 9 * * 1\" --prompt \"Generate weekly report\" --max-executions 4",
                        "srectl scheduledtask create --name \"Agent Task\" --cron \"0 10 * * *\" --prompt \"Run daily checks\" --agent \"ProductionAgent\""
                    });
            return ScheduledTaskCommandHandlers.HandleCreateCommand(pr);
        });

        var listCommand = new Command("list", "List all scheduled tasks")
        {
            ScheduledTaskCommandOptions.VerboseOption,
            ScheduledTaskCommandOptions.FilterThreadIdOption,
            ScheduledTaskCommandOptions.FilterStatusOption
        };
        listCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Scheduled Task List", "List all scheduled tasks from the remote server", listCommand,
                    new[] {
                        "srectl scheduledtask list",
                        "srectl scheduledtask list --verbose",
                        "srectl scheduledtask list --status Active"
                    });
            return ScheduledTaskCommandHandlers.HandleListCommand(pr);
        });

        var getCommand = new Command("get", "Get details of a specific scheduled task")
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        getCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Scheduled Task Get", "Get detailed information about a specific scheduled task", getCommand,
                    new[] {
                        "srectl scheduledtask get --id task-123",
                        "srectl scheduledtask get --id daily-health-check"
                    });
            return ScheduledTaskCommandHandlers.HandleGetCommand(pr);
        });

        var pauseCommand = new Command("pause", "Pause a scheduled task")
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        pauseCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Scheduled Task Pause", "Pause a scheduled task to stop its execution", pauseCommand,
                    new[] {
                        "srectl scheduledtask pause --id task-123",
                        "srectl scheduledtask pause --id daily-health-check"
                    });
            return ScheduledTaskCommandHandlers.HandlePauseCommand(pr);
        });

        var resumeCommand = new Command("resume", "Resume a paused scheduled task")
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        resumeCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Scheduled Task Resume", "Resume a paused scheduled task", resumeCommand,
                    new[] {
                        "srectl scheduledtask resume --id task-123",
                        "srectl scheduledtask resume --id daily-health-check"
                    });
            return ScheduledTaskCommandHandlers.HandleResumeCommand(pr);
        });

        var deleteCommand = new Command("delete", "Delete a scheduled task")
        {
            ScheduledTaskCommandOptions.RequiredTaskIdOption
        };
        deleteCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Scheduled Task Delete", "Permanently delete a scheduled task", deleteCommand,
                    new[] {
                        "srectl scheduledtask delete --id task-123",
                        "srectl scheduledtask delete --id old-maintenance-task"
                    });
            return ScheduledTaskCommandHandlers.HandleDeleteCommand(pr);
        });

        // Quickstart creates a minimal hello-world manifest interactively and applies it
        var quickstart = new Command("quickstart", "Interactive hello-world scheduled task wizard")
        {
            ScheduledTaskCommandOptions.QuickstartNameOption,
            ScheduledTaskCommandOptions.QuickstartCronOption,
            ScheduledTaskCommandOptions.QuickstartDurationHoursOption,
            ScheduledTaskCommandOptions.QuickstartAgentOption,
            ScheduledTaskCommandOptions.QuickstartApplyOption
        };
        quickstart.SetAction((ParseResult pr) => ScheduledTaskCommandHandlers.HandleQuickstartCommand(pr));

        // Apply a YAML manifest for a scheduled task
        var apply = new Command("apply", "Apply a ScheduledTask YAML manifest (apiVersion/kind/spec)")
        {
            AgentCommandOptions.ApplyYamlFileOption
        };
        apply.SetAction((ParseResult pr) => ScheduledTaskCommandHandlers.HandleApplyYamlCommand(pr));

        scheduledTaskCommand.Add(createCommand);
        scheduledTaskCommand.Add(listCommand);
        scheduledTaskCommand.Add(getCommand);
        scheduledTaskCommand.Add(pauseCommand);
        scheduledTaskCommand.Add(resumeCommand);
        scheduledTaskCommand.Add(deleteCommand);
        scheduledTaskCommand.Add(quickstart);
        scheduledTaskCommand.Add(apply);

        // Add default action for scheduled task command to show formatted help
        scheduledTaskCommand.SetAction((ParseResult pr) => ShowFormattedScheduledTaskHelp(scheduledTaskCommand));

        return scheduledTaskCommand;
    }

    private static Task ShowFormattedScheduledTaskHelp(Command scheduledTaskCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Task Management"] = new[] { "create", "list", "get" },
            ["Task Control"] = new[] { "pause", "resume", "delete" }
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

    // Helper methods for consistent subcommand help formatting
    private static bool IsHelpRequested(ParseResult parseResult)
    {
        return parseResult.Tokens.Any(token =>
            token.Value == "-h" || token.Value == "--help" || token.Value == "-?" || token.Value == "help");
    }

    private static Task ShowFormattedSubcommandHelp(string title, string description, Command command, string[] examples)
    {
        StandardHelpFormatter.ShowSrectlHeader();

        ConsoleUI.DrawPanel(title, description, ConsoleColor.Cyan);
        Console.WriteLine();

        // Show usage
        ConsoleUI.WriteSection("Usage");
        Console.WriteLine($"  srectl {GetCommandPath(command)} [options]");
        Console.WriteLine();

        // Show options
        if (command.Options.Any())
        {
            ConsoleUI.WriteSection("Options");
            foreach (var option in command.Options)
            {
                // Always display canonical long form to avoid duplicated dashes from alias lists
                var canonical = $"--{option.Name}";
                var required = option.Required ? " (REQUIRED)" : "";
                ConsoleUI.WriteKeyValue(canonical + required, option.Description ?? "No description", 20, ConsoleColor.Yellow);
            }
            Console.WriteLine();
        }

        // Show examples using the new ConsoleUI renderer
        if (examples?.Any() == true)
        {
            var tuples = examples.Select(e => ("", e)).ToArray();
            ConsoleUI.WriteExamples(tuples);
        }

        return Task.CompletedTask;
    }

    private static string GetCommandPath(Command command)
    {
        // For subcommands, manually construct the path based on command name
        switch (command.Name)
        {
            case "list":
            case "get":
            case "create":
            case "set":
            case "delete":
                // Could be profile, agent, tool, or doc commands
                // We'll use context clues from other properties if needed
                if (command.Description?.Contains("profile") == true)
                    return $"profile {command.Name}";
                if (command.Description?.Contains("agent") == true || command.Options.Any(o => o.Aliases.Contains("--name")))
                    return $"agent {command.Name}";
                if (command.Description?.Contains("tool") == true)
                    return $"tool {command.Name}";
                if (command.Description?.Contains("document") == true)
                    return $"doc {command.Name}";
                break;
            case "validate":
            case "apply":
            case "test":
                return $"agent {command.Name}";
            case "show-types":
            case "show-connectors":
                return $"tool {command.Name}";
            case "upload":
            case "search":
            case "reindex":
                return $"doc {command.Name}";
        }

        return command.Name ?? "";
    }

    private static Command? GetParentCommand(Command command)
    {
        // This is a simplified approach - in a real implementation you might need
        // to track parent relationships differently
        return null; // For now, we'll build the path manually
    }

    // Add this method or update existing BuildCommands method:
    private static Command BuildIncidentHandlerCommand()
    {
        var incidentHandlerCommand = new Command("incidenthandler", "Manage incident handlers and filters");

        var mapAgentCommand = new Command("map-agent", "Map a YAML agent to an incident filter")
        {
            IncidentHandlerCommandOptions.FilterNameOption,
            IncidentHandlerCommandOptions.HandlingAgentOption
        };
        mapAgentCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Incident Handler Map Agent", "Map a YAML agent to an incident filter", mapAgentCommand,
                    new[] {
                        "srectl incidenthandler map-agent --name ProductionFilter --handling-agent ProductionAgent",
                        "srectl incidenthandler map-agent --name StorageIssues --handling-agent StorageAgent"
                    });
            return IncidentHandlerCommandHandlers.HandleMapAgentCommand(pr);
        });

        var listCommand = new Command("list", "List all incident handlers")
        {
            IncidentHandlerCommandOptions.VerboseOption
        };
        listCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Incident Handler List", "List all incident handlers from the remote server", listCommand,
                    new[] {
                        "srectl incidenthandler list",
                        "srectl incidenthandler list --verbose"
                    });
            return IncidentHandlerCommandHandlers.HandleListCommand(pr);
        });

        var createCommand = new Command("create", "Create a new incident filter")
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
        createCommand.SetAction((ParseResult pr) =>
        {
            if (IsHelpRequested(pr))
                return ShowFormattedSubcommandHelp("Incident Handler Create", "Create a new incident filter with specified criteria", createCommand,
                    new[] {
                        "srectl incidenthandler create --id StorageFilter --name \"Storage Issues\" --title-contains \"storage\"",
                        "srectl incidenthandler create --id ProdFilter --priority 1 --incident-type LiveSite --handling-agent ProdAgent",
                        "srectl incidenthandler create --id APIFilter --impacted-service \"Web API\" --max-attempts 5"
                    });
            return IncidentHandlerCommandHandlers.HandleCreateCommand(pr);
        });

        incidentHandlerCommand.Add(mapAgentCommand);
        incidentHandlerCommand.Add(listCommand);
        incidentHandlerCommand.Add(createCommand);
        return incidentHandlerCommand;
    }
}
