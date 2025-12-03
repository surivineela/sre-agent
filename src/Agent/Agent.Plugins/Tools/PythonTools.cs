// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Tools;

namespace Agent.Plugins.Python.Tools
{
    [ToolTypeAttribute("PythonFunctionTool")]
    public class PythonFunctionToolType : IYamlToolAware, IToolArgumentTransformer
    {
        private readonly ISessionPoolService _sessionPool;
        private PythonFunctionToolDefinition? _definition;

        public PythonFunctionToolType(ISessionPoolService sessionPool)
        {
            _sessionPool = sessionPool;
        }

        public void SetToolDefinition(YamlToolDefinitionBase definition)
        {
            _definition = (PythonFunctionToolDefinition)definition;
        }

        /// <summary>
        /// Transforms flat arguments from LLM into a single Dictionary for the Run method.
        /// Python tools expect all parameters as a Dictionary&lt;string, string&gt;.
        /// </summary>
        public object?[] TransformArguments(System.Reflection.MethodInfo method, Dictionary<string, object?> flatArgs, YamlToolDefinitionBase toolDef)
        {
            // Convert all arguments to string dictionary for Python execution
            var stringArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in flatArgs)
            {
                // Skip internal/system arguments
                if (kvp.Key.Equals("threadId", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("toolName", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (kvp.Value != null)
                {
                    stringArgs[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                }
            }

            // Return as single argument (the Run method takes Dictionary<string, string>)
            return new object?[] { stringArgs };
        }

        public async Task<string> Run(Dictionary<string, string> args)
        {
            if (_definition == null)
            {
                throw new InvalidOperationException("Tool definition was not set.");
            }

            try
            {
                // 1. Build the complete Python script with parameter injection
                var script = BuildPythonScript(_definition.FunctionCode, args);

                // 2. Create session identifier
                var identifier = _sessionPool.BuildSessionIdentifier(
                    agentName: $"python-tool-{_definition.Name}",
                    threadId: null,
                    randomSuffix: true
                );

                // 3. Execute via SessionPoolService
                var result = await _sessionPool.ExecutePythonInlineAsync(
                    script,
                    identifier,
                    _definition.TimeoutSeconds
                );

                // 4. Parse and return result
                return ParseExecutionResult(result);
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

            // 4. Call main() with converted params
            script.AppendLine("# Execute main function with type conversion");
            script.AppendLine("try:");
            script.AppendLine("    params = convert_params(main, params_raw)");
            script.AppendLine("    result = main(**params)");
            script.AppendLine("    print(json.dumps({'success': True, 'result': result}))");
            script.AppendLine("except Exception as e:");
            script.AppendLine("    print(json.dumps({'success': False, 'error': str(e), 'type': type(e).__name__}))");

            return script.ToString();
        }

        /// <summary>
        /// Parses the execution result from SessionPoolService
        /// </summary>
        private string ParseExecutionResult(CodeExecutionResponse response)
        {
            // Parse stdout as JSON (our script outputs JSON) - check stdout first before exit code
            try
            {
                var stdout = response.Stdout?.Trim() ?? string.Empty;

                // Try to parse as JSON
                if (!string.IsNullOrEmpty(stdout))
                {
                    var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var lastLine = lines.LastOrDefault()?.Trim() ?? string.Empty;

                    if (lastLine.StartsWith("{") && lastLine.EndsWith("}"))
                    {
                        // Last line looks like JSON, parse it
                        var jsonResult = JsonDocument.Parse(lastLine);
                        var root = jsonResult.RootElement;

                        if (root.TryGetProperty("success", out var successProp))
                        {
                            var isSuccess = successProp.GetBoolean();

                            if (isSuccess && root.TryGetProperty("result", out var resultProp))
                            {
                                // Success case - return the result with stdout/stderr for diagnostics
                                return JsonSerializer.Serialize(new
                                {
                                    success = true,
                                    result = resultProp,
                                    exit_code = response.ExitCode,
                                    stdout = response.Stdout,
                                    stderr = response.Stderr
                                });
                            }
                            else
                            {
                                // Error case from our try/except block
                                return JsonSerializer.Serialize(new
                                {
                                    success = false,
                                    error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error",
                                    error_type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "Exception",
                                    exit_code = response.ExitCode,
                                    stdout = response.Stdout,
                                    stderr = response.Stderr
                                });
                            }
                        }
                    }
                }

                // No valid JSON output - check exit code
                if (response.ExitCode != 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "Python execution failed",
                        exit_code = response.ExitCode,
                        stdout = response.Stdout,
                        stderr = response.Stderr
                    });
                }

                // Fallback: return raw stdout if we can't parse JSON
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    result = stdout,
                    raw_output = true,
                    exit_code = response.ExitCode,
                    stdout = response.Stdout,
                    stderr = response.Stderr
                });
            }
            catch (JsonException)
            {
                // JSON parsing failed - check exit code
                if (response.ExitCode != 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "Python execution failed",
                        exit_code = response.ExitCode,
                        stdout = response.Stdout,
                        stderr = response.Stderr
                    });
                }

                // Return raw output
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    result = response.Stdout,
                    raw_output = true,
                    exit_code = response.ExitCode,
                    stdout = response.Stdout,
                    stderr = response.Stderr
                });
            }
        }
    }
}
