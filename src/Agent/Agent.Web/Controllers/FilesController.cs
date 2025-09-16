// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers;

/// <summary>
/// Serves locally generated artifact files (currently PDF reports) created by the Code Interpreter plugin.
/// Files are read from the 'reports' directory under <see cref="AppContext.BaseDirectory"/>.
/// Both /api/files and legacy /api/reports routes are supported for backward compatibility.
/// </summary>
[ApiController]
[Route("api/files")]            // Preferred new route
[Route("api/reports")]          // Legacy route (do not remove until callers migrated)
public class FilesController : ControllerBase
{
    private static readonly string ReportsRoot = Path.Combine(AppContext.BaseDirectory, "reports");

    /// <summary>
    /// Download a PDF artifact by filename.
    /// </summary>
    [HttpGet("{fileName}")]
    [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)] // reuse read action for artifact retrieval
    public IActionResult Get(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return BadRequest("fileName required");
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return BadRequest("invalid file name");
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return BadRequest("only .pdf artifacts allowed");
        if (fileName.Contains("..")) return BadRequest("invalid path");

        var fullPath = Path.Combine(ReportsRoot, fileName);
        if (!System.IO.File.Exists(fullPath)) return NotFound();

    var stream = System.IO.File.OpenRead(fullPath);
    return File(stream, "application/pdf", fileName);
    }
}
