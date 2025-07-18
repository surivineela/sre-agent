// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public class AppPlanSku
{
    public required string Name { get; set; }
    public required string Tier { get; set; }
    public required string Size { get; set; }
    public required string Family { get; set; }
    public required int Capacity { get; set; }
    public required string Location { get; set; }
}
