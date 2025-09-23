using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Models;

public record ManagedConnector(
    string Id,
    string Name
);

public record ServiceProviderConnector(
    string Id,
    string Name
);

public record Workflow(
    string Id,
    string Name
);
