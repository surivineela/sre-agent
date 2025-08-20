using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Framework;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppQuotaPluginDefinition
    {
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppQuotaPluginDefinition(ICMWorkflowClient icmWorkflowClient, IKustoPlugin kustoPlugin)
        {
            _icmWorkflowClient = icmWorkflowClient;
            _kustoPlugin = kustoPlugin;
        }

                [Description("""
Purpose:
Retrieves subscription details including billing and organization information.

Scenario:
Use this tool to get detailed information about a subscription.

Output:
Returns a JSON object with subscription details:
- BillingType: Type of billing for the subscription
- OfferType: Offer type of the subscription
- OfferName: Name of the offer
- QuotaId: Quota identifier
- OrganizationName: Name of the organization
- Other subscription properties as available
"""
)]
        public async Task<SubscriptionDetail?> GetSubscriptionDetail(
            [Description("Subscription ID.")] string subscriptionId)
        {
            return await _icmWorkflowClient.GetSubscriptionDetail(subscriptionId);
        }

                [Description("""
Purpose:
Retrieves usage details for a subscription, including environment and container app counts.

Scenario:
Use this tool to get usage statistics for a subscription.

Output:
Returns a JSON object with usage statistics:
- NumberOfEnvironments: Number of environments in the subscription
- NumberOfContainerApps: Number of container apps
- NumberOfJobs: Number of jobs
- TrustLevel: Trust level of the subscription
"""
)]
        public async Task<AcaSubscriptionUsage?> GetSubscriptionUsage(
            [Description("Subscription ID.")] string subscriptionId)
        {
            return await _icmWorkflowClient.GetSubscriptionUsage(subscriptionId);
        }

                [Description("""
Purpose:
Retrieves the quota limit for a subscription in a specific region and quota type.

Scenario:
Use this tool to get the current quota limit for a subscription.

Output:
Returns a string containing the quota limit value:
- QuotaLimit: The quota limit value as a string
"""
)]
        public async Task<string> GetSubscriptionQuota(
            [Description("Subscription ID.")] string subscriptionId,
            [Description("Azure region.")] AzureRegion region,
            [Description("Quota type.")] string quotaType)
        {
            return await _icmWorkflowClient.GetSubscriptionQuota(subscriptionId, region, quotaType);
        }

                [Description("""
Purpose:
Sets the quota limit for a subscription in a specific region and quota type.

Scenario:
Use this tool to update the quota limit for a subscription.

Output:
Returns a string indicating if the operation was successful:
- Success: String indicating success or failure of the operation
"""
)]
        public async Task<string> SetSubscriptionQuota(
            [Description("Subscription ID.")] string subscriptionId,
            [Description("Azure region.")] AzureRegion region,
            [Description("Quota type.")] string quotaType,
            [Description("Target quota limit.")] string quotaLimit)
        {
            return await _icmWorkflowClient.SetSubscriptionQuota(subscriptionId, region, quotaType, quotaLimit);
        }

                [Description("""
Purpose:
Retrieves the quota limit for a container app environment in a specific region and quota type.

Scenario:
Use this tool to get the current quota limit for a container app environment.

Output:
Returns a string containing the quota limit value:
- QuotaLimit: The quota limit value as a string
"""
)]
        public async Task<string> GetEnvironmentQuota(
            [Description("Resource URL of the container app environment.")] string environmentResourceURL,
            [Description("Azure region.")] AzureRegion region,
            [Description("Quota type.")] string quotaType)
        {
            return await _icmWorkflowClient.GetEnvironmentQuota(environmentResourceURL, region, quotaType);
        }

                [Description("""
Purpose:
Sets the quota limit for a managed environment.

Scenario:
Use this tool to update the quota limit for a container app environment.

Output:
Returns a JSON object with operation details:
- id: Trace ID of the operation
- message: Description of the operation result
"""
)]
        public async Task<string> SetEnvironmentQuota(
            [Description("Incident ID.")] string incidentId,
            [Description("Resource URL of the container app environment.")] string environmentResourceURL,
            [Description("Azure region.")] AzureRegion region,
            [Description("Quota type.")] string quotaType,
            [Description("Target quota limit.")] string quotaLimit)
        {
            return await _icmWorkflowClient.SetEnvironmentQuota(incidentId, environmentResourceURL, region, quotaType, quotaLimit);
        }

        [Description("""
Purpose:
Gets the NCA100 and T4 GPU quota and usage percentage for a specific region.

Scenario:
Use this tool to get the current quota and usage percentage for consumpation NCA100 and T4 GPU in a specific region. It can help the user to decide whether to approve or reject a quota request based on the current usage and quota limits in that region.

Output:
Returns tab-separated table data in CSV format. Column headers:
- Region: The Azure region 
- Regional_ConsumptionNCA100Gpus_Quota: The total available quota for NCA100 GPUs in the region
- Regional_ConsumptionNCA100Gpus_Used_Percentage: The used percentage of NCA100 GPUs in the region
- Regional_ConsumptionT4Gpus_Quota: The total available quota for T4 GPUs in the region
- Regional_ConsumptionT4Gpus_Used_Percentage: The used percentage of T4 GPUs in the region
"""
)]
        public Task<string> GetRegionalConsumptionGpuQuotaAndUsagePercentage()
        { 
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetRegionalConsumptionGpuQuotaAndUsage",
                "legioneus.eastus", "legion",
                new Dictionary<string, string> {
                });
        }

                [Description("""
Purpose:
Retrieves the result of a set quota operation for a managed environment.

Scenario:
Use this tool to check the status and result of a quota update operation for quota type ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, or ManagedEnvironmentMemoryOptimizedCores

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time when the operation was completed
- operationStatus: Status of the operation result
- message: Description of the operation result
"""
)]
        public Task<string> GetEnvironmentQuotaOperationResult(
            [Description("Operation ID (trace ID).")] string operationId,
            [Description("Azure region.")] AzureRegion region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvironmentQuotaOperationResult", region,
            new Dictionary<string, string> {
                { "operationId", operationId }
            });
        }

                [Description("""
Purpose:
Validates a quota request based on quota type, region, target limit, and subscription.

Scenario:
Use this tool to check if a quota request meets approval rules and get the validation result.

Output:
Returns a JSON object with validation details:
- ApproveResult: Status of the quota request (Approved, Rejected, Pending, NotStarted)
- OfferType: Offer type of the subscription
- Reason: Explanation for the validation decision
"""
)]
        public async Task<string> ValidateQuotaRequest(
            [Description("Quota type.")] string quotaType,
            [Description("Subscription ID.")] string subscriptionId,
            [Description("Azure region.")] AzureRegion region,
            [Description("Target quota limit.")] string targetQuotaLimit,
            [Description("Managed environment resource URI (optional).")] string environmentResourceURL = "")
        {
            var subscriptionDetails = await _icmWorkflowClient.GetSubscriptionDetail(subscriptionId);

            string offerType = subscriptionDetails?.OfferType ?? string.Empty;

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
                || string.Equals(quotaType, "ManagedEnvironmentConsumptionNCA100Gpus", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quotaType, "ManagedEnvironmentConsumptionT4Gpus", StringComparison.OrdinalIgnoreCase)
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

            var validationResult = ValidateQuotaRule(targetQuotaLimit, quotaType, region, offerType);

            string result = JsonSerializer.Serialize(new
            {
                ApproveResult = validationResult.approvalState.ToString(),
                OfferType = offerType,
                Reason = validationResult.reason
            });

            return result;
        }

        private static (ApprovalState approvalState, string reason) ValidateQuotaRule(string targetQuotaLimit, string quotaType, AzureRegion region, string offerType)
        {
            if (string.IsNullOrEmpty(quotaType))
            {
                return (ApprovalState.NotStarted, string.Format(MessageTemplates.RequestInformationMissing, "quota type"));
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
                     && !quotaType.Equals("ManagedEnvironmentConsumptionNCA100Gpus", StringComparison.OrdinalIgnoreCase)
                     && !quotaType.Equals("ManagedEnvironmentConsumptionT4Gpus", StringComparison.OrdinalIgnoreCase)
                     && !quotaType.Equals("ManagedEnvironmentCount", StringComparison.OrdinalIgnoreCase)
                     && !quotaType.Equals("ManagedEnvironmentConsumptionCores", StringComparison.OrdinalIgnoreCase)
                     && !quotaType.Equals("ManagedEnvironmentGeneralPurposeCores", StringComparison.OrdinalIgnoreCase)
                     && !quotaType.Equals("ManagedEnvironmentMemoryOptimizedCores", StringComparison.OrdinalIgnoreCase)
                     && !quotaType.Equals("SessionPools", StringComparison.OrdinalIgnoreCase)
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
                    case AzureRegion.NorthEurope:
                        return (ApprovalState.NotStarted, MessageTemplates.NorthEuropeNotSupported);
                    case AzureRegion.WestUS3:
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
                        return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "SubscriptionNCA100Gpus", region.ToNormalizedString(), "westus3"));
                }
            }
            else if (quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentConsumptionNCA100Gpus))
            {
                if (region.Equals("westus3"))
                {
                    return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApproveDueToShortage, "ManagedEnvironmentConsumptionNCA100Gpus", "westus3"));
                }
                else if (region.Equals("swedencentral") || region.Equals("australiaeast") || region.Equals("westus"))
                {
                    if (isEA)
                    {
                        return limit <= 10
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "ManagedEnvironmentConsumptionNCA100Gpus", offerType, "10"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "ManagedEnvironmentConsumptionNCA100Gpus", offerType, "10"));
                    }
                    else if (isPayAsYouGo || isInternal)
                    {
                        return limit <= 5
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "ManagedEnvironmentConsumptionNCA100Gpus", offerType, "5"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "ManagedEnvironmentConsumptionNCA100Gpus", offerType, "5"));
                    }
                    else
                    {
                        return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "ManagedEnvironmentConsumptionNCA100Gpus", offerType, limit.ToString()));
                    }
                }
                else
                {
                    return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "ManagedEnvironmentConsumptionNCA100Gpus", region, "westus3, australiaeast, or swedensentral"));
                }
            }
            else if (quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentConsumptionT4Gpus))
            {
                if (region.Equals("westus3") || region.Equals("swedencentral") || region.Equals("australiaeast") || region.Equals("westeurope"))
                {
                    if (isEA)
                    {
                        return limit <= 40
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "ManagedEnvironmentConsumptionT4Gpus", offerType, "40"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "ManagedEnvironmentConsumptionT4Gpus", offerType, "40"));
                    }
                    else if (isPayAsYouGo || isInternal)
                    {
                        return limit <= 20
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "ManagedEnvironmentConsumptionT4Gpus", offerType, "20"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "ManagedEnvironmentConsumptionT4Gpus", offerType, "20"));
                    }
                    else
                    {
                        return (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "ManagedEnvironmentConsumptionT4Gpus", offerType, limit.ToString()));
                    }
                }
                else
                {
                    return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "ManagedEnvironmentConsumptionT4Gpus", region, "westus3, australiaeast, swedensentral, or westeurope"));
                }
            }
            else if (quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentCount)
                || quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentConsumptionCores)
                || quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentGeneralPurposeCores)
                || quotaTypeEnum.Equals(QuotaType.ManagedEnvironmentMemoryOptimizedCores)
                || quotaTypeEnum.Equals(QuotaType.ContainerAppAdditionalPorts)
                || quotaTypeEnum.Equals(QuotaType.SessionPools)
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

            public const string QuotaTypeNotSupported = @"Quota type {0} is not supported. The valid quota types are SubscriptionNCA100Gpus, ManagedEnvironmentConsumptionNCA100Gpus, ManagedEnvironmentConsumptionT4Gpus, ManagedEnvironmentCount, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores, ContainerAppAdditionalPorts, SessionPools. Ask the user to provide the correct quota type.";

            public const string NorthEuropeNotSupported = "There is no SubscriptionNCA100Gpus quota available in NorthEurope. Please ask the user if the customer can use ManagedEnvironmentConsumptionNCA100Gpus/ManagedEnvironmentConsumptionT4Gpus in WestUS3/SwedenCentral/AustraliaEast or SubscriptionNCA100Gpus in WestUS3.";

            public const string AutoApproved = @"Auto approved {0} quota for {1} offer type with limit less than or equal to {2}.";

            public const string RequireManualApprove = @"Manual approval is required for {1} offer type to set {0} quota with a limit great than {2}.";

            public const string RequireManualApproveForEnvCoreQuota = @"Manual approval is always required for quota request of quota type: {0}.";

            public const string RequestInformationMissing = @"The request {0} is missing. Ask the user to provide the {0}.";

            public const string SubscriptionInformationMissing = @"Failed to fetch {0} for the given subscription id. Aks the user to provide the correct subscriptionId.";

            public const string ManagedEnvResourceUriMissing = @"The managed environment resource uri is required for {0} quota request. Ask the user to provide the missing managed environment resource uri.";

            public const string InvalidQuotaLimit = @"Invalid target limit number. Ask the user to provide a valid target limit.";

            public const string InvalidQuotaType = @"Invalid quota type. Ask the customer to provide one of the following quota type: SubscriptionNCA100Gpus, ManagedEnvironmentConsumptionNCA100Gpus, ManagedEnvironmentConsumptionT4Gpus, ManagedEnvironmentCount, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores, ContainerAppAdditionalPorts, SessionPools";

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
        /// Quota for additional ports per subscription
        /// </summary>
        ContainerAppAdditionalPorts,

        /// <summary>
        /// Quota for managed environment consumption NCA100 GPUs
        /// </summary>
        ManagedEnvironmentConsumptionNCA100Gpus,

        /// <summary>
        /// Quota for managed environment consumption T4 GPUs
        /// </summary>
        ManagedEnvironmentConsumptionT4Gpus,
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
