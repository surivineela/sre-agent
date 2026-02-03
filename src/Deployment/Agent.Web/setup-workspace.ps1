<#
.SYNOPSIS
    Sets up an SRE Agent workspace with ICM and Kusto MCP connectors.

.DESCRIPTION
    This script performs the following operations:
    1. Adds a User Assigned Managed Identity to the SRE Agent
    2. Creates an ICM data connector
    3. Creates a Kusto MCP data connector
    4. Configures ICM incident management
    5. Initializes srectl with the agent endpoint
    6. Adds predefined repositories to the workspace

.PARAMETER SubscriptionId
    The Azure subscription ID where the SRE Agent is deployed.

.PARAMETER ResourceGroup
    The resource group name containing the SRE Agent.

.PARAMETER AgentName
    The name of the SRE Agent resource.

.PARAMETER IcmConnectorName
    The name for the ICM data connector. Default: "icm"

.PARAMETER KustoConnectorName
    The name for the Kusto MCP data connector. Default: "kusto-mcp"

.PARAMETER UserAssignedIdentity
    The resource ID of the User Assigned Managed Identity to use.
    Default: "/subscriptions/40ed1017-9eaf-4a95-92d2-c19dcc01d4b0/resourceGroups/capps-release-assets/providers/Microsoft.ManagedIdentity/userAssignedIdentities/capps-release-service-umi"

.PARAMETER IcmKeyVaultUri
    The Key Vault URI for the ICM certificate. Default: "https://k8se-testcert-keyvault.vault.azure.net/certificates/icm2"

.PARAMETER ApiVersion
    The ARM API version to use. Default: "2025-05-01-preview"

.PARAMETER SkipConnectors
    Skip creating connectors (useful for re-running repository setup only).

.PARAMETER SkipRepositories
    Skip adding repositories.

.EXAMPLE
    .\setup-workspace.ps1 -SubscriptionId "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" -ResourceGroup "test-agent-monitor-rg" -AgentName "my-agent"

.EXAMPLE
    .\setup-workspace.ps1 -SubscriptionId "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" -ResourceGroup "test-agent-monitor-rg" -AgentName "my-agent" -SkipConnectors

.NOTES
    Prerequisites:
    - Azure CLI must be installed and authenticated
    - srectl must be installed and available in PATH
    - Appropriate permissions to manage the SRE Agent resource
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Azure subscription ID")]
    [ValidateNotNullOrEmpty()]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true, HelpMessage = "Resource group name")]
    [ValidateNotNullOrEmpty()]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true, HelpMessage = "SRE Agent name")]
    [ValidateNotNullOrEmpty()]
    [string]$AgentName,

    [Parameter(HelpMessage = "ICM connector name")]
    [string]$IcmConnectorName = "icm",

    [Parameter(HelpMessage = "Kusto MCP connector name")]
    [string]$KustoConnectorName = "kusto-mcp",

    [Parameter(HelpMessage = "User Assigned Managed Identity resource ID")]
    [string]$UserAssignedIdentity = "/subscriptions/40ed1017-9eaf-4a95-92d2-c19dcc01d4b0/resourceGroups/capps-release-assets/providers/Microsoft.ManagedIdentity/userAssignedIdentities/capps-release-service-umi",

    [Parameter(HelpMessage = "Key Vault URI for ICM certificate")]
    [string]$IcmKeyVaultUri = "https://k8se-testcert-keyvault.vault.azure.net/certificates/icm2",

    [Parameter(HelpMessage = "ARM API version")]
    [string]$ApiVersion = "2025-05-01-preview",

    [Parameter(HelpMessage = "Skip creating connectors")]
    [switch]$SkipConnectors,

    [Parameter(HelpMessage = "Skip adding repositories")]
    [switch]$SkipRepositories
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Validate prerequisites
function Test-Prerequisites {
    Write-Host "Checking prerequisites..." -ForegroundColor Yellow
    
    # Check Azure CLI
    if (-not (Get-Command "az" -ErrorAction SilentlyContinue)) {
        throw "Azure CLI (az) is not installed or not in PATH. Please install it from https://docs.microsoft.com/cli/azure/install-azure-cli"
    }

    # Check srectl
    if (-not (Get-Command "srectl" -ErrorAction SilentlyContinue)) {
        throw "srectl is not installed or not in PATH. Please install it first."
    }

    # Check Azure login
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        throw "Not logged into Azure CLI. Please run 'az login' first."
    }

    Write-Host "  Azure CLI: OK" -ForegroundColor Green
    Write-Host "  srectl: OK" -ForegroundColor Green
    Write-Host "  Azure account: $($account.user.name)" -ForegroundColor Green
}

Test-Prerequisites

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "Setting up SRE Agent Workspace" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Subscription ID  : $SubscriptionId"
Write-Host "Resource Group   : $ResourceGroup"
Write-Host "Agent Name       : $AgentName"
Write-Host "============================================" -ForegroundColor Cyan

# Build resource IDs and URIs
$agentId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/agents/$AgentName"
$connectorId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/agents/$AgentName/DataConnectors/$IcmConnectorName"
$connectorUri = "https://management.azure.com$connectorId" + "?api-version=$ApiVersion"
$kustoConnectorId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/agents/$AgentName/DataConnectors/$KustoConnectorName"
$kustoConnectorUri = "https://management.azure.com$kustoConnectorId" + "?api-version=$ApiVersion"

if (-not $SkipConnectors) {
    # Create temp files
    Write-Host "`n[Step 1/7] Creating temp files for request bodies..." -ForegroundColor Yellow
    $identityFile = [System.IO.Path]::GetTempFileName()
    $connectorFile = [System.IO.Path]::GetTempFileName()
    $kustoConnectorFile = [System.IO.Path]::GetTempFileName()
    $icmConfigFile = [System.IO.Path]::GetTempFileName()

    try {
        @{
            identity = @{
                type = "SystemAssigned,UserAssigned"
                userAssignedIdentities = @{
                    $UserAssignedIdentity = @{}
                }
            }
        } | ConvertTo-Json -Depth 10 | Out-File -FilePath $identityFile -Encoding utf8

        @{
            properties = @{
                name = $IcmConnectorName
                dataConnectorType = "Icm"
                dataSource = "https://prod.microsofticm.com"
                keyVaultUri = $IcmKeyVaultUri
                identity = $UserAssignedIdentity
                source = "Agent"
            }
        } | ConvertTo-Json -Depth 10 | Out-File -FilePath $connectorFile -Encoding utf8

        @{
            properties = @{
                source = "Agent"
                name = $KustoConnectorName
                dataConnectorType = "Mcp"
                identity = $UserAssignedIdentity
                dataSource = "placeholder"
                extendedProperties = @{
                    type = "stdio"
                    command = "npx"
                    args = @(
                        "-y",
                        "@azure/mcp@latest",
                        "server",
                        "start",
                        "--mode",
                        "all",
                        "--namespace",
                        "kusto",
                        "--read-only"
                    )
                    envs = @{
                        AZURE_TOKEN_CREDENTIALS = "managedidentitycredential"
                    }
                }
            }
        } | ConvertTo-Json -Depth 10 | Out-File -FilePath $kustoConnectorFile -Encoding utf8

        @{
            incidentManagementConfiguration = @{
                type = "Icm"
                connectionName = $IcmConnectorName
                icmApi = @{
                    apiEndpoint = "https://prod.microsofticm.com"
                }
            }
        } | ConvertTo-Json -Depth 10 | Out-File -FilePath $icmConfigFile -Encoding utf8

        Write-Host "[Step 2/7] Adding User Assigned Managed Identity to agent..." -ForegroundColor Yellow
        az resource patch --ids $agentId --api-version $ApiVersion --is-full-object -p "@$identityFile"
        if ($LASTEXITCODE -ne 0) { throw "Failed to add User Assigned Managed Identity" }

        Write-Host "`n[Step 3/7] Creating ICM connector '$IcmConnectorName'..." -ForegroundColor Yellow
        az rest --method PUT --uri $connectorUri --body "@$connectorFile"
        if ($LASTEXITCODE -ne 0) { throw "Failed to create ICM connector" }

        Write-Host "`nWaiting 30 seconds for connectors to provision..." -ForegroundColor Gray
        Start-Sleep -Seconds 30

        Write-Host "`n[Step 4/7] Creating Kusto MCP connector '$KustoConnectorName'..." -ForegroundColor Yellow
        az rest --method PUT --uri $kustoConnectorUri --body "@$kustoConnectorFile"
        if ($LASTEXITCODE -ne 0) { throw "Failed to create Kusto MCP connector" }

        Write-Host "`nWaiting 30 seconds for connectors to provision..." -ForegroundColor Gray
        Start-Sleep -Seconds 30

        Write-Host "`n[Step 5/7] Configuring ICM incident management..." -ForegroundColor Yellow
        az resource patch --ids $agentId --api-version $ApiVersion -p "@$icmConfigFile"
        if ($LASTEXITCODE -ne 0) { throw "Failed to configure ICM incident management" }
    }
    finally {
        Write-Host "`n[Step 6/7] Cleaning up temp files..." -ForegroundColor Yellow
        Remove-Item -Path $identityFile, $connectorFile, $kustoConnectorFile, $icmConfigFile -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "`nSkipping connector setup (SkipConnectors flag set)" -ForegroundColor Yellow
}

# Get agent endpoint and initialize srectl
Write-Host "`n[Step 7/7] Initializing srectl with agent endpoint..." -ForegroundColor Yellow
$agent = az resource show --ids $agentId --api-version $ApiVersion | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "Failed to get agent resource" }

$agentEndpoint = $agent.properties.agentEndpoint
Write-Host "Agent endpoint: $agentEndpoint" -ForegroundColor Gray
srectl init --resource-url $agentEndpoint
if ($LASTEXITCODE -ne 0) { throw "Failed to initialize srectl" }

if (-not $SkipRepositories) {
    # Define repositories to add
    $repositories = @(
        @{ Name = "AAPT-Antares-ContainerApps"; Url = "https://msazure.visualstudio.com/One/_git/AAPT-Antares-Containerapps" },
        @{ Name = "AAPT-Antares-ContainerApps-Docs"; Url = "https://msazure.visualstudio.com/One/_git/AAPT-Antares-ContainerApps-Docs" },
        @{ Name = "k4apps"; Url = "https://github.com/serverless-paas-balam/k4apps" },
        @{ Name = "azureContainerAppsWiki"; Url = "https://dev.azure.com/Supportability/AzureContainerApps/_git/AzureContainerApps.wiki" },
        @{ Name = "AAPT-Antares-Websites"; Url = "https://msazure.visualstudio.com/One/_git/AAPT-Antares-Websites" },
        @{ Name = "AAPT-Antares-Legion"; Url = "https://msazure.visualstudio.com/Antares/_git/AAPT-Antares-Legion" }
    )

    Write-Host "`n[Adding repositories]" -ForegroundColor Yellow
    foreach ($repo in $repositories) {
        Write-Host "  Adding $($repo.Name)..." -ForegroundColor Gray
        srectl repo add --name $repo.Name --url $repo.Url
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to add repository: $($repo.Name)"
        }
    }
} else {
    Write-Host "`nSkipping repository setup (SkipRepositories flag set)" -ForegroundColor Yellow
}

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "Setup complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
