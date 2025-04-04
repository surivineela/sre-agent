// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public class Metric

{
    public string Name { get; set; }
    public string Unit { get; set; }
    public string Aggregation { get; set; }
}
