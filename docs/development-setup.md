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

[Next: Running the Application](running-the-app.md) 