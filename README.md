# Maliev Unified Chatbot Service

Central conversational AI service for the Maliev ecosystem, providing multi-channel chatbot capabilities with Gemini AI integration.

## Overview

The Chatbot Service provides intelligent, context-aware conversational AI across multiple communication channels (Website, LINE, Facebook, Instagram, WhatsApp, Internal). It leverages Google's Gemini AI to deliver natural language understanding and generation while maintaining strict business boundaries and consistent user experiences.

## Features

### Core Capabilities
- **Multi-Channel Support**: Seamless conversations across Website, LINE, Facebook, Instagram, WhatsApp, and Intranet
- **Multimodal Input**: Process text, images, PDFs, audio, and video
- **Persistent User Memory**: Remember user preferences and conversation context
- **Cross-Platform Continuity**: Continue conversations across different channels
- **Business Boundary Enforcement**: Focus on manufacturing and B2B topics
- **Internal CRM Agent**: Assist internal users with quotation/order queries
- **Controlled Web Search**: Access technical specifications when permitted
- **Graceful Degradation**: Fallback mechanisms for API failures

### Technical Features
- **Rate Limiting**: 100 messages/hour per user with sliding window
- **Session Management**: 24-hour session expiry with automatic summarization
- **Language Detection**: Automatic English/Thai language detection
- **Structured Responses**: Format responses with quick action buttons
- **Audit Logging**: Comprehensive operation and search domain logging
- **Business Metrics**: Prometheus metrics for conversation volume, latency, success rates

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       API Layer (Api/)                          │
│  Controllers: Sessions, Messages, UserData, Webhooks, Admin     │
│  Middleware: Error Handling, Correlation ID                     │
│  DTOs: Request/Response models with Data Annotations            │
└────────────────────┬────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│                  Application Layer (Application/)               │
│  Commands: InitiateSession, SendMessage                         │
│  Handlers: CQRS command and query handlers                      │
│  Interfaces: Repository and service contracts                   │
└────────────────────┬────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│                   Domain Layer (Domain/)                        │
│  Entities: UserProfile, ConversationSession, Message, etc.      │
│  Enums: Channel, Language, SessionStatus, UserRole, etc.        │
└─────────────────────────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│              Infrastructure Layer (Infrastructure/)             │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ AI: GeminiClient, ResponseFormatter, LanguageDetection   │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │ Data: ChatbotDbContext, Repositories, Configurations     │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │ Services: RateLimit, InputValidation, SessionExpiry      │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │ ExternalServices: IAMClient, LineClient, MetaClient      │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │ BackgroundServices: SessionExpiryBackgroundService       │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│    External Dependencies                                        │
│  ┌────────────┬─────────────┬──────────────┬─────────────────┐ │
│  │ PostgreSQL │   Redis     │  RabbitMQ    │   Gemini API    │ │
│  │   (DB)     │  (Cache)    │ (Events)     │  (AI Model)     │ │
│  └────────────┴─────────────┴──────────────┴─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET | 10.0 |
| Language | C# | 13 |
| Database | PostgreSQL | 18 |
| Cache | Redis | 7.x |
| Messaging | RabbitMQ | via MassTransit |
| ORM | Entity Framework Core | 10.x |
| Testing | xUnit + Testcontainers | - |
| AI Model | Google Gemini | gemini-2.5-flash |
| API Docs | Scalar | - |
| Metrics | Prometheus | via OpenTelemetry |

## Local Development

### Prerequisites

- .NET 10.0 SDK
- Docker Desktop (for Testcontainers & infrastructure)
- Google Gemini API Key ([Get one here](https://aistudio.google.com/apikey))

### Configuration

#### Option 1: User Secrets (Recommended for Development)

```bash
cd Maliev.ChatbotService.Api

# Set Gemini API Key
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY_HERE"

# Optionally configure other services
dotnet user-secrets set "ConnectionStrings:ChatbotDbContext" "Host=localhost;Port=5432;Database=chatbot_app_db;Username=postgres;Password=postgres"
dotnet user-secrets set "ConnectionStrings:Cache" "localhost:6379"
```

#### Option 2: Environment Variables

```bash
# Windows (PowerShell)
$env:Gemini__ApiKey="YOUR_GEMINI_API_KEY_HERE"
$env:ConnectionStrings__ChatbotDbContext="Host=localhost;Port=5432;Database=chatbot_app_db;Username=postgres;Password=postgres"
$env:ConnectionStrings__Cache="localhost:6379"

# Linux/macOS
export Gemini__ApiKey="YOUR_GEMINI_API_KEY_HERE"
export ConnectionStrings__ChatbotDbContext="Host=localhost;Port=5432;Database=chatbot_app_db;Username=postgres;Password=postgres"
export ConnectionStrings__Cache="localhost:6379"
```

#### Option 3: appsettings.Development.json (Not Recommended - Risk of Committing Secrets)

Edit `Maliev.ChatbotService.Api/appsettings.Development.json`:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY_HERE"
  }
}
```

### Running Locally

#### Option 1: Using Aspire Orchestrator (Recommended)

```bash
cd Maliev.Aspire/Maliev.Aspire.AppHost
dotnet run
```

This automatically starts all required infrastructure (PostgreSQL, Redis, RabbitMQ) and the service.

#### Option 2: Manual Infrastructure Setup

```bash
# Start infrastructure with Docker
docker run -d --name postgres -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:18
docker run -d --name redis -p 6379:6379 redis:7-alpine
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management-alpine

# Run the service
cd Maliev.ChatbotService.Api
dotnet run
```

### Verify Service is Running

| Endpoint | URL | Description |
|----------|-----|-------------|
| API Documentation (Scalar) | http://localhost:5000/chatbot/scalar | Interactive API documentation |
| OpenAPI Spec | http://localhost:5000/chatbot/openapi/v1.json | OpenAPI 3.0 specification |
| Liveness Probe | http://localhost:5000/chatbot/liveness | Kubernetes liveness probe |
| Readiness Probe | http://localhost:5000/chatbot/readiness | Kubernetes readiness probe |
| Prometheus Metrics | http://localhost:5000/chatbot/metrics | Business metrics |

### Testing

Run integration tests (requires Docker Desktop for Testcontainers):

```bash
dotnet test Maliev.ChatbotService.Tests --verbosity normal
```

**Note**: Tests automatically spin up real PostgreSQL, Redis, and RabbitMQ containers via Testcontainers.

## API Documentation

### REST Endpoints

#### Session Management

```http
POST /chatbot/v1/sessions/initiate
```
Initiate a new conversation session. Supports auto-detection of language (English/Thai).

#### Messaging

```http
POST /chatbot/v1/messages
```
Send a message to the chatbot. Supports multimodal input (text, images, PDFs, audio, video).

#### User Data Management

```http
GET /chatbot/v1/users/me/preferences
DELETE /chatbot/v1/users/me/data?scope=preferences|history|all
```

#### Webhooks (Multi-Channel)

```http
POST /chatbot/v1/webhooks/line
POST /chatbot/v1/webhooks/facebook
POST /chatbot/v1/webhooks/instagram
POST /chatbot/v1/webhooks/whatsapp
```

#### Admin (System Instructions)

```http
GET /chatbot/v1/admin/instructions
POST /chatbot/v1/admin/instructions
PUT /chatbot/v1/admin/instructions/{id}
DELETE /chatbot/v1/admin/instructions/{id}
```

### Health Endpoints

- `/chatbot/liveness` - Kubernetes liveness probe
- `/chatbot/readiness` - Kubernetes readiness probe
- `/chatbot/metrics` - Prometheus metrics

### Business Metrics (Prometheus)

| Metric | Type | Description |
|--------|------|-------------|
| `conversation_volume` | Counter | Total number of conversations processed |
| `response_latency_p90` | Histogram | 90th percentile response latency |
| `gemini_api_success_rate` | Gauge | Gemini API success rate (%) |
| `active_sessions_count` | Gauge | Number of active conversation sessions |
| `user_satisfaction_score` | Gauge | User satisfaction score (if available) |

All metrics include tags: `service_name`, `version`, `region`, `environment`

## Configuration Reference

### Gemini AI Configuration

```json
{
  "Gemini": {
    "ApiKey": "YOUR_API_KEY",
    "ModelName": "gemini-2.5-flash"
  }
}
```

**Supported Models:**
- `gemini-2.5-flash` (Default - Latest experimental)
- `gemini-3-flash` (Stable)

### Rate Limiting

```json
{
  "RateLimiting": {
    "MaxMessagesPerHour": 100,
    "WindowType": "SlidingWindow"
  }
}
```

- **Limit**: 100 messages per hour per user
- **Window**: Sliding window (1-hour rolling)
- **Tracking**: Per `UserProfileId` or IP for anonymous users
- **Response**: HTTP 429 with `Retry-After` header when exceeded

### Feature Flags

```json
{
  "Features": {
    "WebSearchEnabled": true
  }
}
```

| Flag | Default | Description |
|------|---------|-------------|
| `WebSearchEnabled` | `true` | Allow web search for technical specs |

### Timeouts

```json
{
  "Timeouts": {
    "GeminiApiDefault": 10,
    "GeminiApiWithWebSearch": 30
  }
}
```

All timeouts in seconds.

## Deployment

### GitOps Workflow

This service uses ArgoCD GitOps for deployment:

1. **Build & Push**: GitHub Actions builds Docker image and pushes to Google Artifact Registry
2. **Update Manifests**: CI updates image tags in `maliev-gitops` repository
3. **Auto-Sync**: ArgoCD detects changes and syncs to GKE cluster

### Container Registry

```
asia-southeast1-docker.pkg.dev/maliev-website/maliev-website-artifact-{env}/maliev-chatbot-service:{sha}
```

**Environments:**
- `dev` - Development (maliev-dev namespace)
- `staging` - Staging (maliev-staging namespace)
- `prod` - Production (maliev-prod namespace)

### Environment Variables (Production)

Secrets are injected via Google Secret Manager using External Secrets Operator:

- `ConnectionStrings__ChatbotDbContext` - PostgreSQL connection string
- `ConnectionStrings__Cache` - Redis connection string
- `ConnectionStrings__Messaging` - RabbitMQ connection string
- `Gemini__ApiKey` - Gemini API key
- `Jwt__Key` - RSA public key for JWT validation

## IAM Permissions

All API endpoints require IAM authentication. The following permissions are enforced:

| Permission | Resource | Action | Description |
|------------|----------|--------|-------------|
| `chatbot.sessions.create` | Sessions | Create | Initiate a new conversation session |
| `chatbot.sessions.read` | Sessions | Read | View session details and history |
| `chatbot.messages.send` | Messages | Send | Send messages to the chatbot |
| `chatbot.messages.read` | Messages | Read | View message history |
| `chatbot.preferences.read` | Preferences | Read | View stored user preferences |
| `chatbot.preferences.write` | Preferences | Write | Modify user preferences |
| `chatbot.preferences.delete` | Preferences | Delete | Delete user preferences |
| `chatbot.webhooks.receive` | Webhooks | Receive | Receive webhook events from external channels |
| `chatbot.instructions.read` | Instructions | Read | View system instructions (admin) |
| `chatbot.instructions.write` | Instructions | Write | Modify system instructions (admin) |

## Database Schema

### Key Entities

- **UserProfile**: User account with channel identifiers (LINE, Facebook, etc.)
- **ConversationSession**: 24-hour conversation session with expiry
- **Message**: Individual messages with multimodal content support
- **ConversationSummary**: Generated summaries for expired sessions
- **UserMemory**: Persistent user preferences with confidence scores
- **SystemInstruction**: AI behavior configuration and business constraints
- **IdentityLink**: Cross-platform identity linking
- **OperationLog**: Audit log for internal CRM operations
- **SearchDomainLog**: Web search domain access logging

### Connection String

```
ConnectionStrings:ChatbotDbContext
```

**Database Name**: `chatbot_app_db`

## Contributing

### Code Review

All changes require review from `@MALIEV-Co-Ltd/core-developers` (see `.github/CODEOWNERS`).

### Code Quality Standards

- **TreatWarningsAsErrors**: Enabled - All warnings must be resolved
- **XML Documentation**: Required on all public methods, properties, and classes
- **Test Coverage**: Integration tests required for all features
- **No Banned Libraries**: AutoMapper, FluentValidation, FluentAssertions are prohibited

### Pre-commit Hooks

This repository uses pre-commit hooks (see `.pre-commit-config.yaml`):

```bash
# Install pre-commit
pip install pre-commit

# Install hooks
pre-commit install
```

## Troubleshooting

### Common Issues

#### 1. Gemini API Errors

```
Error: Invalid API key
```

**Solution**: Set your Gemini API key in user secrets or environment variables.

```bash
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY"
```

#### 2. Database Connection Failures

```
Error: Connection refused - localhost:5432
```

**Solution**: Ensure PostgreSQL is running:

```bash
docker run -d --name postgres -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:18
```

#### 3. Redis Connection Failures

```
Error: Connection refused - localhost:6379
```

**Solution**: Ensure Redis is running:

```bash
docker run -d --name redis -p 6379:6379 redis:7-alpine
```

#### 4. Test Failures (Testcontainers)

```
Error: Docker daemon not running
```

**Solution**: Start Docker Desktop before running tests.

#### 5. Rate Limit Exceeded

```
HTTP 429 - Rate limit exceeded
```

**Solution**: Wait for the sliding window to reset (check `Retry-After` header) or increase limits in configuration.

## Support

For issues and questions:
- **Internal**: Contact `@MALIEV-Co-Ltd/core-developers`
- **Issues**: Create a GitHub issue in this repository

## License

Proprietary - MALIEV Co., Ltd. All rights reserved.

## Related Services

- **Maliev.IAMService**: Identity and access management
- **Maliev.CustomerService**: Customer data management
- **Maliev.OrderService**: Order lifecycle management
- **Maliev.QuotationService**: Quotation management
- **Maliev.NotificationService**: Multi-channel notifications

---

**Version**: 1.0.0
**Last Updated**: 2025-12-31
**Maintained by**: MALIEV Core Developers
