FROM mcr.microsoft.com/azurelinux/base/python:3.12 AS installer  

# (Installer stage) Only prepare customized azure cli python site-packages
  
WORKDIR /

# Use the official .NET 9 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-linux?view=azure-cli-latest&pivots=apt#option-2-step-by-step-installation-instructions
RUN apt-get update && \
    apt-get install -y apt-transport-https ca-certificates curl gnupg lsb-release git curl postgresql-client && \
    # Install Node.js 20 (required for MCP CLI)
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    node -v && npm -v && \
    echo 'Installing MCP CLI (@azure/mcp@0.5.11)...' && \
    (npm install -g @azure/mcp@0.5.11 || (echo 'Fallback: cloning repo'; git clone --depth 1 --branch v0.5.11 https://github.com/Azure/mcp /tmp/mcp && cd /tmp/mcp && npm ci && npm install -g .)) && \
    npm cache clean --force && rm -rf /var/lib/apt/lists/* /tmp/mcp && \
    # Install kubectl
    curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl" && \
    install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl && \
    rm kubectl

WORKDIR /app
COPY . .

# Set environment variables (optional)
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose the port the app runs on
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "Agent.Web.dll", "--session"]
