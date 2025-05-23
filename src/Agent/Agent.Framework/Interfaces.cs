// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Interfaces;

public interface IAgentHooks
{
    Task OnStart();
    Task OnEnd(object result);
    Task OnError(System.Exception error);
}
