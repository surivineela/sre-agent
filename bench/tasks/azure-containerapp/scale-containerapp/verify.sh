#!/usr/bin/env bash

# Verify the container app has been scaled correctly
echo "Verifying scaling configuration for '$AZURE_CAPP_NAME'..."

# Check if the container app exists
if ! az containerapp show --name "$AZURE_CAPP_NAME" --resource-group "$AZURE_RG" &>/dev/null; then
    echo "Error: Container App '$AZURE_CAPP_NAME' does not exist in resource group '$AZURE_RG'"
    exit 1
fi

# Get current scaling configuration
app_json=$(az containerapp show --name "$AZURE_CAPP_NAME" --resource-group "$AZURE_RG")
current_min_replicas=$(echo "$app_json" | jq -r '.properties.template.scale.minReplicas')
current_max_replicas=$(echo "$app_json" | jq -r '.properties.template.scale.maxReplicas')

echo "Current scaling configuration:"
echo "Min Replicas: $current_min_replicas"
echo "Max Replicas: $current_max_replicas"

# Verify the expected values
expected_min_replicas=2
expected_max_replicas=5

if [[ "$current_min_replicas" == "$expected_min_replicas" ]] && [[ "$current_max_replicas" == "$expected_max_replicas" ]]; then
    echo "SUCCESS: Container App is correctly scaled to min=$expected_min_replicas, max=$expected_max_replicas"
    exit 0
else
    echo "FAILURE: Container App scaling is incorrect" >&2
    echo "Expected: min=$expected_min_replicas, max=$expected_max_replicas" >&2
    echo "Actual: min=$current_min_replicas, max=$current_max_replicas" >&2
    exit 1
fi