# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY PORMS.sln ./
COPY Directory.Build.props ./
COPY PORMS.API/PORMS.API.csproj PORMS.API/
COPY PORMS.Infrastructure/PORMS.Infrastructure.csproj PORMS.Infrastructure/
COPY PORMS.Tests/PORMS.Tests.csproj PORMS.Tests/

RUN dotnet restore PORMS.sln

FROM restore AS build
COPY . .
RUN dotnet build PORMS.API/PORMS.API.csproj \
    --configuration Release \
    --no-restore

FROM build AS publish
RUN dotnet publish PORMS.API/PORMS.API.csproj \
    --configuration Release \
    --no-restore \
    --no-build \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=restore /root/.nuget/packages /root/.nuget/packages
COPY . .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "run", "--project", "PORMS.API/PORMS.API.csproj", "--urls", "http://+:5000"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PORMS.API.dll"]
