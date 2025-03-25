using Agent.Core.Interfaces;
using Azure.ResourceManager;

namespace Agent.Core.Services;

public class ArmClientFactory : IArmClientFactory
{
    private readonly IAuthenticationService _authService;

    private Lazy<ArmClient> _armClient;
    private Lazy<ArmClient> _crawlerClient;

    public ArmClientFactory(IAuthenticationService authService)
    {
        _authService = authService;

        _armClient = new Lazy<ArmClient>(() => new ArmClient(_authService.GetArmOperationCredential()));
        _crawlerClient = new Lazy<ArmClient>(() => new ArmClient(_authService.GetCrawlerCredential()));
    }

    public ArmClient GetArmClient()
    {
        return _armClient.Value;
    }

    public ArmClient GetCrawlerArmClient()
    {
        return _crawlerClient.Value;
    }
}
