// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

// [MENDATORY]
public interface lHelloWorldPlugin
{
    Task<string> GetHelloWorldMessageAsync();
}
