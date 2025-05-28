// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public interface IPromptDescriptor
{
    string Name { get; }
    string Prompt { get; }
}
