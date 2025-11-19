# Implementation Plan: Payment Gateway Service

**Branch**: `001-payment-gateway-service` | **Date**: 2025-11-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-payment-gateway-service/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

The Payment Gateway Service is a core microservice that acts as a centralized API gateway for all payment operations in the MALIEV system. It manages, standardizes, and routes payment requests from internal microservices to external payment providers (Stripe, PayPal, Omise, SCB API) ensuring microservices can process payments without directly handling provider-specific logic or credentials.

**Technical Approach**: Build a .NET 10 WebAPI microservice using Clean Architecture with Entity Framework Core 9.0.10 and PostgreSQL 18 for data persistence. Implement resilience patterns with Polly 8.5.0 for provider communication, MassTransit 8.3.4 with RabbitMQ for asynchronous event publishing, Redis for distributed caching and idempotency, and JWT authentication for service-to-service security. Expose Prometheus metrics for operational observability.

## Technical Context

**Language/Version**: .NET 10 (C# 13)
**Primary Dependencies**:
- ASP.NET Core 10.0
- Entity Framework Core 9.0.10
- Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4
- MassTransit 8.3.4 (with RabbitMQ transport)
- StackExchange.Redis 2.8.16
- Polly 8.5.0
- Microsoft.AspNetCore.Authentication.JwtBearer 10.0
- Prometheus-net.AspNetCore 8.2.1
- Scalar.AspNetCore 1.2.42

**Storage**: PostgreSQL 18 (dedicated database for service autonomy)
**Testing**: xUnit with Testcontainers (PostgreSQL 18, RabbitMQ 7.0, Redis 7.2 - real infrastructure, no in-memory substitutes)
**Target Platform**: Kubernetes cluster (Linux containers)
**Project Type**: Microservice WebAPI (Clean Architecture)
**Performance Goals**:
- 10,000 transactions per hour peak capacity
- 1,000 concurrent requests without degradation
- <3 seconds p95 latency for payment processing
- <500ms p99 latency for status queries
- <2 seconds webhook processing time

**Constraints**:
- 99.9% success rate when providers are operational
- 30 second timeout per provider API call
- Maximum 3 retry attempts with exponential backoff
- Circuit breaker thresholds for automatic failover
- 1 year active data retention + 3 years archived (4 years total)

**Scale/Scope**:
- 4 initial payment providers (Stripe, PayPal, Omise, SCB API)
- Support for 20+ currencies
- Extensible to additional providers without code changes
- Multi-region deployment capability

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ I. Service Autonomy (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: Payment Gateway Service has dedicated PostgreSQL database, owns payment transaction domain logic, and interacts with other services only via JWT-authenticated APIs and asynchronous message queue events. No shared database access.

### ✅ II. Explicit Contracts
- **Status**: COMPLIANT
- **Evidence**: All APIs documented via OpenAPI specification (consumed by Scalar UI). Contract specifications defined in `contracts/` directory with versioning strategy. Webhook contracts and event schemas explicitly defined.

### ✅ III. Test-First Development (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: Tests will be authored immediately after specification approval, before implementation. Red-Green-Refactor cycle enforced. Unit, integration, and contract tests planned with 80%+ coverage target for business-critical payment logic.

### ✅ IV. Real Infrastructure Testing (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: All tests use Testcontainers for real infrastructure instances:
  - **PostgreSQL 18**: Testcontainers.PostgreSQL for database tests (no EF Core InMemoryDatabase provider)
  - **RabbitMQ 7.0**: Testcontainers.RabbitMQ for message queue/event publishing tests (no in-memory message buses)
  - **Redis 7.2**: Testcontainers.Redis for caching and distributed locking tests (no in-memory cache providers)
  - Test isolation via database transactions, queue purging, and cache flushing
  - Production infrastructure mirrored exactly in test environments

### ✅ V. Auditability & Observability
- **Status**: COMPLIANT
- **Evidence**: Structured JSON logging with correlation IDs for all payment operations. Immutable transaction logs retained for 4 years (1 active + 3 archived). Health check endpoints for liveness/readiness. Complete audit trail for compliance.

### ✅ VI. Security & Compliance
- **Status**: COMPLIANT
- **Evidence**: JWT authentication for service-to-service communication. Provider credentials encrypted at rest using data protection APIs. Sensitive data encrypted in transit (TLS). GDPR compliance via audit logs and data retention policies. Thai tax law compliance via transaction records.

### ✅ VII. Secrets Management & Configuration Security (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: No secrets in source code. Provider credentials injected from Google Secret Manager or environment variables. Configuration sanitized for public repository. Pre-commit hooks scan for secrets.

### ✅ VIII. Zero Warnings Policy (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: Build configuration set to treat warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`). CI pipeline fails on any compiler warnings.

### ✅ IX. Clean Project Artifacts (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: `.gitignore` excludes bin/, obj/, and IDE files. `.dockerignore` excludes build artifacts, specs/, .github/, Test projects. No unused generated artifacts in source control.

### ✅ X. Docker Best Practices (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: Uses built-in `app` user from Microsoft ASP.NET images. Multi-stage builds (mcr.microsoft.com/dotnet/sdk:10.0 for build, mcr.microsoft.com/dotnet/aspnet:10.0 for runtime). Ownership set with `chown -R app:app /app` before `USER app`. Health check validates liveness endpoint. Test projects excluded from Docker build.

### ✅ XI. Simplicity & Maintainability
- **Status**: COMPLIANT
- **Evidence**: YAGNI applied - only implementing required features. Clean Architecture promotes readable, maintainable code. No shared libraries initially - all code self-contained in service.

### ✅ XII. Business Metrics & Analytics (NON-NEGOTIABLE)
- **Status**: COMPLIANT
- **Evidence**: Prometheus metrics endpoint at `/metrics` exposing:
  - Transaction volume counters (total, by provider, by status)
  - Payment success/failure rates
  - Provider health and availability
  - Request latency histograms (p50, p95, p99)
  - Active concurrent requests gauge
  - Refund operation metrics
  - All metrics tagged with service_name, version, environment
  - No PII exposure in metrics

## Project Structure

### Documentation (this feature)

```text
specs/001-payment-gateway-service/
├── plan.md              # This file (/speckit.plan command output)
├── spec.md              # Feature specification
├── research.md          # Phase 0 output - payment provider integration research
├── data-model.md        # Phase 1 output - database schema and entities
├── quickstart.md        # Phase 1 output - developer onboarding guide
├── contracts/           # Phase 1 output - OpenAPI specifications
│   ├── payments-api.yaml
│   ├── providers-api.yaml
│   ├── webhooks-api.yaml
│   ├── metrics-api.yaml
│   └── events-schema.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

Based on Clean Architecture pattern for .NET microservices:

```text
Maliev.PaymentService/
├── Maliev.PaymentService.Api/          # Presentation Layer (Web API)
│   ├── Controllers/
│   │   ├── PaymentsController.cs       # POST /payments, GET /payments/{id}
│   │   ├── RefundsController.cs        # POST /payments/{id}/refund
│   │   ├── WebhooksController.cs       # POST /webhooks/{provider}
│   │   ├── ProvidersController.cs      # GET/POST/PUT /providers
│   │   └── MetricsController.cs        # GET /metrics (Prometheus)
│   ├── Middleware/
│   │   ├── JwtAuthenticationMiddleware.cs
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── RequestLoggingMiddleware.cs
│   ├── Models/                         # DTOs and API contracts
│   │   ├── Requests/
│   │   ├── Responses/
│   │   └── Validators/                 # FluentValidation validators
│   ├── Extensions/
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── WebApplicationExtensions.cs
│   ├── Program.cs                      # Application entry point
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Dockerfile
│   └── Maliev.PaymentService.Api.csproj
│
├── Maliev.PaymentService.Core/         # Domain Layer (Business Logic)
│   ├── Entities/
│   │   ├── PaymentTransaction.cs
│   │   ├── PaymentProvider.cs
│   │   ├── RefundTransaction.cs
│   │   ├── WebhookEvent.cs
│   │   ├── TransactionLog.cs
│   │   └── ProviderConfiguration.cs
│   ├── Interfaces/
│   │   ├── IPaymentRepository.cs
│   │   ├── IProviderRepository.cs
│   │   ├── IRefundRepository.cs
│   │   ├── IWebhookRepository.cs
│   │   ├── IPaymentProviderService.cs
│   │   ├── IPaymentRoutingService.cs
│   │   ├── IIdempotencyService.cs
│   │   ├── IEventPublisher.cs
│   │   └── IMetricsService.cs
│   ├── Services/
│   │   ├── PaymentService.cs           # Core payment orchestration
│   │   ├── RefundService.cs
│   │   ├── PaymentRoutingService.cs    # Provider selection logic
│   │   ├── WebhookValidationService.cs
│   │   └── ReconciliationService.cs
│   ├── Events/                         # Domain events
│   │   ├── PaymentCreatedEvent.cs
│   │   ├── PaymentCompletedEvent.cs
│   │   ├── PaymentFailedEvent.cs
│   │   └── RefundCompletedEvent.cs
│   ├── Enums/
│   │   ├── PaymentStatus.cs
│   │   ├── ProviderStatus.cs
│   │   ├── RefundStatus.cs
│   │   └── WebhookEventType.cs
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── Currency.cs
│   │   └── IdempotencyKey.cs
│   └── Maliev.PaymentService.Core.csproj
│
├── Maliev.PaymentService.Infrastructure/  # Infrastructure Layer
│   ├── Data/
│   │   ├── PaymentDbContext.cs
│   │   ├── Configurations/              # EF Core entity configurations
│   │   │   ├── PaymentTransactionConfiguration.cs
│   │   │   ├── PaymentProviderConfiguration.cs
│   │   │   ├── RefundTransactionConfiguration.cs
│   │   │   ├── WebhookEventConfiguration.cs
│   │   │   └── TransactionLogConfiguration.cs
│   │   ├── Migrations/                  # EF Core migrations
│   │   └── Repositories/
│   │       ├── PaymentRepository.cs
│   │       ├── ProviderRepository.cs
│   │       ├── RefundRepository.cs
│   │       └── WebhookRepository.cs
│   ├── Providers/                       # Payment provider implementations
│   │   ├── IPaymentProviderAdapter.cs   # Common interface
│   │   ├── StripeProvider.cs
│   │   ├── PayPalProvider.cs
│   │   ├── OmiseProvider.cs
│   │   ├── ScbApiProvider.cs
│   │   └── ProviderFactory.cs
│   ├── Messaging/
│   │   ├── MassTransitEventPublisher.cs # Event bus implementation
│   │   └── EventConsumers/              # If needed for internal events
│   ├── Caching/
│   │   ├── RedisIdempotencyService.cs
│   │   └── RedisCacheService.cs
│   ├── Resilience/
│   │   ├── PollyPolicies.cs            # Retry, circuit breaker, timeout
│   │   └── CircuitBreakerStateManager.cs
│   ├── Metrics/
│   │   ├── PrometheusMetricsService.cs
│   │   └── MetricDefinitions.cs
│   ├── Encryption/
│   │   ├── CredentialEncryptionService.cs
│   │   └── IEncryptionService.cs
│   └── Maliev.PaymentService.Infrastructure.csproj
│
├── Maliev.PaymentService.Tests/         # Test Project
│   ├── Unit/
│   │   ├── Services/
│   │   │   ├── PaymentServiceTests.cs
│   │   │   ├── RefundServiceTests.cs
│   │   │   └── PaymentRoutingServiceTests.cs
│   │   └── Validators/
│   ├── Integration/
│   │   ├── Controllers/
│   │   │   ├── PaymentsControllerTests.cs
│   │   │   ├── RefundsControllerTests.cs
│   │   │   ├── WebhooksControllerTests.cs
│   │   │   └── ProvidersControllerTests.cs
│   │   ├── Repositories/
│   │   │   ├── PaymentRepositoryTests.cs
│   │   │   └── ProviderRepositoryTests.cs
│   │   ├── Providers/
│   │   │   ├── StripeProviderTests.cs
│   │   │   ├── PayPalProviderTests.cs
│   │   │   └── ProviderFailoverTests.cs
│   │   └── TestContainersFixture.cs     # PostgreSQL, RabbitMQ, Redis container setup
│   ├── Contract/
│   │   ├── OpenApiContractTests.cs      # Validate OpenAPI spec
│   │   └── EventSchemaTests.cs          # Validate event schemas
│   ├── Helpers/
│   │   ├── TestDataBuilder.cs
│   │   └── MockProviderFactory.cs
│   └── Maliev.PaymentService.Tests.csproj
│
├── .dockerignore
├── .gitignore
├── Maliev.PaymentService.sln
├── docker-compose.yml                    # Local dev environment
└── README.md
```

**Structure Decision**:

The project follows Clean Architecture with clear separation of concerns:

1. **Api Layer**: Handles HTTP requests, authentication, validation, and API contracts. Contains controllers, middleware, and DTOs.

2. **Core Layer**: Contains domain entities, business logic, and interfaces. This is the heart of the application with no dependencies on external frameworks. Defines contracts for repositories and external services.

3. **Infrastructure Layer**: Implements interfaces defined in Core. Contains EF Core repositories, payment provider adapters, messaging with MassTransit, Redis caching, Polly resilience policies, and Prometheus metrics.

4. **Tests Project**: Comprehensive test coverage using xUnit with Testcontainers for all infrastructure (PostgreSQL 18, RabbitMQ 7.0, Redis 7.2). No in-memory substitutes per constitution requirements.

This structure ensures:
- Service autonomy with dedicated database
- Testability through dependency inversion
- Extensibility for new payment providers
- Maintainability through clear boundaries
- Clean Architecture principles compliance

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations. All requirements met without exceptions.

## Implementation Phases

### Phase 0: Research & Discovery
**Output**: `research.md`

Research payment provider integration patterns, resilience strategies, security best practices, and data retention approaches.

### Phase 1: Design & Architecture
**Output**: `data-model.md`, `contracts/`, `quickstart.md`

Define database schema, API contracts, event schemas, and developer onboarding documentation.

### Phase 2: Task Breakdown
**Output**: `tasks.md`

Generate dependency-ordered implementation tasks using `/speckit.tasks` command.

### Phase 3: Implementation
Execute tasks from `tasks.md` following Test-First Development workflow.
