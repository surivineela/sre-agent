
namespace FirstPartyAgent.Core.Configuration;
public class HandoffToAgentSettings
{
    public bool Enabled { get; set; }

    public Dictionary<string, HandoffAgentConfig> ConfiguredAgents { get; set; } = new Dictionary<string, HandoffAgentConfig>(StringComparer.OrdinalIgnoreCase);

    public HandoffToAgentSettings()
    {

    }
}

public class HandoffAgentConfig
{
    public string Endpoint { get; set; } = string.Empty;

    public string AppKey { get; set; } = string.Empty;

    public bool IsDisabled { get; set; } = false;

    public HandoffAgentConfig()
    {
    }
}
