using Azure.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Implementation;

public class AzureSupportCenterPlugin : IAzureSupportCenterPlugin
{
    private readonly ILogger<AzureSupportCenterPlugin> _logger;
    private readonly ArmHelper _armHelper;
    private readonly AzureSupportCenterHelper _azureSupportCenterHelper;

    public AzureSupportCenterPlugin(ILogger<AzureSupportCenterPlugin> logger, ArmHelper armHelper, AzureSupportCenterHelper azureSupportCenterHelper)
    {
        _logger = logger;
        _armHelper = armHelper;
        _azureSupportCenterHelper = azureSupportCenterHelper;
    }

    public async Task<List<SupportProductFromArmModel>> GetSupportProductsFromArm(string resourceId)
    {
        if(string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
        }

        if (!_armHelper.IsWellFormattedResourceId(resourceId))
        {
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
            throw new ArgumentException("Resource ID is not well formatted.", nameof(resourceId));
        }

        return await _azureSupportCenterHelper.GetSupportProductsFromArm(resourceId);
    }

    public async Task<List<SupportProblemClassificationModel>> GetSupportProblemClassificationsForProduct(Guid productId)
    {
        return await _azureSupportCenterHelper.GetSupportProblemClassificationsForProduct(productId);
    }

    public async Task<string> GetAzureSupportCenterDiagnosticResultsForQuestion(string resourceId, SupportProductFromArmModel targetSupportProduct, SupportProblemClassificationModel targetSupportProblemClassification, string question)
    {
        if (string.IsNullOrEmpty(question))
        {
            throw new ArgumentException("Question cannot be null or empty.", nameof(question));
        }

        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
        }

        if (!_armHelper.IsWellFormattedResourceId(resourceId))
        {
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
            throw new ArgumentException("Resource ID is not well formatted.", nameof(resourceId));
        }

        if (targetSupportProduct == null || string.IsNullOrWhiteSpace(targetSupportProduct.name) || string.IsNullOrWhiteSpace(targetSupportProduct.properties.metadata.legacyId))
        {
            throw new ArgumentException("Support product detail must have a valid guid and the legacyId must be a positive number", nameof(SupportProductFromArmModel));
        }

        if (targetSupportProblemClassification == null || string.IsNullOrWhiteSpace(targetSupportProblemClassification.name) || string.IsNullOrWhiteSpace(targetSupportProblemClassification.properties.metadata.legacyId))
        {
            throw new ArgumentException("Support product detail must have a valid guid and the legacyId must be a positive number", nameof(SupportProductFromArmModel));
        }

        try
        {
            var apolloDiagnosticResult = await _azureSupportCenterHelper.GetDiagnosticResultsFromApollo(resourceId, targetSupportProduct, targetSupportProblemClassification, question);

            var sectionsDiagnostics = apolloDiagnosticResult?.Properties?.Sections?
            .SelectMany(section => section.ReplacementMaps.Diagnostics)?
            .Select(diagnostic => diagnostic)?
            .ToList() ?? new List<ApolloDiagnostic>();

            var directMappedDiagnostics = apolloDiagnosticResult?.Properties?.ReplacementMaps?.Diagnostics?.ToList() ?? new List<ApolloDiagnostic>();

            var diagnostics = directMappedDiagnostics?.Concat(sectionsDiagnostics).ToList() ?? new List<ApolloDiagnostic>();

            return JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while getting Azure Support Center diagnostic results.");
            return $"Error occurred while getting Azure Support Center diagnostic results: {ex.Message}";
        }        
    }
}
