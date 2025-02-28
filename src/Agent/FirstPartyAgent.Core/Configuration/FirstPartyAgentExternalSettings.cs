using Agent.Core.Configuration;

namespace FirstPartyAgent.Core.Configuration
{
    public class FirstPartyAgentExternalSettings : ExternalSettings
    {
        public ICMSettings ICM { get; set; } = new();
        public ICMAPISettings ICMAPI { get; set; } = new();
        public ICMWorkflowSettings ICMWorkflow { get; set; } = new();
        public KustoSettings Kusto { get; set; } = new();
    }
}
