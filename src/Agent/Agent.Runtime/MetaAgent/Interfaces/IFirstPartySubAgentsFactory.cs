// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;

namespace Agent.Runtime.MetaAgent.Interfaces;

// We are exposing this interface related to First party sub-agents so that Meta agent can use this from Dependency Injection.
// However actual implmentation of this interface will be in FirstPartyAgent.Core/Agents/FirstPartySubAgentsFactory.cs
// !! Clean implementation would be to expose generic interface and implement it in two different 3P and 1P agent factories !!
public interface IFirstPartySubAgentsFactory
{
    public bool IsFirstPartyAgent();
    public string GetSystemPrompt();
    public Assembly GetSubAgentsAssembly();
    public List<Type> GetRequiredPluginDefinitionTypes();
}
