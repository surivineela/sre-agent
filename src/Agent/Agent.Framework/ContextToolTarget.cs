// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public abstract class ContextToolTarget<TContext> where TContext : class
{
    public TContext? Context { get; set; }
}
