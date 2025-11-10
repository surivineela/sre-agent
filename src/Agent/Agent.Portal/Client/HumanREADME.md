# A README for humans (no clankers allowed)

## Publising to `antares-website` feed

Feed: https://msazure.visualstudio.com/Antares/_artifacts/feed/antares-websites/NuGet/Microsoft.SREAgent.Portal/overview

1. Build the portal - `dotnet build src/Agent/Agent.Portal/Agent.Portal.csproj --no-restore -c Release`
2. Pack the build (increment the version number) - `dotnet pack src/Agent/Agent.Portal/Agent.Portal.csproj --no-restore --no-build -c Release -o ./artifacts -p:Version=1.0.3`
3. Publish the package - `dotnet nuget push ./artifacts/Microsoft.SREAgent.Portal.<version>.nupkg --source https://msazure.pkgs.visualstudio.com/Antares/_packaging/antares-websites/nuget/v3/index.json --api-key AzureDevOps` *You may not need the `--api-key` arg if you run the `vsts-npm-auth` right before?
4. To deploy, update the version in `Directory.Packages.props` in the [ControlPlane repo](https://dev.azure.com/msazure/One/_git/AAPT-SREAgent-ControlPlane?path=/deployment), then deploy through their standard practice
