// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public class TlsStatus
{

    public string ResourceId { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
    public string MinimumTlsVersion { get; set; }
}
