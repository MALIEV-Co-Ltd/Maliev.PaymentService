# Tasks: Payment Gateway Service

**Feature Branch**: `001-payment-gateway-service`
**Input**: Design documents from `/specs/001-payment-gateway-service/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

**Story Priority Order**:
1. **User Story 5** - Manage Payment Providers (P1) - FOUNDATIONAL
2. **User Story 1** - Process Payment Through Gateway (P1) - CORE MVP
3. **User Story 4** - Handle Provider Webhooks (P2)
4. **User Story 2** - Check Payment Status (P2)
5. **User Story 3** - Refund Payment (P3)
6. **User Story 6** - Monitor Gateway Operations (P3)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4, US5, US6)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create .NET 10 solution structure with projects: Maliev.PaymentService.Api, Maliev.PaymentService.Core, Maliev.PaymentService.Infrastructure, Maliev.PaymentService.Tests
- [X] T002 Configure NuGet packages for Api project in Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj (ASP.NET Core 10.0, Scalar.AspNetCore 1.2.42, FluentValidation.AspNetCore 11.3.0, Prometheus-net.AspNetCore 8.2.1)
- [X] T003 [P] Configure NuGet packages for Core project in Maliev.PaymentService.Core/Maliev.PaymentService.Core.csproj (no external dependencies for clean architecture)
- [X] T004 [P] Configure NuGet packages for Infrastructure project in Maliev.PaymentService.Infrastructure/Maliev.PaymentService.Infrastructure.csproj (EF Core 9.0.10, Npgsql 9.0.4, MassTransit 8.3.4, StackExchange.Redis 2.8.16, Polly 8.5.0)
- [X] T005 [P] Configure NuGet packages for Tests project in Maliev.PaymentService.Tests/Maliev.PaymentService.Tests.csproj (xUnit 2.9.3, Testcontainers.PostgreSQL 3.10.0, Testcontainers.RabbitMQ 3.10.0, Testcontainers.Redis 3.10.0, Moq 4.20.72)
- [X] T006 [P] Create project references: Api -> Core, Api -> Infrastructure, Infrastructure -> Core, Tests -> all
- [X] T007 [P] Configure build settings with TreatWarningsAsErrors=true in all .csproj files
- [X] T008 [P] Create .dockerignore excluding bin/, obj/, specs/, .github/, Test projects
- [X] T009 [P] Create .gitignore for .NET projects excluding bin/, obj/, .vs/, .idea/, *.user files

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

### Test Infrastructure

- [X] T010 [P] Create Testcontainers fixture in Maliev.PaymentService.Tests/Integration/TestContainersFixture.cs with PostgreSQL 18, RabbitMQ 7.0, and Redis 7.2 container setup for real infrastructure testing

### Database Infrastructure

- [X] T011 Create PaymentDbContext in Maliev.PaymentService.Infrastructure/Data/PaymentDbContext.cs with DbContextOptions constructor
- [X] T012 Configure connection string settings in Maliev.PaymentService.Api/appsettings.json and appsettings.Development.json
- [X] T013 Create DbContextFactory for Testcontainers in Maliev.PaymentService.Tests/Helpers/PaymentDbContextFactory.cs
- [X] T014 Create initial EF Core migration in Maliev.PaymentService.Infrastructure/Data/Migrations/ using dotnet ef migrations add InitialCreate

### Authentication & Authorization

- [X] T015 Configure JWT authentication in Maliev.PaymentService.Api/Program.cs with Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T016 Create JwtAuthenticationMiddleware in Maliev.PaymentService.Api/Middleware/JwtAuthenticationMiddleware.cs for service identity claims validation
- [X] T017 Create service identity claims constants in Maliev.PaymentService.Core/Constants/AuthConstants.cs

### Middleware Pipeline

- [X] T018 [P] Create CorrelationIdMiddleware in Maliev.PaymentService.Api/Middleware/CorrelationIdMiddleware.cs for distributed tracing
- [X] T019 [P] Create ExceptionHandlingMiddleware in Maliev.PaymentService.Api/Middleware/ExceptionHandlingMiddleware.cs with standardized error responses
- [X] T020 [P] Create RequestLoggingMiddleware in Maliev.PaymentService.Api/Middleware/RequestLoggingMiddleware.cs with structured JSON logging (uses Serilog ILogger configured in T022, logs request/response details with correlation IDs)
- [X] T021 Configure middleware pipeline order in Maliev.PaymentService.Api/Program.cs (Correlation -> Exception -> Logging -> Auth)

### Logging & Metrics Infrastructure

- [X] T022 Configure Serilog structured logging in Maliev.PaymentService.Api/Program.cs with console and file sinks
- [X] T023 Create PrometheusMetricsService in Maliev.PaymentService.Infrastructure/Metrics/PrometheusMetricsService.cs implementing IMetricsService
- [X] T024 Create MetricDefinitions in Maliev.PaymentService.Infrastructure/Metrics/MetricDefinitions.cs for counters, gauges, histograms
- [X] T025 Configure Prometheus /metrics endpoint in Maliev.PaymentService.Api/Program.cs

### Message Queue Setup

- [X] T026 Configure MassTransit with RabbitMQ in Maliev.PaymentService.Infrastructure/Messaging/MassTransitConfiguration.cs
- [X] T027 Create IEventPublisher interface in Maliev.PaymentService.Core/Interfaces/IEventPublisher.cs
- [X] T028 Create MassTransitEventPublisher in Maliev.PaymentService.Infrastructure/Messaging/MassTransitEventPublisher.cs implementing IEventPublisher
- [X] T029 Configure RabbitMQ settings in Maliev.PaymentService.Api/appsettings.json (Host, Port, Username, Password, VirtualHost)

### Redis Caching Setup

- [X] T030 Configure StackExchange.Redis ConnectionMultiplexer in Maliev.PaymentService.Infrastructure/Caching/RedisConfiguration.cs
- [X] T031 Create IIdempotencyService interface in Maliev.PaymentService.Core/Interfaces/IIdempotencyService.cs (handles idempotency for both payment and refund requests using operation type + idempotency key as composite key)
- [X] T032 Create RedisIdempotencyService in Maliev.PaymentService.Infrastructure/Caching/RedisIdempotencyService.cs with distributed locking
- [X] T033 Configure Redis settings in Maliev.PaymentService.Api/appsettings.json (Configuration, InstanceName)

### Base Entities & Enums

- [X] T034 [P] Create PaymentStatus enum in Maliev.PaymentService.Core/Enums/PaymentStatus.cs (Pending, Processing, Completed, Failed, Refunded, PartiallyRefunded)
- [X] T035 [P] Create ProviderStatus enum in Maliev.PaymentService.Core/Enums/ProviderStatus.cs (Active, Disabled, Degraded, Maintenance)
- [X] T036 [P] Create RefundStatus enum in Maliev.PaymentService.Core/Enums/RefundStatus.cs (Pending, Processing, Completed, Failed)
- [X] T037 [P] Create WebhookProcessingStatus enum in Maliev.PaymentService.Core/Enums/WebhookProcessingStatus.cs (Pending, Processing, Completed, Failed, Duplicate)

### Resilience Patterns (Polly)

- [X] T038 Create PollyPolicies in Maliev.PaymentService.Infrastructure/Resilience/PollyPolicies.cs with retry, circuit breaker (5 consecutive failures or 50% failure rate over 30 seconds, break duration: 30 seconds, half-open allows 1 test request), timeout policies
- [X] T039 Create CircuitBreakerStateManager in Maliev.PaymentService.Infrastructure/Resilience/CircuitBreakerStateManager.cs for tracking circuit breaker states
- [X] T040 Configure resilience pipelines in Maliev.PaymentService.Api/Program.cs using AddResiliencePipeline
- [X] T041 Configure Polly settings in Maliev.PaymentService.Api/appsettings.json (RetryCount: 3, TimeoutSeconds: 30, CircuitBreaker thresholds: 5 consecutive failures or 50% failure rate over 30 seconds, BreakDuration: 30 seconds)

### Encryption Infrastructure

- [X] T042 Create IEncryptionService interface in Maliev.PaymentService.Infrastructure/Encryption/IEncryptionService.cs
- [X] T043 Create CredentialEncryptionService in Maliev.PaymentService.Infrastructure/Encryption/CredentialEncryptionService.cs using ASP.NET Core Data Protection APIs

### Health Checks

- [X] T044 Configure health check endpoints in Maliev.PaymentService.Api/Program.cs (/health for liveness, /health/ready for readiness)
- [X] T045 Add database health check for PostgreSQL in Maliev.PaymentService.Api/Program.cs
- [X] T046 Add Redis health check in Maliev.PaymentService.Api/Program.cs
- [X] T047 Add RabbitMQ health check in Maliev.PaymentService.Api/Program.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 5 - Manage Payment Providers (Priority: P1) - FOUNDATIONAL

**Goal**: Enable registration, configuration, and management of payment providers. This is FOUNDATIONAL and BLOCKS US1, US4.

**Independent Test**: Can register a provider with configuration details and verify it becomes available for routing.

**Why First**: Without provider management, no payments can be processed. Required before US1 can function.

### Test Tasks for User Story 5 (Test-First Development)

- [X] T048 [P] [US5] Create ProvidersControllerIntegrationTests in Maliev.PaymentService.Tests/Integration/Controllers/ProvidersControllerIntegrationTests.cs (test provider CRUD operations with real database) - WRITE TESTS FIRST
- [X] T049 [P] [US5] Create ProviderManagementServiceTests in Maliev.PaymentService.Tests/Unit/Services/ProviderManagementServiceTests.cs (test provider registration, update, credential encryption) - WRITE TESTS FIRST

### Entities for User Story 5

- [X] T050 [P] [US5] Create PaymentProvider entity in Maliev.PaymentService.Core/Entities/PaymentProvider.cs with all properties from data-model.md
- [X] T051 [P] [US5] Create ProviderConfiguration entity in Maliev.PaymentService.Core/Entities/ProviderConfiguration.cs for multi-region support
- [X] T052 [US5] Create PaymentProviderConfiguration EF config in Maliev.PaymentService.Infrastructure/Data/Configurations/PaymentProviderConfiguration.cs (table name: payment_providers, indexes, constraints)
- [X] T053 [US5] Create ProviderConfigurationConfiguration EF config in Maliev.PaymentService.Infrastructure/Data/Configurations/ProviderConfigurationConfiguration.cs (table name: provider_configurations)
- [X] T054 [US5] Add PaymentProvider and ProviderConfiguration DbSets to PaymentDbContext in Maliev.PaymentService.Infrastructure/Data/PaymentDbContext.cs
- [X] T055 [US5] Create EF Core migration for provider tables using dotnet ef migrations add AddProviderTables

### Repositories for User Story 5

- [X] T056 [US5] Create IProviderRepository interface in Maliev.PaymentService.Core/Interfaces/IProviderRepository.cs (GetAllAsync, GetByIdAsync, GetActiveByCurrencyAsync, AddAsync, UpdateAsync, DeleteAsync)
- [X] T057 [US5] Create ProviderRepository in Maliev.PaymentService.Infrastructure/Data/Repositories/ProviderRepository.cs implementing IProviderRepository with EF Core queries

### DTOs & Validators for User Story 5

- [X] T058 [P] [US5] Create RegisterProviderRequest DTO in Maliev.PaymentService.Api/Models/Requests/RegisterProviderRequest.cs per providers-api.yaml schema
- [X] T059 [P] [US5] Create UpdateProviderRequest DTO in Maliev.PaymentService.Api/Models/Requests/UpdateProviderRequest.cs per providers-api.yaml schema
- [X] T060 [P] [US5] Create UpdateProviderStatusRequest DTO in Maliev.PaymentService.Api/Models/Requests/UpdateProviderStatusRequest.cs
- [X] T061 [P] [US5] Create ProviderResponse DTO in Maliev.PaymentService.Api/Models/Responses/ProviderResponse.cs per providers-api.yaml schema
- [X] T062 [P] [US5] Create ProviderSummary DTO in Maliev.PaymentService.Api/Models/Responses/ProviderSummary.cs
- [ ] T063 [US5] Create RegisterProviderRequestValidator in Maliev.PaymentService.Api/Validators/RegisterProviderRequestValidator.cs using FluentValidation
- [ ] T064 [US5] Create UpdateProviderRequestValidator in Maliev.PaymentService.Api/Validators/UpdateProviderRequestValidator.cs using FluentValidation

### Services for User Story 5

- [X] T065 [US5] Create IProviderManagementService interface in Maliev.PaymentService.Core/Interfaces/IProviderManagementService.cs
- [X] T066 [US5] Create ProviderManagementService in Maliev.PaymentService.Infrastructure/Services/ProviderManagementService.cs with CRUD operations and credential encryption
- [ ] T067 [US5] Create ProviderHealthCheckService in Maliev.PaymentService.Core/Services/ProviderHealthCheckService.cs for health monitoring

### Controller for User Story 5

- [X] T068 [US5] Create ProvidersController in Maliev.PaymentService.Api/Controllers/ProvidersController.cs with GET /providers, POST /providers, GET /providers/{id}, PUT /providers/{id}, DELETE /providers/{id}, PATCH /providers/{id}/status endpoints per providers-api.yaml
- [X] T069 [US5] Add admin role authorization to ProvidersController using [Authorize(Roles = "Admin")] attributes

### Seed Data for User Story 5

- [ ] T070 [US5] Create provider seed data migration in Maliev.PaymentService.Infrastructure/Data/Migrations/ to insert Stripe, PayPal, Omise, SCB providers with encrypted test credentials

**Checkpoint**: User Story 5 complete - Providers can be registered and managed. US1 and US4 can now proceed.

---

## Phase 4: User Story 1 - Process Payment Through Gateway (Priority: P1) - MVP

**Goal**: Enable internal services to initiate payments through configured providers with routing, resilience, and idempotency.

**Independent Test**: Can submit a payment request through the gateway API and verify that the payment is processed by a configured provider and a response is returned.

**Why MVP**: This is the core value proposition of the service. Without payment processing, the service provides no value.

**Dependencies**: Requires US5 (providers must be configured before payments can be routed).

### Test Tasks for User Story 1 (Test-First Development)

- [X] T071 [P] [US1] Create PaymentsControllerIntegrationTests in Maliev.PaymentService.Tests/Integration/Controllers/PaymentsControllerIntegrationTests.cs (test POST /payments, GET /payments/{id} with real database) - WRITE TESTS FIRST
- [ ] T072 [P] [US1] Create PaymentServiceIntegrationTests in Maliev.PaymentService.Tests/Integration/Services/PaymentServiceIntegrationTests.cs (test payment orchestration with real infrastructure: PostgreSQL, RabbitMQ, Redis; mock only external payment providers) - WRITE TESTS FIRST
- [ ] T073 [P] [US1] Create PaymentRoutingServiceTests in Maliev.PaymentService.Tests/Unit/Services/PaymentRoutingServiceTests.cs (test currency routing, priority selection, circuit breaker awareness) - WRITE TESTS FIRST
- [ ] T074 [P] [US1] Create IdempotencyServiceTests in Maliev.PaymentService.Tests/Integration/Services/IdempotencyServiceTests.cs (test idempotency key handling, distributed locking with real Redis) - WRITE TESTS FIRST
- [X] T075 [US1] Create integration test for idempotency in PaymentsControllerIntegrationTests verifying duplicate payment request with same idempotency key returns original transaction (SC-010) - WRITE TESTS FIRST

### Entities for User Story 1

- [X] T076 [P] [US1] Create PaymentTransaction entity in Maliev.PaymentService.Core/Entities/PaymentTransaction.cs with all properties from data-model.md
- [X] T077 [P] [US1] Create TransactionLog entity in Maliev.PaymentService.Core/Entities/TransactionLog.cs for immutable audit logs
- [X] T078 [US1] Create PaymentTransactionConfiguration EF config in Maliev.PaymentService.Infrastructure/Data/Configurations/PaymentTransactionConfiguration.cs (table name: payment_transactions, indexes, constraints, foreign keys)
- [X] T079 [US1] Create TransactionLogConfiguration EF config in Maliev.PaymentService.Infrastructure/Data/Configurations/TransactionLogConfiguration.cs (table name: transaction_logs, immutable, no row_version)
- [X] T080 [US1] Add PaymentTransaction and TransactionLog DbSets to PaymentDbContext in Maliev.PaymentService.Infrastructure/Data/PaymentDbContext.cs
- [X] T081 [US1] Create EF Core migration for payment tables using dotnet ef migrations add AddPaymentTables

### Repositories for User Story 1

- [X] T082 [US1] Create IPaymentRepository interface in Maliev.PaymentService.Core/Interfaces/IPaymentRepository.cs (GetByIdAsync, GetByIdempotencyKeyAsync, AddAsync, UpdateAsync, GetByDateRangeAsync)
- [X] T083 [US1] Create PaymentRepository in Maliev.PaymentService.Infrastructure/Data/Repositories/PaymentRepository.cs implementing IPaymentRepository with optimistic concurrency

### Provider Adapter Pattern

- [X] T084 [US1] Create IPaymentProviderAdapter interface in Maliev.PaymentService.Infrastructure/Providers/IPaymentProviderAdapter.cs (ProcessPaymentAsync, GetPaymentStatusAsync, ProcessRefundAsync, ValidateWebhookSignature)
- [X] T085 [P] [US1] Create StripeProvider in Maliev.PaymentService.Infrastructure/Providers/StripeProvider.cs implementing IPaymentProviderAdapter using Stripe.net SDK
- [X] T086 [P] [US1] Create PayPalProvider in Maliev.PaymentService.Infrastructure/Providers/PayPalProvider.cs implementing IPaymentProviderAdapter with OAuth 2.0 token management
- [X] T087 [P] [US1] Create OmiseProvider in Maliev.PaymentService.Infrastructure/Providers/OmiseProvider.cs implementing IPaymentProviderAdapter with basic auth
- [X] T088 [P] [US1] Create ScbApiProvider in Maliev.PaymentService.Infrastructure/Providers/ScbApiProvider.cs implementing IPaymentProviderAdapter with OAuth 2.0
- [X] T089 [US1] Create ProviderFactory in Maliev.PaymentService.Infrastructure/Providers/ProviderFactory.cs for provider instantiation based on providerType

### DTOs & Validators for User Story 1

- [X] T090 [P] [US1] Create PaymentRequest DTO in Maliev.PaymentService.Api/Models/Requests/PaymentRequest.cs per payments-api.yaml schema
- [X] T091 [P] [US1] Create PaymentResponse DTO in Maliev.PaymentService.Api/Models/Responses/PaymentResponse.cs per payments-api.yaml schema
- [ ] T092 [P] [US1] Create ErrorResponse DTO in Maliev.PaymentService.Api/Models/Responses/ErrorResponse.cs per payments-api.yaml schema
- [ ] T093 [US1] Create PaymentRequestValidator in Maliev.PaymentService.Api/Validators/PaymentRequestValidator.cs using FluentValidation (amount > 0, currency 3 chars uppercase, customerId required, validate currency against supported currency list from provider registry to reject unsupported currencies early)

### Services for User Story 1

- [X] T094 [US1] Create IPaymentRoutingService interface in Maliev.PaymentService.Core/Interfaces/IPaymentRoutingService.cs
- [X] T095 [US1] Create PaymentRoutingService in Maliev.PaymentService.Infrastructure/Services/PaymentRoutingService.cs with currency-based routing, provider priority, circuit breaker awareness
- [ ] T096 [US1] Create IPaymentService interface in Maliev.PaymentService.Core/Interfaces/IPaymentService.cs
- [ ] T097 [US1] Create PaymentService in Maliev.PaymentService.Core/Services/PaymentService.cs with payment orchestration, idempotency checks, Polly resilience, transaction logging
- [ ] T098 [US1] Integrate Polly resilience pipeline in PaymentService for provider API calls (retry, circuit breaker, timeout)

### Events for User Story 1

- [ ] T099 [P] [US1] Create PaymentCreatedEvent in Maliev.PaymentService.Core/Events/PaymentCreatedEvent.cs per events-schema.yaml
- [ ] T100 [P] [US1] Create PaymentCompletedEvent in Maliev.PaymentService.Core/Events/PaymentCompletedEvent.cs per events-schema.yaml
- [ ] T101 [P] [US1] Create PaymentFailedEvent in Maliev.PaymentService.Core/Events/PaymentFailedEvent.cs per events-schema.yaml
- [ ] T102 [US1] Add event publishing to PaymentService using IEventPublisher (publish PaymentCreatedEvent, PaymentCompletedEvent, PaymentFailedEvent)

### Controller for User Story 1

- [ ] T103 [US1] Create PaymentsController in Maliev.PaymentService.Api/Controllers/PaymentsController.cs with POST /v1/payments endpoint per payments-api.yaml
- [ ] T104 [US1] Add Idempotency-Key header validation and processing in PaymentsController using IIdempotencyService
- [ ] T105 [US1] Add X-Correlation-Id header handling in PaymentsController for distributed tracing
- [ ] T106 [US1] Add JWT authentication requirement to PaymentsController using [Authorize] attribute

### Metrics for User Story 1

- [ ] T107 [US1] Add payment transaction metrics to PrometheusMetricsService (payment_transactions_total counter by provider/status, payment_processing_duration histogram)
- [ ] T108 [US1] Add circuit breaker state metrics to PrometheusMetricsService (circuit_breaker_state gauge by provider)

**Checkpoint**: User Story 1 complete - MVP achieved. Payments can be initiated and processed through configured providers.

---

## Phase 5: User Story 4 - Handle Provider Webhooks (Priority: P2)

**Goal**: Receive, validate, and process asynchronous notifications from payment providers to keep transaction status synchronized.

**Independent Test**: Can simulate webhook events from providers and verify transaction status updates and notifications are sent.

**Dependencies**: Requires US1 (needs payment transactions to update). Can run in parallel with US2/US3 once US1 is complete.

### Test Tasks for User Story 4 (Test-First Development)

- [ ] T109 [P] [US4] Create WebhooksControllerIntegrationTests in Maliev.PaymentService.Tests/Integration/Controllers/WebhooksControllerIntegrationTests.cs (test webhook receipt and processing) - WRITE TESTS FIRST
- [ ] T110 [P] [US4] Create WebhookValidationServiceTests in Maliev.PaymentService.Tests/Unit/Services/WebhookValidationServiceTests.cs (test signature validation for all providers) - WRITE TESTS FIRST

### Entities for User Story 4

- [ ] T111 [US4] Create WebhookEvent entity in Maliev.PaymentService.Core/Entities/WebhookEvent.cs with all properties from data-model.md
- [ ] T112 [US4] Create WebhookEventConfiguration EF config in Maliev.PaymentService.Infrastructure/Data/Configurations/WebhookEventConfiguration.cs (table name: webhook_events, indexes, constraints)
- [ ] T113 [US4] Add WebhookEvent DbSet to PaymentDbContext in Maliev.PaymentService.Infrastructure/Data/PaymentDbContext.cs
- [ ] T114 [US4] Create EF Core migration for webhook table using dotnet ef migrations add AddWebhookTable

### Repositories for User Story 4

- [ ] T115 [US4] Create IWebhookRepository interface in Maliev.PaymentService.Core/Interfaces/IWebhookRepository.cs (AddAsync, GetByProviderEventIdAsync, UpdateAsync, DeleteOlderThanAsync)
- [ ] T116 [US4] Create WebhookRepository in Maliev.PaymentService.Infrastructure/Data/Repositories/WebhookRepository.cs implementing IWebhookRepository

### Webhook Validation Services

- [ ] T117 [P] [US4] Create StripeWebhookValidator in Maliev.PaymentService.Infrastructure/Providers/StripeWebhookValidator.cs with HMAC SHA-256 signature validation and timestamp verification
- [ ] T118 [P] [US4] Create PayPalWebhookValidator in Maliev.PaymentService.Infrastructure/Providers/PayPalWebhookValidator.cs with certificate-based validation using CERT-URL and TRANSMISSION-SIG
- [ ] T119 [P] [US4] Create OmiseWebhookValidator in Maliev.PaymentService.Infrastructure/Providers/OmiseWebhookValidator.cs with IP whitelist validation
- [ ] T120 [P] [US4] Create ScbWebhookValidator in Maliev.PaymentService.Infrastructure/Providers/ScbWebhookValidator.cs with HMAC signature validation
- [ ] T121 [US4] Create IWebhookValidationService interface in Maliev.PaymentService.Core/Interfaces/IWebhookValidationService.cs
- [ ] T122 [US4] Create WebhookValidationService in Maliev.PaymentService.Core/Services/WebhookValidationService.cs that delegates to provider-specific validators

### Webhook Processing Services

- [ ] T123 [US4] Create IWebhookProcessingService interface in Maliev.PaymentService.Core/Interfaces/IWebhookProcessingService.cs
- [ ] T124 [US4] Create WebhookProcessingService in Maliev.PaymentService.Core/Services/WebhookProcessingService.cs with deduplication, transaction status updates, event publishing

### DTOs for User Story 4

- [ ] T125 [P] [US4] Create WebhookReceivedResponse DTO in Maliev.PaymentService.Api/Models/Responses/WebhookReceivedResponse.cs per webhooks-api.yaml
- [ ] T126 [P] [US4] Create TestWebhookRequest DTO in Maliev.PaymentService.Api/Models/Requests/TestWebhookRequest.cs per webhooks-api.yaml

### Controller for User Story 4

- [ ] T127 [US4] Create WebhooksController in Maliev.PaymentService.Api/Controllers/WebhooksController.cs with POST /v1/webhooks/{provider} endpoint per webhooks-api.yaml
- [ ] T128 [US4] Add webhook signature validation to WebhooksController before processing (validate headers based on provider)
- [ ] T129 [US4] Add POST /v1/webhooks/{provider}/test endpoint for sandbox testing (environment check: only in Development/Staging)
- [ ] T130 [US4] Add deduplication logic in WebhooksController using IWebhookRepository.GetByProviderEventIdAsync
- [ ] T131 [P] [US4] Create WebhookRateLimitingMiddleware in Maliev.PaymentService.Api/Middleware/WebhookRateLimitingMiddleware.cs to prevent webhook retry storms (100 requests per minute per provider, applies only to /webhooks/* endpoints)

### Background Processing

- [ ] T132 [US4] Create WebhookCleanupService in Maliev.PaymentService.Infrastructure/BackgroundServices/WebhookCleanupService.cs as IHostedService to delete webhook_events older than 30 days
- [ ] T133 [US4] Register WebhookCleanupService in Maliev.PaymentService.Api/Program.cs with scheduled execution (daily at 2 AM UTC)

### Metrics for User Story 4

- [ ] T134 [US4] Add webhook metrics to PrometheusMetricsService (webhook_events_total counter by provider/status, webhook_processing_duration histogram, webhook_validation_failures counter)

**Checkpoint**: User Story 4 complete - Webhooks are received, validated, processed, and transaction status stays synchronized.

---

## Phase 6: User Story 2 - Check Payment Status (Priority: P2)

**Goal**: Enable internal services to query the current status of a payment transaction.

**Independent Test**: Can initiate a payment (using US1), then query its status and verify the correct status is returned.

**Dependencies**: Requires US1 (needs payment transactions to query). Can run in parallel with US3/US4 once US1 is complete.

### Test Tasks for User Story 2 (Test-First Development)

- [ ] T135 [P] [US2] Create PaymentStatusServiceTests in Maliev.PaymentService.Tests/Unit/Services/PaymentStatusServiceTests.cs (test status queries with caching) - WRITE TESTS FIRST
- [ ] T136 [P] [US2] Create status query integration tests in PaymentsControllerIntegrationTests (test GET /payments/{id} with caching) - WRITE TESTS FIRST

### DTOs for User Story 2

- [ ] T137 [US2] Create PaymentStatusResponse DTO in Maliev.PaymentService.Api/Models/Responses/PaymentStatusResponse.cs per payments-api.yaml schema

### Services for User Story 2

- [ ] T138 [US2] Create IPaymentStatusService interface in Maliev.PaymentService.Core/Interfaces/IPaymentStatusService.cs
- [ ] T139 [US2] Create PaymentStatusService in Maliev.PaymentService.Core/Services/PaymentStatusService.cs with caching using Redis (cache TTL: 60 seconds for active transactions)

### Controller for User Story 2

- [ ] T140 [US2] Add GET /v1/payments/{transactionId} endpoint to PaymentsController per payments-api.yaml
- [ ] T141 [US2] Add Redis caching for status queries in PaymentsController using IDistributedCache (cache hit/miss logging)

### Metrics for User Story 2

- [ ] T142 [US2] Add status query metrics to PrometheusMetricsService (payment_status_queries_total counter, payment_status_cache_hits counter, payment_status_query_duration histogram)

**Checkpoint**: User Story 2 complete - Payment status can be queried with caching for performance.

---

## Phase 7: User Story 3 - Refund Payment (Priority: P3)

**Goal**: Enable internal services to refund completed payment transactions (full or partial).

**Independent Test**: Can complete a payment (using US1), then initiate a refund and verify the refund is processed.

**Dependencies**: Requires US1 (needs completed payments to refund). Can run in parallel with US2/US4 once US1 is complete.

### Test Tasks for User Story 3 (Test-First Development)

- [ ] T143 [P] [US3] Create RefundServiceTests in Maliev.PaymentService.Tests/Unit/Services/RefundServiceTests.cs (test refund validation, partial vs full refund logic) - WRITE TESTS FIRST
- [ ] T144 [P] [US3] Create refund integration tests in PaymentsControllerIntegrationTests (test POST /payments/{id}/refund with real database) - WRITE TESTS FIRST

### Entities for User Story 3

- [ ] T145 [US3] Create RefundTransaction entity in Maliev.PaymentService.Core/Entities/RefundTransaction.cs with all properties from data-model.md
- [ ] T146 [US3] Create RefundTransactionConfiguration EF config in Maliev.PaymentService.Infrastructure/Data/Configurations/RefundTransactionConfiguration.cs (table name: refund_transactions, indexes, constraints, foreign keys)
- [ ] T147 [US3] Add RefundTransaction DbSet to PaymentDbContext in Maliev.PaymentService.Infrastructure/Data/PaymentDbContext.cs
- [ ] T148 [US3] Create EF Core migration for refund table using dotnet ef migrations add AddRefundTable

### Repositories for User Story 3

- [ ] T149 [US3] Create IRefundRepository interface in Maliev.PaymentService.Core/Interfaces/IRefundRepository.cs (GetByIdAsync, GetByPaymentTransactionIdAsync, AddAsync, UpdateAsync)
- [ ] T150 [US3] Create RefundRepository in Maliev.PaymentService.Infrastructure/Data/Repositories/RefundRepository.cs implementing IRefundRepository

### DTOs & Validators for User Story 3

- [ ] T151 [P] [US3] Create RefundRequest DTO in Maliev.PaymentService.Api/Models/Requests/RefundRequest.cs per payments-api.yaml schema
- [ ] T152 [P] [US3] Create RefundResponse DTO in Maliev.PaymentService.Api/Models/Responses/RefundResponse.cs per payments-api.yaml schema
- [ ] T153 [US3] Create RefundRequestValidator in Maliev.PaymentService.Api/Validators/RefundRequestValidator.cs using FluentValidation (amount > 0, amount <= remaining refundable amount)

### Services for User Story 3

- [ ] T154 [US3] Create IRefundService interface in Maliev.PaymentService.Core/Interfaces/IRefundService.cs
- [ ] T155 [US3] Create RefundService in Maliev.PaymentService.Core/Services/RefundService.cs with refund orchestration, validation (check transaction status, refundable amount), provider adapter calls, transaction logging
- [ ] T156 [US3] Add refund logic to provider adapters (implement ProcessRefundAsync in StripeProvider, PayPalProvider, OmiseProvider, ScbApiProvider)

### Events for User Story 3

- [ ] T157 [P] [US3] Create RefundInitiatedEvent in Maliev.PaymentService.Core/Events/RefundInitiatedEvent.cs per events-schema.yaml
- [ ] T158 [P] [US3] Create RefundCompletedEvent in Maliev.PaymentService.Core/Events/RefundCompletedEvent.cs per events-schema.yaml
- [ ] T159 [P] [US3] Create RefundFailedEvent in Maliev.PaymentService.Core/Events/RefundFailedEvent.cs per events-schema.yaml
- [ ] T160 [US3] Add event publishing to RefundService using IEventPublisher (publish RefundInitiatedEvent, RefundCompletedEvent, RefundFailedEvent)

### Controller for User Story 3

- [ ] T161 [US3] Add POST /v1/payments/{transactionId}/refund endpoint to PaymentsController per payments-api.yaml
- [ ] T162 [US3] Add Idempotency-Key validation for refund requests in PaymentsController using IIdempotencyService

### Metrics for User Story 3

- [ ] T163 [US3] Add refund metrics to PrometheusMetricsService (refund_transactions_total counter by provider/status, refund_processing_duration histogram)

**Checkpoint**: User Story 3 complete - Payments can be refunded (full or partial) with proper validation and event publishing.

---

## Phase 8: User Story 6 - Monitor Gateway Operations (Priority: P3)

**Goal**: Provide operations teams with visibility into payment gateway performance, transaction volumes, failure rates, provider health, and latency metrics.

**Independent Test**: Can process payments and refunds, then query metrics endpoint to verify accurate reporting.

**Dependencies**: Builds on all previous stories. Adds observability without blocking core functionality.

### Test Tasks for User Story 6 (Test-First Development)

- [ ] T164 [P] [US6] Create MetricsControllerIntegrationTests in Maliev.PaymentService.Tests/Integration/Controllers/MetricsControllerIntegrationTests.cs (test metrics endpoints) - WRITE TESTS FIRST

### Metrics Endpoints

- [ ] T165 [US6] Create MetricsController in Maliev.PaymentService.Api/Controllers/MetricsController.cs with GET /v1/metrics/summary endpoint for business metrics (transaction volume, success rate, average latency)
- [ ] T166 [US6] Add provider health metrics endpoint GET /v1/metrics/providers in MetricsController (failure rate, circuit breaker state, success rate per provider)
- [ ] T167 [US6] Add current throughput endpoint GET /v1/metrics/throughput in MetricsController (requests per second, active concurrent requests)

### Additional Metrics

- [ ] T168 [P] [US6] Add provider health gauge metrics to PrometheusMetricsService (provider_health_status gauge, provider_failure_rate gauge, provider_success_rate_15min gauge)
- [ ] T169 [P] [US6] Add throughput metrics to PrometheusMetricsService (http_requests_per_second gauge, active_concurrent_requests gauge)
- [ ] T170 [P] [US6] Add business KPI metrics to PrometheusMetricsService (total_transaction_volume_today counter, average_transaction_amount gauge)

### Health Monitoring Services

- [ ] T171 [US6] Create ProviderHealthMonitoringService in Maliev.PaymentService.Infrastructure/BackgroundServices/ProviderHealthMonitoringService.cs as IHostedService to periodically check provider health and update success_rate_15min
- [ ] T172 [US6] Register ProviderHealthMonitoringService in Maliev.PaymentService.Api/Program.cs with scheduled execution (every 5 minutes)

### Transaction Reconciliation (FR-021)

- [ ] T173 [P] [US6] Create ReconciliationServiceTests in Maliev.PaymentService.Tests/Unit/Services/ReconciliationServiceTests.cs (test daily reconciliation logic, discrepancy detection) - WRITE TESTS FIRST
- [ ] T180 [US6] Create IReconciliationService interface in Maliev.PaymentService.Core/Interfaces/IReconciliationService.cs (ReconcileTransactionsAsync, GetDiscrepanciesAsync)
- [ ] T181 [US6] Create ReconciliationService in Maliev.PaymentService.Core/Services/ReconciliationService.cs with provider transaction fetching, comparison logic, and discrepancy detection
- [ ] T182 [US6] Create ReconciliationBackgroundService in Maliev.PaymentService.Infrastructure/BackgroundServices/ReconciliationBackgroundService.cs as IHostedService for automated daily execution (runs at 4:00 AM)
- [ ] T183 [US6] Register ReconciliationBackgroundService in Maliev.PaymentService.Api/Program.cs with scheduled execution
- [ ] T184 [US6] Add reconciliation metrics to PrometheusMetricsService (reconciliation_discrepancies_total counter by provider, reconciliation_last_run_timestamp gauge, reconciliation_transactions_checked_total counter)

### Transaction Archival

- [ ] T185 [US6] Create TransactionArchivalService in Maliev.PaymentService.Infrastructure/BackgroundServices/TransactionArchivalService.cs as IHostedService to move transactions older than 1 year to archive storage (daily at 3:00 AM)
- [ ] T186 [US6] Register TransactionArchivalService in Maliev.PaymentService.Api/Program.cs with scheduled execution

### Events for User Story 6

- [ ] T187 [P] [US6] Create ProviderDegradedEvent in Maliev.PaymentService.Core/Events/ProviderDegradedEvent.cs per events-schema.yaml
- [ ] T188 [P] [US6] Create ProviderRecoveredEvent in Maliev.PaymentService.Core/Events/ProviderRecoveredEvent.cs per events-schema.yaml
- [ ] T189 [US6] Add event publishing to CircuitBreakerStateManager (publish ProviderDegradedEvent when circuit opens, ProviderRecoveredEvent when circuit closes)

### Dashboards (Optional)

- [ ] T190 [US6] Create Grafana dashboard JSON template in docs/grafana/payment-gateway-dashboard.json with panels for transaction volume, success rate, latency, provider health (optional, not blocking)

**Checkpoint**: User Story 6 complete - Full observability with business metrics, provider health monitoring, and operational dashboards.

---

## Phase 9: Cross-Cutting & Polish

**Purpose**: Improvements that affect multiple user stories and production readiness

### API Documentation

- [ ] T191 Add XML documentation comments to all controllers in Maliev.PaymentService.Api/Controllers/ for Scalar API documentation
- [ ] T192 Configure Scalar API documentation in Maliev.PaymentService.Api/Program.cs at /scalar endpoint with API versioning, JWT authentication support, and example requests/responses

### Docker Configuration

- [ ] T193 Create Dockerfile in Maliev.PaymentService.Api/Dockerfile using multi-stage build (mcr.microsoft.com/dotnet/sdk:10.0 for build, mcr.microsoft.com/dotnet/aspnet:10.0 for runtime)
- [ ] T194 Configure Docker health check in Dockerfile for /health/ready endpoint
- [ ] T195 Set Docker user to non-root app user in Dockerfile (USER app after chown -R app:app /app)
- [ ] T196 Create docker-compose.yml in repository root with services: postgres, redis, rabbitmq, payment-gateway-api for local development

### CI/CD Workflows

- [ ] T197 Create .github/workflows/build.yml for build and test workflow (dotnet build, dotnet test with code coverage, upload coverage to Codecov)
- [ ] T198 Create .github/workflows/docker.yml for Docker image build and push workflow (build image, tag with version, push to container registry)
- [ ] T199 Configure CI to fail on warnings (TreatWarningsAsErrors already set in .csproj files)
- [ ] T200 Add pre-commit hook for secret scanning in .git/hooks/pre-commit using git-secrets or similar tool

### Documentation

- [ ] T201 Update README.md in repository root with project overview, architecture diagram, quick start guide, API documentation links
- [ ] T202 Validate quickstart.md steps work end-to-end (docker-compose up, run migrations, test API endpoints)
- [ ] T203 Create integration test validating SC-009 (new provider registration without code changes): register new test provider via API, configure supported currencies, verify payment routing to new provider works without recompiling code
- [ ] T204 Create CONTRIBUTING.md with development workflow, code style guidelines, PR process
- [ ] T205 Create architecture diagram in docs/architecture.md showing service dependencies, data flow, external integrations

### Security Hardening

- [ ] T206 Configure HTTPS redirection in Maliev.PaymentService.Api/Program.cs for production environment
- [ ] T207 Add rate limiting middleware in Maliev.PaymentService.Api/Middleware/RateLimitingMiddleware.cs using AspNetCoreRateLimit library (applies to general API endpoints /v1/payments, /v1/metrics, etc. to prevent abuse; separate from WebhookRateLimitingMiddleware which handles /webhooks/* endpoints)
- [ ] T208 Configure CORS policy in Maliev.PaymentService.Api/Program.cs with allowed origins from configuration
- [ ] T209 Add input sanitization for all user-provided strings in validators (prevent injection attacks)

### Performance Optimization

- [ ] T210 Configure EF Core query performance optimizations in PaymentDbContext (AsNoTracking for read-only queries, compiled queries for hot paths)
- [ ] T211 Add database indexes validation using EF Core migration SQL scripts (verify all indexes from data-model.md are created)
- [ ] T212 Configure HttpClient connection pooling in Maliev.PaymentService.Infrastructure/Providers/ (reuse connections to payment providers)

### Cleanup & Final Review

- [ ] T213 Run dotnet format on entire solution to ensure consistent code formatting
- [ ] T214 Verify all NuGet packages are latest stable versions
- [ ] T215 Remove any unused code, commented-out code, or debug logging
- [ ] T216 Verify all secrets are externalized (no hardcoded credentials in appsettings.json or code)
- [ ] T217 Run final integration test suite with Testcontainers against real infrastructure (PostgreSQL 18, RabbitMQ 7.0, Redis 7.2)
- [ ] T218 Verify zero compiler warnings with TreatWarningsAsErrors=true
- [ ] T219 Review all TODO comments and either implement or create GitHub issues

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Story 5 (Phase 3)**: Depends on Foundational - BLOCKS US1 and US4 (providers must exist before payments)
- **User Story 1 (Phase 4)**: Depends on Foundational + US5 - MVP complete after this
- **User Story 4 (Phase 5)**: Depends on Foundational + US1 - Can run in parallel with US2/US3
- **User Story 2 (Phase 6)**: Depends on Foundational + US1 - Can run in parallel with US3/US4
- **User Story 3 (Phase 7)**: Depends on Foundational + US1 - Can run in parallel with US2/US4
- **User Story 6 (Phase 8)**: Depends on all previous stories - Adds observability
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### Story Completion Order (Priority)

**Critical Path for MVP**:
1. Setup (Phase 1) → Foundational (Phase 2) → US5 (Phase 3) → US1 (Phase 4) = **MVP READY**

**Post-MVP Incremental Delivery**:
2. US4 (Phase 5) - Webhook handling for async status updates
3. US2 (Phase 6) - Status queries (can run parallel with US3)
4. US3 (Phase 7) - Refund capability (can run parallel with US2)
5. US6 (Phase 8) - Full observability
6. Polish (Phase 9) - Production readiness

### Dependency Graph

```
Setup (Phase 1)
    ↓
Foundational (Phase 2) ← BLOCKS everything
    ↓
US5: Manage Providers (Phase 3) ← BLOCKS US1, US4
    ↓
US1: Process Payments (Phase 4) ← BLOCKS US2, US3, US4 (MVP!)
    ├───→ US4: Webhooks (Phase 5)
    ├───→ US2: Status Queries (Phase 6)
    └───→ US3: Refunds (Phase 7)
    ↓
US6: Monitoring (Phase 8)
    ↓
Polish (Phase 9)
```

### Within Each User Story (Test-First Development)

- **Tests**: Write and ensure they FAIL before implementation (RED phase)
- **Entities**: Create entities before repositories (GREEN phase starts)
- **Repositories**: Create repositories before services
- **Services**: Create services before controllers
- **Controllers**: Create controllers before events
- **Events**: Add event publishing last
- **Refactor**: Refactor after tests pass (REFACTOR phase)

### Parallel Opportunities

**Phase 1 - Setup**: All tasks can run in parallel (T002-T009 marked [P])

**Phase 2 - Foundational**:
- Database infrastructure (T011-T014) → Sequential
- Auth + Middleware (T015-T021) → T018, T019, T020 can run in parallel
- Logging + Metrics (T022-T025) → Sequential
- Message Queue (T026-T029) → Sequential
- Redis (T030-T033) → Sequential
- Enums (T034-T037) → All in parallel
- Polly + Encryption (T038-T043) → Sequential
- Health Checks (T044-T047) → Can run in parallel

**Phase 3 - US5**:
- Test tasks (T048-T049) can run in parallel (write failing tests first)
- Entities (T050-T051) can run in parallel
- DTOs (T058-T062) can run in parallel
- Validators (T063-T064) sequential after DTOs
- Controllers/Services sequential after repositories

**Phase 4 - US1**:
- Test tasks (T071-T075) can run in parallel (write failing tests first)
- Entities (T076-T077) can run in parallel
- Provider Adapters (T085-T088) can all run in parallel (different providers)
- DTOs (T090-T092) can run in parallel
- Events (T099-T101) can run in parallel

**Phase 5 - US4**:
- Test tasks (T109-T110) can run in parallel (write failing tests first)
- Webhook Validators (T117-T120) can all run in parallel (different providers)
- DTOs (T125-T126) can run in parallel

**Phase 6 - US2**:
- Test tasks (T135-T136) can run in parallel (write failing tests first)
- Mostly sequential (status queries build on US1)

**Phase 7 - US3**:
- Test tasks (T143-T144) can run in parallel (write failing tests first)
- DTOs (T151-T152) can run in parallel
- Events (T157-T159) can run in parallel

**Phase 8 - US6**:
- Test task (T164) first (write failing tests)
- Metrics endpoints (T168-T170) can run in parallel
- Events (T187-T188) can run in parallel

**Phase 9 - Polish**:
- Docker + CI/CD (T193-T200) can run in parallel
- Documentation (T201-T205) can run in parallel

---

## Parallel Example: User Story 1 Implementation (Test-First)

**Test Phase FIRST** (Red - Write failing tests):
```bash
Task T071: "Create PaymentsControllerIntegrationTests" - WRITE FAILING TEST
Task T072: "Create PaymentServiceIntegrationTests" - WRITE FAILING TEST
Task T073: "Create PaymentRoutingServiceTests" - WRITE FAILING TEST
Task T074: "Create IdempotencyServiceTests" - WRITE FAILING TEST
Task T075: "Create integration test for idempotency" - WRITE FAILING TEST
```

**Entities Phase** (Green - Make tests pass):
```bash
Task T076: "Create PaymentTransaction entity"
Task T077: "Create TransactionLog entity"
```

**Provider Adapters Phase** (can all run together):
```bash
Task T085: "Create StripeProvider"
Task T086: "Create PayPalProvider"
Task T087: "Create OmiseProvider"
Task T088: "Create ScbApiProvider"
```

**DTOs Phase** (can run together):
```bash
Task T090: "Create PaymentRequest DTO"
Task T091: "Create PaymentResponse DTO"
Task T092: "Create ErrorResponse DTO"
```

**Events Phase** (can run together):
```bash
Task T099: "Create PaymentCreatedEvent"
Task T100: "Create PaymentCompletedEvent"
Task T101: "Create PaymentFailedEvent"
```

**Refactor Phase**: Clean up implementation while keeping tests green

---

## Implementation Strategy

### MVP First (US5 + US1 Only)

**Minimum viable product delivers**:
1. Complete Phase 1: Setup (T001-T009)
2. Complete Phase 2: Foundational (T010-T047) - CRITICAL
3. Complete Phase 3: US5 - Manage Providers (T048-T070) - TEST-FIRST
4. Complete Phase 4: US1 - Process Payments (T071-T108) - TEST-FIRST
5. **VALIDATE**: Test payment initiation end-to-end with configured provider
6. **DEPLOY**: MVP ready for production

**Estimated Tasks**: ~108 tasks for MVP (including test-first approach)

### Incremental Delivery After MVP

**Iteration 2** (Add webhook handling):
- Complete Phase 5: US4 - Webhooks (T109-T134) - TEST-FIRST
- **VALUE**: Real-time payment status updates

**Iteration 3** (Add status queries + refunds):
- Complete Phase 6: US2 - Status Queries (T135-T142) - TEST-FIRST
- Complete Phase 7: US3 - Refunds (T143-T163) - TEST-FIRST
- **VALUE**: Full payment lifecycle management

**Iteration 4** (Add observability):
- Complete Phase 8: US6 - Monitoring (T164-T190) - TEST-FIRST
- **VALUE**: Operational excellence

**Iteration 5** (Production hardening):
- Complete Phase 9: Polish (T191-T219)
- **VALUE**: Production-ready, secure, well-tested

### Parallel Team Strategy

**With 3 developers after Foundational phase complete**:

**Developer A**: US5 (Providers) → US1 (Payments) → US6 (Monitoring)
**Developer B**: US4 (Webhooks) → US2 (Status Queries) → Testing
**Developer C**: US3 (Refunds) → Documentation → CI/CD

**Synchronization points**:
- End of Phase 2 (Foundational) - everyone waits
- End of US5 - Developer B/C can start US4/US3
- End of US1 - All user stories unblocked

---

## Task Count Summary

**Phase 1 - Setup**: 9 tasks
**Phase 2 - Foundational**: 38 tasks (CRITICAL PATH - includes Testcontainers fixture for PostgreSQL, RabbitMQ, Redis)
**Phase 3 - US5 (Providers)**: 23 tasks (P1 - FOUNDATIONAL - includes 2 test tasks)
**Phase 4 - US1 (Payments)**: 38 tasks (P1 - MVP - includes 5 test tasks)
**Phase 5 - US4 (Webhooks)**: 26 tasks (P2 - includes 2 test tasks + rate limiting)
**Phase 6 - US2 (Status)**: 8 tasks (P2 - includes 2 test tasks)
**Phase 7 - US3 (Refunds)**: 21 tasks (P3 - includes 2 test tasks)
**Phase 8 - US6 (Monitoring)**: 21 tasks (P3 - includes 2 test tasks + reconciliation + archival)
**Phase 9 - Polish**: 29 tasks

**Total Tasks**: 213 tasks (task IDs: T001-T172, T173, T180-T219 with gaps T174-T179)

**MVP Scope** (Setup + Foundational + US5 + US1): 108 tasks
**Post-MVP** (US4 + US2 + US3 + US6 + Polish): 105 tasks

**Parallel Opportunities**: ~70 tasks marked [P] can run in parallel when team capacity allows

**Test-First Tasks**: 17 test tasks now positioned BEFORE implementation in each user story phase (includes new T173 ReconciliationServiceTests)

---

## Notes

- All tasks follow format: `[ID] [P?] [Story] Description with file path`
- [P] tasks = different files, no dependencies, can run in parallel
- [Story] label (US1-US6) maps task to specific user story for traceability
- **TEST-FIRST**: Test tasks now appear BEFORE implementation tasks in each user story phase (Red-Green-Refactor)
- Each user story is independently completable and testable
- Foundational phase MUST complete before any user story work begins
- US5 MUST complete before US1 (providers required for payment routing)
- US1 MUST complete before US2, US3, US4 (transactions required)
- Stop at any checkpoint to validate story independently
- File paths are absolute from repository root
- All entity configurations follow data-model.md specifications
- All API endpoints follow OpenAPI specifications in contracts/
- All events follow events-schema.yaml specifications
- TreatWarningsAsErrors=true enforced in all projects
- Real infrastructure testing with Testcontainers (PostgreSQL 18, RabbitMQ 7.0, Redis 7.2 - no in-memory substitutes)
- Polly 8.5.0 resilience patterns for all provider communications (circuit breaker: 5 consecutive failures or 50% failure rate over 30 seconds, break duration: 30 seconds, half-open allows 1 test request)
- MassTransit 8.3.4 with RabbitMQ for event publishing
- Redis for idempotency and distributed caching
- Prometheus metrics for observability
- Webhook rate limiting: 100 requests per minute per provider
- Transaction archival: Transactions older than 1 year moved to archive storage daily at 3:00 AM
