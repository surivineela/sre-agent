namespace Agent.Logging;

public class CommonColumn
{
    public string AgentName { get; set; } = string.Empty;

    public string ContainerImage { get; set; } = string.Empty;

    public string ContainerGroupName { get; set; } = string.Empty;

    public static CommonColumn Build()
    {
        return new CommonColumn()
        {
            AgentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? throw new ArgumentNullException("AGENT_NAME", "Environment variable AGENT_NAME is not set."),
            ContainerImage = Environment.GetEnvironmentVariable("AGENT_CONTAINER_IMAGE") ?? string.Empty,
            ContainerGroupName = Environment.GetEnvironmentVariable("AGENT_CONTAINER_GROUP_NAME") ?? string.Empty
        };
    }
}
