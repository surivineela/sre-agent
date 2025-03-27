// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Octokit;
using System.Diagnostics;

namespace Agent.Web.Controllers;

[Route("api/")]
[ApiController]
public class ApiController : Controller
{
    private readonly ILogger<ApiController> _logger;
    private readonly IChatService _chatService;

    public ApiController(ILogger<ApiController> logger, IChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }

    [HttpPost("SendMessage")]
    public async Task<IActionResult> SendMessage([FromBody] MessageRequestBody request)
    {
        _logger.LogInformation($"Agent Invoked with message - {JsonConvert.SerializeObject(request)}");
        var response = await _chatService.ProcessMessageAsync(request);
        return Ok(response);
    }


}
