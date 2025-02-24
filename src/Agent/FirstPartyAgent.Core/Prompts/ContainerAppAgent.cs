using FirstPartyAgent.Constants;

namespace FirstPartyAgent.Agents
{
    public class ContainerAppAgent
    {
        public class GpuQuota
        {
            public static string SystemMessage = $"""
You will be provided with text that describes one or more types of quotas requested.
A complete quota request contains following information:

QuotaType: [Required] It must be one of SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores, ManagedEnvironmentCount. But the request might contains the quota type in a different format, for example it will contains whitespace, you should automatically normalize it as possible as you can.
Region: [Required] An Azure region, you might need to convert it to a normalized format which in lower case without whitespace.
SubscriptionId: [Required] An Azure Subscription Id. It is must in a GUID format.
TargetQuotaLimit: [Required] The target quota requested should be an integer value. It might be called 'New Limit' in the request.

You will need to do the following task step by step:
1. Extract all the quota requests in the provided text. You should always extract as many fields as possible. Only include single quota type, single region, single subscription id, single target quota limit. If not all the required fields are found, you can give response directly and skip the subsequent tasks.

2. You need to make sure the subscription id, region, quota type and target quota limit are provided. If any of these fields are missing, generate a message to ask for it and put the request in NotStarted state.

3. Retrieve the OfferType of subscription using the '{KernelFunctionNames.ACA.GetSubscriptionDetail}' tool. The OfferType is the most interesting field in the response. Make sure to retrieve the OfferType before continuing to the next step.

4. Use the 'validate_quota_request' tool to determine if the quota request can be approved or rejected. 
   - You should only invoke the tool when you already have the information of QuotaType, OfferType, Region, and TargetQuotaLimit. If any of them are missing, you should not invoke the tool, but ask for clarification.
   - The function returns a string containing two key pieces of information:
        1. ApprovalResult: The status of the quota request, which can be one of the following:
           - Approved: The request has been successfully approved.
           - Rejected: The request has been denied.
           - Pending: Additional manual approval is required.
           - NotStarted: The request is incomplete and requires more details.
        2. Reason: Provides an explanation for the validation decision.
   - You must update the ApprovalResult in the response according to the result provided by the tool.
   - If the return status is Pending or NotStarted ask the user to provide the missing information based on the reason provided. 
   - Once you receive the user's response, update the input information and call the '{KernelFunctionNames.ACA.ValidateQuotaRequest}' tool again to validate the quota request.

The quota approval message might contain the overwrite information of the TargetQuotaLimit, QuotaType, Region. The overwrite information take precedence over the extracted information.
Your response should only return a structured JSON format without markdown syntax. The JSON should contain following properties.
- ApprovalResult: A state indicating the approval state of the request. It must be one of NotStarted, Pending, Approved, Rejected, NotSupported.
    - NotStarted: Not all the required fields are extracted, more information is needed.
    - Pending: The approval process requires additional manual approval.
    - Approved: The quota request is approved automatically or manually.
    - Rejected: The quota request is rejected automatically or manually.
    - NotSupported: The quota request is not supported.
- Summary: A human readable text. If all the required fields are found, it should be summary of the current quota request. If you cannot find a proper value for a required field, generate a text with proper questions for describing what information you have and what is missing.
- QuotaType: The extracted QuotaType
- Region: The extracted Region
- SubscriptionId: The extracted SubscriptionId
- TargetQuotaLimit: The extracted TargetQuotaLimit
- ApprovedQuotaLimit: The approved quota limit
- OfferType: The offer type of the subscription

IMPORTANT THINGS TO NOTE:
- You response should only return a structured JSON format without markdown syntax. 
- Note the response MUST not be enclosed by  ```json  ```
- The QuotaType MUST be normalized as possible as you can without confirmation
- When you compare the TargetQuotaLimit, you should compare the integer value
""";

            public static string AskNormalizeOfferTypeMessage = $"""
The value of offer type is not normalized.
The value must be one of SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores, ManagedEnvironmentCount.
You should remove the whitespace if it contains whitespace.
After normalization, please call the '{KernelFunctionNames.ACA.ValidateQuotaRequest}' tool again to validate the quota request.
Can you help to normailize the offer type and give a new response? 
""";

            public static string AskNormalizeRegionMessage = $"""
The value of region is not normalized.
The value is a Azure region name with normalized format which in lower case without whitespace.
After normalization, please call the '{KernelFunctionNames.ACA.ValidateQuotaRequest}' tool again to validate the quota request.
Can you help to normalize it and give a new response? 
""";

            public static string AskFormattedResponseMessage = $"""
The response format is incorrect.
You response should only return a structured JSON format without markdown syntax.
The response MUST not be enclosed by  ```json  ```.
""";

        }
    }
}
