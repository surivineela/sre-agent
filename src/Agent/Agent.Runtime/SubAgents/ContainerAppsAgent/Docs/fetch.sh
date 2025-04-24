#! /bin/bash

# Variables
FOLDER_PATH="content/docs/2.16/scalers" # Replace with the relative folder path in the repository
OUTPUT_FILE="main.zip" # Output zip filename

# Get the folder as a zip file
curl -L -o "$OUTPUT_FILE" "https://github.com/kedacore/keda-docs/archive/main.zip"

# Unzip the file
unzip "$OUTPUT_FILE"

# Move the folder to the current directory
mv "$REPO_NAME-$BRANCH/$FOLDER_PATH" .

# Clean up
rm "$OUTPUT_FILE"
rm -rf "$REPO_NAME-$BRANCH"

echo "Folder downloaded and extracted to the current directory."
