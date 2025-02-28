#!/bin/bash
pushd "$(dirname "$0")" >/dev/null
source deploy-common.bash

PARAMETERS_FILE="../Bicep/Params/dev.bicepparam"

validateArgs "$@"
confirmDeployment $PARAMETERS_FILE

# Delete and purge OpenAI account
# Workaround because bicep doesn't support disabling purge protection
# echo "Deleting and purging OpenAI account..."
# az resource delete -g $NAME_PREFIX-operations-agent-3p-rg --resource-type Microsoft.CognitiveServices/accounts -n ${NAME_PREFIX}OpenAI
# Commenting out since purging via cli seems broken too...
# Purge via portal if you need to re-create resources

# Purge and delete app config
echo "Deleting app config..."
az appconfig delete --name $namePrefixArg-appconfig --yes
echo "Purging app config..."
az appconfig purge --name $namePrefixArg-appconfig --yes

# Delete the rest of the resources
echo "About to delete everything else. In the meantime, for now you need to manually purge the OpenAI resource in the Azure portal."
echo "Once this script deletes the resource, purge the Azure AI Services resource here (under 'manage deleted resources'):"
echo "https://ms.portal.azure.com/#view/Microsoft_Azure_ProjectOxford/CognitiveServicesHub/~/OpenAI"

if [ "$useStack" == true ]; then
    echo "Deleting deployment stack (graph db takes a while)......"
    az stack sub delete --name mnils-operations-agent-deployment --action-on-unmanage deleteAll --yes
else
    echo "Deleting resource group (graph db takes a while)..."
    az group delete --name $namePrefixArg-operations-agent-3p-rg --yes
fi

popd >/dev/null