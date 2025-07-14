# Reset the container app scaling configuration back to min=1, max=1
Write-Host "Cleaning up: Resetting scaling configuration for '$env:AZURE_CAPP_NAME'..."

# Check if the container app exists
$appExists = az containerapp show --name $env:AZURE_CAPP_NAME --resource-group $env:AZURE_RG 2>$null
if (!$appExists) {
    Write-Warning "Container App '$env:AZURE_CAPP_NAME' does not exist in resource group '$env:AZURE_RG'. Nothing to clean up."
    exit 0
}

# Get current scaling configuration
$app = az containerapp show --name $env:AZURE_CAPP_NAME --resource-group $env:AZURE_RG | ConvertFrom-Json
$currentMinReplicas = $app.properties.template.scale.minReplicas
$currentMaxReplicas = $app.properties.template.scale.maxReplicas

Write-Host "Current scaling configuration before cleanup:"
Write-Host "Min Replicas: $currentMinReplicas"
Write-Host "Max Replicas: $currentMaxReplicas"

# Reset to min=1, max=1
if ($currentMinReplicas -ne 1 -or $currentMaxReplicas -ne 1) {
    Write-Host "Resetting scaling configuration to min=1, max=1..."
    
    az containerapp update `
        --name $env:AZURE_CAPP_NAME `
        --resource-group $env:AZURE_RG `
        --min-replicas 1 `
        --max-replicas 1 `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to reset scaling configuration"
        exit 1
    }
    
    Write-Host "Successfully reset min and max replicas to 1"
} else {
    Write-Host "Scaling already at min=1, max=1. No cleanup needed."
}

# Verify the cleanup
$cleanedApp = az containerapp show --name $env:AZURE_CAPP_NAME --resource-group $env:AZURE_RG | ConvertFrom-Json
Write-Host "Final scaling configuration after cleanup:"
Write-Host "Min Replicas: $($cleanedApp.properties.template.scale.minReplicas)"
Write-Host "Max Replicas: $($cleanedApp.properties.template.scale.maxReplicas)"