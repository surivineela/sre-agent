# Development Setup Guide

## Prerequisites

- .NET Core SDK
- NodeJS 22
- Just

```bash
# Windows
winget install --id Casey.Just

# MacOS
brew install just

# Linux

## Ubuntu/Debian
apt install just

## Mariner
dnf install just
```

## Private Environment Setup
1. **Deploy the necessary resources**

   > [!IMPORTANT]
   > On Windows, you **MUST** use git bash to run these commands, either via the VSCode terminal or directly.

   For the very first execution, run the command bellow to prepare your environment and perform an initial deployment:

   ```bash
   just deploy3p -n <stamp_prefix> [-s <subscriptionId>]
   ```

   The `<stamp prefix>` above is the prefix that would be used for your resource names. Your alias is a good option.

   Optionally, you can specify a target subscription with `-s <subscriptionId>`. If not provided, the deployment will use your current Azure CLI subscription.

   After first time you run, an untracked `dev.bicepparam` file will be created which you can use to re-run the command without needing to specify the `-n <stamp prefix>` argument.

## NuGet Configuration

### Windows

Either `just setup-windows` or:

1. Install NuGet if needed:
   ```powershell
   winget install Microsoft.NuGet   # Run as Administrator
   ```
   Or download from [nuget.org/downloads](https://www.nuget.org/downloads)

1. Set the API key:
   ```powershell
   nuget.exe setApiKey az -Source https://msazure.pkgs.visualstudio.com/Antares/_packaging/antares-websites/nuget/v3/index.json
   ```

### Linux/WSL/MacOS

Either `just setup-mac` or `just setup-ubuntu` or:

1. Download Artifact Credentials Provider [microsoft/articfacts-credprovider](https://github.com/microsoft/artifacts-credprovider)

1. set `NUGET_PLUGIN_PATHS` to `plugins/netcore/CredentialProvider.Microsoft/CredentialProvider.Microsoft.dll`

```bash
# create a place under ~ to store the plugin.
# This can be anywhere as long as you update `NUGET_PLUGIN_PATHS` to match
mkdir -p ~/.nuget/

# temp working dir
mkdir -p /tmp/scratch/nuget
cd /tmp/scratch/nuget

# download and extract
wget https://github.com/microsoft/artifacts-credprovider/releases/download/v1.4.1/Microsoft.NuGet.CredentialProvider.tar.gz
tar xzvf Microsoft.Nuget.CredentialProvider.tar.gz

mv plugins ~/.nuget/


# set this in your profile
export NUGET_PLUGIN_PATHS="$HOME/.nuget/plugins/netcore/CredentialProvider.Microsoft/CredentialProvider.Microsoft.dll"
```

> [!IMPORTANT]
> Set `NUGET_PLUGIN_PATHS` in your `.zshrc` or `.bashrc`
> You might need to run `just login` or `dotnet restore --interactive` the first time.


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

   ```powershell
   Remove-Item "$env:USERPROFILE\.npmrc" -Force
   ```

   *If project build still fails with `npm install` error or `vsts-npm-auth is not recognized` error, try below command
   ```powershell
   npx vsts-npm-auth -R -E 131400 -C src\Agent\Agent.Web\Client\.npmrc
   ```


### Linux/WSL/MacOS

#### Recommended: Cross-Platform Credential Provider

The `vsts-npm-auth` tool only works on Windows. For Linux/WSL/MacOS, use the [cross-platform npm credential provider](https://eng.ms/docs/coreai/devdiv/one-engineering-system-1es/1es-docs/azure-artifacts/npm-credprovider) from the 1ES team.

1. Install the credential provider globally:
   ```bash
   npm install --global @microsoft/artifacts-npm-credprovider --registry https://pkgs.dev.azure.com/artifacts-public/PublicTools/_packaging/AzureArtifacts/npm/registry/
   ```

2. Authenticate by running the provider in the same directory as `.npmrc` ([Agent.Web/Client/](../src/Agent/Agent.Web/Client/)):
   ```bash
   cd src/Agent/Agent.Web/Client
   artifacts-npm-credprovider
   ```

   Alternatively, you can use the npm script (also run from the Client directory):
   ```bash
   cd src/Agent/Agent.Web/Client
   npm run refresh-creds
   ```

> [!NOTE]
> If the credential provider doesn't work for your setup, use one of the manual authentication methods below.

#### Alternative: Script-Based Setup

You can set up npm authentication using our setup script:

1. Generate a [Personal Access Token](https://dev.azure.com/msazure/_details/security/tokens) with scopes: Packaging read, write & manage; Drop read & write. (Select Access Scope to be "All accessible organizations")

1. Run the setup script:
   ```bash
   ./scripts/setup-npm-auth.sh <your-PAT>
   ```

1. The script will encode your PAT and configure your `~/.npmrc` file automatically.

1. Remember to refresh the token every 7 days by running the script with a new PAT.

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
   echo -n "<your PAT>" | base64 -w 0 | more
   ```
1. Replace `<base64encoded token>` in `~/.npmrc` with the output in above step.
1. Refresh the token manually (step 2 to step 4) since it gets expired in 7 days.

## Configuration Setup

1. Project should automatically start with no additional configuration. Required settings should be pulled from the Azure App Config instance that was set up as part of the private environment deployment. For optional settings, copy appsettings.json to appsettings.development.json and add any settings you need. These settings will override any other settings.

> **Disclaimer: Advanced Configurations Ahead**
> The following section describes advanced configuration steps for setting up Dashboard Settings.
> These are not required for running the agent. Only complete them if they are necessary for your use case, otherwise, proceed to:
> [Next: Running the Application](running-the-app.md) or [Continue Setup for 1p Agent](1p-agent-development.md)

### Dashboard Settings
* Note: Configuring dashboard settings is not mandatory, and will not affect your execution of the application. This is an optional section and should be done if you require the analytics here.

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

[Next: Running the Application](running-the-app.md) or [Continue Setup for 1p Agent](1p-agent-development.md)

[FAQs: Check frequently faced issues in development setup](development-setup-faqs.md)
