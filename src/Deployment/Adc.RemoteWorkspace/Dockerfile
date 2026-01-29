# Use the official .NET 9 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0-azurelinux3.0 AS runtime

# Install tools required by the workspace agent
RUN tdnf install -y \
    git \
    bash \
    procps-ng \
    curl \
    wget \
    tcpdump \
    && tdnf clean all

WORKDIR /app
COPY . .

# Set environment variables (optional)
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose the port the app runs on
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "Adc.RemoteWorkspace.dll"]
