// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Services;


public interface IAgentYamlTranslatorFactory
{
    IAgentYamlTranslator GetTranslator(string apiVersion);
}

