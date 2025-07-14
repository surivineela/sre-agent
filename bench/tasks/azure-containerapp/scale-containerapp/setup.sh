#!/usr/bin/env bash

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

# Check if replicas need to be set to 1
needs_update=false
if [[ "$current_min_replicas" != "1" ]] || [[ "$current_max_replicas" != "1" ]]; then
    needs_update=true
    echo "Updating scaling configuration to min=1, max=1..."
    
    # Update the scaling configuration
    if ! az containerapp update \
        --name "$AZURE_CAPP_NAME" \
        --resource-group "$AZURE_RG" \
        --min-replicas 1 \
        --max-replicas 1 \
        --output none; then
        echo "Error: Failed to update scaling configuration"
        exit 1
    fi
    
    echo "Successfully set min and max replicas to 1"
else
    echo "Scaling already configured with min=1, max=1. No update needed."
fi

# Verify the update
updated_app=$(az containerapp show --name "$AZURE_CAPP_NAME" --resource-group "$AZURE_RG")
echo "Final scaling configuration:"
echo "Min Replicas: $(echo "$updated_app" | jq -r '.properties.template.scale.minReplicas')"
echo "Max Replicas: $(echo "$updated_app" | jq -r '.properties.template.scale.maxReplicas')"