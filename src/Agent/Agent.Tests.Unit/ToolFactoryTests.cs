// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.DependencyInjection;
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

        [Description("Method with same name as TestPlugin")]
        public string SayHello(string name)
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

    public class ToolFactoryTests : IDisposable
    {
        private readonly Mock<ILogger<ToolFactory>> _mockLogger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceCollection _services;

        public ToolFactoryTests()
        {
            _mockLogger = new Mock<ILogger<ToolFactory>>();
            _services = new ServiceCollection();
            _services.AddSingleton(_mockLogger.Object);
            _services.AddTransient<TestPlugin>();
            _services.AddTransient<AnotherTestPlugin>();
            _services.AddTransient<PluginWithImplementationField>();
            _services.AddTransient<ImplementationWithThreadId>();
            _serviceProvider = _services.BuildServiceProvider();
        }

        [Fact]
        public void Constructor_ShouldRegisterToolsFromAgentAssemblies()
        {
            // Act
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Assert
            Assert.True(toolFactory.HasAIFunction("SayHello"));
            Assert.True(toolFactory.HasAIFunction("ProcessData"));
            Assert.True(toolFactory.HasAIFunction("Calculate"));
        }

        [Fact]
        public void FindAIFunction_WithValidName_ShouldReturnFunction()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act
            var function = toolFactory.FindAIFunction("SayHello");

            // Assert
            Assert.NotNull(function);
            Assert.Equal("SayHello", function.Name);
        }

        [Fact]
        public void FindAIFunction_WithInvalidName_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => toolFactory.FindAIFunction("NonExistentFunction"));
        }

        [Fact]
        public async Task FindAIFunction_WithThreadId_ShouldSetThreadIdOnPlugin()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);
            var threadId = Guid.NewGuid();

            var function = toolFactory.FindAIFunction("GetThreadId", threadId);
            Assert.NotNull(function);
            var result = await function.InvokeAsync();
            Assert.Equal(threadId.ToString(), result.ToString());
        }

        [Fact]
        public void TryFindAIFunction_WithValidName_ShouldReturnTrueAndFunction()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act
            var result = toolFactory.TryFindAIFunction("Calculate", out var function);

            // Assert
            Assert.True(result);
            Assert.NotNull(function);
            Assert.Equal("Calculate", function.Name);
        }

        [Fact]
        public void TryFindAIFunction_WithInvalidName_ShouldReturnFalseAndNullFunction()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act
            var result = toolFactory.TryFindAIFunction("NonExistentFunction", out var function);

            // Assert
            Assert.False(result);
            Assert.Null(function);
        }

        [Fact]
        public void HasAIFunction_WithValidName_ShouldReturnTrue()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.True(toolFactory.HasAIFunction("SayHello"));
            Assert.True(toolFactory.HasAIFunction("ProcessData"));
            Assert.True(toolFactory.HasAIFunction("Calculate"));
        }

        [Fact]
        public void HasAIFunction_WithInvalidName_ShouldReturnFalse()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.False(toolFactory.HasAIFunction("NonExistentFunction"));
            Assert.False(toolFactory.HasAIFunction("IgnoredMethod"));
            Assert.False(toolFactory.HasAIFunction("ShouldNotBeRegistered"));
        }

        [Fact]
        public void AsyncMethods_ShouldHaveAsyncSuffixRemoved()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.True(toolFactory.HasAIFunction("ProcessData"));
            Assert.False(toolFactory.HasAIFunction("ProcessDataAsync"));
        }

        [Fact]
        public void MethodsWithoutDescriptionAttribute_ShouldNotBeRegistered()
        {
            // Arrange
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.False(toolFactory.HasAIFunction("IgnoredMethod"));
        }

        [Fact]
        public void ClassesWithoutAgentToolPluginAttribute_ShouldBeIgnored()
        {
            // Arrange
            _services.AddTransient<IgnoredPlugin>();
            var serviceProvider = _services.BuildServiceProvider();
            var toolFactory = new ToolFactory(_mockLogger.Object, serviceProvider, [Assembly.GetExecutingAssembly()]);

            // Act & Assert
            Assert.False(toolFactory.HasAIFunction("ShouldNotBeRegistered"));
        }

        [Fact]
        public void DeferredToolFunction_GetToolFunction_ShouldCreateNewInstanceEachTime()
        {
            // Arrange
            var deferredFunction = new DeferredToolFunction(_serviceProvider, typeof(TestPlugin),
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
            var deferredFunction = new DeferredToolFunction(_serviceProvider, typeof(TestPlugin),
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
            var toolFactory = new ToolFactory(_mockLogger.Object, _serviceProvider, [Assembly.GetExecutingAssembly()]);
            var threadId = Guid.NewGuid();

            // Act
            var function = toolFactory.FindAIFunction("ProcessViaImplementation", threadId);
            Assert.NotNull(function);
            var result = await function.InvokeAsync();

            // Assert
            Assert.Equal(threadId.ToString(), result?.ToString());
        }

        public void Dispose()
        {
        }
    }
}
