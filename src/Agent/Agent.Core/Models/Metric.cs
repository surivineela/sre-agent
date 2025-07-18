// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public class Metric

{
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public required string Aggregation { get; set; }
}
