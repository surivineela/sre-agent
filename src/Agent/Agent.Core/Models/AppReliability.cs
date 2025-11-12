using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public sealed record AppReliability(
    string ResourceId,
    bool AlwaysOnEnabled,
    bool HealthCheckEnabled,
    bool AutoHealEnabled,
    int NumberOfWorkers);

