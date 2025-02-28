#!/bin/bash

################################################################
# Will use this once deployment stacks are less buggy and slow
################################################################

set -e

pushd "$(dirname "$0")" >/dev/null

source helpers.bash

# Variables
TEMPLATE_FILE="../Bicep/main.bicep"
PARAMETERS_FILE="../Bicep/Params/dev.bicepparam"
NAME_PREFIX=$(grep "param namePrefix" "$PARAMETERS_FILE" | awk -F "'" '{print $2}')
DEPLOYMENT_NAME="${NAME_PREFIX}-operations-agent-deployment"

confirmDeployment $PARAMETERS_FILE

# Deploy the Bicep template
echo "Creating deployment stack with name $DEPLOYMENT_NAME..."
az stack sub create \
    --name $DEPLOYMENT_NAME \
    --template-file $TEMPLATE_FILE \
    --parameters $PARAMETERS_FILE \
    --location westus \
    --action-on-unmanage deleteAll \
    --deny-settings-mode none \
    --yes

# # Check the deployment status
if [ $? -eq 0 ]; then
    echo "Deployment succeeded."
else
    echo "Deployment failed."
    exit 1
fi

popd >/dev/null
