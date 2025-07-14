namespace Agent.Framework;

public sealed record CustomAgentFiles(
    Dictionary<string, string> yaml,
    Dictionary<string, string> kql,
    Dictionary<string, string> appsettings
    );
