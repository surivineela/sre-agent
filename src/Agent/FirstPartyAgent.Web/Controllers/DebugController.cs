using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace FirstPartyAgent.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DebugController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("config")]
        public IActionResult GetConfiguration()
        {
            var configDict = new Dictionary<string, object>();
            ProcessConfiguration(_configuration, configDict);
            return Ok(configDict);
        }

        private void ProcessConfiguration(IConfiguration configSection, Dictionary<string, object> parentDict)
        {
            foreach (var child in configSection.GetChildren())
            {
                if (child.Value != null)
                {
                    parentDict[child.Key] = child.Value;
                }
                else
                {
                    var nestedDict = new Dictionary<string, object>();
                    parentDict[child.Key] = nestedDict;
                    ProcessConfiguration(child, nestedDict);
                }
            }
        }
    }
}
