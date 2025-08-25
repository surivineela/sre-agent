using Agent.Plugins.Helpers;
using Xunit;

namespace Agent.Tests.Unit.Plugins;

public class MarkdownEmojiSanitizerTests
{
    [Theory]
    [InlineData("Hello 😊 world", "Hello world")]
    [InlineData("- **Bold** text", "- **Bold** text")]
    [InlineData("-**Bold** text", "- **Bold** text")] // ensure a space remains after list marker when opener follows
    [InlineData("End of **bold**text", "End of **bold** text")] // space after closing ** when next is letter
    [InlineData("Heading 😀\n\nText", "Heading\n\nText")] // remove emoji across paragraphs
    [InlineData("Mix 🔧in**bold**list\n-**Item**", "Mix in **bold** list\n- **Item**")]
    [InlineData("Flags 🇺🇸 are gone", "Flags are gone")]
    [InlineData(OriginalText, ExpectedText)] // test with original text
    public void RemoveEmojis_PreservesMarkdown(string input, string expected)
    {
        var result = MarkdownEmojiSanitizer.RemoveEmojisPreserveMarkdown(input);
        Assert.Equal(expected, result);
    }

    private const string OriginalText = @"
## 📋 Blob Trigger RCA Preflight Summary

### 🔍 Analysis Results by Component:

**✅ Worker/Runtime Status:**
FunctionCompleted activity was observed at 2025-05-28T02:00:00Z and 2025-06-11T03:00:00Z, and host signals were present within the window. However, outside these isolated events, function completions appear to be missing for the bulk of the window, indicating significant periods of inactivity or failure to trigger.

**✅ Site Metadata:**
- Site: abc007 (ElasticPremium SKU)
- VNET: 9999999999-6600-cccc-be5b-18db0ad9b01a_100Somene002
- Trigger: BlobTrigger (EventGrid-based) on path 'zoom-audio-file/{name}' via STORAGE_ACCOUNT_CONNECTION_STRING
- Host config: MaxDegreeOfParallelism: 16, poisonBlobThreshold: 5, dynamicConcurrencyEnabled: true, maximumFunctionConcurrency: 500

**✅ Event Source IP Analysis:**
The most frequent EventIpAddress was 10.60.0.141, appearing 275,223 times—indicating that the event source or listener was consistently running on this node. There is no suggestion of IP-level routing/partitioning issues.

**✅ Stop/Recovered Time Window (Gap Detection):**
Gap analysis revealed no function completion data for the entire timeframe (2025-05-28T02:39:00Z to 2025-07-07T08:41:45Z) except sparse activity before the interval. This indicates the function was not operational, not triggered, or completions were not recorded—suggesting a pervasive issue throughout the window.

**✅ Polling/Listener Health:**
Polling was detected only once at 5/29/2025 04:00 AM (15 events), with no polling at all outside this timestamp between 2025-05-28 and 2025-07-07. This is highly abnormal—expected polling or listener checkpoint activity is missing, pointing to a malfunction (worker, listener, or scale controller activity lost).

**✅ Pattern Mismatch / Enqueue–Dequeue Consistency:**
No BlobDoesNotMatchPattern events detected, confirming all incoming blobs matched patterns and no enqueue/dequeue pattern consistency issues. No unprocessed/mismatched blobs.

**✅ Function Correlation Result:**
Where events did occur, function executions had clean correlation between BlobMessageEnqueued, FunctionStarted, and FunctionCompleted events (with matched InvocationIds/timestamps). This confirms that when triggered, the pipeline was healthy end-to-end. However, events are extremely sparse.

### 🎯 Root Cause Summary:
Across independent checks (worker status, event IP, gap analysis, polling health), the primary symptom is a near-total absence of function triggers, completions, and polling activity for the BlobTrigger function over a prolonged window. The function itself is healthy when triggered, but the listener/polling infrastructure (which reads and dispatches blob events) was non-functional for the entirety of the observed period except for one isolated polling event.

This strongly suggests a systemic breakdown of the BlobTrigger listener subsystem—possibly due to a deployment/desync bug, host or scale-unit misconfiguration, or underlying listener registration failure. No evidence of blob pattern issues, network isolation (as VNET is used but events did reach the worker), or SKU quota/saturation. The function host appears healthy, but the event source is starved due to listener inactivity.

### 💡 Recommendations / Next Steps:
- **Listener/Host Restart:** Proactively restart the function app and underlying workers to force re-registration of the BlobTrigger listener.
- **Review/Audit Host and Scale Controller Logs:** Examine logs for scale controller and listener initialization errors or EventGrid subscription problems starting 2025-05-28.
- **VNET Diagnostic Check:** Ensure VNET integration is not preventing listener heartbeats or causing subnet-level isolation; verify storage event subscriptions from both the Azure portal and backend (Resource Explorer).
- **Configuration Validation:** Double-check host.json and storage connection string (STORAGE_ACCOUNT_CONNECTION_STRING) correctness for EventGrid BlobTrigger; ensure EventGrid webhooks are correctly associated and not in a failed state.
- **Contact Azure Support:** If no root cause can be surfaced in host and storage logs, escalate to Microsoft support to investigate potential platform-scale issues affecting BlobTrigger workers on ElasticPremium plans.
- **Resume Monitoring:** After corrective action, monitor logs for resumption of polling/listener activity and new FunctionCompleted events to confirm resolution. System is currently NOT healthy.
---

**Thread Details:** [View detailed conversation](https://localhost:7023/static/#/views/activities/threads/b00dc4af-18bc-4238-a2b8-33a89eb9efa1)
    ";
    private const string ExpectedText = @"
## Blob Trigger RCA Preflight Summary

### Analysis Results by Component:

**Worker/Runtime Status:**
FunctionCompleted activity was observed at 2025-05-28T02:00:00Z and 2025-06-11T03:00:00Z, and host signals were present within the window. However, outside these isolated events, function completions appear to be missing for the bulk of the window, indicating significant periods of inactivity or failure to trigger.

**Site Metadata:**
- Site: abc007 (ElasticPremium SKU)
- VNET: 9999999999-6600-cccc-be5b-18db0ad9b01a_100Somene002
- Trigger: BlobTrigger (EventGrid-based) on path 'zoom-audio-file/{name}' via STORAGE_ACCOUNT_CONNECTION_STRING
- Host config: MaxDegreeOfParallelism: 16, poisonBlobThreshold: 5, dynamicConcurrencyEnabled: true, maximumFunctionConcurrency: 500

**Event Source IP Analysis:**
The most frequent EventIpAddress was 10.60.0.141, appearing 275,223 times—indicating that the event source or listener was consistently running on this node. There is no suggestion of IP-level routing/partitioning issues.

**Stop/Recovered Time Window (Gap Detection):**
Gap analysis revealed no function completion data for the entire timeframe (2025-05-28T02:39:00Z to 2025-07-07T08:41:45Z) except sparse activity before the interval. This indicates the function was not operational, not triggered, or completions were not recorded—suggesting a pervasive issue throughout the window.

**Polling/Listener Health:**
Polling was detected only once at 5/29/2025 04:00 AM (15 events), with no polling at all outside this timestamp between 2025-05-28 and 2025-07-07. This is highly abnormal—expected polling or listener checkpoint activity is missing, pointing to a malfunction (worker, listener, or scale controller activity lost).

**Pattern Mismatch / Enqueue–Dequeue Consistency:**
No BlobDoesNotMatchPattern events detected, confirming all incoming blobs matched patterns and no enqueue/dequeue pattern consistency issues. No unprocessed/mismatched blobs.

**Function Correlation Result:**
Where events did occur, function executions had clean correlation between BlobMessageEnqueued, FunctionStarted, and FunctionCompleted events (with matched InvocationIds/timestamps). This confirms that when triggered, the pipeline was healthy end-to-end. However, events are extremely sparse.

### Root Cause Summary:
Across independent checks (worker status, event IP, gap analysis, polling health), the primary symptom is a near-total absence of function triggers, completions, and polling activity for the BlobTrigger function over a prolonged window. The function itself is healthy when triggered, but the listener/polling infrastructure (which reads and dispatches blob events) was non-functional for the entirety of the observed period except for one isolated polling event.

This strongly suggests a systemic breakdown of the BlobTrigger listener subsystem—possibly due to a deployment/desync bug, host or scale-unit misconfiguration, or underlying listener registration failure. No evidence of blob pattern issues, network isolation (as VNET is used but events did reach the worker), or SKU quota/saturation. The function host appears healthy, but the event source is starved due to listener inactivity.

### Recommendations / Next Steps:
- **Listener/Host Restart:** Proactively restart the function app and underlying workers to force re-registration of the BlobTrigger listener.
- **Review/Audit Host and Scale Controller Logs:** Examine logs for scale controller and listener initialization errors or EventGrid subscription problems starting 2025-05-28.
- **VNET Diagnostic Check:** Ensure VNET integration is not preventing listener heartbeats or causing subnet-level isolation; verify storage event subscriptions from both the Azure portal and backend (Resource Explorer).
- **Configuration Validation:** Double-check host.json and storage connection string (STORAGE_ACCOUNT_CONNECTION_STRING) correctness for EventGrid BlobTrigger; ensure EventGrid webhooks are correctly associated and not in a failed state.
- **Contact Azure Support:** If no root cause can be surfaced in host and storage logs, escalate to Microsoft support to investigate potential platform-scale issues affecting BlobTrigger workers on ElasticPremium plans.
- **Resume Monitoring:** After corrective action, monitor logs for resumption of polling/listener activity and new FunctionCompleted events to confirm resolution. System is currently NOT healthy.
---

**Thread Details:** [View detailed conversation](https://localhost:7023/static/#/views/activities/threads/b00dc4af-18bc-4238-a2b8-33a89eb9efa1)
    ";
}
