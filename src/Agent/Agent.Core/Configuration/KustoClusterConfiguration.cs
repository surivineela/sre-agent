namespace Agent.Core.Configuration
{
    public class KustoClusterConfiguration
    {
        public string ClusterUri { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string Identity { get; set; } = string.Empty;
    }
}
