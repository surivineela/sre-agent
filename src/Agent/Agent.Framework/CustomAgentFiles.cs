namespace Agent.Framework;

public sealed record CustomAgentFiles(
    Dictionary<string, string> yaml,
    Dictionary<string, string> tools,
    Dictionary<string, string> appsettings
    );
