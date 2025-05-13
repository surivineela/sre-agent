#!/bin/bash

# Script to set up npm authentication with Azure DevOps
# Usage: ./setup-npm-auth.sh <personal-access-token>

# Check if personal access token was provided
if [ -z "$1" ]; then
    echo "Error: Personal Access Token is required"
    echo "Usage: ./setup-npm-auth.sh <personal-access-token>"
    echo "go to https://dev.azure.com/msazure/_usersSettings/tokens to create a new token with 'Packaging (Read&Write)', 'Drop (Read&Write)' scope"
    exit 1
fi

PAT=$1
NPMRC_PATH=~/.npmrc

# Encode PAT to base64
ENCODED_PAT=$(echo -n "$PAT" | base64)

# Create or update .npmrc file
cat >"$NPMRC_PATH" <<EOL
//msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/registry/:username=msazure
//msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/registry/:_password="${ENCODED_PAT}"
//msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/:username=msazure
//msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/:_password="${ENCODED_PAT}"
//msazure.pkgs.visualstudio.com/One/_packaging/microsoft-logic-apps/npm/:email=npm requires email to be set but doesn't use the value
EOL

# Make the file readable only by the owner for security
chmod 600 "$NPMRC_PATH"

echo "✅ npm authentication has been set up successfully!"
echo "🔑 PAT has been encoded and added to $NPMRC_PATH"
echo "⚠️  Remember: This token will expire in 7 days. Run this script again with a new PAT when needed."
echo "📅 Setup date: $(date)"
