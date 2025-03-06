using System.Collections.Generic;

namespace Agent.Core.Models;

public class TlsBestPracticesInput
{
    public string DesiredVersion { get; set; }

    public List<TlsStatus> AppsInViolation { get; set; }
}
