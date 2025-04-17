namespace Agent.Core.Configuration
{
    public class FirstPartyConfiguration
    {
        public List<string> SubAgents { get; set; } = new();
        public KustoClusterConfiguration KustoClusterConfiguration { get; set; } = new();
    }
}
