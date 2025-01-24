using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using OperationalAgentRuntime.Planner;
using Microsoft.Extensions.Logging;

namespace OperationalAgentRuntime.Functions;

public class PlannerFunction
{
    private readonly SkillsPlanner _planner;
    private readonly ILogger<PlannerFunction> _logger;

    public PlannerFunction(SkillsPlanner planner, ILogger<PlannerFunction> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    [Function("ExecutePlan")]
    public async Task<HttpResponseData> ExecutePlan(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<PlanRequest>(requestBody);

            if (string.IsNullOrEmpty(request?.UserInput))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("User input is required");
                return badResponse;
            }

            var result = await _planner.ExecuteRequestAsync(request.UserInput);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { result });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing plan");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error executing plan");
            return errorResponse;
        }
    }

    [Function("GetAvailableSkills")]
    public async Task<HttpResponseData> GetAvailableSkills(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var skills = _planner.GetAvailableSkills();
        await response.WriteStringAsync(skills);
        return response;
    }
}

public class PlanRequest
{
    public string UserInput { get; set; } = string.Empty;
}
