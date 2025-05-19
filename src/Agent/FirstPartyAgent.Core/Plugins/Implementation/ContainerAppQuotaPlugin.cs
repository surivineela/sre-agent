// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Agent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;
using static FirstPartyAgent.Plugins.ContainerAppsPlugin;


namespace FirstPartyAgent.Core.Plugins.Implementation;
public class ContainerAppQuotaPlugin : IContainerAppQuotaPlugin
{
    private readonly ILogger<ContainerAppQuotaPlugin> _logger;
    private readonly ICMWorkflowClient _icmWorkflowClient;
    private readonly IKustoPluginChat _kustoPlugin;

    public ContainerAppQuotaPlugin(ILogger<ContainerAppQuotaPlugin> logger, ICMWorkflowClient icmWorkflowClient, IKustoPluginChat kustoPlugin)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _icmWorkflowClient = icmWorkflowClient ?? throw new ArgumentNullException(nameof(icmWorkflowClient));
        _kustoPlugin = kustoPlugin ?? throw new ArgumentNullException(nameof(kustoPlugin));
    }

    public async Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit)
    {
        return await _icmWorkflowClient.SetSubscriptionQuota(subscriptionId, region, quotaType, quotaLimit);
    }

    public async Task<string> SetEnvironmentQuota(string incidentId, string managedEnvironmentResourceUri, string region, string quotaType, string quotaLimit)
    {
        return await _icmWorkflowClient.SetEnvironmentQuota(incidentId, managedEnvironmentResourceUri, region, quotaType, quotaLimit);
    }

    public Task<string> GetEnvironmentQuotaOperationResult(string operationId, string region)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvironmentQuotaOperationResult", region,
        new Dictionary<string, string> {
            { "operationId", operationId }
        });
    }

    public async Task<string> ValidateQuotaRequest(
            [Description("The quota type of the quota request")] string quotaType,
            [Description("The subscription id of the quota request")] string subscriptionId,
            [Description("The region of the quota request")] string region,
            [Description("The target quota limit of the quota request")] string targetQuotaLimit,
            [Description("The managed environment resource uri")] string environmentResourceURL = "")
    {
        _logger.LogInformation($"ValidateQuotaRequest Started: quotaType={quotaType}, subscriptionId={subscriptionId}, region={region}, targetQuotaLimit={targetQuotaLimit}");
        var subscriptionDetails = await _icmWorkflowClient.GetSubscriptionDetail(subscriptionId);
        _logger.LogInformation($"ValidateQuotaRequest: subscriptionDetails={JsonSerializer.Serialize(subscriptionDetails)}");
        string offerType = subscriptionDetails?.OfferType;

        if (string.IsNullOrEmpty(offerType))
        {
            return JsonSerializer.Serialize(new
            {
                ApproveResult = ApprovalState.NotStarted.ToString(),
                OfferType = "Unknown",
                Reason = string.Format(MessageTemplates.SubscriptionInformationMissing, "offer type")
            });
        }

        if (string.Equals(quotaType, "ManagedEnvironmentConsumptionCores", StringComparison.OrdinalIgnoreCase)
            || string.Equals(quotaType, "ManagedEnvironmentGeneralPurposeCores", StringComparison.OrdinalIgnoreCase)
            || string.Equals(quotaType, "ManagedEnvironmentMemoryOptimizedCores", StringComparison.OrdinalIgnoreCase)
            )
        {
            if (string.IsNullOrEmpty(environmentResourceURL))
            {
                return JsonSerializer.Serialize(new
                {
                    ApproveResult = ApprovalState.NotStarted.ToString(),
                    OfferType = offerType,
                    Reason = string.Format(MessageTemplates.ManagedEnvResourceUriMissing, quotaType)
                });
            }
        }

        var validationResult = ValidateQuotaRule(targetQuotaLimit, quotaType, region.ToLowerInvariant(), offerType);

        string result = JsonSerializer.Serialize(new
        {
            ApproveResult = validationResult.approvalState.ToString(),
            OfferType = offerType,
            Reason = validationResult.reason
        });

        _logger.LogInformation($"ValidateQuotaRequest Completed: {result}");

        return result;
    }

    public static (ApprovalState approvalState, string reason) ValidateQuotaRule(string targetQuotaLimit, string quotaType, string region, string offerType)
    {
        if (string.IsNullOrEmpty(quotaType))
        {
            return (ApprovalState.NotStarted, string.Format(MessageTemplates.RequestInformationMissing, "quota type"));
        }

        if (string.IsNullOrEmpty(region))
        {
            return (ApprovalState.NotStarted, string.Format(MessageTemplates.RequestInformationMissing, "region"));
        }

        if (!int.TryParse(targetQuotaLimit, out int limit))
        {
            return (ApprovalState.NotStarted, string.Format(MessageTemplates.InvalidQuotaLimit));
        }

        if (!Enum.TryParse(quotaType, true, out QuotaType quotaTypeEnum))
        {
            return (ApprovalState.NotStarted, string.Format(MessageTemplates.InvalidQuotaType));
        }
        else if (!quotaType.Equals("SubscriptionNCA100Gpus", StringComparison.OrdinalIgnoreCase)
                 && !quotaType.Equals("SubscriptionConsumptionNCA100Gpus", StringComparison.OrdinalIgnoreCase)
                 && !quotaType.Equals("SubscriptionConsumptionT4Gpus", StringComparison.OrdinalIgnoreCase)
                 && !quotaType.Equals("ManagedEnvironmentCount", StringComparison.OrdinalIgnoreCase)
                 && !quotaType.Equals("ManagedEnvironmentConsumptionCores", StringComparison.OrdinalIgnoreCase)
                 && !quotaType.Equals("ManagedEnvironmentGeneralPurposeCores", StringComparison.OrdinalIgnoreCase)
                 && !quotaType.Equals("ManagedEnvironmentMemoryOptimizedCores", StringComparison.OrdinalIgnoreCase)
                 )
        {
            return (ApprovalState.NotSupported, string.Format(MessageTemplates.QuotaTypeNotSupported, quotaType));
        }

        bool isEA = offerType.Equals("EA", StringComparison.OrdinalIgnoreCase);
        bool isPayAsYouGo = offerType.Equals("CustomerLed", StringComparison.OrdinalIgnoreCase) || offerType.Equals("Consumption", StringComparison.OrdinalIgnoreCase);
        bool isInternal = offerType.Equals("Internal", StringComparison.OrdinalIgnoreCase);
        bool isFieldLed = offerType.Equals("FieldLed", StringComparison.OrdinalIgnoreCase);
        bool isPartnerLed = offerType.Equals("PartnerLed", StringComparison.OrdinalIgnoreCase);
        bool isAzureInOpen = offerType.Equals("Open", StringComparison.OrdinalIgnoreCase);
        bool isSponsored = offerType.Equals("Sponsored", StringComparison.OrdinalIgnoreCase);
        bool isFreeTrial = offerType.Contains("Benefit", StringComparison.OrdinalIgnoreCase);

        if (isFreeTrial)
        {
            return (ApprovalState.Rejected, $"Auto rejected quota request for {offerType} offer type.");
        }

        if (quotaTypeEnum.Equals(QuotaType.SubscriptionNCA100Gpus))
        {
            switch (region)
            {
                case "northeurope":
                    return (ApprovalState.NotStarted, MessageTemplates.NorthEuropeNotSupported);
                case "westus3":
                    if (isEA)
                    {
                        return limit <= 10
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionNCA100Gpus", offerType, "10"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionNCA100Gpus", offerType, "10"));
                    }
                    else if (isPayAsYouGo || isInternal)
                    {
                        return limit <= 5
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionNCA100Gpus", offerType, "5"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionNCA100Gpus", offerType, "5"));
                    }
                    else
                    {
                        return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionNCA100Gpus", offerType, limit.ToString()));
                    }
                default:
                    return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "SubscriptionNCA100Gpus", region, "westus3"));
            }
        }
        else if (quotaTypeEnum.Equals(QuotaType.SubscriptionConsumptionNCA100Gpus))
        {
            if (region.Equals("westus3"))
            {
                return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApproveDueToShortage, "SubscriptionConsumptionNCA100Gpus", "westus3"));
            }
            else if (region.Equals("swedencentral") || region.Equals("australiaeast"))
            {
                if (isEA)
                {
                    return limit <= 10
                        ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionNCA100Gpus", offerType, "10"))
                        : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionNCA100Gpus", offerType, "10"));
                }
                else if (isPayAsYouGo || isInternal)
                {
                    return limit <= 5
                        ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionNCA100Gpus", offerType, "5"))
                        : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionNCA100Gpus", offerType, "5"));
                }
                else
                {
                    return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionNCA100Gpus", offerType, limit.ToString()));
                }
            }
            else
            {
                return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "SubscriptionConsumptionNCA100Gpus", region, "westus3, australiaeast, or swedensentral"));
            }
        }
        else if (quotaTypeEnum.Equals(QuotaType.SubscriptionConsumptionT4Gpus))
        {
            if (region.Equals("westus3") || region.Equals("swedencentral") || region.Equals("australiaeast"))
            {
                if (isEA)
                {
                    return limit <= 40
                        ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionT4Gpus", offerType, "40"))
                        : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionT4Gpus", offerType, "40"));
                }
                else if (isPayAsYouGo || isInternal)
                {
                    return limit <= 20
                        ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionT4Gpus", offerType, "20"))
                        : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionT4Gpus", offerType, "20"));
                }
                else
                {
                    return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionT4Gpus", offerType, limit.ToString()));
                }
            }
            else
            {
                return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "SubscriptionConsumptionT4Gpus", region, "westus3, australiaeast, or swedensentral"));
            }
        }
        else if (quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentCount)
            || quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentConsumptionCores)
            || quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentGeneralPurposeCores)
            || quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentMemoryOptimizedCores)
            )
        {
            return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApproveForEnvCoreQuota, quotaType));
        }
        else
        {

            return (ApprovalState.NotStarted, string.Format(MessageTemplates.QuotaTypeNotSupported, quotaType.ToString()));
        }
    }

    public static class MessageTemplates
    {
        public const string RegionNotSupported = @"Quota type {0} is not supported in this region {1}. For QuotaType {0}, the valid region should be {2}. Ask the user to provide the correct region.";

        public const string QuotaTypeNotSupported = @"Quota type {0} is not supported. The valid quota types are SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ManagedEnvironmentCount, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores. Ask the user to provide the correct quota type.";

        public const string NorthEuropeNotSupported = "There is no SubscriptionNCA100Gpus quota available in NorthEurope. Please ask the user if the customer can use SubscriptionConsumptionNCA100Gpus/SubscriptionConsumptionT4Gpus in WestUS3/SwedenCentral/AustraliaEast or SubscriptionNCA100Gpus in WestUS3.";

        public const string AutoApproved = @"Auto approved {0} quota for {1} offer type with limit less than or equal to {2}.";

        public const string RequireManualApprove = @"Manual approval is required for {1} offer type to set {0} quota with a limit great than {2}.";

        public const string RequireManualApproveForEnvCoreQuota = @"Manual approval is always required for quota request of quota type: {0}.";

        public const string RequestInformationMissing = @"The request {0} is missing. Ask the user to provide the {0}.";

        public const string SubscriptionInformationMissing = @"Failed to fetch {0} for the given subscription id. Aks the user to provide the correct subscriptionId.";

        public const string ManagedEnvResourceUriMissing = @"The managed environment resource uri is required for {0} quota request. Ask the user to provide the missing managed environment resource uri.";

        public const string InvalidQuotaLimit = @"Invalid target limit number. Ask the user to provide a valid target limit.";

        public const string InvalidQuotaType = @"Invalid quota type. Ask the customer to provide one of the following quota type: SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ManagedEnvironmentCount, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores";

        public const string RequireManualApproveDueToShortage = @"Manual approval is required for {0} offer type in {1} region due to a capacity shortage.";
    }
}

