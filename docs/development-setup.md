# Development Setup Guide

## Prerequisites

- Visual Studio or preferred IDE
- .NET Core SDK
- NuGet Package Manager
- NodeJS 22

## Private Environment Setup
1. **Depoloy the necessary resources**
  You can use git bash to run these on windows via the VSCode terminal or directly. The first time you run, an untracked `dev.bicepparam` file will be created which you can use to re-run the command without needing to specify the `-n` arg.
   ```bash
   source aliases.bash
   deploy3p -n <stamp prefix>
   ```
   The `<stamp prefix>` above is the prefix that would be used for your resource names. Your alias is a good option.

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

## NodeJS setup

### Windows
1. Install NodeJS 22 (https://nodejs.org/en)
   ```powershell
   winget install OpenJS.NodeJS.LTS
   ```

1. Install vsts-npm
   ```powershell
   npm install -g vsts-npm-auth --registry https://registry.npmjs.com --always-auth false
   ```

1. Login to VSTS registry
   ```powershell
   vsts-npm-auth -config src\Agent\Agent.Web\Client\.npmrc
   ```

   *If this fails due to the error "Couldn't get an authentication token for ... /npm/registry", then delete your user-level .npmrc (%userprofile%\\.npmrc) and rerun the command.*

   *If project build still fails with `npm install` error, try below command
   ```powershell
   npx vsts-npm-auth -R -E 131400 -C src\Agent\Agent.Web\Client\.npmrc
   ```
    

### Linux/WSL/MacOS

You can set up npm authentication using our script:

1. Generate a [Personal Access Token](https://dev.azure.com/msazure/_details/security/tokens) with scopes: Packaging read, write & manage; Drop read & write. (Select Access Scope to be "All accessible organizations")

2. Run the setup script:
   ```bash
   ./scripts/setup-npm-auth.sh <your-PAT>
   ```

3. The script will encode your PAT and configure your `~/.npmrc` file automatically.

4. Remember to refresh the token every 7 days by running the script with a new PAT.

#### Manual Setup (Alternative)
1. Copy the code below to your User npm profile (.npmrc) file, located at `~/.npmrc`:
    ```bash
    //msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/registry/:username=msazure
    //msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/registry/:_password="<base64encoded token>"
    //msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/:username=msazure
    //msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/:_password="<base64encoded token>"
    //msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/:email=npm requires email to be set but doesn't use the value
    ```
1. Generate a [Personal Access Token](https://dev.azure.com/msazure/_details/security/tokens) with scopes: Packaging read, write & manage; Drop read & write. (Select Access Scope to be "All accessible organizations")
1. Encode the PAT in base64:
   ```bash
   echo -n "<your PAT>" | base64
   ```
1. Replace `<base64encoded token>` in `~/.npmrc` with the output in above step.
1. Refresh the token manually (step 2 to step 4) since it gets expired in 7 days.

## Configuration Setup

1. Project should automatically start with no additional configuration. Required settings should be pulled from the Azure App Config instance that was set up as part of the private environment deployment. For optional settings, copy appsettings.json to appsettings.development.json and add any settings you need. These settings will override any other settings.

### Durable Task Scheduler


The deployment script deploys the DTS service. You can grab the connection string from the portal and update your `appsettings.Development.json`.
Note: If you get the connection string from the deployed resource in the portal, it will be missing the `TaskHub` parameter, which you'll need to add manually (also in the portal).

```
  "AppSettings": {
    "Core": {
      "Azure": {
        "DTS": {
          "ConnectionString": "<connection string>"
        },
      }
    }
  }
```    

Another option is to leave the connection string blank so it uses the emulator. Its convenient because every time you restart it, you start from a blank slate.
Run the emulator using: `./src/run-durable-emulator.ps1`

### Dashboard Settings

An Azure managed grafana and azure managed prometheus(Azure Monitor Worksapce) will be deployed using the deployment scirpt,
which are used by DailyReportAgent.
In production, they will be user-provided resources.
Please configure Dashboard Settings properly:
```
"Dashboard": {
    "GrafanaApiKey": "<your grafana admin api key>", // generated using az grafana api-key create --key <key-name>  --name <grafana name> --resource-group <grafana-rg> --role admin --time-to-live 365d
    "GrafanaUrl": "<your grafana endpoint>",
    "PrometheusUrl": "<Azure Monitor Workspace Query endpoint>",
    // metrics ingestion ednpoint of Azure Monitor workspace(Azure mangaged prometheus). It is not the same as the prometheus url for the same AMW resource.
    "MetricsIngestionEndpoint": "<Azure Monitor Workspace Metrics ingestion endpoint>",
    "GrafanaDataSourceName": "<Azure monitor workspace's datasource name in grafana>",
    "MermaidServerAPI" : ""
}
```
Azure Monitor Workspace Query endpoint and Metrics ingestion endpoint can be found on Azure Monitor Workspace's overview page.

[Next: Running the Application](running-the-app.md) 

[FAQs: Check frequently faced issues in development setup](development-setup-faqs.md)
