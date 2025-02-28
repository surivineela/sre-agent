# Development Setup Guide

## Prerequisites

- Visual Studio or preferred IDE
- .NET Core SDK
- NuGet Package Manager

## NuGet Configuration

We use an internal NuGet source for packages. To set up:
1. **Depoloy the necessary resources**
   ```bash
   source aliases.bash
   deploy3p
   ```

1. Install NuGet if needed:
   ```powershell
   winget install Microsoft.NuGet   # Run as Administrator
   ```
   Or download from [nuget.org/downloads](https://www.nuget.org/downloads)

1. Set the API key:
   ```powershell
   nuget.exe setApiKey az -Source https://msazure.pkgs.visualstudio.com/Antares/_packaging/antares-websites/nuget/v3/index.json
   ```

> **Warning**: The cross-platform `dotnet nuget` command doesn't support setting API keys.

## Configuration Setup

1. Project should automatically start with no additional configuration. Required settings should be pulled from the Azure App Config instance
that was set up as part of the private environment deployment. For optional settings, copy appsettings.json to appsettings.development.json and add any settings
you need. These settings will override any other settings.

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