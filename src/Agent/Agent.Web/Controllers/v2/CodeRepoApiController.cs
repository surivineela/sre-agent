// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Runtime.Interfaces;
using Agent.Web.ApiResources;
using Agent.Web.Authorization;
using Agent.Web.Validation;
using Agent.Web.Views.v2;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v2;

/// <summary>
/// API controller for code repository operations.
/// </summary>
[ApiController]
[Route("api/v2/repos")]
public class CodeRepoApiController : ControllerBase
{
    private readonly ILogger<CodeRepoApiController> _logger;
    private readonly ICodeRepoService _codeRepoService;
    private readonly ICodeRepoValidator _validator;

    public CodeRepoApiController(
        ILogger<CodeRepoApiController> logger,
        ICodeRepoService codeRepoService,
        ICodeRepoValidator validator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _codeRepoService = codeRepoService ?? throw new ArgumentNullException(nameof(codeRepoService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Creates or updates a code repository.
    /// </summary>
    /// <param name="repoName">The repository name.</param>
    /// <param name="request">The repository request.</param>
    /// <returns>The created or updated repository.</returns>
    [HttpPut("{repoName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponseEnvelope<CodeRepoView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateCodeRepoAsync(
        string repoName,
        [FromBody] ApiRequestEnvelope<CodeRepoView> request)
    {
        if (request.Type != null && request.Type != CodeRepoDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (repoName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(repoName, request.Name));
        }

        var existingRepo = await _codeRepoService.GetCodeRepoAsync(repoName);
        var model = CodeRepoView.CreateModel(request, existingRepo?.Metadata, null);

        // Validate the model
        var validationResult = await _validator.ValidateCodeRepoAsync(model);
        if (!validationResult.IsValid)
        {
            _logger.LogInternalWarning("Validation failed for code repository: {RepoName}. Errors: {Errors}",
                repoName, string.Join(", ", validationResult.Errors));
            return BadRequest(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.ToString()));
        }

        var repo = await _codeRepoService.CreateOrUpdateCodeRepoAsync(repoName, model);

        var authConnector = _codeRepoService.FindAuthConnector(repo.Spec.Url, repo.Spec.Type);
        return Ok(CodeRepoView.CreateApiResponseEnvelope(repo, authConnector?.Name));
    }

    /// <summary>
    /// Gets a code repository by name.
    /// </summary>
    /// <param name="repoName">The repository name.</param>
    /// <returns>The repository.</returns>
    [HttpGet("{repoName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiResponseEnvelope<CodeRepoView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCodeRepoAsync(string repoName)
    {
        var repo = await _codeRepoService.GetCodeRepoAsync(repoName);
        if (repo == null)
        {
            return NotFound();
        }

        var authConnector = _codeRepoService.FindAuthConnector(repo.Spec.Url, repo.Spec.Type);
        return Ok(CodeRepoView.CreateApiResponseEnvelope(repo, authConnector?.Name));
    }

    /// <summary>
    /// Gets all code repositories.
    /// </summary>
    /// <returns>Collection of all repositories.</returns>
    [HttpGet]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiCollectionEnvelope<CodeRepoView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListCodeReposAsync()
    {
        var repos = await _codeRepoService.GetCodeReposAsync();

        var responseEnvelope = new ApiCollectionEnvelope<CodeRepoView>
        {
            Value = [.. repos.Select(repo =>
            {
                var authConnector = _codeRepoService.FindAuthConnector(repo.Spec.Url, repo.Spec.Type);
                return CodeRepoView.CreateApiResponseEnvelope(repo, authConnector?.Name);
            })]
        };
        return Ok(responseEnvelope);
    }

    /// <summary>
    /// Deletes a code repository.
    /// </summary>
    /// <param name="repoName">The repository name.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{repoName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteCodeRepoAsync(string repoName)
    {
        var repo = await _codeRepoService.DeleteCodeRepoAsync(repoName);
        if (repo == null)
        {
            return NotFound();
        }

        return NoContent();
    }
}
