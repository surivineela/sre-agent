namespace Agent.Core.Models;

public sealed record AppServiceLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool SCMBasicAuthEnabled,
    bool FTPBasicAuthEnabled
    );
