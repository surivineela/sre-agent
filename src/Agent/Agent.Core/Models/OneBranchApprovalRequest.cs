using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Agent.Core.Models;
public class OneBranchApprovalRequest
{
    public string CorrelationId { get; set; } = string.Empty;
    public string RequestDescription { get; set; } = string.Empty;
    public string Submitter { get; set; } = string.Empty;
    public string ServiceTreeGuid { get; set; } = string.Empty;
    public List<string> ReleaseApproversAllowed { get; set; } = new List<string>();
    public string Title { get; set; } = string.Empty;
}

public class OneBranchApprovalResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string ApprovalDocumentId { get; set; } = string.Empty;
    public string ApprovalDocumentUri { get; set; } = string.Empty;
}

public class OneBranchApprovalStatus
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    [JsonProperty("data")]
    public ApprovalData Data { get; set; } = new ApprovalData();
    public string EventType { get; set; } = string.Empty;
    public string DataVersion { get; set; } = string.Empty;
    public string MetadataVersion { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string Topic { get; set; } = string.Empty;

    public class ApprovalData
    {
        public string CorrelationId { get; set; } = string.Empty;
        [JsonProperty("ApprovalDocumentId")]
        public string ApprovalDocumentId { get; set; } = string.Empty;
        public DocumentCompleteDetails ApprovalDocumentCompleteDetails { get; set; } = new DocumentCompleteDetails();

        public class DocumentCompleteDetails
        {
            public string Principal { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string Comments { get; set; } = string.Empty;

        }
    }
}

