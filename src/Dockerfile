FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM  --platform=$BUILDPLATFORM  mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src

# Copy project files for restore
COPY ["src/pefi.proxy.csproj", "src/"]
COPY ["Dashboard/Dashboard.csproj", "Dashboard/"]

RUN --mount=type=secret,id=github_token,env=GITHUB_TOKEN dotnet nuget add source --username petefield --password $GITHUB_TOKEN --store-password-in-clear-text --name petefield "https://nuget.pkg.github.com/petefield/index.json"

RUN dotnet restore "src/pefi.proxy.csproj"

# Copy all source
COPY src/ src/
COPY Dashboard/ Dashboard/

WORKDIR "/src/src"
RUN dotnet build "pefi.proxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN dotnet publish "pefi.proxy.csproj" -a "$TARGETARCH" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "pefi.proxy.dll"]
