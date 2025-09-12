// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public abstract class AsyncInitializerBase : IAsyncInitializer
{
    private readonly Lazy<Task> _initTask;

    protected AsyncInitializerBase()
    {
        _initTask = new Lazy<Task>(InitializeAsyncCore);
    }

    protected abstract Task InitializeAsyncCore();

    public Task InitializeAsync() => _initTask.Value;
}


