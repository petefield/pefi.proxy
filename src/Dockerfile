#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["pefi.proxy.csproj", "."]
RUN --mount=type=secret,id=github_token,env=GITHUB_TOKEN dotnet nuget add source --username petefield --password $GITHUB_TOKEN --store-password-in-clear-text --name petefield "https://nuget.pkg.github.com/petefield/index.json"

RUN dotnet restore "./././pefi.proxy.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./pefi.proxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./pefi.proxy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "pefi.proxy.dll"]