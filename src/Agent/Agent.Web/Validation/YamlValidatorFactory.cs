// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Services;


public interface IYamlValidatorFactory
{
    IYamlValidator GetValidator(string apiVersion);
}

public class YamlValidatorFactory : IYamlValidatorFactory
{
    public IYamlValidator GetValidator(string apiVersion) => apiVersion switch
    {
        "agent.platform.ai/v1" => new YamlValidatorV1(),
        _ => throw new NotSupportedException($"Unsupported apiVersion: {apiVersion}")
    };
}
