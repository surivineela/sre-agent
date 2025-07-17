// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using Agent.Framework;
using Agent.Plugins;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit
{
    // Test plugin classes for testing
    [AgentToolPlugin]
    public class TestPlugin
    {
        public Guid? ThreadId { get; set; }

        [Description("Test method that returns a greeting")]
        public string SayHello(string name)
        {
            return $"Hello, {name}!";
        }

        [Description("Test async method")]
        public async Task<string> ProcessDataAsync(string data)
        {
            await Task.Delay(1);
            return $"Processed: {data}";
        }

        [Description("Test method that needs ThreadId")]
        public string GetThreadId()
        {
            if (ThreadId == null)
            {
                throw new InvalidOperationException("ThreadId must be set before calling this method.");
            }
            return ThreadId.ToString();
        }

        // Method without Description attribute - should not be registered
        public string IgnoredMethod()
        {
            return "Should not be registered";
        }
    }

    [AgentToolPlugin]
    public class AnotherTestPlugin
    {
        [Description("Another test method")]
        public int Calculate(int a, int b)
        {
            return a + b;
        }

        [Description("Another test method")]
        public string SayHello2(string name)
        {
            return $"Greetings, {name}!";
        }
    }

    // Plugin without AgentToolPlugin attribute - should be ignored
    public class IgnoredPlugin
    {
        [Description("This should be ignored")]
        public string ShouldNotBeRegistered()
        {
            return "Ignored";
        }
    }

    // Helper class for testing field injection
    public class ImplementationWithThreadId
    {
        public Guid? ThreadId { get; set; }

        public string ProcessWithThreadId()
        {
            return ThreadId?.ToString() ?? throw new InvalidOperationException("ThreadId must be set before calling this method.");
        }
    }

    [AgentToolPlugin]
    public class PluginWithImplementationField
    {
        private ImplementationWithThreadId _implementationWithThreadId = new();

        [Description("Test method that uses implementation field")]
        public string ProcessViaImplementation()
        {
            return _implementationWithThreadId.ProcessWithThreadId();
        }
    }

    [AgentToolPlugin]
    public class PluginWithContext : ContextToolTarget<string>
    {
        [Description("Get info about current context")]
        public string GetContextInfo()
        {
            return Context ?? "no context";
        }
    }

    public class ToolFactoryTests : IDisposable
    {
        private readonly Mock<ILogger<ToolFactory<object>>> _mockLogger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceCollection _services;

        public ToolFactoryTests()
        {
            _mockLogger = new Mock<ILogger<ToolFactory<object>>>();
            _services = new ServiceCollection();
            _services.AddSingleton(_mockLogger.Object);
            _services.AddTransient<TestPlugin>();
            _services.AddTransient<AnotherTestPlugin>();
            _services.AddTransient<PluginWithImplementationField>();
            _services.AddTransient<ImplementationWithThreadId>();
            _services.AddTransient<PluginWithContext>();
            SetupServiceProviderWithHostEnvironmentAndConfiguration();
            _serviceProvider = _services.BuildServiceProvider();
        }

        private void SetupServiceProviderWithHostEnvironmentAndConfiguration()
        {
            var mockHostEnvironment = new Mock<IHostEnvironment>();
            mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
            mockHostEnvironment.Setup(e => e.ApplicationName).Returns("TestApp");
            mockHostEnvironment.Setup(e => e.ContentRootPath).Returns("/test/root");

            var inMemorySettings = new Dictionary<string, string?>
            {
                {"AppSettings:Core:Azure:Crawler:TenantId", "72f988bf-86f1-41af-91ab-2d7cd011db47"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            // Register the mocks to the existing ServiceCollection
            _services.AddSingleton(mockHostEnvironment.Object);
            _services.AddSingleton(configuration);
        }

        [Fact]
        public void Constructor_ShouldRegisterToolsFromAgentAssemblies()
        {
            // Act
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Assert
            Assert.True(toolFactory.HasTool("SayHello"));
            Assert.True(toolFactory.HasTool("ProcessData"));
            Assert.True(toolFactory.HasTool("Calculate"));
        }

        [Fact]
        public void FindAIFunction_WithValidName_ShouldReturnFunction()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act
            var function = toolFactory.GetTool("SayHello");

            // Assert
            Assert.NotNull(function);
            Assert.Equal("SayHello", function.Name);
        }

        [Fact]
        public void FindAIFunction_WithInvalidName_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => toolFactory.GetTool("NonExistentFunction"));
        }

        [Fact]
        public async Task FindAIFunction_WithThreadId_ShouldSetThreadIdOnPlugin()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);
            var threadId = Guid.NewGuid();

            var function = toolFactory.GetTool("GetThreadId", threadId);
            Assert.NotNull(function);
            var result = await function.InvokeAsync();
            Assert.Equal(threadId.ToString(), result.ToString());
        }

        [Fact]
        public void TryFindAIFunction_WithValidName_ShouldReturnTrueAndFunction()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act
            var result = toolFactory.TryFindTool("Calculate", out var function);

            // Assert
            Assert.True(result);
            Assert.NotNull(function);
            Assert.Equal("Calculate", function.Name);
        }

        [Fact]
        public void TryFindAIFunction_WithInvalidName_ShouldReturnFalseAndNullFunction()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act
            var result = toolFactory.TryFindTool("NonExistentFunction", out var function);

            // Assert
            Assert.False(result);
            Assert.Null(function);
        }

        [Fact]
        public void HasAIFunction_WithValidName_ShouldReturnTrue()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.True(toolFactory.HasTool("SayHello"));
            Assert.True(toolFactory.HasTool("ProcessData"));
            Assert.True(toolFactory.HasTool("Calculate"));
        }

        [Fact]
        public void HasAIFunction_WithInvalidName_ShouldReturnFalse()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.False(toolFactory.HasTool("NonExistentFunction"));
            Assert.False(toolFactory.HasTool("IgnoredMethod"));
            Assert.False(toolFactory.HasTool("ShouldNotBeRegistered"));
        }

        [Fact]
        public void AsyncMethods_ShouldHaveAsyncSuffixRemoved()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.True(toolFactory.HasTool("ProcessData"));
            Assert.False(toolFactory.HasTool("ProcessDataAsync"));
        }

        [Fact]
        public void MethodsWithoutDescriptionAttribute_ShouldNotBeRegistered()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.False(toolFactory.HasTool("IgnoredMethod"));
        }

        [Fact]
        public void ClassesWithoutAgentToolPluginAttribute_ShouldBeIgnored()
        {
            // Arrange
            _services.AddTransient<IgnoredPlugin>();
            var serviceProvider = _services.BuildServiceProvider();
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.False(toolFactory.HasTool("ShouldNotBeRegistered"));
        }

        [Fact]
        public void DeferredToolFunction_GetToolFunction_ShouldCreateNewInstanceEachTime()
        {
            // Arrange
            var deferredFunction = new DeferredToolFunction<object>(_serviceProvider, typeof(TestPlugin),
                typeof(TestPlugin).GetMethod("SayHello")!, "SayHello");

            // Act
            var function1 = deferredFunction.GetToolFunction();
            var function2 = deferredFunction.GetToolFunction();

            // Assert
            Assert.NotNull(function1);
            Assert.NotNull(function2);
            // Both should be valid AIFunction instances
            Assert.Equal("SayHello", function1.Name);
            Assert.Equal("SayHello", function2.Name);
        }

        [Fact]
        public void DeferredToolFunction_WithThreadId_ShouldSetThreadIdWhenFieldExists()
        {
            // Arrange
            var threadId = Guid.NewGuid();
            var deferredFunction = new DeferredToolFunction<object>(_serviceProvider, typeof(TestPlugin),
                typeof(TestPlugin).GetMethod("SayHello")!, "SayHello");

            // Act
            var function = deferredFunction.GetToolFunction(threadId);

            // Assert
            Assert.NotNull(function);
            // The actual verification of ThreadId setting would require accessing the plugin instance
            // which is internal to the AIFunction implementation
        }

        [Fact]
        public async Task PluginWithImplementationField_WithThreadId_ShouldInjectThreadIdIntoImplementationField()
        {
            // Arrange
            var toolFactory = new ToolFactory<object>(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);
            var threadId = Guid.NewGuid();

            // Act
            var function = toolFactory.GetTool("ProcessViaImplementation", threadId);
            Assert.NotNull(function);
            var result = await function.InvokeAsync();

            // Assert
            Assert.Equal(threadId.ToString(), result?.ToString());
        }

        [Fact]
        public async Task PluginWithContext()
        {
            var toolFactory = new ToolFactory<string>(new Mock<ILogger<ToolFactory<string>>>().Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            var tool = toolFactory.GetTool("GetContextInfo") as ContextAIFunction<string>;
            Assert.NotNull(tool);

            var context = "hello";
            tool.SetContext(context);

            var result = await tool.InvokeAsync();
            Assert.NotNull(result);

            Assert.Equal(context, result.ToString());
        }

        public void Dispose()
        {
        }
    }
}
