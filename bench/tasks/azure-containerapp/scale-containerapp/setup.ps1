# Check if the container app exists
$appExists = az containerapp show --name $env:AZURE_CAPP_NAME --resource-group $env:AZURE_RG 2>$null
if (!$appExists) {
    Write-Error "Container App '$env:AZURE_CAPP_NAME' does not exist in resource group '$env:AZURE_RG'"
    exit 1
}

# Get current scaling configuration
$app = az containerapp show --name $env:AZURE_CAPP_NAME --resource-group $env:AZURE_RG | ConvertFrom-Json
$currentMinReplicas = $app.properties.template.scale.minReplicas
$currentMaxReplicas = $app.properties.template.scale.maxReplicas

Write-Host "Current scaling configuration:"
Write-Host "Min Replicas: $currentMinReplicas"
Write-Host "Max Replicas: $currentMaxReplicas"

# Check if replicas need to be set to 1
$needsUpdate = $false
if ($currentMinReplicas -ne 1 -or $currentMaxReplicas -ne 1) {
    $needsUpdate = $true
    Write-Host "Updating scaling configuration to min=1, max=1..."
    
    # Update the scaling configuration
    az containerapp update `
        --name $env:AZURE_CAPP_NAME `
        --resource-group $env:AZURE_RG `
        --min-replicas 1 `
        --max-replicas 1 `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to update scaling configuration"
        exit 1
    }
    
    Write-Host "Successfully set min and max replicas to 1"
} else {
    Write-Host "Scaling already configured with min=1, max=1. No update needed."
}

# Verify the update
$updatedApp = az containerapp show --name $env:AZURE_CAPP_NAME --resource-group $env:AZURE_RG | ConvertFrom-Json
Write-Host "Final scaling configuration:"
Write-Host "Min Replicas: $($updatedApp.properties.template.scale.minReplicas)"
Write-Host "Max Replicas: $($updatedApp.properties.template.scale.maxReplicas)"