using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;
public class WebAppDownInput
{
    public required List<DownApp> Apps { get; set; }
}

public sealed record DownApp(string ResourceId);
