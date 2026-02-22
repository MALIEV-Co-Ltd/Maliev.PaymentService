# Implementation Plan: Bank Transfer Slip Upload & LLM Verification

**Branch**: `008-slip-upload-verification` | **Date**: 2026-02-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/008-slip-upload-verification/spec.md`

## Summary

Enable Thai B2B customers to upload bank transfer slip images for payment verification. The system will automatically verify slips using an LLM-based vision service, marking payments as Completed when valid, or PendingVerification for manual review when verification fails or is inconclusive.

## Technical Context

**Language/Version**: C# 13 / .NET 10
**Primary Dependencies**: ASP.NET Core, EF Core 10, MassTransit, IHttpClientFactory
**Storage**: PostgreSQL 18 (via EF Core), Redis 7 (idempotency/caching), GCS (slip images via UploadService)
**Testing**: xUnit, Moq, Testcontainers (Integration tests)
**Target Platform**: Linux containers (Docker/Kubernetes)
**Project Type**: web-service (microservice)
**Performance Goals**: Slip upload + verification in <10s (95th percentile), 100 concurrent uploads
**Constraints**: File size ≤10MB, images only (JPEG/PNG/WebP), graceful degradation on LLM failures
**Scale/Scope**: Thai B2B payment verification, ~1000 daily bank transfers

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Service Autonomy | PASS | PaymentService owns its database schema, calls ChatbotService/UploadService via HTTP |
| II. Explicit Contracts | PASS | New API endpoint documented, SlipUploadResponse/SlipAnalysisResult DTOs defined |
| III. Test-First Development | PASS | 6 unit tests defined in spec (SlipUploadTests.cs) |
| IV. Real Infrastructure Testing | PASS | Integration tests use Testcontainers for PostgreSQL/RabbitMQ |
| V. Auditability & Observability | PASS | Slip data persisted for audit trail, logging for LLM calls |
| VI. Security & Compliance | PASS | JWT auth, ownership validation, employee permission check |
| VII. Secrets Management | PASS | No secrets in code, config via IOptions |
| VIII. Zero Warnings Policy | PASS | Will verify with `dotnet build` |
| IX. Clean Project Artifacts | PASS | No additional markdown in root |
| X. Docker Best Practices | PASS | Existing Dockerfile pattern followed |
| XI. Simplicity & Maintainability | PASS | Minimal changes, follows existing patterns |
| XII. Business Metrics | PASS | Auto-verification rate, manual review rate trackable |
| XIII. .NET Aspire Integration | PASS | Uses Maliev.Aspire.ServiceDefaults package |
| XIV. Code Quality | PASS | No AutoMapper, FluentValidation, or FluentAssertions |
| XV. Project Structure | PASS | Follows existing flat structure |
| XVI. CI/CD Standards | PASS | No docker-compose required |

**Gate Status**: PASS - No violations detected

## Project Structure

### Documentation (this feature)

```text
specs/008-slip-upload-verification/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── slip-api.yaml    # OpenAPI spec for new endpoint
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
Maliev.PaymentService.Core/
├── Enums/
│   └── PaymentStatus.cs           # EDIT: Add PendingVerification = 6
├── Entities/
│   └── PaymentTransaction.cs      # EDIT: Add slip-related properties

Maliev.PaymentService.Infrastructure/
├── Data/Configurations/
│   └── PaymentTransactionConfiguration.cs  # EDIT: Map new columns
└── Migrations/
    └── [timestamp]_AddSlipUrlToPaymentTransaction.cs  # CREATE

Maliev.PaymentService.Api/
├── Clients/                        # CREATE DIRECTORY
│   ├── IUploadServiceClient.cs    # CREATE
│   ├── UploadServiceClient.cs     # CREATE
│   ├── IChatbotServiceClient.cs   # CREATE
│   └── ChatbotServiceClient.cs    # CREATE
├── Controllers/
│   └── PaymentsController.cs      # EDIT: Add UploadSlip action
├── Models/Responses/
│   ├── SlipAnalysisResult.cs      # CREATE
│   └── SlipUploadResponse.cs      # CREATE
└── Program.cs                     # EDIT: Register new clients

Maliev.PaymentService.Tests/
└── Unit/Controllers/
    └── SlipUploadTests.cs         # CREATE

Maliev.ChatbotService.Api/
└── Controllers/V1/
    └── VisionController.cs        # CREATE (prerequisite)
```

**Structure Decision**: Extends existing flat project structure with new `Clients/` directory for HTTP client interfaces following the established repository pattern.

## Complexity Tracking

No constitutional violations requiring justification.

## Phase 0: Research Summary

See [research.md](./research.md) for full details.

**Key Decisions:**
1. HTTP clients use `IHttpClientFactory` with typed clients (existing pattern in ServiceDefaults)
2. LLM verification failures gracefully degrade to PendingVerification (never throw to caller)
3. Slip data persisted directly on PaymentTransaction entity (no separate audit table)
4. Concurrent uploads handled via last-write-wins (no distributed locking)
5. File upload path: `payment-slips/{paymentId}/{timestamp}_{filename}`

## Phase 1: Design Artifacts

- [data-model.md](./data-model.md) - Entity extensions and state transitions
- [contracts/slip-api.yaml](./contracts/slip-api.yaml) - OpenAPI specification
- [quickstart.md](./quickstart.md) - Developer quick start guide

## Implementation Phases

### Phase 1: ChatbotService Vision Endpoint (Prerequisite)

**Owner**: ChatbotService team or coordinated effort
**Blocking**: PaymentService slip upload feature

1. Create `Maliev.ChatbotService.Api/Controllers/V1/VisionController.cs`
2. Implement `POST /chatbot/v1/vision/analyze-slip` with Anthropic Claude vision API
3. Verify endpoint returns `SlipAnalysisResult` JSON structure

### Phase 2: PaymentService Enum & Entity

1. Add `PendingVerification = 6` to `PaymentStatus` enum
2. Add slip properties to `PaymentTransaction` entity
3. Update EF Core configuration
4. Generate and apply migration

### Phase 3: HTTP Clients

1. Create `IUploadServiceClient` / `UploadServiceClient`
2. Create `IChatbotServiceClient` / `ChatbotServiceClient`
3. Create `SlipAnalysisResult` DTO
4. Register clients in `Program.cs`

### Phase 4: Controller Endpoint

1. Create `SlipUploadResponse` DTO
2. Add `UploadSlip` action to `PaymentsController`
3. Implement business logic per FR-001 through FR-015
4. Add ownership and permission validation

### Phase 5: Testing & Validation

1. Create `SlipUploadTests.cs` with 6 required test scenarios
2. Run `dotnet build` - verify zero warnings
3. Run `dotnet test` - verify all tests pass
4. Verify no `PaymentStatus` switch statements broken
