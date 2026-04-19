# pefi.proxy

A dynamic reverse proxy built with [.NET 9](https://dotnet.microsoft.com/) and [YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/). It automatically discovers and routes HTTP traffic to microservices based on hostname matching, integrating with a central Service Manager API and RabbitMQ for real-time configuration updates.

## Features

- **Dynamic routing** -- Routes are updated automatically when services are registered or deregistered via the Service Manager API.
- **Real-time configuration** -- Listens to RabbitMQ events (`events.service.#`) and refreshes in-memory YARP configuration without downtime.
- **Static routes** -- Routes can be defined via environment variables or `appsettings.json` for services that are always present.
- **Dashboard** -- Embedded Blazor WebAssembly UI at `/` for viewing active routes and cluster mappings.
- **Docker-ready** -- Multi-stage Dockerfile included with secure secret handling for private NuGet feeds.
- **OpenAPI/Swagger** -- Built-in Swagger UI for exploring the proxy's own endpoints.

## Architecture

```
Incoming Request
       |  (Host: <service-name>.pefi.co.uk)
       v
YARP Reverse Proxy
       |
       +-- Static Routes  (env vars / appsettings.json)
       +-- Dynamic Routes (in-memory, updated at runtime)
                |
       ProxyConfigUpdater (background service)
                |
       RabbitMQ (events.service.#)
                |
       Service Manager API  --> Destination Services
                                (http://host.docker.internal:<port>)
```

**Request flow:**
1. An HTTP request arrives with a `Host` header such as `payment-api.pefi.co.uk`.
2. YARP matches the request against the merged set of static and dynamic routes.
3. The request is forwarded to the corresponding Docker-hosted service.
4. In the background, `ProxyConfigUpdater` subscribes to RabbitMQ messages. When a service is created or removed, it fetches the current list from the Service Manager API and updates the in-memory YARP configuration with zero downtime.

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 9.0+ |
| RabbitMQ | Any recent version |
| Service Manager API | [pefi.servicemanager](https://github.com/petefield) |
| Docker | Optional, for containerised deployment |

## Configuration

Configuration is loaded from `appsettings.json`, environment-specific overrides (`appsettings.{Environment}.json`), and environment variables. Environment variables use double-underscore (`__`) as the section separator.

| Setting | Environment Variable | Description |
|---|---|---|
| `Messaging:address` | `Messaging__address` | RabbitMQ broker hostname or IP |
| `Messaging:username` | `Messaging__username` | RabbitMQ username |
| `Messaging:password` | `Messaging__password` | RabbitMQ password |

### Static Routes

Static routes can be configured via environment variables (recommended for Docker) or in `appsettings.json`.

**Via environment variables (`.env` file):**

```bash
ReverseProxy__Routes__my-service__ClusterId=my-service
ReverseProxy__Routes__my-service__Match__Hosts__0=my-service.pefi.co.uk
ReverseProxy__Clusters__my-service__Destinations__destination1__Address=http://host.docker.internal:7005
```

**Via `appsettings.json`:**

```json
{
  "ReverseProxy": {
    "Routes": {
      "my-service": {
        "ClusterId": "my-service",
        "Match": {
          "Hosts": ["my-service.pefi.co.uk"]
        }
      }
    },
    "Clusters": {
      "my-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://host.docker.internal:7005"
          }
        }
      }
    }
  }
}
```

## Getting Started

### Running with Docker Compose

The quickest way to run the proxy:

```bash
# Copy the example env file and edit as needed
cp .env.example .env

# Start the proxy
docker compose up -d
```

This starts **pefi.proxy** on `http://localhost:5053` (dashboard at `/dashboard`, config API at `/config`, reverse proxy). RabbitMQ is expected to be running separately. Configure routes in the `.env` file.

To stop:

```bash
docker compose down
```

### Running locally

```bash
# Clone the repository
git clone https://github.com/petefield/pefi.proxy.git
cd pefi.proxy

# Set required environment variables (or configure in launchSettings.json)
export Messaging__address=localhost
export Messaging__username=guest
export Messaging__password=guest

# Run the application
dotnet run --project src/pefi.proxy.csproj
```

The proxy will start on `http://localhost:5053` (or the port configured in `launchSettings.json`). The dashboard is available at `/dashboard` and the config API at `/config`.

### Running with Docker

```bash
# Build the image (requires a GitHub token for private NuGet packages)
docker build --secret id=github_token,env=GITHUB_TOKEN \
             -f src/Dockerfile -t pefi-proxy:latest .

# Run the container
docker run -p 5053:8080 \
  -e "Messaging__address=host.docker.internal" \
  -e "Messaging__username=guest" \
  -e "Messaging__password=guest" \
  --add-host=host.docker.internal:host-gateway \
  pefi-proxy:latest
```

## API Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/dashboard` | GET | Blazor WASM dashboard for viewing routes |
| `/dashboard/routes` | GET | Dashboard routes page |
| `/config` | GET | Returns the current merged proxy configuration (routes + clusters) |
| `/swagger` | GET | Swagger UI for exploring available endpoints |

## Testing

The solution includes an xUnit test project covering the main components of the proxy.

```bash
# Run all tests
dotnet test tests/pefi.proxy.tests/pefi.proxy.tests.csproj
```

| Test class | What it covers |
|---|---|
| `MappersTests` | Unit tests for `Mappers.cs` -- verifies that service responses are correctly converted to YARP `RouteConfig` and `ClusterConfig` instances, including null-safety for missing hostname or port. |
| `ProxyConfigUpdaterTests` | Unit tests for `ProxyConfigUpdater` -- verifies RabbitMQ topic creation, subscription to `events.service.#`, startup service loading, and in-memory config updates (filtering services with null hostname/port). |
| `ConfigEndpointTests` | Integration tests using `WebApplicationFactory<Program>` -- verifies that the `/config` endpoint returns HTTP 200 with a JSON body containing routes and clusters, and that static routes from `appsettings.json` are present. |

Dependencies used in tests: [xUnit](https://xunit.net/), [NSubstitute](https://nsubstitute.github.io/), and `Microsoft.AspNetCore.Mvc.Testing`.

## Project Structure

```
pefi.proxy/
├── Dashboard/
│   ├── Layout/                          # Blazor layout and nav components
│   ├── Pages/                           # Dashboard pages (Home, Routes)
│   ├── wwwroot/                         # Static assets (CSS, icons)
│   ├── Program.cs                       # Blazor WASM entry point
│   └── Dashboard.csproj                 # Blazor WebAssembly project
├── src/
│   ├── Services/
│   │   ├── ServiceManager.cs            # Auto-generated Service Manager HTTP client
│   │   └── service_mgr_openapi.json     # Service Manager OpenAPI specification
│   ├── Program.cs                       # Application entry point and DI setup
│   ├── ProxyConfigUpdater.cs            # Background service for real-time config updates
│   ├── ServiceCollectionExtensions.cs   # Messaging DI extensions
│   ├── Mappers.cs                       # Maps service responses to YARP route/cluster config
│   ├── GenerateHttpClientAttribute.cs   # Marker for the pefi.http source generator
│   ├── appsettings.json                 # Default configuration and static routes
│   ├── Dockerfile                       # Multi-stage Docker build
│   └── pefi.proxy.csproj               # Project file (.NET 9.0)
├── tests/
│   └── pefi.proxy.tests/
│       ├── MappersTests.cs              # Unit tests for route/cluster mapping
│       ├── ProxyConfigUpdaterTests.cs   # Unit tests for the background config updater
│       ├── ConfigEndpointTests.cs       # Integration tests for the /config endpoint
│       ├── MockHttpMessageHandler.cs    # Fake HTTP handler used in tests
│       └── pefi.proxy.tests.csproj     # Test project file (xUnit, NSubstitute)
├── .env.example                         # Example environment configuration
├── docker-compose.yml                   # Docker Compose for running the proxy
└── LICENSE                              # GNU AGPLv3
```

## Key Dependencies

| Package | Purpose |
|---|---|
| `Yarp.ReverseProxy` | Core reverse proxy framework |
| `pefi.messaging.rabbit` | RabbitMQ messaging integration |
| `pefi.http` | Source-generator-based HTTP client from OpenAPI specs |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI UI |

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE). Any modifications to this software that are used to provide a network service must be made available under the same license.
