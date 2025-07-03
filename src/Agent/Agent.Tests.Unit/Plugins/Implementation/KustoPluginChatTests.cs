// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.IO;
using Agent.Plugins.Kusto;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class KustoPluginChatTests : IDisposable
    {
        private readonly string _testBaseDirectory;

        public KustoPluginChatTests()
        {
            // Create a temporary test directory structure
            _testBaseDirectory = Path.Combine(Path.GetTempPath(), "KustoPluginTests", Guid.NewGuid().ToString());
            var pluginsDir = Path.Combine(_testBaseDirectory, "Plugins", "Definitions", "Queries");
            Directory.CreateDirectory(pluginsDir);

            // Create test KQL files
            CreateTestKqlFiles(pluginsDir);
        }

        public void Dispose()
        {
            // Clean up test files
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, true);
            }
        }

        private void CreateTestKqlFiles(string baseDir)
        {
            // Create flat structure files
            File.WriteAllText(Path.Combine(baseDir, "SimpleQuery.kql"), "// Simple query");
            File.WriteAllText(Path.Combine(baseDir, "GetAdminEventErrorMessagesByTraceId.kql"), "// Existing query");

            // Create hierarchical structure
            var containerAppsDir = Path.Combine(baseDir, "ContainerApps", "Monitoring");
            Directory.CreateDirectory(containerAppsDir);
            File.WriteAllText(Path.Combine(containerAppsDir, "GetHealthStatus.kql"), "// Health status query");

            var aksDir = Path.Combine(baseDir, "AKS", "Diagnostics");
            Directory.CreateDirectory(aksDir);
            File.WriteAllText(Path.Combine(aksDir, "GetNodeStatus.kql"), "// Node status query");

            var deepDir = Path.Combine(baseDir, "Level1", "Level2", "Level3");
            Directory.CreateDirectory(deepDir);
            File.WriteAllText(Path.Combine(deepDir, "DeepQuery.kql"), "// Deep nested query");
        }

        [Fact]
        public void GetKqlFilePath_SimpleFileName_ReturnsDirectPath()
        {
            // Arrange
            var functionName = "SimpleQuery";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.EndsWith("SimpleQuery.kql", result);
            Assert.True(File.Exists(result), "File should exist");
        }

        [Fact]
        public void GetKqlFilePath_ExistingFile_ReturnsCorrectPath()
        {
            // Arrange
            var functionName = "GetAdminEventErrorMessagesByTraceId";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.EndsWith("GetAdminEventErrorMessagesByTraceId.kql", result);
            Assert.True(File.Exists(result), "File should exist");
        }

        [Fact]
        public void GetKqlFilePath_NamespaceFormat_ReturnsHierarchicalPath()
        {
            // Arrange
            var functionName = "ContainerApps.Monitoring.GetHealthStatus";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("ContainerApps", result);
            Assert.Contains("Monitoring", result);
            Assert.EndsWith("GetHealthStatus.kql", result);
            Assert.True(File.Exists(result), "Namespaced file should exist");
        }

        [Fact]
        public void GetKqlFilePath_DeepNamespace_ReturnsCorrectPath()
        {
            // Arrange
            var functionName = "Level1.Level2.Level3.DeepQuery";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("Level1", result);
            Assert.Contains("Level2", result);
            Assert.Contains("Level3", result);
            Assert.EndsWith("DeepQuery.kql", result);
            Assert.True(File.Exists(result), "Deep nested file should exist");
        }

        [Fact]
        public void GetKqlFilePath_NonExistentFile_ReturnsFallbackPath()
        {
            // Arrange
            var functionName = "NonExistentQuery";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.EndsWith("NonExistentQuery.kql", result);
            Assert.False(File.Exists(result), "File should not exist (fallback path)");
        }

        [Fact]
        public void GetKqlFilePath_NonExistentNamespace_ReturnsFallbackPath()
        {
            // Arrange
            var functionName = "NonExistent.Namespace.Query";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.EndsWith("Query.kql", result);
            Assert.False(File.Exists(result), "Namespace file should not exist (fallback to direct path)");
            // Should fallback to direct path since namespace file doesn't exist
        }

        [Fact]
        public void GetKqlFilePath_EmptyString_ReturnsPath()
        {
            // Arrange
            var functionName = "";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.EndsWith(".kql", result);
        }

        [Fact]
        public void GetKqlFilePath_SingleDot_ReturnsCorrectPath()
        {
            // Arrange
            var functionName = "Category.Query";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("Category", result);
            Assert.EndsWith("Query.kql", result);
        }

        [Fact]
        public void GetKqlFilePath_PreferDirectPathOverNamespace()
        {
            // This test verifies that if both a direct file and a namespaced file exist,
            // the direct file takes precedence (backward compatibility)
            
            // Arrange
            var functionName = "GetAdminEventErrorMessagesByTraceId";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.True(File.Exists(result), "Direct file should exist");
            
            // Should prefer the direct path (no subdirectories in the path after Queries/)
            var queriesIndex = result.IndexOf("Queries");
            var afterQueries = result.Substring(queriesIndex + "Queries".Length + 1);
            Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), afterQueries);
        }

        [Fact]
        public void GetKqlFilePath_AKSNamespace_ReturnsCorrectPath()
        {
            // Arrange
            var functionName = "AKS.Diagnostics.GetNodeStatus";
            
            // Act
            var result = KustoPluginChat.GetKqlFilePath(functionName, _testBaseDirectory);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("AKS", result);
            Assert.Contains("Diagnostics", result);
            Assert.EndsWith("GetNodeStatus.kql", result);
            Assert.True(File.Exists(result), "AKS namespace file should exist");
        }
    }
}
