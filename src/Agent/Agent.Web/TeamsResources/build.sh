#!/bin/bash

# Exit on error
set -e

# Source .env file if it exists
if [ -f .env ]; then
    echo "Loading environment variables from .env file"
    # Load and export all variables from .env
    export $(grep -v '^#' .env | sed 's/"//g' | xargs)
fi

# Debug: Print environment variables to verify they're loaded
echo "Checking environment variables:"
echo "TEAMS_APP_ID=$TEAMS_APP_ID"
echo "AAD_APP_CLIENT_ID=$AAD_APP_CLIENT_ID"
echo "RESOURCE_SUFFIX=$RESOURCE_SUFFIX"
echo "BOT_DOMAIN=$BOT_DOMAIN"
echo "MICROSOFT_APP_TYPE=$MICROSOFT_APP_TYPE"
echo "MICROSOFT_APP_TENANT_ID=$MICROSOFT_APP_TENANT_ID"
echo "RESOURCE_GROUP=$RESOURCE_GROUP"

# Check if environment variables are set
if [ -z "$TEAMS_APP_ID" ]; then
    echo "Error: TEAMS_APP_ID environment variable is not set"
    exit 1
fi

if [ -z "$AAD_APP_CLIENT_ID" ]; then
    echo "Error: AAD_APP_CLIENT_ID environment variable is not set"
    exit 1
fi

if [ -z "$BOT_DOMAIN" ]; then
    echo "Error: BOT_DOMAIN environment variable is not set"
    exit 1
fi

#!/bin/bash

# Source the .env file
if [ -f .env ]; then
    source .env
fi

# Create processed parameters file using sed
cp ./deploy/teams-bot.parameters.json ./deploy/teams-bot.parameters.processed.json

# Replace each variable one by one
sed -i.bak "s/\${RESOURCE_SUFFIX}/$RESOURCE_SUFFIX/g" ./deploy/teams-bot.parameters.processed.json
sed -i.bak "s/\${AAD_APP_CLIENT_ID}/$AAD_APP_CLIENT_ID/g" ./deploy/teams-bot.parameters.processed.json
sed -i.bak "s/\${BOT_DOMAIN}/$BOT_DOMAIN/g" ./deploy/teams-bot.parameters.processed.json
sed -i.back "s/\${BOT_NAME}/$BOT_NAME/g" ./deploy/teams-bot.parameters.processed.json
sed -i.bak "s/\${MICROSOFT_APP_TYPE}/$MICROSOFT_APP_TYPE/g" ./deploy/teams-bot.parameters.processed.json
sed -i.bak "s/\${MICROSOFT_APP_TENANT_ID}/$MICROSOFT_APP_TENANT_ID/g" ./deploy/teams-bot.parameters.processed.json

# Remove backup file
rm ./deploy/teams-bot.parameters.processed.json.bak

# Display result for verification
echo "Processed parameters file content:"
cat ./deploy/teams-bot.parameters.processed.json

# Deploy using the processed parameters file
az deployment group create \
    --name "sre-agent-deployment" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file ./deploy/teams-bot.bicep \
    --parameters ./deploy/teams-bot.parameters.processed.json

echo "Successfully deployed Teams bot to Azure"

TEMPLATE_PATH="./manifest.json.template"
MANIFEST_PATH="./appPackage/manifest.json"

# Check if template file exists
if [ ! -f "$TEMPLATE_PATH" ]; then
    echo "Error: manifest.json.template not found at $TEMPLATE_PATH"
    exit 1
fi

# Create appPackage directory if it doesn't exist
if [ ! -d "./appPackage" ]; then
    echo "Creating appPackage directory..."
    mkdir -p "./appPackage"
fi

# Copy template to manifest.json in appPackage directory
echo "Copying template to manifest.json..."
cp "$TEMPLATE_PATH" "$MANIFEST_PATH"

# Replace placeholders with environment values
echo "Replacing placeholders in manifest.json..."
sed -i.bak "s/\${{TEAMS_APP_ID}}/$TEAMS_APP_ID/g" "$MANIFEST_PATH"
sed -i.bak "s/\${{AAD_APP_CLIENT_ID}}/$AAD_APP_CLIENT_ID/g" "$MANIFEST_PATH"
sed -i.bak "s/\${{BOT_DOMAIN}}/$BOT_DOMAIN/g" "$MANIFEST_PATH"

# Clean up backup file
rm "${MANIFEST_PATH}.bak"

echo "Successfully updated manifest.json with TEAMS_APP_ID=$TEAMS_APP_ID, AAD_APP_CLIENT_ID=$AAD_APP_CLIENT_ID, and BOT_DOMAIN=$BOT_DOMAIN"

# Set the zip filename
ZIP_FILENAME="appPackage.zip"

echo "Creating Teams app package..."

# Check if zip command is available
if ! command -v zip &>/dev/null; then
    echo "Error: 'zip' command not found. Please install zip before proceeding."
    exit 1
fi

# Remove any existing zip file
if [ -f "$ZIP_FILENAME" ]; then
    rm "$ZIP_FILENAME"
fi

# Create the zip file with only the contents (no directory structure)
echo "Zipping only the contents of appPackage directory..."
cd appPackage
zip -r "../$ZIP_FILENAME" . >/dev/null
cd ..

if [ $? -eq 0 ]; then
    echo "Successfully created Teams app package: $ZIP_FILENAME"
    echo "Manifest is ready for deployment!"
else
    echo "Error: Failed to create zip file"
    exit 1
fi
