# Development Setup Guide

## Prerequisites

- Visual Studio or preferred IDE
- .NET Core SDK
- NuGet Package Manager

## NuGet Configuration

We use an internal NuGet source for packages. To set up:

1. Install NuGet if needed:
   ```powershell
   winget install Microsoft.NuGet   # Run as Administrator
   ```
   Or download from [nuget.org/downloads](https://www.nuget.org/downloads)

2. Set the API key:
   ```powershell
   nuget.exe setApiKey az -Source https://msazure.pkgs.visualstudio.com/Antares/_packaging/antares-websites/nuget/v3/index.json
   ```

> **Warning**: The cross-platform `dotnet nuget` command doesn't support setting API keys.

## Configuration Setup

1. Create `appsettings.Development.json` in the `Agent.Web` project:

```json
{
  "Azure": {
    "OpenAI": {
      "DeploymentName": "gpt-4o",
      "Endpoint": "<open-ai-endpoint>",
      "ApiKey": "<azure-openai-key>"
    }
  }
}
```

### Durable Task Scheduler

You have two options:
- run the emulator `./src/run-durable-emulator.ps1` (recommended)
- deploy the Durable Task Scheduler service `./src/deploy-durable-task-service.ps1`

If you deploy the service, update `appsettings.Development.json` with the connection string outputted by the deployment script

```
    "DurableTaskScheduler": {
      "ConnectionString": "<connection string>"
    },
```    

[Next: Running the Application](running-the-app.md) 