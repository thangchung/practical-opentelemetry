#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 5003
EXPOSE 15003

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build

ARG feed='--source "https://api.nuget.org/v3/index.json;https://www.myget.org/F/opentelemetry/api/v3/index.json"'

WORKDIR /src
COPY ["src/MeteoriteService/MeteoriteService.csproj", "src/MeteoriteService/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/MeteoriteService/MeteoriteService.csproj" ${feed} -nowarn:msb3202,nu1503
COPY . .
WORKDIR "/src/src/MeteoriteService"
RUN dotnet build "MeteoriteService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MeteoriteService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MeteoriteService.dll"]