// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public sealed record StorageAccountStatus(
    string ResourceId,
    string Name,
    string Location,
    bool StorageKeyEnabled,
    bool PublicContainersEnabled
    );
