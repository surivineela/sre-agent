using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OperationalAgentRuntime.Configuration;
using OperationalAgentRuntime.Configuration.Settings;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using OperationalAgentRuntime.Cli;
using OperationalAgentRuntime.Cli.DemoExec.Tasks;
using OperationalAgentCore;

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
    var azureSettings = scope.ServiceProvider.GetRequiredService<IOptions<AzureSettings>>().Value;

    var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

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
                ListAvailableFunctions(kernel);
                break;
            case "2":
                await ExecuteFunctionAsync(kernel, logger);
                break;
            case "3":
                await DemoExec2.Execute(kernel, logger);
                return;
            case "4":
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