using Agent.Adc.RemoteWorkspace.Services;
using Agent.Common.Services;
using Agent.Plugins.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 8080 with HTTP/2 enabled (required for gRPC over insecure HTTP)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, o => o.Protocols = HttpProtocols.Http2);
});

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<ShellManager>();
builder.Services.AddSingleton<ISandboxPaths, LocalSandboxPaths>();
builder.Services.AddSingleton<IWorkspaceContext, LocalWorkspaceContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<FileSystemService>();
app.MapGrpcService<ExecutionService>();
app.MapGrpcService<AmbientContextService>();

app.Run();
