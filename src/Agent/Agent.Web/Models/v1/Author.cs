namespace Agent.Web.Models.v1;

public enum Role
{
    User,
    SREAgent
}

public record Author(
    Role Role,
    string UserId,
    string DisplayName);
