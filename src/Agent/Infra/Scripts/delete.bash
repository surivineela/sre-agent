#!/bin/bash

################################################################
# Will use this once deployment stacks are less buggy and slow
################################################################

set -e

pushd "$(dirname "$0")" >/dev/null
source helpers.bash

PARAMETERS_FILE="../Bicep/Params/dev.bicepparam"
NAME_PREFIX=$(grep "param namePrefix" "$PARAMETERS_FILE" | awk -F "'" '{print $2}')

confirmDeployment $PARAMETERS_FILE

# Delete and purge OpenAI account
# Workaround because bicep doesn't support disabling purge protection
# echo "Deleting and purging OpenAI account..."
# az resource delete -g $NAME_PREFIX-operations-agent-3p-rg --resource-type Microsoft.CognitiveServices/accounts -n ${NAME_PREFIX}OpenAI
# Commenting out since purging via cli seems broken too...
# Purge via portal if you need to re-create resources

# Delete the rest of the resources
echo "Deleting deployment stack..."
az stack sub delete --name mnils-operations-agent-deployment --action-on-unmanage deleteAll --yes

echo "To finish cleaning up, purge the Azure AI Services resource in the Azure portal:"
echo "https://ms.portal.azure.com/#view/Microsoft_Azure_ProjectOxford/CognitiveServicesHub/~/OpenAI"

popd >/dev/null