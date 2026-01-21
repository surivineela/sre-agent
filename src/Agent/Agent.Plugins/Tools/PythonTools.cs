// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Helpers;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Python.Tools
{
    /// <summary>
    /// Factory for creating PythonTool instances.
    /// </summary>
    [ToolType("PythonFunctionTool")]
    public class PythonToolExecutorFactory : IYamlToolExecutorFactory
    {
        public IYamlToolExecutor Create(YamlToolDefinitionBase definition, IServiceProvider serviceProvider)
        {
            var sessionPool = serviceProvider.GetRequiredService<ISessionPoolService>();
            var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var threadFileStorageService = serviceProvider.GetRequiredService<IThreadFileStorageService>();
            var logger = serviceProvider.GetRequiredService<ILogger<PythonFunctionTool>>();
            var pythonDefinition = (PythonFunctionToolDefinition)definition;

            return new PythonFunctionTool(
                sessionPool,
                hostEnvironment,
                threadFileStorageService,
                logger,
                pythonDefinition);
        }
    }

    /// <summary>
    /// Python tool implementation that extends YamlToolExecutor.
    /// Uses factory pattern for instantiation and provides clean ExecuteAsync interface.
    /// </summary>
    public class PythonFunctionTool : YamlToolExecutor<PythonFunctionToolDefinition>
    {
        private readonly ISessionPoolService _sessionPool;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IThreadFileStorageService _threadFileStorageService;
        private readonly ILogger<PythonFunctionTool> _logger;
        private readonly bool _testMode;

        public PythonFunctionTool(
            ISessionPoolService sessionPool,
            IHostEnvironment hostEnvironment,
            IThreadFileStorageService threadFileStorageService,
            ILogger<PythonFunctionTool> logger,
            PythonFunctionToolDefinition definition,
            bool testMode = false) : base(definition)
        {
            _sessionPool = sessionPool;
            _hostEnvironment = hostEnvironment;
            _threadFileStorageService = threadFileStorageService;
            _logger = logger;
            _testMode = testMode;
        }

        public override async Task<object?> ExecuteAsync(string threadId, AIFunctionArguments parameters)
        {
            // Convert AIFunctionArguments to Dictionary<string, string> using base class helper
            var paramsDict = ConvertToStringDictionary(parameters);

            try
            {
                // 1. Build the complete Python script with parameter injection
                var script = BuildPythonScript(ToolDefinition.FunctionCode, paramsDict);

                // 2. Create session identifier - use stable identifier with threadId like CodeInterpreterPlugin
                // This ensures all executions for the same thread reuse the same session
                var agentName = AgentNameHelper.GetAgentName(!_hostEnvironment.IsDevelopment());
                var identifier = _sessionPool.BuildSessionIdentifier(
                    agentName: agentName,
                    threadId: threadId?.ToString(),
                    randomSuffix: false
                );

                // 3. Execute via SessionPoolService
                var result = await _sessionPool.ExecutePythonInlineAsync(
                    script,
                    identifier,
                    ToolDefinition.TimeoutSeconds
                );

                // 4. Process image results and auto-retrieve session files (same as ExecutePythonCodeAsync)
                if (!_testMode && !string.IsNullOrEmpty(threadId) && Guid.TryParse(threadId, out var parsedThreadId))
                {
                    await SessionFileHelper.ProcessExecutionFilesAsync(
                        result,
                        _sessionPool,
                        identifier,
                        parsedThreadId,
                        _threadFileStorageService,
                        _logger);
                }

                // 5. Return result
                return result;
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    error = true,
                    message = ex.Message,
                    type = ex.GetType().Name
                });
            }
        }

        /// <summary>
        /// Builds the complete Python script by injecting user function + parameters + invocation
        /// </summary>
        private string BuildPythonScript(string functionCode, Dictionary<string, string> args)
        {
            var script = new StringBuilder();

            // 1. Add user's function code
            script.AppendLine(functionCode);
            script.AppendLine();

            // 2. Add parameter injection and invocation
            script.AppendLine("# Auto-generated invocation");
            script.AppendLine("import json");
            script.AppendLine("import inspect");
            script.AppendLine();

            // Build parameters dict with proper JSON escaping
            script.AppendLine("# Parameters from LLM (as strings)");
            script.Append("params_raw = ");
            script.AppendLine(JsonSerializer.Serialize(args, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
            script.AppendLine();

            // 3. Smart type conversion based on function signature
            script.AppendLine("# Convert parameter types based on function signature");
            script.AppendLine("def convert_params(func, raw_params):");
            script.AppendLine("    \"\"\"Convert string parameters to correct types based on function signature\"\"\"");
            script.AppendLine("    sig = inspect.signature(func)");
            script.AppendLine("    converted = {}");
            script.AppendLine("    ");
            script.AppendLine("    for key, value in raw_params.items():");
            script.AppendLine("        if key not in sig.parameters:");
            script.AppendLine("            converted[key] = value  # Keep unknown params as-is");
            script.AppendLine("            continue");
            script.AppendLine("        ");
            script.AppendLine("        param = sig.parameters[key]");
            script.AppendLine("        param_type = param.annotation");
            script.AppendLine("        ");
            script.AppendLine("        # Handle empty string for optional params");
            script.AppendLine("        if value == '' and param.default != inspect.Parameter.empty:");
            script.AppendLine("            continue  # Skip empty optional params, use default");
            script.AppendLine("        ");
            script.AppendLine("        # Type conversion");
            script.AppendLine("        try:");
            script.AppendLine("            if param_type == int or param_type == 'int':");
            script.AppendLine("                converted[key] = int(value)");
            script.AppendLine("            elif param_type == float or param_type == 'float' or param_type == 'double':");
            script.AppendLine("                converted[key] = float(value)");
            script.AppendLine("            elif param_type == bool or param_type == 'bool':");
            script.AppendLine("                converted[key] = value.lower() in ('true', '1', 'yes', 'on')");
            script.AppendLine("            elif param_type == str or param_type == 'str' or param_type == inspect.Parameter.empty:");
            script.AppendLine("                converted[key] = value  # Keep as string");
            script.AppendLine("            else:");
            script.AppendLine("                # Try JSON parse for complex types (list, dict)");
            script.AppendLine("                try:");
            script.AppendLine("                    converted[key] = json.loads(value)");
            script.AppendLine("                except:");
            script.AppendLine("                    converted[key] = value  # Fallback to string");
            script.AppendLine("        except (ValueError, TypeError) as e:");
            script.AppendLine("            raise TypeError(f\"Parameter '{key}' expected {param_type} but got '{value}': {e}\")");
            script.AppendLine("    ");
            script.AppendLine("    return converted");
            script.AppendLine();

            script.AppendLine("def call_main_with_params(params):"); // Temp workaround for code interpreter bug: if the main returned result and generate image at the same time, the image is not returned
            script.AppendLine("    result = main(**params)");
            script.AppendLine("    if result is not None:");
            script.AppendLine("        return json.dumps(result)"); // If directly return result, Jupyter server returns str() of it instead of a valid Json string 
            script.AppendLine();

            // 4. Call main() with converted params
            script.AppendLine("# Execute main function with type conversion");
            script.AppendLine("params = convert_params(main, params_raw)");
            script.AppendLine("call_main_with_params(params)");

            return script.ToString();
        }
    }
}
