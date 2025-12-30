# Use the Python code interpreter image as the base
FROM mcr.microsoft.com/k8se/services/codeinterpreter:0.9.34-python3.12 AS runtime

USER root

ARG AZCLI_VERSION=2.76.0

# Install .NET 9 SDK (includes runtime) for Session.Proxy
RUN apt-get update && \
    apt-get install -y wget libicu-dev && \
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet && \
    rm dotnet-install.sh && \
    ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Install Azure CLI
# https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-linux?view=azure-cli-latest&pivots=apt#option-2-step-by-step-installation-instructions
RUN apt-get install -y ca-certificates curl gnupg lsb-release && \
    mkdir -p /etc/apt/keyrings && \
    curl -sLS https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | tee /etc/apt/keyrings/microsoft.gpg > /dev/null && \
    chmod go+r /etc/apt/keyrings/microsoft.gpg && \
    AZ_DIST=$(lsb_release -cs) && \
    printf "Types: deb \nURIs: https://packages.microsoft.com/repos/azure-cli/ \nSuites: ${AZ_DIST} \nComponents: main \nArchitectures: $(dpkg --print-architecture) \nSigned-by: /etc/apt/keyrings/microsoft.gpg" | tee /etc/apt/sources.list.d/azure-cli.sources && \
    apt-get update && \
    apt-get install -y azure-cli=${AZCLI_VERSION}-1~${AZ_DIST} && \
    # Install kubectl
    curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl" && \
    install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl && \
    rm kubectl

# Install Node.js (if not already present - code interpreter base may have it)
RUN if ! command -v node &> /dev/null; then \
        curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
        apt-get install -y nodejs; \
    fi

# Install Python packages
RUN pip install azure-identity GitPython

# Copy Session.Proxy published output
WORKDIR /app/session-proxy
COPY . .

# Temporarily install azure devops mcp server from source code instead of npm registry due to federation auth
RUN apt-get install -y git && \
    git clone https://github.com/howang-ms/azure-devops-mcp.git /azdevop && \
    cd /azdevop && \
    npm install && \
    npm run build && \
    mkdir -p /app/session-proxy/mcp-tools && \
    cp -r /azdevop /app/session-proxy/mcp-tools/ && \
    cd / && \
    rm -rf /azdevop

# Clean up apt cache
RUN apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Install Azure CLI extensions
RUN az extension add --allow-preview -y --system --name containerapp && \
    az extension add --allow-preview -y --system --name log-analytics && \
    az extension add --allow-preview -y --system --name kusto && \
    az extension add --allow-preview -y --system --name vm-repair

# Set environment variable for Session.Proxy to proxy to code interpreter
ENV PythonProxyService__TargetUrl="http://localhost:6000"

# Set ENABLE_EGRESS to true to skip connectivity configuration in entrypoint
ENV ENABLE_EGRESS=true

WORKDIR /app

# Copy and set up the custom entrypoint script
COPY start-session-proxy.sh /app/start-session-proxy.sh
RUN chmod +x /app/start-session-proxy.sh

# Run the custom entrypoint that starts Session.Proxy before the original entrypoint
ENTRYPOINT ["/app/start-session-proxy.sh"]
