namespace Agent.Core.Models.Api.v1;

// Don't change the order here as it's persisted in Database and used by SQL Query to filter all agent messages
public enum Role
{
    User,
    SREAgent
}

public record Author(
    Role Role,
    string UserId,
    string DisplayName);
