# Maliev Payment Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/ORGANIZATION/Maliev.PaymentService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Enterprise-grade payment orchestration gateway with Omise-first routing for the Thai market and fallback provider support where explicitly configured.

**Role in MALIEV Architecture**: The centralized financial gateway for all incoming and outgoing transactions. Omise is the primary provider for Thailand because it supports cards, bank payments, and QR-code payment flows better aligned with MALIEV's current market. The service still abstracts provider complexity for configured fallbacks while ensuring transactional integrity, idempotency, and high availability for the platform's revenue streams.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x
- **Distributed Cache**: Redis 7.x (Idempotency storage & provider health)
- **Messaging**: RabbitMQ via MassTransit
- **Resilience**: Polly-based Circuit Breakers and Retry policies for providers
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

- **Multi-Provider Intelligent Routing**: Automatic selection of the best provider based on cost, performance, and current health status.
- **Strict Idempotency**: Guaranteed duplicate detection using Redis-backed idempotency keys to prevent double-charging.
- **Circuit Breaker Resilience**: Automated monitoring of provider health with instant failover to healthy alternatives.
- **Secure Webhook Orchestration**: Robust signature verification and reliable async processing for provider-initiated status updates.
- **Precision Refund Engine**: Full and partial refund support with multi-layered validation and allocation logic.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/ORGANIZATION/Maliev.PaymentService.git
cd Maliev.PaymentService
```

2. **Spin up Infrastructure**
```bash
docker run --name payment-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name payment-redis -p 6379:6379 -d redis:7-alpine
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__PaymentDbContext="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Cache="YOUR_REDIS_CONNECTION_STRING"
$env:PaymentProviders__Omise__PublicKey="YOUR_OMISE_PUBLIC_KEY"
$env:PaymentProviders__Omise__SecretKey="YOUR_OMISE_SECRET_KEY"
$env:PaymentProviders__Omise__WebhookSecret="YOUR_OMISE_WEBHOOK_SECRET"
# Stored provider credentials must include webhook signing material.
```

4. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.PaymentService.Api
dotnet run --project Maliev.PaymentService.Api
```

The service will be available at `http://localhost:5000/payments`. Access the interactive documentation at `http://localhost:5000/payments/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/payments/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/payments` | Initiate a new payment transaction |
| GET | `/payments/{id}` | Retrieve payment status and details |
| POST | `/payments/{id}/refund` | Process a full or partial refund |
| POST | `/webhooks/{provider}` | Receive provider webhooks after provider-specific signature validation |
| GET | `/metrics` | Retrieve Prometheus performance metrics |

## Security Assumptions

- Payment mutations require permission checks and idempotency keys before state changes.
- Provider webhook payloads must fail closed when signature material is missing or invalid.
- Omise webhooks require cryptographic verification of the `Omise-Signature` header using the configured Omise webhook secret; header presence alone is not trusted.
- Non-production test/simulation endpoints still require payment processing permission before publishing payment events; environment checks are not a substitute for IAM.
- Provider credentials and webhook signing material must come from environment/GCP Secret Manager, never tracked config.

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /payments/liveness`
- **Readiness**: `GET /payments/readiness` (Checks DB and Redis connectivity)
- **Metrics**: `GET /payments/metrics` (Prometheus format)

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

- **Docker Image**: `REGION-docker.pkg.dev/PROJECT_ID/REPOSITORY/maliev-payment-service:{sha}`
- **Environments**: Development, Staging, Production

---

## 📄 License

Proprietary - © 2025 MALIEV Co., Ltd. All rights reserved.
