// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Cli.Mcp;

/// <summary>
/// Persistent context store for the MCP server session.
/// Tracks the agent building process, user preferences, conversation history, and knowledge base.
/// Data is automatically persisted to disk and survives restarts.
/// </summary>
public class McpMemoryStore
{
    private readonly Dictionary<string, AgentBuildingContext> _agentContexts = new();
    private readonly Dictionary<string, SubagentBuildingContext> _subagentContexts = new();
    private readonly List<ConversationEntry> _conversationHistory = new();
    private readonly Dictionary<string, string> _userPreferences = new();
    private readonly List<string> _recentActions = new();
    private readonly List<KnowledgeEntry> _knowledgeBase = new();
    private const int MaxRecentActions = 50;
    private const int MaxConversationHistory = 100;

    private readonly string _persistencePath;
    private readonly bool _autoPersist;
    private readonly object _persistLock = new();

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates a new McpMemoryStore with optional persistence.
    /// </summary>
    /// <param name="autoPersist">If true, automatically saves to disk on changes.</param>
    /// <param name="persistencePath">Custom path for persistence file. Defaults to ~/.sreagent/mcp-session.json</param>
    public McpMemoryStore(bool autoPersist = true, string? persistencePath = null)
    {
        _autoPersist = autoPersist;
        _persistencePath = persistencePath ?? GetDefaultPersistencePath();

        if (_autoPersist)
        {
            LoadFromDisk();
        }
    }

    /// <summary>
    /// Gets the default persistence path based on the OS.
    /// </summary>
    private static string GetDefaultPersistencePath()
    {
        string baseDir;
        if (OperatingSystem.IsWindows())
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sreagent");
        }
        else
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sreagent");
        }

        return Path.Combine(baseDir, "mcp-session.json");
    }

    /// <summary>
    /// Gets the path where session data is persisted.
    /// </summary>
    public string PersistencePath => _persistencePath;

    /// <summary>
    /// Gets or creates an agent building context by name.
    /// </summary>
    public AgentBuildingContext GetOrCreateAgentContext(string agentName)
    {
        if (!_agentContexts.TryGetValue(agentName, out var context))
        {
            context = new AgentBuildingContext { Name = agentName };
            _agentContexts[agentName] = context;
            AutoPersist();
        }
        return context;
    }

    /// <summary>
    /// Lists all agent contexts being built.
    /// </summary>
    public IReadOnlyList<AgentBuildingContext> ListAgentContexts()
        => _agentContexts.Values.ToList().AsReadOnly();

    /// <summary>
    /// Removes an agent context.
    /// </summary>
    public bool RemoveAgentContext(string agentName)
    {
        var result = _agentContexts.Remove(agentName);
        if (result) AutoPersist();
        return result;
    }

    /// <summary>
    /// Gets or creates a subagent building context by name.
    /// </summary>
    public SubagentBuildingContext GetOrCreateSubagentContext(string subagentName)
    {
        if (!_subagentContexts.TryGetValue(subagentName, out var context))
        {
            context = new SubagentBuildingContext { Name = subagentName };
            _subagentContexts[subagentName] = context;
            AutoPersist();
        }
        return context;
    }

    /// <summary>
    /// Lists all subagent contexts being built.
    /// </summary>
    public IReadOnlyList<SubagentBuildingContext> ListSubagentContexts()
        => _subagentContexts.Values.ToList().AsReadOnly();

    /// <summary>
    /// Removes a subagent context.
    /// </summary>
    public bool RemoveSubagentContext(string subagentName)
    {
        var result = _subagentContexts.Remove(subagentName);
        if (result) AutoPersist();
        return result;
    }

    /// <summary>
    /// Adds a conversation entry to history.
    /// </summary>
    public void AddConversationEntry(string role, string content, string? toolName = null)
    {
        _conversationHistory.Add(new ConversationEntry
        {
            Role = role,
            Content = content,
            ToolName = toolName,
            Timestamp = DateTime.UtcNow
        });

        // Trim history if too long
        while (_conversationHistory.Count > MaxConversationHistory)
        {
            _conversationHistory.RemoveAt(0);
        }

        AutoPersist();
    }

    /// <summary>
    /// Gets recent conversation history.
    /// </summary>
    public IReadOnlyList<ConversationEntry> GetConversationHistory(int count = 20)
        => _conversationHistory.TakeLast(count).ToList().AsReadOnly();

    /// <summary>
    /// Sets a user preference.
    /// </summary>
    public void SetPreference(string key, string value)
    {
        _userPreferences[key] = value;
        AutoPersist();
    }

    /// <summary>
    /// Gets a user preference.
    /// </summary>
    public string? GetPreference(string key)
        => _userPreferences.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Gets all user preferences.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllPreferences()
        => _userPreferences.AsReadOnly();

    /// <summary>
    /// Records a recent action for context.
    /// </summary>
    public void RecordAction(string action)
    {
        _recentActions.Add($"[{DateTime.UtcNow:HH:mm:ss}] {action}");
        while (_recentActions.Count > MaxRecentActions)
        {
            _recentActions.RemoveAt(0);
        }
        AutoPersist();
    }

    /// <summary>
    /// Gets recent actions.
    /// </summary>
    public IReadOnlyList<string> GetRecentActions(int count = 10)
        => _recentActions.TakeLast(count).ToList().AsReadOnly();

    /// <summary>
    /// Gets a summary of the current session state.
    /// </summary>
    public string GetSessionSummary()
    {
        var summary = new
        {
            AgentsInProgress = _agentContexts.Count,
            AgentNames = _agentContexts.Keys.ToList(),
            SubagentsInProgress = _subagentContexts.Count,
            SubagentNames = _subagentContexts.Keys.ToList(),
            ConversationEntries = _conversationHistory.Count,
            PreferencesSet = _userPreferences.Count,
            RecentActionsCount = _recentActions.Count,
            KnowledgeEntries = _knowledgeBase.Count,
            PersistencePath = _persistencePath,
            AutoPersistEnabled = _autoPersist
        };

        return JsonSerializer.Serialize(summary, s_jsonOptions);
    }

    #region Knowledge Base

    /// <summary>
    /// Adds a knowledge entry to the session knowledge base.
    /// </summary>
    public KnowledgeEntry AddKnowledgeEntry(string category, string title, string content, List<string>? tags = null)
    {
        var entry = new KnowledgeEntry
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Category = category,
            Title = title,
            Content = content,
            Tags = tags ?? new List<string>()
        };
        _knowledgeBase.Add(entry);
        RecordAction($"Added knowledge: [{category}] {title}");
        return entry;
    }

    /// <summary>
    /// Searches the knowledge base.
    /// </summary>
    public IReadOnlyList<KnowledgeEntry> SearchKnowledge(string query, string? category = null)
    {
        var queryLower = query.ToLowerInvariant();
        return _knowledgeBase
            .Where(e => (category == null || e.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) &&
                        (e.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         e.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         e.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Lists all knowledge entries, optionally filtered by category.
    /// </summary>
    public IReadOnlyList<KnowledgeEntry> ListKnowledge(string? category = null)
    {
        if (category == null)
            return _knowledgeBase.AsReadOnly();

        return _knowledgeBase
            .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a knowledge entry by ID.
    /// </summary>
    public KnowledgeEntry? GetKnowledgeEntry(string id)
        => _knowledgeBase.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Removes a knowledge entry by ID.
    /// </summary>
    public bool RemoveKnowledgeEntry(string id)
    {
        var entry = _knowledgeBase.FirstOrDefault(e => e.Id == id);
        if (entry != null)
        {
            _knowledgeBase.Remove(entry);
            AutoPersist();
            return true;
        }
        return false;
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Saves the session to disk.
    /// </summary>
    public void SaveToDisk()
    {
        lock (_persistLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_persistencePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var state = new PersistedSessionState
                {
                    AgentContexts = _agentContexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    SubagentContexts = _subagentContexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    ConversationHistory = _conversationHistory.ToList(),
                    UserPreferences = _userPreferences.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    RecentActions = _recentActions.ToList(),
                    KnowledgeBase = _knowledgeBase.ToList(),
                    LastSavedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(state, s_jsonOptions);
                File.WriteAllText(_persistencePath, json);
            }
            catch
            {
                // Silently fail on persistence errors to not disrupt the main workflow
            }
        }
    }

    /// <summary>
    /// Loads the session from disk.
    /// </summary>
    public void LoadFromDisk()
    {
        lock (_persistLock)
        {
            try
            {
                if (!File.Exists(_persistencePath))
                    return;

                var json = File.ReadAllText(_persistencePath);
                var state = JsonSerializer.Deserialize<PersistedSessionState>(json, s_jsonOptions);

                if (state == null)
                    return;

                _agentContexts.Clear();
                foreach (var kvp in state.AgentContexts)
                    _agentContexts[kvp.Key] = kvp.Value;

                _subagentContexts.Clear();
                foreach (var kvp in state.SubagentContexts)
                    _subagentContexts[kvp.Key] = kvp.Value;

                _conversationHistory.Clear();
                _conversationHistory.AddRange(state.ConversationHistory);

                _userPreferences.Clear();
                foreach (var kvp in state.UserPreferences)
                    _userPreferences[kvp.Key] = kvp.Value;

                _recentActions.Clear();
                _recentActions.AddRange(state.RecentActions);

                _knowledgeBase.Clear();
                _knowledgeBase.AddRange(state.KnowledgeBase);
            }
            catch
            {
                // If load fails, start fresh
            }
        }
    }

    /// <summary>
    /// Deletes the persistence file.
    /// </summary>
    public void DeletePersistenceFile()
    {
        lock (_persistLock)
        {
            try
            {
                if (File.Exists(_persistencePath))
                {
                    File.Delete(_persistencePath);
                }
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }

    /// <summary>
    /// Auto-persists if enabled.
    /// </summary>
    private void AutoPersist()
    {
        if (_autoPersist)
        {
            SaveToDisk();
        }
    }

    #endregion

    /// <summary>
    /// Clears all session data.
    /// </summary>
    /// <param name="deleteFile">If true, also deletes the persistence file.</param>
    public void ClearSession(bool deleteFile = false)
    {
        _agentContexts.Clear();
        _subagentContexts.Clear();
        _conversationHistory.Clear();
        _userPreferences.Clear();
        _recentActions.Clear();
        _knowledgeBase.Clear();

        if (deleteFile)
        {
            DeletePersistenceFile();
        }
        else
        {
            AutoPersist();
        }
    }
}

/// <summary>
/// Context for building a single agent.
/// </summary>
public class AgentBuildingContext
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public List<string> Tools { get; set; } = new();
    public List<string> Handoffs { get; set; } = new();
    public List<string> Connectors { get; set; } = new();
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public double? Temperature { get; set; }
    public int? MaxReflectionCount { get; set; }
    public bool? DisableDocumentRetrieval { get; set; }
    public bool? VanillaMode { get; set; }
    public List<string> CommonPrompts { get; set; } = new();
    public string? LlmModelName { get; set; }

    // Trigger configuration (for incident handlers)
    public TriggerConfiguration? Trigger { get; set; }

    // Scheduled task configuration
    public ScheduledTaskConfiguration? ScheduledTask { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Notes { get; set; } = new();
    public BuildStatus Status { get; set; } = BuildStatus.Draft;

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Trigger configuration for incident-based agents.
/// </summary>
public class TriggerConfiguration
{
    public TriggerType Type { get; set; } = TriggerType.None;
    public string? ImpactedService { get; set; }
    public List<int> Priorities { get; set; } = new();
    public string? TitleContains { get; set; }
    public string? AlertId { get; set; }
    public string? OwningTeamId { get; set; }
    public int MaxAttempts { get; set; } = 3;
}

/// <summary>
/// Scheduled task configuration.
/// </summary>
public class ScheduledTaskConfiguration
{
    public string? CronExpression { get; set; }
    public string? AgentPrompt { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? MaxExecutions { get; set; }
    public string? NotificationChannel { get; set; }
}

/// <summary>
/// Types of triggers for agents.
/// </summary>
public enum TriggerType
{
    None,
    IcmIncident,
    PagerDutyIncident,
    ServiceNowIncident,
    ManualInvocation,
    ScheduledTask,
    Webhook
}

/// <summary>
/// Types of agents.
/// </summary>
public enum AgentType
{
    Autonomous,
    Orchestrator,
    Activity
}

/// <summary>
/// Build status of an agent.
/// </summary>
public enum BuildStatus
{
    Draft,
    InProgress,
    ReadyForValidation,
    Validated,
    Applied,
    Failed
}

/// <summary>
/// A conversation history entry.
/// </summary>
public class ConversationEntry
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Context for building a subagent (programmatic C# agent).
/// Subagents are specialized agents that extend the SubAgent base class.
/// </summary>
public class SubagentBuildingContext
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Namespace { get; set; } = "Agent.Runtime.SubAgents";

    // Tool methods to generate
    public List<SubagentToolMethod> ToolMethods { get; set; } = new();

    // Dependencies to inject
    public List<SubagentDependency> Dependencies { get; set; } = new();

    // Whether this subagent delegates to other agents
    public List<string> DelegatesToAgents { get; set; } = new();

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Notes { get; set; } = new();
    public BuildStatus Status { get; set; } = BuildStatus.Draft;

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// A tool method for a subagent.
/// </summary>
public class SubagentToolMethod
{
    public string Name { get; set; } = string.Empty;
    public string KernelFunctionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SubagentToolParameter> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = "Task<string>";
    public bool IsAsync { get; set; } = true;
    public string? Implementation { get; set; }
}

/// <summary>
/// A parameter for a subagent tool method.
/// </summary>
public class SubagentToolParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? DefaultValue { get; set; }
}

/// <summary>
/// A dependency for a subagent.
/// </summary>
public class SubagentDependency
{
    public string InterfaceType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// A knowledge entry in the session knowledge base.
/// </summary>
public class KnowledgeEntry
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Serializable state for persistence.
/// </summary>
public class PersistedSessionState
{
    public Dictionary<string, AgentBuildingContext> AgentContexts { get; set; } = new();
    public Dictionary<string, SubagentBuildingContext> SubagentContexts { get; set; } = new();
    public List<ConversationEntry> ConversationHistory { get; set; } = new();
    public Dictionary<string, string> UserPreferences { get; set; } = new();
    public List<string> RecentActions { get; set; } = new();
    public List<KnowledgeEntry> KnowledgeBase { get; set; } = new();
    public DateTime LastSavedAt { get; set; }
}
