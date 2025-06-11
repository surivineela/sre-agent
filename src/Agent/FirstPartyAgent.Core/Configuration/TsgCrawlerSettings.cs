using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using FirstPartyAgent.Core.Services;

namespace FirstPartyAgent.Core.Configuration;
public class TsgCrawlerSettings
{
    public bool Enabled { get; set; } = false;
    public string Type { get; set; } = string.Empty;
    public string IndexerName { get; set; } = string.Empty;
    public string TsgRootPath { get; set; } = string.Empty;
    public AzureDevOpsSettings DevOpsRepoSettings { get; set; } = new();
    public AzureSearchSettings AiSearchSettings { get; set; } = new();
    public StorageAccountSettings TsgStorageSettings { get; set; } = new();
}
