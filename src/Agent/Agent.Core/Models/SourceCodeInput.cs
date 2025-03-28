using System.ComponentModel;

namespace Agent.Core.Models;

public class SourceCodeInput
{
    [Description("Apps without source nodes")]
    public List<SourceCodeStatus> AppsWithoutSourceCodeNodes { get; set; }
}
