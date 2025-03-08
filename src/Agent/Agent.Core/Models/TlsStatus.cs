// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public sealed record TlsStatus(
    string ResourceId,
    string Name,
    string Location,
    string MinimumTlsVersion);
