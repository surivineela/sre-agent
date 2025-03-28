using System.ComponentModel;

namespace Agent.Core.Models;

public class CVEInput
{
    [Description("Repos to scan")]
    public List<RepoUrlStatus> ReposToScan { get; set; }
}
