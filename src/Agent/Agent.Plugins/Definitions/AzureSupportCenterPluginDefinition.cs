using Agent.Core.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins;
public class AzureSupportCenterPluginDefinition
{
    private readonly IAzureSupportCenterPlugin _supportCenterPlugin;

    public AzureSupportCenterPluginDefinition(IAzureSupportCenterPlugin supportCenterPlugin)
    {
        _supportCenterPlugin = supportCenterPlugin;
    }

    [KernelFunction("GetSupportProductsFromArm")]
    [Description("Retrieve a list of support products matching the given resource provider. May return more than one matching product. Disambiguate before using the results for further processing.")]
    public async Task<List<SupportProductFromArmModel>> GetSupportProductsFromArm(
        [Description("The resource ID of the azure resource to execute diagnostics against.")] string resourceId)
    {
        return await _supportCenterPlugin.GetSupportProductsFromArm(resourceId);
    }

    [KernelFunction("GetSupportProblemClassificationsForProduct")]
    [Description("Retrieve support problem classifications for a specific product.")]
    public async Task<List<SupportProblemClassificationModel>> GetSupportProblemClassificationsForProduct(Guid productId)
    {
        return await _supportCenterPlugin.GetSupportProblemClassificationsForProduct(productId);
    }

    [KernelFunction("GetAzureSupportCenterDiagnosticResultsForQuestion")]
    [Description("Retrieve diagnostic results for a specific question from Azure Support Center.")]
    public async Task<string> GetAzureSupportCenterDiagnosticResultsForQuestion(
        [Description("The resource ID of the azure resource to execute diagnostics against.")] string resourceId,
        [Description("Support product from arm that, post disambiguation, best matches the issue being investigated.")] SupportProductFromArmModel targetSupportProduct,
        [Description("Support problem classification that, post disambiguation, best matches the issue being investigated.")] SupportProblemClassificationModel targetSupportProblemClassification,
        [Description("Detailed description of the issue being investigated.")] string question)
    {
        string supportCenterDiagnosticResponse = string.Empty;
        try
        {
            supportCenterDiagnosticResponse = await _supportCenterPlugin.GetAzureSupportCenterDiagnosticResultsForQuestion(resourceId, targetSupportProduct, targetSupportProblemClassification, question);
        }
        catch (Exception ex)
        {
            // Handle exception
            supportCenterDiagnosticResponse = $"Error retrieving diagnostic results: {ex.Message}";
        }

        return supportCenterDiagnosticResponse;
    }
}
