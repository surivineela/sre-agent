using System.Security.Cryptography.X509Certificates;
using Azure.Core;

namespace Agent.Core.Interfaces;
public interface IIcmTokenAuthService
{
    ValueTask<AccessToken> GetAccessTokenAsync();
}

public interface IIcmCertAuthService
{
    X509Certificate2 GetClientCertificate();
}
