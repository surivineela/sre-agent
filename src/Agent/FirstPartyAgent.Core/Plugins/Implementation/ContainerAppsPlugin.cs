// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Configuration;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace FirstPartyAgent.Plugins
{
    public class ContainerAppsPlugin : IContainerAppsPlugin
    {
        private readonly IcmSettings _icmSettings;
        private readonly IcmAutomationClient _icmAutomationClient;
        private readonly HttpClient _httpClient = new HttpClient();
      
        public ContainerAppsPlugin(IOptions<IcmSettings> icmSettings, IcmAutomationClient icmAutomationClient)
        {
            _icmSettings = icmSettings.Value;
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
                approvalResult = ApprovalState.Pending.ToString();
                return string.IsNullOrEmpty(quotaType) ? "ApproveResult: NotStarted. Reason: The Quota type is missing. Need to ask the user to provide the QuotaType."
                    : string.IsNullOrEmpty(region) ? "ApproveResult: NotStarted. Reason: The Region information is missing. Need to ask the user to provide the Region."
                    : "ApproveResult: NotStarted. Reason: The subscription offer type information is missing. Try to use get_subscription_detail tool to get the subscription information.";
            }

            if (!int.TryParse(targetQuotaLimit, out int limit))
            {
                approvalResult = ApprovalState.Pending.ToString();
                return "ApproveResult: NotStarted. Reason: Invalid target limit number. Ask the user to provide a valid target limit.";
            }

            if (!Enum.TryParse(quotaType, true, out QuotaType quotaTypeEnum))
            {
                approvalResult = ApprovalState.Pending.ToString();
                return "ApproveResult: NotStarted. Reason: Invalid quota type. Ask the customer to provide one of the following quota type: SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus.";
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
                        return (ApprovalState.NotStarted, "We have run out of SubscriptionNCA100Gpus in northeurope. Please ask the user if the customer can switch to westus3, or use SubscriptionConsumptionNCA100Gpus or SubscriptionConsumptionT4Gpus instead.");
                    case "westus3":
                        if (isEA)
                        {
                            return limit <= 10
                                ? (ApprovalState.Approved, $"Auto approved SubscriptionNCA100Gpus quota for EA offer type with limit less than or equal to 10.")
                                : (ApprovalState.Pending, $"Manual approval is required for EA offer type to set SubscriptionNCA100Gpus quota with a limit great than 10.");
                        }
                        else if (isPayAsYouGo || isInternal)
                        {
                            return limit <= 5
                                ? (ApprovalState.Approved, $"Auto approved SubscriptionNCA100Gpus quota for {offerType} offer type with limit less than or equal to 5.")
                                : (ApprovalState.Pending, $"Manual approval is required for {offerType} offer type to set SubscriptionNCA100Gpus quota with a limit great than 5.");
                        }
                        else
                        {
                            return (ApprovalState.Pending, $"Manual approval is required for {offerType} offer type to set SubscriptionNCA100Gpus quota.");
                        }
                    default:
                        return (ApprovalState.NotStarted, $"Quota type SubscriptionNCA100Gpus is not supported in this region: {region}. Ask the customer to provide the correct region.");
                }
            }
            else if (quotaType.Equals(QuotaType.SubscriptionConsumptionNCA100Gpus))
            {
                if (region.Equals("westus3") || region.Equals("swedencentral") || region.Equals("australiaeast"))
                {
                    if (isEA)
                    {
                        return limit <= 10
                            ? (ApprovalState.Approved, $"Auto approved SubscriptionConsumptionNCA100Gpus quota for EA offer typewith limit less than or equal to 10.")
                            : (ApprovalState.Pending, $"Manual approval is required for EA offer type to set SubscriptionConsumptionNCA100Gpus quota with a limit great than 10.");
                    }
                    else if (isPayAsYouGo || isInternal)
                    {
                        return limit <= 5
                            ? (ApprovalState.Approved, $"Auto approved SubscriptionConsumptionNCA100Gpus quota for {offerType} offer type with limit less than or equal to 5.")
                            : (ApprovalState.Pending, $"Manual approval is required for {offerType} offer type to set SubscriptionConsumptionNCA100Gpus quota with a limit great than 5.");
                    }
                    else
                    {
                        return (ApprovalState.Pending, $"Manual approval is required for {offerType} offer type to set SubscriptionConsumptionNCA100Gpus quota.");
                    }
                }
                else
                {
                    return (ApprovalState.Pending, $"SubscriptionConsumptionNCA100Gpus is not supported in this region: {region}. Ask the customer to provide correct region.");
                }
            }
            else if (quotaType.Equals(QuotaType.SubscriptionConsumptionT4Gpus))
            {
                if (region.Equals("westus3") || region.Equals("swedencentral") || region.Equals("australiaeast"))
                {
                    if (isEA)
                    {
                        return limit <= 40
                            ? (ApprovalState.Approved, $"Auto approved SubscriptionConsumptionT4Gpus quota for EA offer type with limit less than or equal to 40.")
                            : (ApprovalState.Pending, $"Manual approval is required for EA offer type to set SubscriptionConsumptionT4Gpus quota with a limit great than 40.");
                    }
                    else if (isPayAsYouGo || isInternal)
                    {
                        return limit <= 20
                            ? (ApprovalState.Approved, $"Auto approved SubscriptionConsumptionT4Gpus quota for {offerType} offer type with limit less than or equal to 20.")
                            : (ApprovalState.Pending, $"Manual approval is required for {offerType} offer type to set SubscriptionConsumptionT4Gpus quota with a limit great than 20.");
                    }
                    else
                    {
                        return (ApprovalState.Pending, $"Manual approval is required for {offerType} offer type to set SubscriptionConsumptionT4Gpus quota.");
                    }
                }
                else
                {
                    return (ApprovalState.Pending, $"SubscriptionConsumptionT4Gpus is not supported in this region: {region}. Ask the customer to provide correct region.");
                }

            }
            else if (quotaType.Equals(QuotaType.ManagedEnvironmentConsumptionCores)
                    || quotaType.Equals(QuotaType.ManagedEnvironmentGeneralPurposeCores)
                    || quotaType.Equals(QuotaType.ManagedEnvironmentMemoryOptimizedCores)
                    || quotaType.Equals(QuotaType.ManagedEnvironmentCount))
            {
                return (ApprovalState.NotStarted, "Managed Environment Count and Managed Environment Cores quota types are not suppored.");
            }
            else
            {
                return (ApprovalState.Pending, $"Quota type {quotaType} is not supported. Ask the user to provide correct QuotaType.");
            }
        }
    }
}
