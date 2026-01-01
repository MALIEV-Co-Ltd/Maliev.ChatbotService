# Quickstart: Chatbot Service

## Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for Testcontainers & Infrastructure)
- Google Gemini API Key

## Configuration

Set the following environment variables (or use `dotnet user-secrets`):

```bash
# Database
ConnectionStrings__ChatbotDbContext="Host=localhost;Database=chatbot_app_db;Username=postgres;Password=postgres"

# Cache
ConnectionStrings__Cache="localhost:6379"

# RabbitMQ (MassTransit)
RabbitMQ__Host="localhost"
RabbitMQ__Username="guest"
RabbitMQ__Password="guest"

# Gemini API
Gemini__ApiKey="YOUR_API_KEY"

# JWT Authentication (optional for local dev)
Jwt__Key="your-base64-encoded-rsa-public-key"
Jwt__Issuer="Maliev.IAMService"
Jwt__Audience="Maliev.Services"
```

## Running Locally

### Option 1: Using Aspire (Recommended)
```bash
cd Maliev.Aspire/Maliev.Aspire.AppHost
dotnet run
```
This starts all infrastructure (PostgreSQL, Redis, RabbitMQ) automatically.

### Option 2: Manual Infrastructure
```bash
# Start infrastructure with Docker
docker run -d --name postgres -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:18
docker run -d --name redis -p 6379:6379 redis:7
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### Run the Service
```bash
cd Maliev.ChatbotService.Api
dotnet run
```

## Verify

| Endpoint | URL |
|----------|-----|
| API Documentation (Scalar) | `http://localhost:5000/chatbot/scalar` |
| OpenAPI Spec | `http://localhost:5000/chatbot/openapi/v1.json` |
| Liveness Probe | `http://localhost:5000/chatbot/liveness` |
| Readiness Probe | `http://localhost:5000/chatbot/readiness` |
| Prometheus Metrics | `http://localhost:5000/chatbot/metrics` |

## Testing

Run integration tests (requires Docker for Testcontainers):
```bash
dotnet test Maliev.ChatbotService.Tests --verbosity normal
```

## Rate Limiting

- **Limit**: 100 messages per hour per user
- **Window**: Sliding window (1-hour rolling window)
- **Tracking**: Per `UserProfileId` or `externalUserId` for anonymous users
- **Response**: HTTP 429 with `Retry-After` header when exceeded
