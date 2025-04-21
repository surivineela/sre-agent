using System.ComponentModel;

namespace Agent.Core.Models.Api.v1;
public record ApprovalInformation(
    [Description("The url to present to the user to approve or reject the operation")] string ApprovalUrl);
