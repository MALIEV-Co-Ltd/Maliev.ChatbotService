# Maliev Chatbot Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/ORGANIZATION/Maliev.ChatbotService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Central conversational AI service providing multi-channel chatbot capabilities with Gemini AI integration.

**Role in MALIEV Architecture**: Provides intelligent, context-aware conversational AI across multiple communication channels (Website, LINE, Facebook, Instagram, WhatsApp). It leverages Google's Gemini AI to deliver natural language understanding while maintaining strict business boundaries.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **AI Model**: Google Gemini 2.0/2.5
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x
- **Distributed Cache**: Redis 7.x (Session management & rate limiting)
- **Messaging**: RabbitMQ via MassTransit
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations (`[Required]`, `[EmailAddress]`) only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.
- ❌ **In-memory Test DB**: All integration tests use **Testcontainers** with real PostgreSQL 18.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **No Test Config in Program.cs**: Test configuration in test fixtures only.
- ✅ **IAM Integration**: Self-registers permissions with the IAM Service using GCP-style naming: `{service}.{resource}.{action}`.

---

## ✨ Key Features

- **Multi-Channel Support**: Seamless conversations across Website, LINE, Facebook, Instagram, and WhatsApp.
- **Multimodal Input**: Process text, images, PDFs, audio, and video via Gemini AI.
- **Persistent Memory**: Remember user preferences and conversation context across platforms.
- **Business Boundary Enforcement**: Focuses AI interactions on manufacturing and B2B topics.
- **Rate Limiting**: Protection against API abuse with user-based sliding windows.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)
- Google Gemini API Key

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/ORGANIZATION/Maliev.ChatbotService.git
cd Maliev.ChatbotService
```

2. **Spin up Infrastructure**
```bash
docker run --name chatbot-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name chatbot-redis -p 6379:6379 -d redis:7-alpine
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__ChatbotDbContext="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Cache="YOUR_REDIS_CONNECTION_STRING"
$env:Gemini__ApiKey="YOUR_GEMINI_API_KEY"
```

4. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.ChatbotService.Infrastructure
dotnet run --project Maliev.ChatbotService.Api
```

The service will be available at `http://localhost:5000/chatbot`. Access the interactive documentation at `http://localhost:5000/chatbot/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/chatbot/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/sessions/initiate` | Initiate a new conversation session |
| POST | `/messages` | Send a message to the chatbot (Supports multimodal) |
| GET | `/users/me/preferences` | Get current user's stored preferences |
| POST | `/webhooks/line` | LINE messaging webhook |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /chatbot/liveness`
- **Readiness**: `GET /chatbot/readiness` (Checks DB, Redis, and Gemini connectivity)
- **Metrics**: `GET /chatbot/metrics` (Prometheus format)

---

## 🧪 Testing

We prioritize reliable tests over mock-heavy unit tests.

```bash
# Run all tests using Testcontainers
dotnet test --verbosity normal
```

- **Integration Tests**: Use real PostgreSQL 18 containers.
- **Contract Tests**: Ensure API stability for consumers.

---

## 📦 Deployment

Infrastructure management is handled via GitOps patterns.

- **Docker Image**: `REGION-docker.pkg.dev/PROJECT_ID/REPOSITORY/maliev-chatbot-service:{sha}`
- **Environments**: Development, Staging, Production

---

## 📄 License

Proprietary - © 2025 MALIEV Co., Ltd. All rights reserved.
