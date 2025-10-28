using Microsoft.AspNetCore.Mvc;

namespace Agent.Portal.Controllers;

[ApiController]
[Route("[controller]")]
public class LogController : ControllerBase
{
    private readonly ILogger<LogController> _logger;

    public LogController(ILogger<LogController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Log([FromBody] LogRequest? request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        var eventId = new EventId(request.EventId ?? 0, request.EventName);
        
        _logger.Log(
            request.LogLevel,
            eventId,
            new Exception(request.Exception),
            request.Message,
            request.Args ?? Array.Empty<object>()
        );
        return Ok();
    }
}

public class LogRequest
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public int? EventId { get; set; }
    public String? EventName { get; set; }
    public String? Exception { get; set; }
    public String Message { get; set; } = String.Empty;
    public Object[]? Args { get; set; }
}
