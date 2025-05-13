// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IHelloWorldPlugin
{
    Task<string> GetHelloWorldMessageAsync();
}
