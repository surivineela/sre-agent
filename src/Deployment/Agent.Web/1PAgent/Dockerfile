# Use the official .NET 9 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY . .

# Set environment variables (optional)
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Hack to ensure AGENT uses UAMI as the default authentication method
ENV AZURE_CLIENT_ID="5cc4f734-a234-443c-9092-04e06a2b80c2"

# Expose the port the app runs on
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "Agent.Web.dll", "--session"]
