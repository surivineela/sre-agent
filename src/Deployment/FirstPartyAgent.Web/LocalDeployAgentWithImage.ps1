param(
    [switch]$RunBuild
)

# Set variables
$subscriptionId = "14300d68-d0c8-4060-82af-bf2d9b70f130"
$resourceGroupName = "sreagent1p-rg"
$location = "Sweden Central"
$agentName = "ajsharmappagent7"
$acrResourceGroup = "sreagent-rg"
$acrName = "customprivateacr"
$managedIdentityName = "msi-7twcjnr43y4cg"
$apiVersion = "2025-05-01-preview"
$acrSku = "Basic"  # Adjust as needed
$buildScriptPath = Join-Path $PSScriptRoot "buildandpush.bat"

if (-not (Get-AzContext)) {
    Connect-AzAccount -Subscription $subscriptionId
}

# Check and create the resource group if it doesn't exist
if (-not (Get-AzResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue)) {
    Write-Host "Creating resource group: $resourceGroupName"
    New-AzResourceGroup -Name $resourceGroupName -Location $location
}

# Check and create the managed identity if it doesn't exist
$managedIdentity = Get-AzUserAssignedIdentity -ResourceGroupName $resourceGroupName -Name $managedIdentityName -ErrorAction SilentlyContinue
if (-not $managedIdentity) {
    Write-Host "Creating managed identity: $managedIdentityName"
    $managedIdentity = New-AzUserAssignedIdentity -ResourceGroupName $resourceGroupName -Name $managedIdentityName -Location $location
}

# Check if ACR exists; if not, create it and run the build script
$acr = Get-AzContainerRegistry -Name $acrName -ResourceGroupName $acrResourceGroup -ErrorAction SilentlyContinue
if (-not $acr) {
    Write-Host "ACR not found. Creating ACR: $acrName"
    $acr = New-AzContainerRegistry -Name $acrName -ResourceGroupName $acrResourceGroup -Location $location -Sku $acrSku -AdminUserEnabled $true
}
elseif (-not $acr.AdminUserEnabled) {
    Write-Host "Enabling admin user for ACR: $acrName"
    Update-AzContainerRegistry -Name $acrName -ResourceGroupName $acrResourceGroup -AdminUserEnabled $true
}

# Run the external build script
if ($RunBuild) {
    Write-Host "Running build and push script since -RunBuild was specified..."
    & $buildScriptPath $acrName

    # Explicit docker push after batch script
    $imageName = "$acrName.azurecr.io/fpagentweb:latest"
    Write-Host "Running docker push explicitly in PowerShell for: $imageName"
    
    # Ensure docker is logged into ACR
    az acr login --name $acrName

    # Push the image
    docker push $imageName
} else {
    Write-Host "Skipping build and push script (use -RunBuild to enable)"
}

# Get ACR username and password
$acrCreds = az acr credential show --name $acrName | ConvertFrom-Json
$acrUsername = $acrCreds.username
$acrPassword = $acrCreds.passwords[0].value

# Build the agent deployment URL
$resourceUrl = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.App/agents/$agentName" + "?api-version=" + $apiVersion

# Get the access token
$token = (Get-AzAccessToken -ResourceUrl "https://management.azure.com/").Token

# Construct the identity reference
$userAssignedIdentityKey = "/subscriptions/$subscriptionId/resourcegroups/$resourceGroupName/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$managedIdentityName"
$userAssignedIdentities = @{}
$userAssignedIdentities[$userAssignedIdentityKey] = @{}

# Build the full body
$body = @{
    location = $location
    properties = @{
        mcpServers = @()
        logConfiguration = @{}
        firstPartyConfiguration = @{
            agentImageConfiguration = @{
                imageName = "$acrName.azurecr.io/fpagentweb:latest"
                registryUserName = "$acrUsername"
                registryPassword = "$acrPassword"
            }
        }
    }
    identity = @{
        type = "SystemAssigned, UserAssigned"
        userAssignedIdentities = $userAssignedIdentities
    }
} | ConvertTo-Json -Depth 10

# Deploy the agent resource
$response = Invoke-RestMethod -Uri $resourceUrl `
                              -Method Put `
                              -Headers @{Authorization = "Bearer $token"} `
                              -ContentType "application/json" `
                              -Body $body

Write-Host "Deployment initiated. Monitoring status..."

# Poll the provisioning state
do {
    Start-Sleep -Seconds 10
    Write-Host "Checking provisioning state...."
    $statusResponse = Invoke-RestMethod -Uri $resourceUrl `
                                        -Method Get `
                                        -Headers @{Authorization = "Bearer $token"}
    $provisioningState = $statusResponse.properties.provisioningState
} while ($provisioningState -ne "Succeeded")

# This is for manually updating "defaultIcmTeam" under AgentFactoryConfigs container for now
# In future, it can be via ARM request after agent is deployed
$agentEndpoint = $statusResponse.properties.agentEndpoint
if ($agentEndpoint) {
    # Acquire sre token
    $sreTokenJson = az account get-access-token --scope https://azuresre.dev/.default -o json | ConvertFrom-Json
    $sreToken = $sreTokenJson.accessToken

    $configUrl = "$agentEndpoint/api/config/containers/AgentFactoryConfigs/documents"
    Write-Host "Posting configuration to $configUrl"

    $configBody = @{
        id = "defaultIcmTeam"
        Content = @{
            IcmServiceId = 10060
            IcmServiceName = "App Service (Web Apps)"
            IcmTeamName = "Windows Azure Websites Servicing"
            IcmTeamId = 10468
            TeamPublicId = "WINDOWSAZUREWEBSITES\WindowsAzureWebsitesServicing"
        }
    } | ConvertTo-Json -Depth 5

    $configResponse = Invoke-RestMethod -Uri $configUrl `
                                        -Method Post `
                                        -Headers @{Authorization = "Bearer $sreToken"} `
                                        -ContentType "application/json" `
                                        -Body $configBody

    Write-Host "Config POST response:"
    Write-Host $configResponse
} else {
    Write-Host "agentEndpoint not found in provisioning response."
}


Write-Host "Provisioning succeeded!"
