using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Agent.Cli.Commands;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds the complete command-line interface structure for the SRECTL tool.
/// </summary>
public static class CommandBuilder
{
    /// <summary>
    /// Creates and configures all CLI commands and their hierarchical structure.
    /// </summary>
    /// <returns>The root command with all subcommands configured</returns>
    public static RootCommand BuildCommands()
    {
        // Build agent commands
        var agentCreate = CreateAgentCreateCommand();
        var agentValidate = CreateAgentValidateCommand();
        var agentApply = CreateAgentApplyCommand();
        var agentDelete = CreateAgentDeleteCommand();
        var agentTest = CreateAgentTestCommand();

        var agent = new Command("agent", "Agent commands");
        agent.Subcommands.Add(agentCreate);
        agent.Subcommands.Add(agentValidate);
        agent.Subcommands.Add(agentApply);
        agent.Subcommands.Add(agentDelete);
        agent.Subcommands.Add(agentTest);

        // Build tool commands
        var toolCreate = CreateToolCreateCommand();
        var toolValidate = CreateToolValidateCommand();
        var toolApply = CreateToolApplyCommand();
        var toolDelete = CreateToolDeleteCommand();
        var toolShowTypes = CreateToolShowTypesCommand();
        var toolShowConnectors = CreateToolShowConnectorsCommand();

        var tool = new Command("tool", "Tool commands");
        tool.Subcommands.Add(toolCreate);
        tool.Subcommands.Add(toolValidate);
        tool.Subcommands.Add(toolApply);
        tool.Subcommands.Add(toolDelete);
        tool.Subcommands.Add(toolShowTypes);
        tool.Subcommands.Add(toolShowConnectors);

        // Build document commands
        var docUpload = CreateDocumentUploadCommand();
        var docSearch = CreateDocumentSearchCommand();
        var docReindex = CreateDocumentReindexCommand();

        var doc = new Command("doc", "Document management commands");
        doc.Subcommands.Add(docUpload);
        doc.Subcommands.Add(docSearch);
        doc.Subcommands.Add(docReindex);

        // Build profile commands
        var profileList = CreateProfileListCommand();
        var profileGet = CreateProfileGetCommand();
        var profileCreate = CreateProfileCreateCommand();
        var profileSet = CreateProfileSetCommand();
        var profileDelete = CreateProfileDeleteCommand();

        var profile = new Command("profile", "Profile management commands. Profiles store connection settings for different SRE Agent instances (local or remote)");
        profile.Subcommands.Add(profileList);
        profile.Subcommands.Add(profileGet);
        profile.Subcommands.Add(profileCreate);
        profile.Subcommands.Add(profileSet);
        profile.Subcommands.Add(profileDelete);

        // Build general commands
        var initCommand = CreateInitCommand();
        var listCommand = CreateListCommand();
        var applyYamlCommand = CreateApplyYamlCommand();
        var threadCommand = CreateThreadCommand();
        var chatCommand = CreateChatCommand();

        // Global options
        var debugOption = new Option<bool>("--debug") { Description = "Enable debug logging" };
        var quietOption = new Option<bool>("--quiet") { Description = "Minimize output" };

        // Root command
        var root = new RootCommand("SRE Agent CLI");
        root.Options.Add(debugOption);
        root.Options.Add(quietOption);

        root.Subcommands.Add(initCommand);
        root.Subcommands.Add(listCommand);
        root.Subcommands.Add(applyYamlCommand);
        root.Subcommands.Add(threadCommand);
        root.Subcommands.Add(chatCommand);
        root.Subcommands.Add(agent);
        root.Subcommands.Add(tool);
        root.Subcommands.Add(doc);
        root.Subcommands.Add(profile);

        return root;
    }

    private static Command CreateAgentCreateCommand()
    {
        var agentCreate = new Command("create", "Create a new agent YAML")
        {
            AgentCommandOptions.DebugOption,
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
        agentCreate.SetAction(async parseResult =>
        {
            await AgentCommandHandlers.HandleCreateCommand(parseResult);
        });
        return agentCreate;
    }

    private static Command CreateAgentValidateCommand()
    {
        var agentValidate = new Command("validate", "Validate an agent")
        {
            AgentCommandOptions.DebugOption,
            AgentCommandOptions.FileOptionValidate,
            AgentCommandOptions.AllOption,
            AgentCommandOptions.CheckToolsOption
        };
        agentValidate.SetAction(async parseResult =>
        {
            await AgentCommandHandlers.HandleValidateCommand(parseResult);
        });
        return agentValidate;
    }

    private static Command CreateAgentApplyCommand()
    {
        var agentApply = new Command("apply", "Apply an agent configuration to the remote server")
        {
            AgentCommandOptions.DebugOption,
            AgentCommandOptions.ApplyNameOption,
            AgentCommandOptions.ApplyDryRunOption
        };
        agentApply.SetAction(async parseResult =>
        {
            await AgentCommandHandlers.HandleApplyCommand(parseResult);
        });
        return agentApply;
    }

    private static Command CreateAgentDeleteCommand()
    {
        var agentDelete = new Command("delete", "Delete an agent from the remote server")
        {
            AgentCommandOptions.DebugOption,
            AgentCommandOptions.DeleteNameOption
        };
        agentDelete.SetAction(async parseResult =>
        {
            await AgentCommandHandlers.HandleDeleteCommand(parseResult);
        });
        return agentDelete;
    }

    private static Command CreateAgentTestCommand()
    {
        var agentTest = new Command("test", "Test an agent with a specific message")
        {
            AgentCommandOptions.DebugOption,
            AgentCommandOptions.TestNameOption,
            AgentCommandOptions.TestMessageOption,
            AgentCommandOptions.TestUserIdOption,
            AgentCommandOptions.TestDisplayNameOption,
            AgentCommandOptions.TestNoWaitOption
        };
        agentTest.SetAction(async parseResult =>
        {
            await AgentCommandHandlers.HandleTestCommand(parseResult);
        });
        return agentTest;
    }

    private static Command CreateToolCreateCommand()
    {
        var toolCreate = new Command("create", "Create a new tool YAML")
        {
            ToolCommandOptions.DebugOption,
            ToolCommandOptions.NameOption,
            ToolCommandOptions.TypeOption,
            ToolCommandOptions.PathOption,
            ToolCommandOptions.ExtraOption
        };
        toolCreate.SetAction(async parseResult =>
        {
            await ToolCommandHandlers.HandleCreateCommand(parseResult);
        });
        return toolCreate;
    }

    private static Command CreateToolValidateCommand()
    {
        var toolValidate = new Command("validate", "Validate a tool YAML")
        {
            ToolCommandOptions.DebugOption,
            ToolCommandOptions.NameOptionValidate,
            ToolCommandOptions.AllOption
        };
        toolValidate.SetAction(parseResult =>
        {
            ToolCommandHandlers.HandleValidateCommand(parseResult);
        });
        return toolValidate;
    }

    private static Command CreateToolApplyCommand()
    {
        var toolApply = new Command("apply", "Apply a tool configuration to the remote server")
        {
            ToolCommandOptions.DebugOption,
            ToolCommandOptions.ApplyNameOption,
            ToolCommandOptions.DryRunOption
        };
        toolApply.SetAction(async parseResult =>
        {
            await ToolCommandHandlers.HandleApplyCommand(parseResult);
        });
        return toolApply;
    }

    private static Command CreateToolDeleteCommand()
    {
        var toolDelete = new Command("delete", "Delete a tool from the remote server")
        {
            ToolCommandOptions.DebugOption,
            ToolCommandOptions.DeleteNameOption,
            ToolCommandOptions.DeleteDryRunOption
        };
        toolDelete.SetAction(async parseResult =>
        {
            await ToolCommandHandlers.HandleDeleteCommand(parseResult);
        });
        return toolDelete;
    }

    private static Command CreateInitCommand()
    {
        var initResourceUrlOption = new Option<string>("--resource-url") { Required = true };
        var initCommand = new Command("init", "Initialize SREAgent CLI configuration")
        {
            initResourceUrlOption
        };
        initCommand.SetAction(async parseResult =>
        {
            var resourceUrl = parseResult.GetValue(initResourceUrlOption);
            await GeneralCommandHandlers.HandleInitCommandWithResourceUrl(resourceUrl!);
        });
        return initCommand;
    }

    private static Command CreateListCommand()
    {
        // List agents subcommand
        var listAgentsCommand = new Command("agents", "List all agents from the remote server");
        listAgentsCommand.SetAction(async parseResult =>
        {
            await GeneralCommandHandlers.HandleListAgentsCommand(parseResult);
        });

        // List tools subcommand
        var listToolsCommand = new Command("tools", "List all tools from the remote server");
        listToolsCommand.SetAction(async parseResult =>
        {
            await GeneralCommandHandlers.HandleListToolsCommand(parseResult);
        });

        // List extended-tools subcommand
        var listExtendedToolsCommand = new Command("extended-tools", "List all extended tools added to the server through apply command");
        listExtendedToolsCommand.SetAction(async parseResult =>
        {
            await GeneralCommandHandlers.HandleListExtendedToolsCommand(parseResult);
        });

        // List data-connectors subcommand
        var listDataConnectorsCommand = new Command("data-connectors", "List all data connectors configured on the server");
        listDataConnectorsCommand.SetAction(async parseResult =>
        {
            await GeneralCommandHandlers.HandleListDataConnectorsCommand(parseResult);
        });

        var listCommand = new Command("list", "List agents, tools, extended tools, or data connectors from the remote server");
        listCommand.Subcommands.Add(listAgentsCommand);
        listCommand.Subcommands.Add(listToolsCommand);
        listCommand.Subcommands.Add(listExtendedToolsCommand);
        listCommand.Subcommands.Add(listDataConnectorsCommand);

        return listCommand;
    }

    private static Command CreateToolShowTypesCommand()
    {
        var toolShowTypes = new Command("show-types", "Show available tool types and their definitions")
        {
            ToolCommandOptions.VerboseOption,
            ToolCommandOptions.TypeFilterOption
        };
        toolShowTypes.SetAction(parseResult =>
        {
            ToolCommandHandlers.HandleShowTypesCommand(parseResult);
        });
        return toolShowTypes;
    }

    private static Command CreateToolShowConnectorsCommand()
    {
        var toolShowConnectors = new Command("show-connectors", "Show available connector types")
        {
            ToolCommandOptions.VerboseOption
        };
        toolShowConnectors.SetAction(parseResult =>
        {
            ToolCommandHandlers.HandleShowConnectorsCommand(parseResult);
        });
        return toolShowConnectors;
    }

    private static Command CreateApplyYamlCommand()
    {
        var applyYamlCommand = new Command("apply-yaml", "Apply a YAML file directly to the remote server")
        {
            AgentCommandOptions.ApplyYamlFileOption
        };
        applyYamlCommand.SetAction(async parseResult =>
        {
            await GeneralCommandHandlers.HandleApplyYamlCommand(parseResult);
        });
        return applyYamlCommand;
    }

    private static Command CreateThreadCommand()
    {
        var threadNew = new Command("new", "Create a new thread and send a message")
        {
            AgentCommandOptions.ThreadMessageOption,
            AgentCommandOptions.ThreadUserIdOption,
            AgentCommandOptions.ThreadDisplayNameOption,
            AgentCommandOptions.ThreadWaitOption,
            AgentCommandOptions.ThreadNoWaitOption
        };
        threadNew.SetAction(async parseResult =>
        {
            await ThreadCommandHandlers.HandleThreadNewCommand(parseResult);
        });

        var threadContinue = new Command("continue", "Continue an existing thread")
        {
            AgentCommandOptions.ThreadIdOption,
            AgentCommandOptions.ThreadMessageOptionalOption,
            AgentCommandOptions.ThreadUserIdOption,
            AgentCommandOptions.ThreadDisplayNameOption,
            AgentCommandOptions.ThreadWaitOption,
            AgentCommandOptions.ThreadNoWaitOption
        };
        threadContinue.SetAction(async parseResult =>
        {
            await ThreadCommandHandlers.HandleThreadContinueCommand(parseResult);
        });

        var threadList = new Command("list", "List all threads");
        threadList.SetAction(async parseResult =>
        {
            await ThreadCommandHandlers.HandleThreadListCommand(parseResult);
        });

        var threadDelete = new Command("delete", "Delete a thread")
        {
            AgentCommandOptions.ThreadIdRequiredOption
        };
        threadDelete.SetAction(async parseResult =>
        {
            await ThreadCommandHandlers.HandleThreadDeleteCommand(parseResult);
        });

        var threadTrack = new Command("track", "Track an existing thread for new messages")
        {
            AgentCommandOptions.ThreadIdRequiredOption
        };
        threadTrack.SetAction(async parseResult =>
        {
            await ThreadCommandHandlers.HandleThreadTrackCommand(parseResult);
        });

        var thread = new Command("thread", "Thread management commands");
        thread.Subcommands.Add(threadNew);
        thread.Subcommands.Add(threadContinue);
        thread.Subcommands.Add(threadList);
        thread.Subcommands.Add(threadDelete);
        thread.Subcommands.Add(threadTrack);

        return thread;
    }

    private static Command CreateDocumentUploadCommand()
    {
        var docUpload = new Command("upload", "Upload documents to the SRE Agent memory storage")
        {
            DocumentCommandOptions.FileOption,
            DocumentCommandOptions.FolderOption,
            DocumentCommandOptions.TriggerIndexingOption,
            DocumentCommandOptions.NoIndexingOption,
            DocumentCommandOptions.RecursiveOption
        };
        docUpload.SetAction(async parseResult =>
        {
            await DocumentCommandHandlers.HandleUploadCommand(parseResult);
        });
        return docUpload;
    }

    private static Command CreateDocumentSearchCommand()
    {
        var docSearch = new Command("search", "Search documents in the SRE Agent knowledge base")
        {
            DocumentCommandOptions.QueryOption
        };
        docSearch.SetAction(async parseResult =>
        {
            await DocumentCommandHandlers.HandleSearchCommand(parseResult);
        });
        return docSearch;
    }

    private static Command CreateDocumentReindexCommand()
    {
        var docReindex = new Command("reindex", "Trigger reindexing of all documents in the SRE Agent knowledge base");
        docReindex.SetAction(async parseResult =>
        {
            await DocumentCommandHandlers.HandleReindexCommand(parseResult);
        });
        return docReindex;
    }

    private static Command CreateChatCommand()
    {
        var chatCommand = new Command("chat", "Start persistent interactive chat session");
        chatCommand.SetAction(async parseResult =>
        {
            await GeneralCommandHandlers.HandleChatCommand(parseResult);
        });
        return chatCommand;
    }

    private static Command CreateProfileListCommand()
    {
        var profileList = new Command("list", "List all available profiles and show which one is currently active");
        profileList.SetAction(async parseResult =>
        {
            await ProfileCommandHandlers.HandleListCommand(parseResult);
        });
        return profileList;
    }

    private static Command CreateProfileGetCommand()
    {
        var profileGet = new Command("get", "Get details of a specific profile or the current active profile")
        {
            ProfileCommandOptions.ProfileNameOption
        };
        profileGet.SetAction(async parseResult =>
        {
            await ProfileCommandHandlers.HandleGetCommand(parseResult);
        });
        return profileGet;
    }

    private static Command CreateProfileCreateCommand()
    {
        var profileCreate = new Command("create", "Create a new profile to connect to an SRE Agent instance (local or remote)")
        {
            ProfileCommandOptions.ProfileNameRequiredOption,
            ProfileCommandOptions.ResourceUrlOption,
            ProfileCommandOptions.SetCurrentOption
        };
        profileCreate.SetAction(async parseResult =>
        {
            await ProfileCommandHandlers.HandleCreateCommand(parseResult);
        });
        return profileCreate;
    }

    private static Command CreateProfileSetCommand()
    {
        var profileSet = new Command("set", "Switch to a different profile to change which SRE Agent instance you're connected to")
        {
            ProfileCommandOptions.ProfileNameRequiredOption
        };
        profileSet.SetAction(async parseResult =>
        {
            await ProfileCommandHandlers.HandleSetCommand(parseResult);
        });
        return profileSet;
    }

    private static Command CreateProfileDeleteCommand()
    {
        var profileDelete = new Command("delete", "Delete a profile (cannot delete the currently active profile)")
        {
            ProfileCommandOptions.ProfileNameRequiredOption
        };
        profileDelete.SetAction(async parseResult =>
        {
            await ProfileCommandHandlers.HandleDeleteCommand(parseResult);
        });
        return profileDelete;
    }
}
