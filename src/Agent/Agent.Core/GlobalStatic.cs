// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Agent.Core;

// TODO: figure out how to DI these into DiagnosePlugin
public static class GlobalStatic
{
    public static TeamsConnector TeamsConnector;

    public static ConcurrentDictionary<ApprovalDescriptor, ApprovalStatus> ApprovalStatus { get; } = new();
}


/*
 * Example helper for polling for approvals when DF WaitForExternalEvent is not available
 * 
 * Sample usage => start approval project and navigate to https://localhost:7268/action_name=sample-approval-id
 * Then run the following code in your runtime project
 * 
 * var approval = await ApprovalHelper.WaitForApprovalAsync("https://localhost:7268", "sample-approval-id", 5, 120);
 */

public static class ApprovalHelper
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly CancellationTokenSource cts = new CancellationTokenSource();

    public class ApprovalPayload
    {
        public string Id { get; set; }
        public bool IsApproved { get; set; }
        public string ApproverName { get; set; }
    }


    public static async Task<ApprovalPayload> PullApprovalResult(string approvalBaseLink, string id)
    {
        string approvalLink = Path.Join(approvalBaseLink, "/", "GetApprovals");


        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(approvalLink);
            Console.WriteLine($"Approval Response: {response}, requested id: {id}");

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                var approvalPayload = JsonConvert.DeserializeObject<List<ApprovalPayload>>(responseBody);

                if (approvalPayload != null && approvalPayload.Any(a => a.Id == id))
                {
                    return approvalPayload.First(a => a.Id == id);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Approval Error: {ex.Message}");
        }


        return new ApprovalPayload()
        {
            Id = id,
            IsApproved = false,
            ApproverName = "NoApprovalFoundWithinTimeout"
        };
    }
}

