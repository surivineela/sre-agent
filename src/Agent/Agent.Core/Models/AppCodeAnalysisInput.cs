using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;
public class AppCodeAnalysisInput
{
    public required List<AppCodeDown> Apps { get; set; }

}

public sealed record AppCodeDown(string ResourceId);
