param(
    [string]$FeedUrl = "https://pkgs.dev.azure.com/msazure/One/_packaging/SREAgentCli/nuget/v3/index.json"
)

# Optional: ensure NuGet Credential Provider is available (needed for private feeds)
# You can install it from https://github.com/microsoft/artifacts-credprovider

dotnet tool uninstall sreagent.cli --global 2>$null

dotnet tool install sreagent.cli --global --add-source $FeedUrl --verbosity normal
