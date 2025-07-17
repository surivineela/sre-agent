// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenerateApprovalLinkActivity : TaskActivity<(string ApprovalId, string Reason, string Description), string>
{
    private readonly IConfiguration _configuration;

    public GenerateApprovalLinkActivity(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override Task<string> RunAsync(TaskActivityContext context, (string ApprovalId, string Reason, string Description) input)
    {
        try
        {
            // Get the approval,callback endpoint from configuration
            string approvalEndpoint = _configuration["AppSettings:ApprovalsEndpoint"];
            string callbackUrl = System.Environment.GetEnvironmentVariable("AGENT_ENDPOINT") ?? "";

            if (string.IsNullOrEmpty(approvalEndpoint))
            {
                throw new InvalidOperationException("Core:ApprovalEndpoint configuration is missing");
            }

            // Create a query string with the approval data
            var queryData = new
            {
                approvalId = input.ApprovalId,
                reason = input.Reason,
                description = input.Description,
                callbackUrl = callbackUrl
            };

            // Serialize and encode the query data
            string jsonData = System.Text.Json.JsonSerializer.Serialize(queryData);
            string encodedData = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonData));

            // Generate the approval link
            string approvalLink = $"{approvalEndpoint}?data={encodedData}";

            return Task.FromResult(approvalLink);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error generating an approval link: {ex}");
        }
    }
}
