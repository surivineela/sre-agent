using Agent.Core.Models;

namespace Agent.Plugins;
public interface IAzureSupportCenterPlugin
{
    public Task<List<SupportProductFromArmModel>> GetSupportProductsFromArm(string resourceId);

    public Task<List<SupportProblemClassificationModel>> GetSupportProblemClassificationsForProduct(Guid productId);

    public Task<string> GetAzureSupportCenterDiagnosticResultsForQuestion(string resourceId, SupportProductFromArmModel targetSupportProduct, SupportProblemClassificationModel targetSupportProblemClassification, string question);
}
