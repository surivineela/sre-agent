namespace Agent.Core.Models.Api.v1;

// Always append and don't change the order here as it's persisted in Database and used by SQL Query to filter all agent messages
public enum Role
{
    /// <summary>
    /// The real user who provides input to the agent
    /// </summary>
    User,
    /// <summary>
    /// The agent itself, which is the one responding to the user with answers computed by LLM and plugins
    /// </summary>
    SREAgent,
    /// <summary>
    /// The system, which is the one that orchestrates the agent and provides the context for the agent to work
    /// It can be used for SystemPrompt provided by meta agent, agent or task based orchestrator
    /// </summary>
    System,
    /// <summary>
    /// The plugin which provides functionality to the agent.
    /// This can be used by output execution process and provides observability to user.
    /// The plugin log should be filtered out from the chat history to talk with LLM due to token limitation.
    /// </summary>
    PluginLog
}

public record Author(
    Role Role,
    string UserId,
    string DisplayName);
