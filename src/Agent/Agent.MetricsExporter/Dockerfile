FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY . .

# Expose the port the app runs on
EXPOSE 80
EXPOSE 9090

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "Agent.MetricsExporter.dll"]
