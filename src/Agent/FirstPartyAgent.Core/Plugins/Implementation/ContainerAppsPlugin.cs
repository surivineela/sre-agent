// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace FirstPartyAgent.Plugins
{
    public class ContainerAppsPlugin : IContainerAppsPlugin
    {
        private readonly ICMSettings _icmSettings;
        private readonly IcmAutomationClient _icmAutomationClient;
        private readonly HttpClient _httpClient = new HttpClient();
      
        public ContainerAppsPlugin(ICMSettings icmSettings, IcmAutomationClient icmAutomationClient)
        {
            _icmSettings = icmSettings;
            _icmAutomationClient = icmAutomationClient;
        }

        public async Task<SubscriptionDetail?> GetSubscriptionDetail(
     string subscriptionId)
        {
            const string workflowName = "Workflow-Data-GetSubscriptionDetail";

            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId }
            };

            var (success, subscriptionDetail) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<SubscriptionDetail>(workflowName, body);

            if (success)
            {
                return subscriptionDetail;
            }
            else
            {
                return new SubscriptionDetail(subscriptionId);
            }
        }

        public async Task<bool> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit)
        {
            const string workflowName = "Workflow-GenevaAction-SetSubscriptionQuota";

            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId },
                { "Region", region },
                { "QuotaType", quotaType },
                { "QuotaLimit", quotaLimit },
            };
            var (success, _) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<object>(workflowName, body, "manual");
            return success;
        }

        public async Task<TeamsPostMessageResponse?> PostTeamsDiscussionAsync(string incidentId, string title, string content)
        {
            // prepend the icm link of the incident
            content = $"<p><a href=\"https://portal.microsofticm.com/imp/v5/incidents/details/{incidentId}/summary\">{incidentId}</a></p><br/>{content}";

            var body = new Dictionary<string, object>
            {
                { "IncidentId", incidentId },
                { "Title", title },
                { "Content", content }
            };

            return await SendTeamsRequestAsync(body);
        }

        public async Task<TeamsPostMessageResponse?> ReplyTeamsDiscussionAsync(string incidentId, string messageId, string content)
        {
            var body = new Dictionary<string, object>
            {
                { "IncidentId", incidentId },
                { "MessageId", messageId },
                { "Content", content }
            };
            return await SendTeamsRequestAsync(body);
        }

        private async Task<TeamsPostMessageResponse?> SendTeamsRequestAsync(object body)
        {
            var triggerUrl = _icmSettings.PostIncidentDiscussionUrl;
            if (string.IsNullOrEmpty(triggerUrl))
            {
                throw new Exception("ICM:PostIncidentDiscussionUrl is not configured.");
            }
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, triggerUrl);
            if (body != null)
            {
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();
            var respContent = await response.Content.ReadAsStringAsync();
            var respBody = JsonConvert.DeserializeObject<TeamsPostMessageResponse>(respContent);
            return respBody;
        }

        public async Task<string> ValidateQuotaRequest(
            [Description("The quota type of the quota request")] string quotaType,
            [Description("The offer type of the subscription")] string offerType,
            [Description("The region of the quota request")] string region,
            [Description("The target quota limit of the quota request")] string targetQuotaLimit
            )
        {
            string approvalResult;
            if (string.IsNullOrEmpty(quotaType) || string.IsNullOrEmpty(region) || string.IsNullOrEmpty(offerType))
            {
                approvalResult = ApprovalState.NotStarted.ToString();
                return string.IsNullOrEmpty(quotaType) ? string.Concat($"ApproveResult: {approvalResult}. Reason: ", string.Format(MessageTemplates.RequestInformationMissing, "quota type"))
                    : string.IsNullOrEmpty(region) ? string.Concat($"ApproveResult: {approvalResult}. Reason: ", string.Format(MessageTemplates.RequestInformationMissing, "region"))
                    : string.Concat($"ApproveResult: {approvalResult}. Reason: ", string.Format(MessageTemplates.SubscriptionInformationMissing, "offer type", KernelFunctionNames.ACA.GetSubscriptionDetail));
            }

            if (!int.TryParse(targetQuotaLimit, out int limit))
            {
                approvalResult = ApprovalState.NotStarted.ToString();
                return string.Concat($"ApproveResult: {approvalResult}. Reason: ", string.Format(MessageTemplates.InvalidQuotaLimit));
            }

            if (!Enum.TryParse(quotaType, true, out QuotaType quotaTypeEnum))
            {
                approvalResult = ApprovalState.NotStarted.ToString();
                return string.Concat($"ApproveResult: {approvalResult}. Reason: ", string.Format(MessageTemplates.InvalidQuotaType));
            }

            var result = ValidateQuotaRule(limit, quotaTypeEnum, region.ToLowerInvariant(), offerType);
            approvalResult = result.approvalState.ToString();

            return $"ApproveResult: {approvalResult}. Reason: {result.reason}";
        }

        private (ApprovalState approvalState, string reason) ValidateQuotaRule(int limit, QuotaType quotaType, string region, string offerType)
        {
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
                return (ApprovalState.Rejected, $"Auto rejected quota request for Benefit offer type.");
            }

            if (quotaType.Equals(QuotaType.SubscriptionNCA100Gpus))
            {
                switch (region)
                {
                    case "northeurope":
                        return (ApprovalState.NotStarted, MessageTemplates.NorthEuropeNotSupported);
                    case "westus3":
                        if (isEA)
                        {
                            return limit <= 10
                                ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionNCA100Gpus", "EA", "10"))
                                : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionNCA100Gpus", "EA", "10"));
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
            else if (quotaType.Equals(QuotaType.SubscriptionConsumptionNCA100Gpus))
            {
                if (region.Equals("westus3") || region.Equals("swedencentral") || region.Equals("australiaeast"))
                {
                    if (isEA)
                    {
                        return limit <= 10
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionNCA100Gpus", "EA", "10"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionNCA100Gpus", "EA", "10"));
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
            else if (quotaType.Equals(QuotaType.SubscriptionConsumptionT4Gpus))
            {
                if (region.Equals("westus3") || region.Equals("swedencentral") || region.Equals("australiaeast"))
                {
                    if (isEA)
                    {
                        return limit <= 40
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionT4Gpus", "EA", "40"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionT4Gpus", "EA", "40"));
                    }
                    else if (isPayAsYouGo || isInternal)
                    {
                        return limit <= 20
                            ? (ApprovalState.Approved, string.Format(MessageTemplates.AutoApproved, "SubscriptionConsumptionT4Gpus", offerType, "20"))
                            : (ApprovalState.Pending, string.Format(MessageTemplates.RequireManualApprove, "SubscriptionConsumptionT4Gpus", offerType, "20"));
                    }
                    else
                    {
                        return (ApprovalState.Pending, string.Format(MessageTemplates.RegionNotSupported, "SubscriptionConsumptionT4Gpus", region, "westus3, australiaeast, or swedensentral"));
                    }
                }
                else
                {
                    return (ApprovalState.NotStarted, string.Format(MessageTemplates.RegionNotSupported, "SubscriptionConsumptionT4Gpus", region, "westus3, australiaeast, or swedensentral"));
                }

            }
            else if (quotaType.Equals(QuotaType.ManagedEnvironmentConsumptionCores)
                    || quotaType.Equals(QuotaType.ManagedEnvironmentGeneralPurposeCores)
                    || quotaType.Equals(QuotaType.ManagedEnvironmentMemoryOptimizedCores)
                    || quotaType.Equals(QuotaType.ManagedEnvironmentCount))
            {
                return (ApprovalState.NotStarted, string.Format(MessageTemplates.QuotaTypeNotSupported, quotaType.ToString()));
            }
            else
            {
               
                return (ApprovalState.NotStarted, string.Format(MessageTemplates.QuotaTypeNotSupported, quotaType.ToString()));
            }
        }

        public static class MessageTemplates
        {
            public const string RegionNotSupported = @"Quota type {0} is not supported in this region {1}. For QuotaType {0}, the valid region should be {2}. Ask the user to provide the correct region.";

            public const string QuotaTypeNotSupported = @"Quota type {0} is not supported. The valid quota types are SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus. Ask the user to provide the correct quota type.";

            public const string NorthEuropeNotSupported = "We have run out of SubscriptionNCA100Gpus in northeurope. Ask the user if the customer can switch to westus3, or use SubscriptionConsumptionNCA100Gpus or SubscriptionConsumptionT4Gpus instead.";

            public const string AutoApproved = @"Auto approved {0} quota for {1} offer type with limit less than or equal to {2}.";

            public const string RequireManualApprove = @"Manual approval is required for {1} offer type to set {0} quota with a limit great than {2}.";

            public const string RequestInformationMissing = @"The request {0} is missing. Ask the user to provide the {0}.";

            public const string SubscriptionInformationMissing = @"The subscription {0} is missing. Try to use {1} tool to get the subscription information.";

            public const string InvalidQuotaLimit = @"Invalid target limit number. Ask the user to provide a valid target limit.";

            public const string InvalidQuotaType = @"Invalid quota type. Ask the customer to provide one of the following quota type: SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus.";
        }
    }
}
