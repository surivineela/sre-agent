set shell := ["bash", "-c"]
set windows-shell := ["pwsh.exe", "-NoLogo", "-CommandWithArgs"]

build:
  dotnet build ./src/Agent/Agent.sln

build-web:
  dotnet build ./src/Agent/Agent.Web/Agent.Web.csproj

run:
  dotnet run --project ./src/Agent/Agent.Web/Agent.Web.csproj

run-cmd *args='':
  dotnet run --project ./src/Agent/Agent.Cmd/Agent.Cmd.csproj -- {{args}}

alias build-react := react
react:
  cd ./src/Agent/Agent.Web/Client && npm install && npm run build

clean:
  dotnet clean ./src/Agent/Agent.sln

test-unit:
  dotnet test ./src/Agent/Agent.sln --filter "FullyQualifiedName~Agent.Tests.Unit"

test-integration:
  dotnet test ./src/Agent/Agent.sln --filter "FullyQualifiedName!~Agent.Tests&FullyQualifiedName!~Agent.Evals"

test: test-unit test-integration
  echo "All tests passed."

deploy3p *args='':
  bash -c "./src/Agent/Infra/Scripts/deploy.bash {{args}}"

delete3p *args='':
  bash -c "./src/Agent/Infra/Scripts/deploy.bash {{args}}"

setup-windows:
  winget install Microsoft.NuGet
  nuget.exe setApiKey az -Source https://msazure.pkgs.visualstudio.com/Antares/_packaging/antares-websites/nuget/v3/index.json

  winget install OpenJS.NodeJS.LTS
  npm install -g vsts-npm-auth --registry https://registry.npmjs.com --always-auth false
  vsts-npm-auth -config src\Agent\Agent.Web\Client\.npmrc
  npx vsts-npm-auth -R -E 131400 -C src\Agent\Agent.Web\Client\.npmrc

durable-emulator:
  "Starting Durable Task Emulator..." 
  "Connection string: Endpoint=http://localhost:14280;TaskHub=default;Authentication=None"
  "Dashboard url: http://localhost:14282"
  docker run --rm -it --name dts-emulator -p 14280:8080 -p 14282:8082 -e ClientAuth__DisableAuthentication=true mcr.microsoft.com/dts/dts-emulator:v0.0.6
