// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Runtime.Reasoning.Models;
using YamlDotNet.Serialization;

public interface IToolArgumentTransformer
{
    object?[] TransformArguments(MethodInfo method, Dictionary<string, object?> flatArgs, YamlToolDefinitionBase tool);
}
