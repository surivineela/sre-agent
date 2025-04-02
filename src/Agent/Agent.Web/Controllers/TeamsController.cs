using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Agent.Core.Configuration;

namespace Agent.Web.Controllers;

// Added group name for conditional mapping
[ApiController]
[Route("api/messages")]
[ApiExplorerSettings(GroupName = "TeamsBot")]
public class BotController : ControllerBase
{
    private readonly IBotFrameworkHttpAdapter Adapter;
    private readonly IBot Bot;
    private readonly TeamsBotSettings _setting;

    public BotController(IBotFrameworkHttpAdapter adapter, IBot bot, TeamsBotSettings setting)
    {
        Adapter = adapter;
        Bot = bot;
        _setting = setting;
    }

    [HttpPost, HttpGet]
    public async Task PostAsync()
    {
        // First verify Teams configuration is valid
        if (string.IsNullOrEmpty(_setting.AppId) || string.Equals(_setting.AppId, "dummy", StringComparison.OrdinalIgnoreCase))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsync("Teams bot functionality is not configured properly");
            return;
        }

        // Delegate the processing of the HTTP request to the adapter.
        // The adapter will invoke the bot.
        await Adapter.ProcessAsync(Request, Response, Bot);
    }
}
