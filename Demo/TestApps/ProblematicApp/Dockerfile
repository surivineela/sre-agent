FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
COPY --from=build /src/dotnet-dump .
COPY --from=build /src/dotnet_dump_cmd.txt .
COPY --from=build /src/collect-dump.sh .

WORKDIR /app
RUN mkdir -p scripts
COPY *.sh ./scripts/
RUN chmod +x ./scripts/*.sh

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    curl \
    procps \
    net-tools \
    dnsutils \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet tool install --global dotnet-trace --version 9.0.553101 && \
    dotnet tool install --global dotnet-dump --version 9.0.553101

ENV PATH="$PATH:/root/.dotnet/tools"

EXPOSE 5877
WORKDIR /app
ENTRYPOINT ["dotnet", "ProblematicApp.dll"]
