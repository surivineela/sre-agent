// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public interface IAsyncInitializer
{
    public Task InitializeAsync();
}

