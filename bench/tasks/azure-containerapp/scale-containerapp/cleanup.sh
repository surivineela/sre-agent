#!/usr/bin/env bash

# Reset the container app scaling configuration back to min=1, max=1
echo "Cleaning up: Resetting scaling configuration for '$AZURE_CAPP_NAME'..."

# Check if the container app exists
if ! az containerapp show --name "$AZURE_CAPP_NAME" --resource-group "$AZURE_RG" &>/dev/null; then
    echo "Warning: Container App '$AZURE_CAPP_NAME' does not exist in resource group '$AZURE_RG'. Nothing to clean up."
    exit 0
fi

# Get current scaling configuration
app_json=$(az containerapp show --name "$AZURE_CAPP_NAME" --resource-group "$AZURE_RG")
current_min_replicas=$(echo "$app_json" | jq -r '.properties.template.scale.minReplicas')
current_max_replicas=$(echo "$app_json" | jq -r '.properties.template.scale.maxReplicas')

echo "Current scaling configuration before cleanup:"
echo "Min Replicas: $current_min_replicas"
echo "Max Replicas: $current_max_replicas"

# Reset to min=1, max=1
if [[ "$current_min_replicas" != "1" ]] || [[ "$current_max_replicas" != "1" ]]; then
    echo "Resetting scaling configuration to min=1, max=1..."
    
    if ! az containerapp update \
        --name "$AZURE_CAPP_NAME" \
        --resource-group "$AZURE_RG" \
        --min-replicas 1 \
        --max-replicas 1 \
        --output none; then
        echo "Error: Failed to reset scaling configuration"
        exit 1
    fi
    
    echo "Successfully reset min and max replicas to 1"
else
    echo "Scaling already at min=1, max=1. No cleanup needed."
fi

# Verify the cleanup
cleaned_app=$(az containerapp show --name "$AZURE_CAPP_NAME" --resource-group "$AZURE_RG")
echo "Final scaling configuration after cleanup:"
echo "Min Replicas: $(echo "$cleaned_app" | jq -r '.properties.template.scale.minReplicas')"
echo "Max Replicas: $(echo "$cleaned_app" | jq -r '.properties.template.scale.maxReplicas')"