# Verify the container app has been scaled correctly
Write-Host "Verifying scaling configuration for '$env:AZURE_CAPP_NAME'..."

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

# Verify the expected values
$expectedMinReplicas = 2
$expectedMaxReplicas = 5

if ($currentMinReplicas -eq $expectedMinReplicas -and $currentMaxReplicas -eq $expectedMaxReplicas) {
    Write-Host "SUCCESS: Container App is correctly scaled to min=$expectedMinReplicas, max=$expectedMaxReplicas"
    exit 0
} else {
    Write-Error "FAILURE: Container App scaling is incorrect"
    Write-Error "Expected: min=$expectedMinReplicas, max=$expectedMaxReplicas"
    Write-Error "Actual: min=$currentMinReplicas, max=$currentMaxReplicas"
    exit 1
}