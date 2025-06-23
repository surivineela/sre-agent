using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Framework;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppQuotaPluginDefinition
    {
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppQuotaPluginDefinition(ICMWorkflowClient icmWorkflowClient, IKustoPluginChat kustoPlugin)
        {
            _icmWorkflowClient = icmWorkflowClient;
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"Get Subscription details, including BillingType, OfferType, OfferName, QuotaId, OrganizationName, etc.")]
        public async Task<SubscriptionDetail?> GetSubscriptionDetail([Description("The subscription Id")] string subscriptionId)
        {
               return await _icmWorkflowClient.GetSubscriptionDetail(subscriptionId);
        }

        [Description(@"Get Subscription Usage details, including the NumberOfEnvironments, NumberOfContainerApps, NumberOfJobs, TrustLevel of the subscription.")]
        public async Task<AcaSubscriptionUsage?> GetSubscriptionUsage([Description("The subscription Id")] string subscriptionId)
        {
            return await _icmWorkflowClient.GetSubscriptionUsage(subscriptionId);
        }

        [Description(@"Get Subscription Quota limit.
        Input parameters:
        - subscriptionId: The subscription Id.
        - region: The region of the quota need to be retrieved.
        - quotaType: The quota type.
        The return value is a string containing the quota limit value for the specified subscription, region, and quota type.
        ")]
        public async Task<string> GetSubscriptionQuota(
            [Description("The subscription Id")] string subscriptionId,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType)
        {
            return await _icmWorkflowClient.GetSubscriptionQuota(subscriptionId, region, quotaType);
        }

        [Description(@"Set Subscription Quota limit.
        Input parameters:
        - subscriptionId: The subscription Id.
        - region: The region of the quota need to be set.
        - quotaType: The quota type. 
        The return value is a boolean value for indicating if the operation is successful.
        ")]
        public async Task<string> SetSubscriptionQuota(
            [Description("The subscription Id")] string subscriptionId,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType,
            [Description("The target quota limit")] string quotaLimit)
        {
            return await _icmWorkflowClient.SetSubscriptionQuota(subscriptionId, region, quotaType, quotaLimit);
        }


        [Description(@"Get Container App Environment Quota limit.
        Input parameters:
        - environmentResourceURL: The resource url of the container app environment. Format `/subscriptions/[SubscriptionId]/resourceGroups/[resource group name]/providers/Microsoft.App/managedEnvironments/[environment name]`
        - region: The region of the quota need to be set. example eastus
        - quotaType: The quota type. example ManagedEnvironmentConsumptionCores
        The return value is a string containing the quota limit value for the specified environment, region, and quota type.
        ")]
        public async Task<string> GetEnvironmentQuota(
            [Description("The resource URL of the container app environment")] string environmentResourceURL,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType)
        {
            return await _icmWorkflowClient.GetEnvironmentQuota(environmentResourceURL, region, quotaType);
        }

        [Description(@"Set Managed Environment Quota limit.
Input parameters:
- incidentId: The incident id.
- environmentResourceURL: The resource url of the container app environment.
- region: The region of the quota need to be set.
- quotaType: The quota type.
- quotaLimit: The target quota limit.

Output:
- id: The trace id of te operation, which can be used to track the operation in the kusto table ContainerAppsAdminEvents.
- message: Describes the set Managed Environment Quota limit operation result.
")]
        public async Task<string> SetEnvironmentQuota(
    [Description("The incident id")] string incidentId,
    [Description("The resource URL of the container app environment")] string environmentResourceURL,
    [Description("The region")] string region,
    [Description("The quota type")] string quotaType,
    [Description("The target quota limit")] string quotaLimit)
        {
            return await _icmWorkflowClient.SetEnvironmentQuota(incidentId, environmentResourceURL, region, quotaType, quotaLimit);
        }

        [Description(@"Get the operation result of setting Managed Environment Quota limit.
Input parameters:
- operationId: The trace id of the operation, which can be used to track the operation in the kusto table ContainerAppsAdminEvents.
- region: The region of the quota need to be set.

Output:
- PreciseTimeStamp: the time when the operation is completed.
- operationStatus: the status of the operation result.
- message: Describes the set Managed Environment Quota limit operation result.
")]
        public Task<string> GetEnvironmentQuotaOperationResult(
            [Description("The operation id")] string operationId,
            [Description("The region")] string region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvironmentQuotaOperationResult", region,
            new Dictionary<string, string> {
                { "operationId", operationId }
            });
        }

        [Description(@"validate quota request
This function evaluates a quota request based on specified parameters, including quota type, region, target limit, and subscription id.
This operation determines whether the quota request adheres to approval rules and returns a validation result.
Output:
1. ApprovalResult: The status of the quota request, which can be one of the following:
   - Approved: The request has been successfully approved.
   - Rejected: The request has been denied.
   - Pending: Additional manual approval is required.
   - NotStarted: The request is incomplete and requires more details.
2. OfferType: The offer type of the subscription.
3. Reason: Provides an explanation for the validation decision.

This function helps ensure quota requests comply with predefined rules and provides a clear decision with supporting context.
")]
        public async Task<string> ValidateQuotaRequest(
            [Description("(Required) The quota type of the quota request")] string quotaType,
            [Description("(Required) The subscription id of the quota request")] string subscriptionId,
            [Description("(Required) The Azure region of the quota request")] string region,
            [Description("(Required) The target quota limit of the quota request")] string targetQuotaLimit,
            [Description("(Optional) The managed environment resource uri")] string environmentResourceURL = "")
        {
            var subscriptionDetails = await _icmWorkflowClient.GetSubscriptionDetail(subscriptionId);

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

            return result;
        }

        private static (ApprovalState approvalState, string reason) ValidateQuotaRule(string targetQuotaLimit, string quotaType, string region, string offerType)
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
                     && !quotaType.Equals("ContainerAppAdditionalPorts", StringComparison.OrdinalIgnoreCase))
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
                || quotaTypeEnum.Equals(QuotaType.ContainerAppAdditionalPorts)
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

            public const string QuotaTypeNotSupported = @"Quota type {0} is not supported. The valid quota types are SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ManagedEnvironmentCount, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores, ContainerAppAdditionalPorts. Ask the user to provide the correct quota type.";

            public const string NorthEuropeNotSupported = "There is no SubscriptionNCA100Gpus quota available in NorthEurope. Please ask the user if the customer can use SubscriptionConsumptionNCA100Gpus/SubscriptionConsumptionT4Gpus in WestUS3/SwedenCentral/AustraliaEast or SubscriptionNCA100Gpus in WestUS3.";

            public const string AutoApproved = @"Auto approved {0} quota for {1} offer type with limit less than or equal to {2}.";

            public const string RequireManualApprove = @"Manual approval is required for {1} offer type to set {0} quota with a limit great than {2}.";

            public const string RequireManualApproveForEnvCoreQuota = @"Manual approval is always required for quota request of quota type: {0}.";

            public const string RequestInformationMissing = @"The request {0} is missing. Ask the user to provide the {0}.";

            public const string SubscriptionInformationMissing = @"Failed to fetch {0} for the given subscription id. Aks the user to provide the correct subscriptionId.";

            public const string ManagedEnvResourceUriMissing = @"The managed environment resource uri is required for {0} quota request. Ask the user to provide the missing managed environment resource uri.";

            public const string InvalidQuotaLimit = @"Invalid target limit number. Ask the user to provide a valid target limit.";

            public const string InvalidQuotaType = @"Invalid quota type. Ask the customer to provide one of the following quota type: SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ManagedEnvironmentCount, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores, ContainerAppAdditionalPorts";

            public const string RequireManualApproveDueToShortage = @"Manual approval is required for {0} offer type in {1} region due to a capacity shortage.";
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ApprovalState>))]
    internal enum ApprovalState
    {
        NotStarted,
        Pending,
        Approved,
        Rejected,
        NotSupported,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<QuotaType>))]
    internal enum QuotaType
    {
        /// <summary>
        /// Gpus quota for NCA100 workload profiles in subscription
        /// </summary>
        SubscriptionNCA100Gpus,

        /// <summary>
        /// Quota for consumption GPUs for NCA100 VMs per subscription
        /// </summary>
        SubscriptionConsumptionNCA100Gpus,

        /// <summary>
        /// Quota for consumption GPUs for T4 VMs per subscription
        /// </summary>
        SubscriptionConsumptionT4Gpus,

        /// <summary>
        /// Quota for additional ports per subscription
        /// </summary>
        ContainerAppAdditionalPorts,

        /// <summary>
        /// Quota for managed environment consumption cores
        /// </summary>
        ManagedEnvironmentConsumptionCores,

        /// <summary>
        /// Quota for managed environment general purpose cores
        /// </summary>
        ManagedEnvironmentGeneralPurposeCores,

        /// <summary>
        /// Quota for managed environment memory optimized cores
        /// </summary>
        ManagedEnvironmentMemoryOptimizedCores,

        /// <summary>
        /// Quota for managed environment compute optimized cores
        /// </summary>
        ManagedEnvironmentComputeOptimizedCores,

        /// <summary>
        /// Quota for managed environment count
        /// </summary>
        ManagedEnvironmentCount,

        /// <summary>
        /// Quota for Session Pools
        /// </summary>
        SessionPools
    }
}
