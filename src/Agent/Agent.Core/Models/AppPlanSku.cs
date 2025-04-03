// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public class AppPlanSku
{
    public string Name { get; set; }
    public string Tier { get; set; }
    public string Size { get; set; }
    public string Family { get; set; }
    public int Capacity { get; set; }
    public string Location { get; set; }
}
