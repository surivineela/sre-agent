namespace Agent.Core.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class RequiresApprovalAttribute : Attribute
{
    public string? DisplayMessage { get; set; }

    public RequiresApprovalAttribute(string? displayMessage = null)
    {
        DisplayMessage = displayMessage;
    }
}
