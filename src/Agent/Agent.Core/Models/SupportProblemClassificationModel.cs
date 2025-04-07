
namespace Agent.Core.Models;
public sealed record SupportProblemClassificationModel(
    string id,
    string name,
    SupportProblemClassificationPropertiesModel properties
    );

public sealed record SupportProblemClassificationPropertiesModel(
    string displayName,
    List<SupportProblemSecondaryConsentModel> secondaryConsentEnabled,
    SupportProblemClassificationMetadataModel metadata
    );

public sealed record SupportProblemSecondaryConsentModel(
    string description,
    string type
    );

public sealed record SupportProblemClassificationMetadataModel(
    string shortDescription,
    string diagnosticid,
    string category,
    string searchTags,
    string state,
    string azureSubscriptionRequired,
    string legacyId
    );
