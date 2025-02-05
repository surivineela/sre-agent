// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public class Metric

{
    public string Name { get; set; }
    public string Unit { get; set; }
    public string Aggregation { get; set; }
}
