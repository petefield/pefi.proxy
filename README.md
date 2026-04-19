# pefi.proxy

A dynamic reverse proxy built with [.NET 9](https://dotnet.microsoft.com/) and [YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/). It automatically discovers and routes HTTP traffic to microservices based on hostname matching, integrating with a central Service Manager API and RabbitMQ for real-time configuration updates.

## Features

- **Dynamic routing** – Routes are updated automatically when services are registered or deregistered via the Service Manager API.
- **Real-time configuration** – Listens to RabbitMQ events (`events.service.#`) and refreshes in-memory YARP configuration without downtime.
- **Static routes** – Bootstrap routes can be defined in `appsettings.json` for services that are always present.
- **Docker-ready** – Multi-stage Dockerfile included with secure secret handling for private NuGet feeds.
- **OpenAPI/Swagger** – Built-in Swagger UI for exploring the proxy's own endpoints.

## Architecture

```
Incoming Request
       │  (Host: <service-name>.pefi.co.uk)
       ▼
YARP Reverse Proxy
       │
       ├─ Static Routes  (appsettings.json)
       └─ Dynamic Routes (in-memory, updated at runtime)
                │
       ProxyConfigUpdater (background service)
                │
       RabbitMQ (events.service.#)
                │
       Service Manager API  ──► Destination Services
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
| `ServiceManager:baseurl` | `ServiceManager__baseurl` | Base URL of the Service Manager API |
| `Messaging:Address` | `Messaging__Address` | RabbitMQ broker hostname or IP |
| `Messaging:Username` | `Messaging__Username` | RabbitMQ username |
| `Messaging:Password` | `Messaging__Password` | RabbitMQ password |

### Static Routes

Static routes and clusters are defined in `appsettings.json` under the `ReverseProxy` key. They are merged with the dynamic routes at runtime.

```json
{
  "ReverseProxy": {
    "Routes": {
      "my-static-service": {
        "ClusterId": "my-static-cluster",
        "Match": {
          "Hosts": ["my-static-service.pefi.co.uk"]
        }
      }
    },
    "Clusters": {
      "my-static-cluster": {
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

### Running locally

```bash
# Clone the repository
git clone https://github.com/petefield/pefi.proxy.git
cd pefi.proxy

# Set required environment variables (or configure in launchSettings.json)
export ServiceManager__baseurl=http://localhost:5550
export Messaging__Address=localhost
export Messaging__Username=guest
export Messaging__Password=guest

# Run the application
dotnet run --project src/pefi.proxy.csproj
```

The proxy will start on `http://localhost:5000` (or the port configured in `launchSettings.json`). A `/config` endpoint is available to inspect the merged route and cluster configuration.

### Running with Docker Compose

The quickest way to run the proxy:

```bash
# Set RabbitMQ connection (or use defaults in docker-compose.yml)
export RABBITMQ_HOST=your-rabbitmq-host
export RABBITMQ_USER=guest
export RABBITMQ_PASS=guest

docker compose up -d
```

This starts **pefi.proxy** on `http://localhost:8080` (dashboard, config API, reverse proxy). RabbitMQ is expected to be running separately.

To stop:

```bash
docker compose down
```

### Running with Docker

```bash
# Build the image (requires a GitHub token for private NuGet packages)
docker build --secret id=github_token,env=GITHUB_TOKEN \
             -f src/Dockerfile -t pefi-proxy:latest .

# Run the container
docker run -p 8080:8080 \
  -e "ServiceManager__baseurl=http://host.docker.internal:5550" \
  -e "Messaging__Address=host.docker.internal" \
  -e "Messaging__Username=guest" \
  -e "Messaging__Password=guest" \
  pefi-proxy:latest
```

## API Endpoints

| Endpoint | Method | Description |
|---|---|---|
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
| `MappersTests` | Unit tests for `Mappers.cs` – verifies that `ServiceDescription` objects are correctly converted to YARP `RouteConfig` and `ClusterConfig` instances, including null-safety for missing hostname or port. |
| `ProxyConfigUpdaterTests` | Unit tests for `ProxyConfigUpdater` – verifies RabbitMQ topic creation, subscription to `events.service.#`, startup service loading, and in-memory config updates (filtering services with null hostname/port). |
| `ConfigEndpointTests` | Integration tests using `WebApplicationFactory<Program>` – verifies that the `/config` endpoint returns HTTP 200 with a JSON body containing routes and clusters, and that static routes from `appsettings.json` are present. |

Dependencies used in tests: [xUnit](https://xunit.net/), [NSubstitute](https://nsubstitute.github.io/), and `Microsoft.AspNetCore.Mvc.Testing`.

## Project Structure

```
pefi.proxy/
├── src/
│   ├── Models/
│   │   └── ServiceDescription.cs        # Service domain model (MongoDB)
│   ├── Services/
│   │   ├── ServiceManager.cs            # Auto-generated Service Manager HTTP client
│   │   └── service_mgr_openapi.json     # Service Manager OpenAPI specification
│   ├── Program.cs                       # Application entry point and DI setup
│   ├── ProxyConfigUpdater.cs            # Background service for real-time config updates
│   ├── ServiceCollectionExtensions.cs   # Messaging DI extensions
│   ├── Mappers.cs                       # Maps ServiceDescription → YARP route/cluster config
│   ├── GenerateHttpClientAttribute.cs   # Marker for the pefi.http source generator
│   ├── ServiceCreatedMessage.cs         # RabbitMQ message record
│   ├── appsettings.json                 # Default configuration and static routes
│   ├── appsettings.Development.json     # Development logging overrides
│   ├── Dockerfile                       # Multi-stage Docker build
│   └── pefi.proxy.csproj               # Project file (.NET 9.0)
├── tests/
│   └── pefi.proxy.tests/
│       ├── MappersTests.cs              # Unit tests for route/cluster mapping
│       ├── ProxyConfigUpdaterTests.cs   # Unit tests for the background config updater
│       ├── ConfigEndpointTests.cs       # Integration tests for the /config endpoint
│       ├── MockHttpMessageHandler.cs    # Fake HTTP handler used in tests
│       └── pefi.proxy.tests.csproj     # Test project file (xUnit, NSubstitute)
└── LICENSE                              # GNU AGPLv3
```

## Key Dependencies

| Package | Purpose |
|---|---|
| `Yarp.ReverseProxy` | Core reverse proxy framework |
| `pefi.messaging.rabbit` | RabbitMQ messaging integration |
| `pefi.http` | Source-generator-based HTTP client from OpenAPI specs |
| `MongoDB.Driver` | MongoDB persistence |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI UI |

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE). Any modifications to this software that are used to provide a network service must be made available under the same license.
