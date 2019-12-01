#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build

ARG feed='--source "https://api.nuget.org/v3/index.json;https://www.myget.org/F/opentelemetry/api/v3/index.json"'

WORKDIR /src
COPY ["src/SampleWeb/SampleWeb.csproj", "src/SampleWeb/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/SampleWeb/SampleWeb.csproj" ${feed} -nowarn:msb3202,nu1503
COPY . .
WORKDIR "/src/src/SampleWeb"
RUN dotnet build "SampleWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SampleWeb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SampleWeb.dll"]