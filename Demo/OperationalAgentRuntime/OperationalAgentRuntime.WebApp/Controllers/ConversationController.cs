using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.AspNetCore.Mvc;

namespace OperationalAgentRuntime.WebApp;

[ApiController]
[Route("[controller]")]
public class ConversationController : ControllerBase
{
    private readonly ILogger<ConversationController> _logger;

    public ConversationController(ILogger<ConversationController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public async Task<IReadOnlyList<string>> Post(Conversation conversation)
    {
        throw new NotImplementedException();
    }
}
