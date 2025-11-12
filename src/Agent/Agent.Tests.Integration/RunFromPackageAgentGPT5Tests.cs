// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq;
using Xunit.Abstractions;
using YamlDotNet.Serialization;

namespace Agent.Tests.Integration
{
    public class RunFromPackageAgentGPT5Tests
    {
        private readonly ITestOutputHelper _output;

        private static readonly string GPT5AgentPath = GetAgentPath("AgentsGPT5", "ThirdParty", "RunFromPackageAgent.yaml");

        private static string GetAgentPath(params string[] pathParts)
        {
            // Find the project root by looking for the Agent.sln file
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Agent.sln")))
            {
                currentDir = currentDir.Parent;
            }

            if (currentDir == null)
                throw new DirectoryNotFoundException("Could not find Agent.sln to determine project root");

            var fullPath = new List<string> { currentDir.FullName, "Agent.Runtime" };
            fullPath.AddRange(pathParts);
            return Path.Combine(fullPath.ToArray());
        }

        public RunFromPackageAgentGPT5Tests(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        #region GPT5 Agent Structure Validation Tests

        [Fact]
        public void GPT5Agent_YamlFile_ExistsAndIsValid()
        {
            // Test that the GPT5 agent YAML file exists and can be parsed
            Assert.True(File.Exists(GPT5AgentPath), "GPT5 RunFromPackageAgent.yaml file should exist");

            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();

            // This should not throw an exception
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            Assert.NotNull(agentData);
            Assert.True(agentData.ContainsKey("name"));
            Assert.Equal("run_from_package_agent", agentData["name"]);
        }

        [Fact]
        public void GPT5Agent_SystemPrompt_HasRequiredMarkdownStructure()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            Assert.True(agentData.ContainsKey("system_prompt"));
            var systemPrompt = agentData["system_prompt"].ToString()!;

            // Validate markdown structure following GPT5 patterns
            Assert.Contains("# Role and Objective", systemPrompt);
            Assert.Contains("# Planning", systemPrompt);
            Assert.Contains("# Core Responsibilities", systemPrompt);
            Assert.Contains("# Security Guidelines", systemPrompt);
            Assert.Contains("# Operational Workflow", systemPrompt);

            // Validate planning instruction follows GPT5 pattern
            Assert.Contains("Begin with a concise checklist (3-7 bullets)", systemPrompt);

            // Validate structured workflow sections
            Assert.Contains("## 1. Initial Assessment", systemPrompt);
            Assert.Contains("## 2. Configuration Analysis", systemPrompt);
            Assert.Contains("## 6. Package Structure Validation", systemPrompt);
            Assert.Contains("## 8. Solution Implementation", systemPrompt);
        }

        [Fact]
        public void GPT5Agent_SystemPrompt_FollowsSecurityGuidelines()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            var systemPrompt = agentData["system_prompt"].ToString()!;

            // Validate security guidelines are prominent and clear
            Assert.Contains("**NEVER** display, log, or expose SAS tokens", systemPrompt);
            Assert.Contains("mask sensitive query parameters", systemPrompt);
            Assert.Contains("***MASKED***", systemPrompt);

            // Ensure no contradictory security instructions
            Assert.DoesNotContain("expose secrets", systemPrompt);
            Assert.DoesNotContain("show sensitive", systemPrompt);
        }

        [Fact]
        public void GPT5Agent_SystemPrompt_HasClearPrioritizationLogic()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            var systemPrompt = agentData["system_prompt"].ToString()!;

            // Validate prioritization logic is clear and non-contradictory (GPT-5 guideline)
            Assert.Contains("Prioritize actual issues over recommendations", systemPrompt);
            Assert.Contains("Focus on critical issues that could cause customer problems", systemPrompt);
            Assert.Contains("Distinguish between critical issues requiring fixes vs optimization recommendations", systemPrompt);

            // Ensure no contradictory prioritization instructions (following GPT-5 best practices)
            Assert.DoesNotContain("recommendations first", systemPrompt);
            Assert.DoesNotContain("optimize before fixing", systemPrompt);
        }

        [Fact]
        public void GPT5Agent_SystemPrompt_HasPackageValidationRequirements()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            var systemPrompt = agentData["system_prompt"].ToString()!;

            // Validate package structure validation requirements
            Assert.Contains("Always run `InspectPackageStructure` before any WEBSITE_RUN_FROM_PACKAGE configuration updates", systemPrompt);
            Assert.Contains("Block configuration updates if package structure is invalid or missing required files", systemPrompt);
            Assert.Contains("Only proceed with configuration updates after BOTH explicit user consent AND confirmed valid package structure", systemPrompt);
        }

        [Fact]
        public void GPT5Agent_SystemPrompt_FollowsGPT5Guidelines()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            var systemPrompt = agentData["system_prompt"].ToString()!;

            // Validate GPT-5 specific improvements
            // 1. Structured workflow with clear sections
            var sectionCount = CountOccurrences(systemPrompt, "## ");
            Assert.True(sectionCount >= 8, $"Should have at least 8 workflow sections, found {sectionCount}");

            // 2. Post-action validation requirement
            Assert.Contains("Post-action Validation", systemPrompt);
            Assert.Contains("validate the result in 1-2 lines", systemPrompt);

            // 3. Clear tool usage guidelines
            Assert.Contains("# Tool Usage Guidelines", systemPrompt);

            // 4. Success criteria definition
            Assert.Contains("# Success Criteria", systemPrompt);
        }

        [Fact]
        public void GPT5Agent_YamlStructure_MatchesGPT5Pattern()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // Validate YAML structure matches GPT5 agent pattern
            Assert.True(agentData.ContainsKey("name"));
            Assert.True(agentData.ContainsKey("system_prompt"));
            Assert.True(agentData.ContainsKey("handoff_description"));
            Assert.True(agentData.ContainsKey("tools"));
            Assert.True(agentData.ContainsKey("common_prompts"));

            // Validate temperature setting for consistency
            if (agentData.ContainsKey("temperature"))
            {
                var temperature = Convert.ToDouble(agentData["temperature"]);
                Assert.True(temperature <= 0.5, "GPT5 agents should use lower temperature for consistent technical responses");
            }
        }

        [Fact]
        public void GPT5Agent_SystemPrompt_RemovesContradictoryInstructions()
        {
            var yamlContent = File.ReadAllText(GPT5AgentPath);
            var deserializer = new DeserializerBuilder().Build();
            var agentData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            var systemPrompt = agentData["system_prompt"].ToString()!;

            // Validate that contradictory instructions have been removed (GPT-5 guideline)
            // These are examples of contradictory patterns that should be avoided

            // Security contradictions - check for valid NEVER instructions vs contradictory permissions
            var hasSecurityNever = systemPrompt.Contains("**NEVER** display, log, or expose SAS tokens");
            var hasSensitiveNever = systemPrompt.Contains("**NEVER** show the full content of package URLs containing sensitive parameters");

            // Look for contradictory positive instructions that would expose sensitive data
            // These should NOT exist in a well-designed prompt
            var hasShowSensitive = systemPrompt.Contains("show sensitive information") && !systemPrompt.Contains("NEVER");
            var hasDisplaySensitive = systemPrompt.Contains("display sensitive data") && !systemPrompt.Contains("NEVER");
            var hasExposeSensitive = systemPrompt.Contains("expose sensitive") && !systemPrompt.Contains("NEVER");

            Assert.True(hasSecurityNever || hasSensitiveNever, "Should have clear NEVER security instruction");
            Assert.False(hasShowSensitive, "Should not have instructions to show sensitive information");
            Assert.False(hasDisplaySensitive, "Should not have instructions to display sensitive data");
            Assert.False(hasExposeSensitive, "Should not have instructions to expose sensitive information");

            // Priority contradictions  
            var hasPriorityGuidance = systemPrompt.Contains("Prioritize actual issues over recommendations");
            var hasRecommendationsFirst = systemPrompt.Contains("provide recommendations first") || systemPrompt.Contains("recommendations before fixes");
            Assert.True(hasPriorityGuidance, "Should have clear prioritization guidance");
            Assert.False(hasRecommendationsFirst, "Should not prioritize recommendations over critical fixes");
        }

        [Fact]
        public void GPT5Agent_PromptStructure_ComparedToOriginal()
        {
            // Compare GPT5 agent structure to original V2 agent to validate improvements
            var originalAgentPath = GetAgentPath("AgentsV2", "RunFromPackageAgent.yaml");
            var gpt5AgentPath = GPT5AgentPath;

            Assert.True(File.Exists(originalAgentPath), "Original agent file should exist for comparison");
            Assert.True(File.Exists(gpt5AgentPath), "GPT5 agent file should exist");

            var deserializer = new DeserializerBuilder().Build();

            var originalYaml = File.ReadAllText(originalAgentPath);
            var gpt5Yaml = File.ReadAllText(gpt5AgentPath);

            var originalData = deserializer.Deserialize<Dictionary<string, object>>(originalYaml);
            var gpt5Data = deserializer.Deserialize<Dictionary<string, object>>(gpt5Yaml);

            var originalPrompt = originalData["system_prompt"].ToString()!;
            var gpt5Prompt = gpt5Data["system_prompt"].ToString()!;

            // GPT5 improvements validation
            // 1. Has structured markdown headers (GPT5 has more structure)
            var originalHeaderCount = CountOccurrences(originalPrompt, "#");
            var gpt5HeaderCount = CountOccurrences(gpt5Prompt, "#");
            Assert.True(gpt5HeaderCount > originalHeaderCount, $"GPT5 should have more structured headers. Original: {originalHeaderCount}, GPT5: {gpt5HeaderCount}");

            // 2. Includes planning section (GPT5 specific)
            Assert.DoesNotContain("Begin with a concise checklist", originalPrompt);
            Assert.Contains("Begin with a concise checklist", gpt5Prompt);

            // 3. More structured workflow sections
            var originalWorkflowSections = CountOccurrences(originalPrompt, "## ");
            var gpt5WorkflowSections = CountOccurrences(gpt5Prompt, "## ");
            Assert.True(gpt5WorkflowSections >= originalWorkflowSections, "GPT5 should have equal or more structured workflow sections");

            _output.WriteLine($"Original headers: {originalHeaderCount}, GPT5 headers: {gpt5HeaderCount}");
            _output.WriteLine($"Original workflow sections: {originalWorkflowSections}, GPT5 workflow sections: {gpt5WorkflowSections}");
        }

        #endregion

        #region Helper Methods

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        #endregion
    }
}
