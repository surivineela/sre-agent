// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;
public class ObserverClientSettings
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
}
