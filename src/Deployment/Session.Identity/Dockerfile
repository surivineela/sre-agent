# Use the official .NET 9 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0-azurelinux3.0 AS runtime

WORKDIR /app
COPY . .

# Run the application
ENTRYPOINT ["dotnet", "Session.Identity.dll"]
