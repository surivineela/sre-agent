using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OperationalAgentCore;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

var config = builder.Configuration;
config.Sources.Clear();

config.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
            .AddEnvironmentVariables();

// Add configuration
builder.Services.AddApplicationConfiguration(config);

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

SemanticKernelHelper.ConfigService(builder.Services);

var host = builder.Build();
// await host.StartAsync();

// Start services (this runs RemediationWorker in the background)  
// await host.StartAsync();

// Main interaction loop
using (var scope = host.Services.CreateScope())
{
    var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
    var tc = scope.ServiceProvider.GetRequiredService<TeamsConnector>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();

    if (azureSettings == null)
    {
        throw new NullReferenceException("Azure settings are required.");
    }

    while (true)
    {
        Console.WriteLine("\nAvailable commands:");
        Console.WriteLine("1. List available functions");
        Console.WriteLine("2. Execute function");
        Console.WriteLine("3. Demo mode");
        Console.WriteLine("4. Exit");
        Console.Write("\nEnter your choice (1-4): ");

        var choice = Console.ReadLine();
        switch (choice)
        {
            case "1":
                var subPlugin = scope.ServiceProvider.GetRequiredService<SubscriptionPlugin>();
                await subPlugin.GetAppIdentityAsync("/subscriptions/5abde51d-cc72-4bcc-b0d7-3c86b4db2a7c/resourceGroups/appservices-sre-demo/providers/Microsoft.Web/sites/oa-demo-web-sanchit");
                //ListAvailableFunctions(kernel);
                break;
            case "2":
                await ExecuteFunctionAsync(kernel, logger);
                break;
            case "3":
                await DemoExec2.Execute(kernel, config, logger);
                return;
            case "4":
                using (var repoScope = host.Services.CreateScope())
                {
                    var repo = repoScope.ServiceProvider.GetRequiredService<CodeAnalyzerPlugin>();
                    await repo.RequestGitHubTokenAsync("Ingest repo to analyze the MI Migration flow", "https://github.com/sanchitmehta/sample-app-memory-leak");
                    var analysisResult = "To analyze the memory dumps and identify potential memory leaks, we need to compare the object counts and their associated memory usage between the two dumps. Objects with significant growth in both count and memory that are not expected to persist across requests or operations may indicate a memory leak.\r\n\r\nHere are the objects that show significant increases and could potentially be candidates for memory leaks:\r\n\r\n---\r\n\r\n### **1. `System.Byte[]`**\r\n- **Memory Dump 1**: 7,211 objects, 424,258 bytes\r\n- **Memory Dump 2**: 7,845 objects, 10,968,894 bytes\r\n- **Increase**: +634 objects, +10,544,636 bytes\r\n\r\nA significant increase in memory usage for `System.Byte[]` suggests that large byte arrays are being retained in memory. This could indicate improper cleanup of buffers or data being accumulated unnecessarily.\r\n\r\n---\r\n\r\n### **2. `System.String`**\r\n- **Memory Dump 1**: 9,425 objects, 730,502 bytes\r\n- **Memory Dump 2**: 10,293 objects, 823,526 bytes\r\n- **Increase**: +868 objects, +93,024 bytes\r\n\r\nThe increase in `System.String` instances may suggest that strings are being retained unexpectedly. This could be due to caching, logging, or references in static fields.\r\n\r\n---\r\n\r\n### **3. `System.Object`**\r\n- **Memory Dump 1**: 19,768 objects, 474,432 bytes\r\n- **Memory Dump 2**: 21,040 objects, 504,960 bytes\r\n- **Increase**: +1,272 objects, +30,528 bytes\r\n\r\nA noticeable increase in generic `System.Object` instances may indicate that objects are being allocated but not properly released.\r\n\r\n---\r\n\r\n### **4. `System.IO.Pipelines.Pipe`**\r\n- **Memory Dump 1**: 3,238 objects, 932,544 bytes\r\n- **Memory Dump 2**: 3,450 objects, 993,600 bytes\r\n- **Increase**: +212 objects, +61,056 bytes\r\n\r\nPipes are commonly used for I/O operations, and their growth might point to improper disposal or an accumulation of resources in the pipelines.\r\n\r\n---\r\n\r\n### **5. `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.Http1Connection<Microsoft.AspNetCore.Hosting.HostingApplication+Context>`**\r\n- **Memory Dump 1**: 1,619 objects, 1,359,960 bytes\r\n- **Memory Dump 2**: 1,725 objects, 1,449,000 bytes\r\n- **Increase**: +106 objects, +89,040 bytes\r\n\r\nAn increase in `Http1Connection` objects may suggest that HTTP connections are not being closed or cleaned up properly, leading to resource retention.\r\n\r\n---\r\n\r\n### **6. `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpRequestHeaders`**\r\n- **Memory Dump 1**: 1,619 objects, 790,072 bytes\r\n- **Memory Dump 2**: 1,725 objects, 841,800 bytes\r\n- **Increase**: +106 objects, +51,728 bytes\r\n\r\nThe growth in `HttpRequestHeaders` objects is often correlated with the growth in HTTP connections, further suggesting a potential issue with connection cleanup.\r\n\r\n---\r\n\r\n### **7. `Microsoft.Extensions.Logging.LoggerFactoryScopeProvider+Scope`**\r\n- **Memory Dump 1**: 1,621 objects, 77,808 bytes\r\n- **Memory Dump 2**: 1,731 objects, 83,088 bytes\r\n- **Increase**: +110 objects, +5,280 bytes\r\n\r\nThe increase in `LoggerFactoryScopeProvider+Scope` objects might indicate that logging scopes are not being disposed of correctly, causing memory retention.\r\n\r\n---\r\n\r\n### **8. `System.Net.IPAddress` and `System.Net.IPEndPoint`**\r\n- **`System.Net.IPAddress`**:\r\n  - **Memory Dump 1**: 4,863 objects, 194,520 bytes\r\n  - **Memory Dump 2**: 5,181 objects, 207,240 bytes\r\n  - **Increase**: +318 objects, +12,720 bytes\r\n- **`System.Net.IPEndPoint`**:\r\n  - **Memory Dump 1**: 4,860 objects, 155,520 bytes\r\n  - **Memory Dump 2**: 5,178 objects, 165,696 bytes\r\n  - **Increase**: +318 objects, +10,176 bytes\r\n\r\nThe growth in `IPAddress` and `IPEndPoint` instances suggests that networking-related objects are being retained. This could be due to sockets or connections not being released properly.\r\n\r\n---\r\n\r\n### **9. `System.Threading.CancellationTokenSource`**\r\n- **Memory Dump 1**: 3,264 objects, 156,672 bytes\r\n- **Memory Dump 2**: 3,478 objects, 166,944 bytes\r\n- **Increase**: +214 objects, +10,272 bytes\r\n\r\nThe increase in `CancellationTokenSource` objects may indicate that tokens are being created but not disposed of after use.\r\n\r\n---\r\n\r\n### **10. `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.DateHeaderValueManager+DateHeaderValues`**\r\n- **Memory Dump 1**: 5,381 objects, 172,192 bytes\r\n- **Memory Dump 2**: 5,722 objects, 183,104 bytes\r\n- **Increase**: +341 objects, +10,912 bytes\r\n\r\nThe increase in `DateHeaderValues` suggests that header-related resources are not being reused efficiently.\r\n\r\n---\r\n\r\n## **Summary of Potential Leaks**\r\n\r\nThe objects most likely to be leaking include:\r\n1. **`System.Byte[]`** – Large increase in memory usage.\r\n2. **`System.String`** – Noticeable increase in string instances.\r\n3. **`System.Object`** – Generic objects growing in count.\r\n4. **`System.IO.Pipelines.Pipe`** – Indicating a potential issue with pipeline cleanup.\r\n5. **HTTP-related objects** (`Http1Connection`, `HttpRequestHeaders`, etc.) – Suggesting connection cleanup problems.\r\n6. **Logging-related objects** (`LoggerFactoryScopeProvider+Scope`) – Possible improper disposal of logging scopes.\r\n7. **Networking-related objects** (`IPAddress`, `IPEndPoint`) – Retained sockets or connections.\r\n8. **`CancellationTokenSource`** – Tokens not being disposed of properly.\r\n\r\nThese findings suggest areas in the application where resource cleanup and proper disposal mechanisms should be reviewed.\r\n";
                    //await repo.AnalyzeAndFixMemoryLeaksAsync("https://github.com/sanchitmehta/sample-app-memory-leak", "main", "leak-fix-1", analysisResult);
                }
                return;
            default:
                Console.WriteLine("Invalid choice. Please try again.");
                break;
        }
    }
}

static void ListAvailableFunctions(Kernel kernel)
{
    var functions = kernel.Plugins.GetFunctionsMetadata()
        .GroupBy(f => f.PluginName)
        .Select(g => new
        {
            SkillName = g.Key,
            Functions = g.Select(f => new
            {
                f.Name,
                Description = f.Description,
                Parameters = f.Parameters.Select(p => new { p.Name, p.Description })
            }).ToList()
        });

    var options = new JsonSerializerOptions { WriteIndented = true };
    Console.WriteLine(JsonSerializer.Serialize(functions, options));
}

static async Task ExecuteFunctionAsync(Kernel kernel, ILogger logger)
{
    Console.Write("Enter the skill name: ");
    var skillName = Console.ReadLine();

    Console.Write("Enter the function name: ");
    var functionName = Console.ReadLine();

    if (string.IsNullOrEmpty(skillName) || string.IsNullOrEmpty(functionName))
    {
        Console.WriteLine("Skill name and function name are required.");
        return;
    }

    try
    {
        var function = kernel.Plugins.GetFunction(skillName, functionName);

        // Get parameters
        var parameters = new KernelArguments();
        foreach (var param in function.Metadata.Parameters)
        {
            Console.Write($"Enter value for parameter '{param.Name}' ({param.Description}): ");
            var value = Console.ReadLine();
            if (!string.IsNullOrEmpty(value))
            {
                parameters[param.Name] = value;
            }
        }

        // Execute function
        var result = await kernel.InvokeAsync(function, parameters);

        Console.WriteLine("\nResult:");
        Console.WriteLine(result.ToString());
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error executing function {SkillName}.{FunctionName}", skillName, functionName);
        Console.WriteLine($"Error: {ex.Message}");
    }
}