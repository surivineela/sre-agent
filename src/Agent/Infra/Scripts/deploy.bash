#!/bin/bash
set -e

pushd "$(dirname "$0")" >/dev/null

source deploy-common.bash

# Validate arguments
validateArgs "$@"

# Variables
TEMPLATE_FILE="../Bicep/main.bicep"

# Deploy the Bicep template
prepare_deployment "$PARAMETERS_FILE"

# Prep CLI
echo "Upgrading bicep..."
az bicep upgrade

configureAutoscale

if [ "$useStack" == true ]; then
    echo "Creating deployment stack with name $DEPLOYMENT_NAME..."
    subId="${subscriptionId:-$(az account show --query id -o tsv)}"
    az stack sub create \
        --name "$DEPLOYMENT_NAME" \
        --template-file "$TEMPLATE_FILE" \
        --parameters "$PARAMETERS_FILE" \
        --location westus \
        --action-on-unmanage deleteAll \
        --deny-settings-mode none \
        --subscription "$subId" \
        --yes
else
    echo "Creating deployment with name $DEPLOYMENT_NAME..."
    subId="${subscriptionId:-$(az account show --query id -o tsv)}"
    echo "You can check deployment status here: https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/$subId/resourceGroups/$RG_NAME/deployments"
    az deployment sub create \
        --name "$DEPLOYMENT_NAME" \
        --template-file "$TEMPLATE_FILE" \
        --parameters "$PARAMETERS_FILE" \
        --location westus \
        --subscription "$subId"
fi

# Check the deployment status
if [ $? -eq 0 ]; then
    echo "Deployment succeeded."
else
    echo "Deployment failed."
    exit 1
fi

popd >/dev/null
