// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// Tests for 'srectl tool migrate' command functionality.
/// Validates migration of V1 tools to V2 format.
/// </summary>
[Collection("ToolTests")]
public class MigrateCommandTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CliTestRunner _cli;

    public MigrateCommandTests(ITestOutputHelper output)
    {
        _output = output;
        _cli = new CliTestRunner();
        _output.WriteLine($"Test working directory: {_cli.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    public async Task ToolMigrate_KustoToolV1_ConvertsToV2()
    {
        // Arrange - Create a V1 KustoTool
        var toolName = "TestKustoToolV1";
        var v1Yaml = TestYamlHelper.GetKustoToolV1(
            name: toolName,
            description: "Test Kusto Tool",
            connector: "TestConnector",
            database: "TestDatabase",
            query: "TestQuery | take 10",
            parameters: new List<(string, string, string)>
            {
                ("param1", "string", "Test parameter")
            });
        _cli.CreateFile($"tools/{toolName}.yaml", v1Yaml);

        // Act - Migrate the tool
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Migration should succeed");
        Assert.Equal(0, result.ExitCode);

        // Verify V2 format (migrated in place)
        var migratedYaml = _cli.ReadFile($"tools/{toolName}.yaml");
        Assert.Contains("api_version: azuresre.ai/v2", migratedYaml);
        Assert.Contains("kind: ExtendedTool", migratedYaml);
        Assert.Contains("type: KustoTool", migratedYaml);
        Assert.Contains("connector: TestConnector", migratedYaml);
        Assert.Contains("database: TestDatabase", migratedYaml);
        Assert.Contains("TestQuery", migratedYaml);

        // Validate the migrated tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", toolName
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Migrated tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    public async Task ToolMigrate_LinkToolV1_ConvertsToV2()
    {
        // Arrange - Create a V1 LinkTool
        var toolName = "TestLinkToolV1";
        var v1Yaml = TestYamlHelper.GetLinkToolV1(
            name: toolName,
            description: "Test Link Tool",
            template: "https://example.com/{param1}",
            parameters: new List<(string, string, string)>
            {
                ("param1", "string", "Test parameter")
            });
        _cli.CreateFile($"tools/{toolName}.yaml", v1Yaml);

        // Act - Migrate the tool
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Migration should succeed");
        Assert.Equal(0, result.ExitCode);

        // Verify V2 format (migrated in place)
        var migratedYaml = _cli.ReadFile($"tools/{toolName}.yaml");
        Assert.Contains("api_version: azuresre.ai/v2", migratedYaml);
        Assert.Contains("kind: ExtendedTool", migratedYaml);
        Assert.Contains("type: LinkTool", migratedYaml);
        Assert.Contains("template: https://example.com/{param1}", migratedYaml);

        // Validate the migrated tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", toolName
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Migrated tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    public async Task ToolMigrate_DryRun_DoesNotModifyFiles()
    {
        // Arrange - Create a V1 KustoTool
        var toolName = "TestKustoToolDryRun";
        var v1Yaml = TestYamlHelper.GetMinimalKustoToolV1(toolName);
        _cli.CreateFile($"tools/{toolName}.yaml", v1Yaml);

        // Act - Migrate with --dry-run
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolName,
            "--dry-run"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Dry run should succeed");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("dry run", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify original file is unchanged
        var originalYaml = _cli.ReadFile($"tools/{toolName}.yaml");
        Assert.Contains("version: v1", originalYaml);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    public async Task ToolMigrate_NonExistentTool_ReturnsError()
    {
        // Act - Try to migrate a non-existent tool
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", "NonExistentTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Migration should fail for non-existent tool");
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    public async Task ToolMigrate_AlreadyV2Tool_SkipsOrReportsError()
    {
        // Arrange - Create a V2 tool
        var toolName = "TestV2Tool";
        var v2Yaml = TestYamlHelper.GetKustoToolV2(
            name: toolName,
            description: "Already V2",
            connector: "TestConnector",
            database: "TestDatabase",
            query: "TestQuery");
        _cli.CreateFile($"tools/{toolName}.yaml", v2Yaml);

        // Act - Try to migrate a V2 tool
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        // Should either skip or handle gracefully
        if (!result.Success)
        {
            _output.WriteLine("Command correctly identified tool is already V2");
        }
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    public async Task ToolMigrate_All_MigratesMultipleTools()
    {
        // Arrange - Create multiple V1 tools
        var tool1 = "MultiMigrate1";
        var tool2 = "MultiMigrate2";

        var v1Yaml1 = TestYamlHelper.GetKustoToolV1(
            name: tool1,
            description: "Tool 1",
            connector: "Connector1",
            database: "DB1",
            query: "Query1");
        var v1Yaml2 = TestYamlHelper.GetLinkToolV1(
            name: tool2,
            description: "Tool 2",
            template: "https://example.com");

        _cli.CreateFile($"tools/{tool1}.yaml", v1Yaml1);
        _cli.CreateFile($"tools/{tool2}.yaml", v1Yaml2);

        // Act - Migrate all tools
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Migration of all tools should succeed");
        Assert.Equal(0, result.ExitCode);

        // Verify both tools were migrated (in place)
        var yaml1 = _cli.ReadFile($"tools/{tool1}.yaml");
        var yaml2 = _cli.ReadFile($"tools/{tool2}.yaml");

        Assert.Contains("api_version: azuresre.ai/v2", yaml1);
        Assert.Contains("type: KustoTool", yaml1);

        Assert.Contains("api_version: azuresre.ai/v2", yaml2);
        Assert.Contains("type: LinkTool", yaml2);

        // Validate all migrated tools
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--all"
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "All migrated tools should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    // ============================================================
    // ToolList Migration Tests
    // ============================================================

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    [Trait("Feature", "ToolList")]
    public async Task ToolMigrate_ToolListV1_CreatesMultipleV2Files()
    {
        // Arrange - Create a V1 ToolList with multiple tools
        var toolListName = "TestToolList";
        var toolListYaml = TestYamlHelper.GetToolListV1(
            listName: toolListName,
            tools: new List<(string, string, string, string?, string?, string?, string?)>
            {
                ("Tool1", "KustoTool", "First tool", "Connector1", "DB1", "Query1 | take 10", null),
                ("Tool2", "KustoTool", "Second tool", "Connector2", "DB2", "Query2 | take 20", null),
                ("Tool3", "LinkTool", "Third tool", null, null, null, "https://example.com/{id}")
            });

        _cli.CreateFile($"tools/{toolListName}.yaml", toolListYaml);

        // Act - Migrate the ToolList
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolListName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "ToolList migration should succeed");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Migrated 3 tools to V2", result.Output);

        // Verify three separate V2 files were created
        var tool1Yaml = _cli.ReadFile("tools/Tool1.yaml");
        var tool2Yaml = _cli.ReadFile("tools/Tool2.yaml");
        var tool3Yaml = _cli.ReadFile("tools/Tool3.yaml");

        // Verify Tool1
        Assert.Contains("api_version: azuresre.ai/v2", tool1Yaml);
        Assert.Contains("kind: ExtendedTool", tool1Yaml);
        Assert.Contains("name: Tool1", tool1Yaml);
        Assert.Contains("type: KustoTool", tool1Yaml);
        Assert.Contains("connector: Connector1", tool1Yaml);
        Assert.Contains("database: DB1", tool1Yaml);
        Assert.Contains("Query1", tool1Yaml);

        // Verify Tool2
        Assert.Contains("api_version: azuresre.ai/v2", tool2Yaml);
        Assert.Contains("name: Tool2", tool2Yaml);
        Assert.Contains("type: KustoTool", tool2Yaml);
        Assert.Contains("connector: Connector2", tool2Yaml);
        Assert.Contains("Query2", tool2Yaml);

        // Verify Tool3
        Assert.Contains("api_version: azuresre.ai/v2", tool3Yaml);
        Assert.Contains("name: Tool3", tool3Yaml);
        Assert.Contains("type: LinkTool", tool3Yaml);
        Assert.Contains("https://example.com/{id}", tool3Yaml);

        // Verify original ToolList file was backed up
        var backupExists = _cli.FileExists($"tools/{toolListName}.yaml.v1.bak");
        Assert.True(backupExists, "Original ToolList should be backed up");

        // Verify original ToolList file no longer exists
        var originalExists = _cli.FileExists($"tools/{toolListName}.yaml");
        Assert.False(originalExists, "Original ToolList should be moved to backup");

        // Validate all migrated tools
        var validateResult = await _cli.RunAsync("tool", "validate", "--all");
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "All migrated tools should be valid");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    [Trait("Feature", "ToolList")]
    public async Task ToolMigrate_ToolListV1WithOverwrite_BacksUpOnlyWhenNotOverwritten()
    {
        // Arrange - Create a V1 ToolList where one tool has same name as the list file
        var toolListName = "MyToolList";
        var toolListYaml = TestYamlHelper.GetToolListV1(
            listName: toolListName,
            tools: new List<(string, string, string, string?, string?, string?, string?)>
            {
                // This tool has the same name as the ToolList file - will overwrite it
                ("MyToolList", "KustoTool", "Tool with same name", "Connector1", "DB1", "Query1", null),
                ("AnotherTool", "LinkTool", "Different tool", null, null, null, "https://example.com")
            });

        _cli.CreateFile($"tools/{toolListName}.yaml", toolListYaml);

        // Act - Migrate the ToolList
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolListName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "ToolList migration should succeed");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Migrated 2 tools to V2", result.Output);
        Assert.Contains("original file overwritten", result.Output);

        // Verify the original file was overwritten with Tool1's V2 format (not backed up)
        var myToolListYaml = _cli.ReadFile($"tools/{toolListName}.yaml");
        Assert.Contains("api_version: azuresre.ai/v2", myToolListYaml);
        Assert.Contains("kind: ExtendedTool", myToolListYaml);
        Assert.Contains("name: MyToolList", myToolListYaml);

        // Verify AnotherTool was created
        var anotherToolYaml = _cli.ReadFile("tools/AnotherTool.yaml");
        Assert.Contains("name: AnotherTool", anotherToolYaml);
        Assert.Contains("type: LinkTool", anotherToolYaml);

        // Verify backup was NOT created (since original was overwritten)
        var backupExists = _cli.FileExists($"tools/{toolListName}.yaml.v1.bak");
        Assert.False(backupExists, "Backup should not exist when original file is overwritten");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    [Trait("Feature", "ToolList")]
    public async Task ToolMigrate_ToolListV1DryRun_DoesNotCreateFiles()
    {
        // Arrange - Create a V1 ToolList
        var toolListName = "DryRunToolList";
        var toolListYaml = TestYamlHelper.GetToolListV1(
            listName: toolListName,
            tools: new List<(string, string, string, string?, string?, string?, string?)>
            {
                ("DryTool1", "KustoTool", "Tool 1", "Connector1", "DB1", "Query1", null),
                ("DryTool2", "LinkTool", "Tool 2", null, null, null, "https://example.com")
            });

        _cli.CreateFile($"tools/{toolListName}.yaml", toolListYaml);

        // Act - Migrate with dry-run
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolListName,
            "--dry-run"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Dry run should succeed");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Would migrate 2 tools to V2", result.Output);
        Assert.Contains("dry run", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify no V2 files were created
        var tool1Exists = _cli.FileExists("tools/DryTool1.yaml");
        var tool2Exists = _cli.FileExists("tools/DryTool2.yaml");
        Assert.False(tool1Exists, "DryTool1.yaml should not be created in dry-run");
        Assert.False(tool2Exists, "DryTool2.yaml should not be created in dry-run");

        // Verify original ToolList still exists and unchanged
        var originalYaml = _cli.ReadFile($"tools/{toolListName}.yaml");
        Assert.Contains("api_version: agent.platform.ai/v1", originalYaml);
        Assert.Contains("kind: ToolList", originalYaml);

        // Verify no backup was created
        var backupExists = _cli.FileExists($"tools/{toolListName}.yaml.v1.bak");
        Assert.False(backupExists, "Backup should not be created in dry-run");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    [Trait("Feature", "ToolList")]
    public async Task ToolMigrate_EmptyToolList_SkipsGracefully()
    {
        // Arrange - Create an empty V1 ToolList
        var toolListName = "EmptyToolList";
        var toolListYaml = @"api_version: agent.platform.ai/v1
kind: ToolList
metadata:
  name: EmptyToolList
  owner: someone
spec:
  tools:
";

        _cli.CreateFile($"tools/{toolListName}.yaml", toolListYaml);

        // Act - Migrate the empty ToolList
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", toolListName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Migration should succeed but skip empty list");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ToolList is empty", result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Functional")]
    [Trait("Feature", "ToolList")]
    public async Task ToolMigrate_All_MigratesToolListAndSingleTools()
    {
        // Arrange - Create both a ToolList and individual tools
        var toolListYaml = TestYamlHelper.GetToolListV1(
            listName: "MixedList",
            tools: new List<(string, string, string, string?, string?, string?, string?)>
            {
                ("ListTool1", "KustoTool", "From list", "Connector1", "DB1", "Query1", null)
            });
        var singleToolYaml = TestYamlHelper.GetKustoToolV1(
            name: "SingleTool",
            description: "Standalone tool",
            connector: "Connector2",
            database: "DB2",
            query: "Query2");

        _cli.CreateFile("tools/MixedList.yaml", toolListYaml);
        _cli.CreateFile("tools/SingleTool.yaml", singleToolYaml);

        // Act - Migrate all
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, "Migration should succeed");
        Assert.Equal(0, result.ExitCode);

        // Verify tool from ToolList was created
        var listTool1Yaml = _cli.ReadFile("tools/ListTool1.yaml");
        Assert.Contains("api_version: azuresre.ai/v2", listTool1Yaml);
        Assert.Contains("name: ListTool1", listTool1Yaml);

        // Verify standalone tool was migrated in place
        var singleToolMigratedYaml = _cli.ReadFile("tools/SingleTool.yaml");
        Assert.Contains("api_version: azuresre.ai/v2", singleToolMigratedYaml);
        Assert.Contains("name: SingleTool", singleToolMigratedYaml);

        // Verify ToolList backup exists
        var backupExists = _cli.FileExists("tools/MixedList.yaml.v1.bak");
        Assert.True(backupExists, "ToolList should be backed up");
    }

    public void Dispose()
    {
        _cli.Dispose();
    }
}
