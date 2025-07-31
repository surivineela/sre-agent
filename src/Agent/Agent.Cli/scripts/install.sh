#!/bin/bash

FEED_URL="https://pkgs.dev.azure.com/msazure/One/_packaging/SREAgentCli/nuget/v3/index.json"

# Uninstall existing version
dotnet tool uninstall sreagent.cli --global 2>/dev/null

# Install new version from feed
dotnet tool install sreagent.cli --global --add-source "$FEED_URL" --verbosity normal
