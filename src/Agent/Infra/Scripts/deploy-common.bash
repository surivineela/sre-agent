#!/bin/bash

prepare_deployment() {
    local parameters_file="$1"
    # Extract namePrefix and set DEPLOYMENT_NAME env var
    export NAME_PREFIX=$(grep "param namePrefix" "$parameters_file" | awk -F"'" '{print $2}')
    export DEPLOYMENT_NAME="${NAME_PREFIX}-operations-agent-deployment"
    export RG_NAME="${NAME_PREFIX}-operations-agent-3p-rg"

    confirmDeployment "$parameters_file"
}

confirmDeployment() {
    local parameters_file="$1"
    echo "Contents of $parameters_file:"
    sed 's/^/\t/' "$parameters_file"
    echo
    while true; do
        read -p "Do you want to continue with these parameters? (y/n): " CONFIRM
        case $CONFIRM in
            y) break ;;
            n) echo "Deployment aborted by user"; exit 1 ;;
            *) echo "Please answer y/n" ;;
        esac
    done
}

validateArgs() {
    # Validate arguments using getopts
    PARAMS_DIR="../Bicep/Params"
    SOURCE_PARAM="$PARAMS_DIR/dev.example.bicepparam"
    PARAMETERS_FILE="$PARAMS_DIR/dev.bicepparam"

    usageStr="Usage: $0 -n <namePrefix> [-s] [-o]"
    while getopts ":n:so" opt; do
        case $opt in
            n)
                namePrefixArg="$OPTARG"
                ;;
            s)
                useStack=true
                ;;
            o)
                useOldOpenAIName=true
                ;;
            :)
                echo "Option -${OPTARG} requires an argument."
                exit 1
                ;;
            ?)
                if [ ! -f "$PARAMETERS_FILE" ]; then
                    echo "$usageStr"
                    exit 1
                fi
                ;;
        esac
    done
    if [ -z "$namePrefixArg" ]; then
        if [ ! -f "$PARAMETERS_FILE" ]; then
            echo "$usageStr"
            exit 1
        else
            namePrefixArg=$(grep "param namePrefix" "$PARAMETERS_FILE" | awk -F"'" '{print $2}')
        fi
    fi

    # Update the parameters file with namePrefix
    sed "s/param namePrefix = '.*'/param namePrefix = '$namePrefixArg'/" "$SOURCE_PARAM" > "$PARAMETERS_FILE"

    # Add useExistingOpenAI parameter based on -o flag
    if [ "$useOldOpenAIName" == true ]; then
        echo "param useOldOpenAIName = true" >> "$PARAMETERS_FILE"
    else
        echo "param useOldOpenAIName = false" >> "$PARAMETERS_FILE"
    fi
}

registerFeature() {
    # Register the feature
    echo "Registering Microsoft.DurableTask feature..."
    az feature register --namespace Microsoft.DurableTask --name PrivatePreview
    echo "Waiting for feature registration to complete..."

    # Wait for the feature to be registered
    while true; do
        state=$(az feature show --namespace Microsoft.DurableTask --name PrivatePreview --query "properties.state" -o tsv)
        state_trimmed=$(echo "$state" | tr -d '\r\n')
        if [[ "$state_trimmed" == "Registered" ]]; then
            echo "Feature 'PrivatePreview' is now registered."
            break
        else
            echo "Feature registration state: $state. Waiting 30 seconds..."
            sleep 30
        fi
    done
}

registerProvider() {
    # Register the provider
    echo "Registering Microsoft.DurableTask provider..."
    az provider register -n Microsoft.DurableTask
    echo "Waiting for provider registration to complete..."

    # Wait for the provider to be registered
    while true; do
        state=$(az provider show -n Microsoft.DurableTask --query "registrationState" -o tsv)
        state_trimmed=$(echo "$state" | tr -d '\r\n')
        if [[ "$state_trimmed" == "Registered" ]]; then
            echo "Provider 'Microsoft.DurableTask' is now registered."
            break
        else
            echo "Provider registration state: $state. Waiting 30 seconds..."
            sleep 30
        fi
    done

    echo "All registrations completed successfully!"
}

# We cannot use bicep to migrate a db from manual to autoscale, so we need to do it here.
# Then we can use bicep to set the autoscale settings for the graph.
configureAutoscale() {
    local account_name="$NAME_PREFIX-cosmosdb-graph"
    local database_name="resourcegraph"
    local graphName="configuration"
    if az cosmosdb gremlin graph show --account-name $account_name --database-name $database_name --name $graphName --resource-group $RG_NAME --only-show-errors &> /dev/null; then
        mode=$(az cosmosdb gremlin graph throughput show --account-name $account_name --database-name $database_name --name $graphName --resource-group $RG_NAME --query "resource.autoscaleSettings.maxThroughput" --output tsv)

        if [[ -z "$mode" ]]; then
            echo "Throughput mode: MANUAL"
            echo "Setting throughput to AUTOSCALE"

            az cosmosdb gremlin graph throughput migrate --account-name $account_name --database-name $database_name --name $graphName --resource-group $RG_NAME --throughput-type autoscale
        else
            echo "Throughput mode: AUTOSCALE (max $mode RU/s)"
        fi
        else
        echo "Graph does not exist."
        fi
}