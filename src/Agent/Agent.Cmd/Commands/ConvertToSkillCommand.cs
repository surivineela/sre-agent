// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Services;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Agent.Cmd.Commands;

public class ConvertToSkillCommand(
    ILogger<ConvertToSkillCommand> logger,
    AgentToSkillService agentToSkillService
)
{
    // These agents will be converted to top-level skills, so we don't need to create separate markdown files for them in each skill.
    private static readonly string[] TopLevelAgentSkills = [
        "resource_discovery_agent",
        "logs_resource_discovery_agent",
        "scheduled_task_agent",
        "container_apps_agent",
        "tls_minimum_version_upgrade_agent",
        "azure_cli_command_executor_agent",
        "web_app_down_agent",
        "web_app_restart_agent",
        "aks_general_agent",
        "metrics_and_chart_visualization_agent",
        "local_auth_agent",
        "diagnostic_cpu_agent",
        "diagnostic_memory_agent",
        "function_app_agent",
        "api_management_agent",
        "repository_agent",
        "postgresql_agent",
        "cdb_general_agent",
        "incident_management_agent",
        "source_code_analysis_agent",
        "self_manual_agent",
        "logic_app_agent",
        "cannot_connect_to_vm_agent",
    ];

    public async Task ExecuteAsync(
        CommandLineApplication app,
        string agentName,
        string outputDirectory)
    {
        logger.LogInformation("Converting agent {AgentName} to skill in directory {OutputDir}", agentName, outputDirectory);

        var skill = await agentToSkillService.ConvertAgentToSkillAsync(agentName, TopLevelAgentSkills);

        string skillDir = Path.Combine(outputDirectory, skill.Name);

        if (Directory.Exists(skillDir))
        {
            // ask user if they want to overwrite
            logger.LogWarning("Skill directory {SkillDir} already exists.", skillDir);
            app.Out.Write("Directory already exists. Overwrite? (y/n): ");
            string? response = Console.ReadLine();
            if (response?.ToLower() != "y")
            {
                logger.LogInformation("Conversion cancelled by user.");
                return;
            }
        }
        else
        {
            Directory.CreateDirectory(skillDir);
        }

        // create metadata.yaml
        string metadataYaml = $"""
        name: {skill.Name}
        description: |
          {skill.Description}
        tools:
        {string.Join(Environment.NewLine, skill.Tools.Select(t => $"  - {t}"))}

        """;

        File.WriteAllText(Path.Combine(skillDir, "metadata.yaml"), metadataYaml);
        logger.LogInformation("Created metadata.yaml");

        // create SKILL.md
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), skill.SkillMdContent);
        logger.LogInformation("Created SKILL.md");

        foreach (var subFile in skill.AdditionalFiles)
        {
            File.WriteAllText(Path.Combine(skillDir, subFile.FilePath), subFile.Content);
            logger.LogInformation("Created additional file {FileName}", Path.GetFileName(subFile.FilePath));
        }

        logger.LogInformation("Conversion completed successfully.");
    }
}
