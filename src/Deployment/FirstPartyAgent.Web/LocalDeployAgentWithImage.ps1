if (-not (Get-AzContext)) {
    Connect-AzAccount -Subscription "14300d68-d0c8-4060-82af-bf2d9b70f130"
}

# Set variables
$subscriptionId = "14300d68-d0c8-4060-82af-bf2d9b70f130"
$resourceGroupName = "sreagent1p-rg"
$agentName = "ajsharmappagent3"
$apiVersion = "2025-05-01-preview"

# Build the URL using string concatenation
$resourceUrl = "https://management.azure.com/subscriptions/" + $subscriptionId + 
               "/resourceGroups/" + $resourceGroupName + 
               "/providers/Microsoft.App/agents/" + $agentName + 
               "?api-version=" + $apiVersion

# Get the access token
$token = (Get-AzAccessToken -ResourceUrl "https://management.azure.com/").Token

# Construct the full identity key
$userAssignedIdentityKey = "/subscriptions/" + $subscriptionId + 
                           "/resourcegroups/" + $resourceGroupName + 
                           "/providers/Microsoft.ManagedIdentity/userAssignedIdentities/msi-7twcjnr43y4cg"

# Create the userAssignedIdentities hashtable separately
$userAssignedIdentities = @{}
$userAssignedIdentities[$userAssignedIdentityKey] = @{}

# Build the full body
$body = @{
    location = "Sweden Central"
    properties = @{
        mcpServers = @()
        logConfiguration = @{}
        firstPartyConfiguration = @{
            agentImageOverride = "custompublicacr.azurecr.io/fpagentweb:latest"
        }
    }
    identity = @{
        type = "SystemAssigned, UserAssigned"
        userAssignedIdentities = $userAssignedIdentities
    }
} | ConvertTo-Json -Depth 10

# Perform the PUT request
$response = Invoke-RestMethod -Uri $resourceUrl `
                              -Method Put `
                              -Headers @{Authorization = "Bearer " + $token} `
                              -ContentType "application/json" `
                              -Body $body

Write-Host "Deployment initiated. Monitoring status..."

# Poll the provisioning state until it becomes 'Succeeded'
do {
    Start-Sleep -Seconds 10
    Write-Host "Checking provisioning state...."

    $statusResponse = Invoke-RestMethod -Uri $resourceUrl `
                                        -Method Get `
                                        -Headers @{Authorization = "Bearer " + $token}

    $provisioningState = $statusResponse.properties.provisioningState

} while ($provisioningState -ne "Succeeded")

Write-Host "Provisioning succeeded!"
