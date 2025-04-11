using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;

namespace Agent.Plugins.Mocks;
public class MockContainerImagePullFailurePlugin : IContainerImagePullFailurePlugin
{
    public Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<ImagePullingResult> CheckImagePulling(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetImageReferenceFromResourceId(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNetworkSecurityRulesForResource(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<ImagePullingResult> IsACRImageManifestAccessibleAsync(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(string resourceId)
    {
        throw new NotImplementedException();
    }
}
