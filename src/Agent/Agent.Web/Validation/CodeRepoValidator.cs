// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Validation;
using Agent.Data.DataModels;
using Agent.Data.Repositories;

namespace Agent.Web.Validation;

/// <summary>
/// Validator for code repository configurations.
/// </summary>
public partial class CodeRepoValidator : ICodeRepoValidator
{
    private readonly ILogger<CodeRepoValidator> _logger;
    private readonly ICodeRepoRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;

    // Regex pattern for validating resource names: alphanumeric, hyphens, underscores, dots; 1-128 chars
    [GeneratedRegex(@"^[a-zA-Z0-9_\.-]{1,128}$")]
    private static partial Regex NameValidationRegex();

    public CodeRepoValidator(
        ILogger<CodeRepoValidator> logger,
        ICodeRepoRepository repository,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<ApiValidationResult> ValidateCodeRepoAsync(CodeRepoDocumentModel model)
    {
        _logger.LogDebug("Validating CodeRepoDocumentModel: {RepoName}", model.Name);

        var result = new ApiValidationResult();

        // Validate resource metadata
        ValidateResourceMetadata(model.Metadata, result);
        if (!result.IsValid)
        {
            return result;
        }

        // Validate and normalize URL
        try
        {
            var normalizedUrl = CodeRepoUrlHelper.NormalizeRepoUrl(model.Spec.Url);
            model.Spec.Url = normalizedUrl;
            _logger.LogDebug("Normalized repository URL to: {NormalizedUrl}", normalizedUrl);
        }
        catch (ArgumentException ex)
        {
            result.AddError($"Invalid repository URL: {ex.Message}");
            return result;
        }

        // Auto-detect repository type if not explicitly set or if set to default
        var detectedType = RepoTypeHelper.DetectRepoType(model.Spec.Url);
        if (model.Spec.Type != detectedType)
        {
            _logger.LogDebug("Auto-detected repository type as {DetectedType} for URL {Url}", detectedType, model.Spec.Url);
            model.Spec.Type = detectedType;
        }

        // Check URL uniqueness (normalized URL must be unique)
        await CheckUrlUniqueness(model, result);

        return result;
    }

    private void ValidateResourceMetadata(ResourceMetadata metadata, ApiValidationResult result)
    {
        if (string.IsNullOrEmpty(metadata.Name))
        {
            result.AddError("Repository name is required.");
            return;
        }

        if (!NameValidationRegex().IsMatch(metadata.Name))
        {
            result.AddError($"Repository name '{metadata.Name}' is invalid. Name must be 1-128 characters and contain only alphanumeric characters, hyphens, underscores, or dots.");
        }
    }

    private async Task CheckUrlUniqueness(CodeRepoDocumentModel model, ApiValidationResult result)
    {
        try
        {
            var existingRepo = await _repository.GetCodeRepoByUrlAsync(model.Spec.Url);
            if (existingRepo != null && !existingRepo.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError($"A repository with URL '{model.Spec.Url}' already exists with name '{existingRepo.Name}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to check URL uniqueness for {Url}", model.Spec.Url);
            result.AddWarning("Could not verify URL uniqueness due to a database error.");
        }
    }
}
