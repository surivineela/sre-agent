# Development Setup Guide

## Prerequisites

- Visual Studio or preferred IDE
- .NET Core SDK
- NuGet Package Manager

## Private Environment Setup
1. **Depoloy the necessary resources**
  You can use git bash to run these on windows via the VSCode terminal or directly. The first time you run, an untracked `dev.bicepparam` file will be created which you can use to re-run the command without needing to specify the `-n` arg.
   ```bash
   source aliases.bash
   deploy3p -n <stamp prefix>
   ```

## NuGet Configuration

We use an internal NuGet source for packages. To set up:

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

1. Project should automatically start with no additional configuration. Required settings should be pulled from the Azure App Config instance that was set up as part of the private environment deployment. For optional settings, copy appsettings.json to appsettings.development.json and add any settings you need. These settings will override any other settings.

### Durable Task Scheduler


Deploy the Durable Task Scheduler service `./src/deploy-durable-task-service.ps1`

Update `appsettings.Development.json` with the connection string outputted by the deployment script

```
    "DurableTaskScheduler": {
      "ConnectionString": "<connection string>"
    },
```    

Note - there is also a script for running the durable emulator `./src/run-durable-emulator.ps1`
HOWEVER the emulator has issues that we are waiting for fixes:
- GetAllInstancesAsync hangs and doesnt return orchestration instances
- external events don't fire (context.WaitForExternalEvent)

TODO - update these instructions once we have new emulator build with fixes

[Next: Running the Application](running-the-app.md) 