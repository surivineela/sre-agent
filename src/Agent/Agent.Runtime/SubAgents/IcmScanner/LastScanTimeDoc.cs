using Agent.Data;
using Agent.Data.DataModels;

namespace Agent.Runtime.SubAgents.IcmScanner;
public class LastScanTimeDoc : ICosmosDocument
{
    public const string LastScanTimeKey = "LastScanTimeIcm";
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string Id => LastScanTimeKey;

    public string DocumentType => LastScanTimeKey;

    public string PartitionKey => LastScanTimeKey;

    public DateTime LastScanTime { get; set; }
}
