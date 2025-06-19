// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Data.Repositories;
using Agent.Runtime;
using Agent.Runtime.Communication;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.LoadLocalAppSettings();

var config = builder.Configuration;
config.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
            .LoadKeyVaultAppSettings()
            .AddEnvironmentVariables();

builder.Services.RegisterServiceDependencies(builder.Environment);

if (builder.Environment.IsDevelopment())
{
    LocalAadAuthenticator.Initialize();
}

builder.Services.ConfigureSemanticKernel();
builder.Services.AddSingleton<IChatService, ChatProcessingService>();

var threadRepository = new InMemoryThreadRepository(new NullLogger<InMemoryThreadRepository>());
var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
builder.Services.AddSingleton<IThreadRepository>(threadRepository);
builder.Services.AddSingleton<SinkService>(sinkService);

// Add services to the container.
// Register our chat service
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Allow HTML in JSON responses
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

// Map controllers for API endpoints
app.MapControllers();

// Map Blazor components
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();


public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { ex.Message, ex.StackTrace }));
        }
    }
}
