using Newtonsoft.Json;

/*
 * Example helper for polling for approvals when DF WaitForExternalEvent is not available
 * 
 * Sample usage => start approval project and navigate to https://localhost:7268/action_name=sample-approval-id
 * Then run the following code in your runtime project
 * 
 * var approval = await ApprovalHelper.WaitForApprovalAsync("https://localhost:7268", "sample-approval-id", 5, 120);
 */
namespace OperationalAgentRuntime.Helpers
{
    public static class ApprovalHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public class ApprovalPayload
        {
            public string Id { get; set; }
            public bool IsApproved { get; set; }
            public string ApproverName { get; set; }
        }


        public static async Task<ApprovalPayload> WaitForApprovalAsync(string approvalBaseLink, string id, int pollingIntervalInSeconds = 5, int timeoutInSeconds = 120)
        {
            // e.g. https://localhost:7268?action_name=12345 to raise request and https://localhost:7268/GetApprovals to poll for results"
            string approvalLink = Path.Join(approvalBaseLink, "/", "GetApprovals");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds));
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollingIntervalInSeconds));

            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(approvalLink, cts.Token);

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
            }

            return new ApprovalPayload()
            {
                Id = id,
                IsApproved = false,
                ApproverName = "NoApprovalFoundWithinTimeout"
            };
        }
    }
}
