# Use the official .NET 9 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-linux?view=azure-cli-latest&pivots=apt#option-2-step-by-step-installation-instructions
RUN apt-get update && \
    apt-get install -y apt-transport-https ca-certificates curl gnupg lsb-release && \
    mkdir -p /etc/apt/keyrings && \
    curl -sLS https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | tee /etc/apt/keyrings/microsoft.gpg > /dev/null && \
    chmod go+r /etc/apt/keyrings/microsoft.gpg && \
    AZ_DIST=$(lsb_release -cs) && \
    printf "Types: deb \nURIs: https://packages.microsoft.com/repos/azure-cli/ \nSuites: ${AZ_DIST} \nComponents: main \nArchitectures: $(dpkg --print-architecture) \nSigned-by: /etc/apt/keyrings/microsoft.gpg" | tee /etc/apt/sources.list.d/azure-cli.sources && \
    apt-get update && \
    apt-get install -y azure-cli && \
    # Install kubectl
    curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl" && \
    install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl && \
    rm kubectl

WORKDIR /app
COPY . .

# Run the application
ENTRYPOINT ["dotnet", "Session.Cli.dll"]
