#!/bin/bash

# Exit on error
set -e

# Source .env file if it exists
if [ -f .env ]; then
    echo "Loading environment variables from .env file"
    source .env
fi

# Check if environment variables are set
if [ -z "$TEAMS_APP_ID" ]; then
    echo "Error: TEAMS_APP_ID environment variable is not set"
    exit 1
fi

if [ -z "$BOT_ID" ]; then
    echo "Error: BOT_ID environment variable is not set"
    exit 1
fi

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
sed -i.bak "s/\${{BOT_ID}}/$BOT_ID/g" "$MANIFEST_PATH"

# Clean up backup file
rm "${MANIFEST_PATH}.bak"

echo "Successfully updated manifest.json with TEAMS_APP_ID=$TEAMS_APP_ID and BOT_ID=$BOT_ID"

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
