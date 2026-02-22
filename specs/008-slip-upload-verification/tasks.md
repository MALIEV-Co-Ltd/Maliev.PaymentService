# Implementation Tasks: Bank Transfer Slip Upload & LLM Verification

**Input**: Design documents from `/specs/008-slip-upload-verification/`
**Prerequisites**: plan.md, spec.md, data-model.md, contracts/slip-api.yaml, quickstart.md, research.md

**Tests**: Test-First Development (TDD) is mandated by the project constitution. Test tasks are included and prioritized before implementation steps.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Setup (External Prerequisites)

**Purpose**: Ensure cross-service dependencies are met.

- [x] T001 Verify or create `Maliev.ChatbotService.Api/Controllers/V1/VisionController.cs` with `analyze-slip` endpoint per implementation plan

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure, data models, and HTTP clients that MUST be complete before ANY user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Update `Maliev.PaymentService.Core/Enums/PaymentStatus.cs` with `PendingVerification = 6`
- [x] T003 Update `Maliev.PaymentService.Core/Entities/PaymentTransaction.cs` to add `SlipUrl`, `SlipExtractedAmount`, `SlipBankName`, `SlipTransferDate`, `SlipVerificationNotes`, and `SlipVerifiedAt`
- [x] T004 Update `Maliev.PaymentService.Infrastructure/Data/Configurations/PaymentTransactionConfiguration.cs` with column mappings for the new slip properties
- [x] T005 Generate EF Core migration `AddSlipUrlToPaymentTransaction` via `dotnet ef migrations add`
- [x] T006 [P] Create `Maliev.PaymentService.Api/Models/Responses/SlipAnalysisResult.cs`
- [x] T007 [P] Create `Maliev.PaymentService.Api/Models/Responses/SlipUploadResponse.cs`
- [x] T008 [P] Create `Maliev.PaymentService.Api/Clients/IUploadServiceClient.cs` and `Maliev.PaymentService.Api/Clients/UploadServiceClient.cs`
- [x] T009 [P] Create `Maliev.PaymentService.Api/Clients/IChatbotServiceClient.cs` and `Maliev.PaymentService.Api/Clients/ChatbotServiceClient.cs`
- [x] T010 Register `IUploadServiceClient` and `IChatbotServiceClient` in `Maliev.PaymentService.Api/Program.cs` with resilience handlers

**Checkpoint**: Foundation ready - database is prepared, and clients are available for injection.

---

## Phase 3: User Story 1 - Customer Uploads Bank Transfer Slip (Priority: P1) 🎯 MVP

**Goal**: Allow customers to upload a valid transfer slip image, which the system stores and uses to initiate verification.

**Independent Test**: An authorized request with a valid image is accepted, saved via UploadService, and returns a 200 OK (even if verification is mocked).

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T011 [P] [US1] Create `Maliev.PaymentService.Tests/Unit/Controllers/SlipUploadTests.cs` and write failing tests for file validation, 409 status checks, and upload success
- [x] T011b [P] [US1] Add failing integration tests for slip upload endpoint using Testcontainers in `Maliev.PaymentService.Tests/Integration/Controllers/PaymentsControllerIntegrationTests.cs`

### Implementation for User Story 1

- [x] T012 [US1] Add `UploadSlip` endpoint skeleton to `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`
- [x] T013 [US1] Implement file validation (size <= 10MB, type JPEG/PNG/WebP) and PaymentStatus precondition checks (409 Conflict) in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`
- [x] T014 [US1] Implement GCS upload via `IUploadServiceClient`, handle 502 gracefully, and save/replace `SlipUrl` (FR-013 re-upload logic) in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently (slip uploads work, validation works).

---

## Phase 4: User Story 2 - Payment Auto-Verified by LLM (Priority: P1)

**Goal**: Automatically verify uploaded slips via ChatbotService and mark payment as Completed if valid.

**Independent Test**: Uploading a slip representing a valid transfer with a matching amount automatically transitions the payment status to Completed and fires the completion event.

### Tests for User Story 2 ⚠️

- [x] T015 [P] [US2] Add failing tests for successful LLM auto-verification to `Maliev.PaymentService.Tests/Unit/Controllers/SlipUploadTests.cs`

### Implementation for User Story 2

- [x] T016 [US2] Implement `IChatbotServiceClient.AnalyzeSlipAsync` call inside the `UploadSlip` method in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`
- [x] T017 [US2] Implement status transition to `Completed` if amount matches/exceeds, and ALWAYS save extracted verification data to the entity regardless of outcome in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`
- [x] T018 [US2] Publish `PaymentCompletedEvent` on successful auto-verification in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`
- [x] T018b [US2] Instrument auto-verification business metrics (success/manual review rate) via `IMetricsService` in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`

**Checkpoint**: Slips can now be uploaded AND automatically verified.

---

## Phase 5: User Story 3 - Manual Review Fallback (Priority: P2)

**Goal**: Ensure the system degrades gracefully to `PendingVerification` if LLM fails, times out, or detects an invalid slip/mismatched amount.

**Independent Test**: Simulating an HTTP timeout from ChatbotService or uploading a slip with a lower amount correctly sets the payment to `PendingVerification`.

### Tests for User Story 3 ⚠️

- [x] T019 [P] [US3] Add failing tests for LLM failure, timeout, and amount mismatch to `Maliev.PaymentService.Tests/Unit/Controllers/SlipUploadTests.cs`

### Implementation for User Story 3

- [x] T020 [P] [US3] Implement graceful degradation try/catch block for HTTP exceptions in `Maliev.PaymentService.Api/Clients/ChatbotServiceClient.cs`
- [x] T021 [US3] Implement status transition to `PendingVerification` for invalid slips, low amounts, or LLM unavailability in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`

**Checkpoint**: Resilience is implemented. Failures do not drop the uploaded slip.

---

## Phase 6: User Story 4 - Employee Uploads Slip on Behalf of Customer (Priority: P3)

**Goal**: Allow customer support employees to bypass ownership checks to upload slips on behalf of customers.

**Independent Test**: An employee token with `payments.slip.upload` permission successfully uploads a slip for any CustomerId.

### Tests for User Story 4 ⚠️

- [x] T022 [P] [US4] Add failing tests for employee authorization bypass and customer ownership enforcement to `Maliev.PaymentService.Tests/Unit/Controllers/SlipUploadTests.cs`

### Implementation for User Story 4

- [x] T023 [US4] Implement ownership check and `payments.slip.upload` permission bypass logic in `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`

**Checkpoint**: All user stories are implemented.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Ensure the feature meets the zero warnings policy and all tests pass.

- [x] T024 Verify `dotnet build` completes with zero warnings (warnings are errors per constitution)
- [x] T025 Run all unit tests (`dotnet test`) to ensure no regressions
- [x] T026 Update OpenAPI documentation comments on the new endpoint
- [x] T027 Run Integration tests (with Testcontainers) to verify DB mappings, real PostgreSQL persistence, and RabbitMQ event publishing
- [x] T028 Verify metrics endpoint exposes the new slip verification business metrics correctly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can be checked/completed immediately.
- **Foundational (Phase 2)**: Depends on nothing. BLOCKS all user stories.
- **User Stories (Phase 3-6)**: 
  - US1 (Phase 3) must be completed first to establish the upload flow.
  - US2 (Phase 4) depends on US1.
  - US3 (Phase 5) depends on US1 & US2.
  - US4 (Phase 6) can be done anytime after US1.
- **Polish (Phase 7)**: Depends on all implementation phases being complete.

### Parallel Opportunities

- **T006 - T009** (DTOs and HTTP Clients) can be created in parallel by different developers.
- **T020** (ChatbotServiceClient resilience) can be implemented in parallel with **T019** (Test writing).
- **T022** (US4 Tests) can be written in parallel with any other test suite.

---

## Parallel Example

```bash
# Developer A implements the DTOs and Contracts:
- [ ] T006 [P] Create Maliev.PaymentService.Api/Models/Responses/SlipAnalysisResult.cs
- [ ] T007 [P] Create Maliev.PaymentService.Api/Models/Responses/SlipUploadResponse.cs

# Developer B implements the API Clients:
- [ ] T008 [P] Create Maliev.PaymentService.Api/Clients/IUploadServiceClient.cs ...
- [ ] T009 [P] Create Maliev.PaymentService.Api/Clients/IChatbotServiceClient.cs ...
```

---

## Implementation Strategy

### Incremental Delivery

1. **Foundations**: Complete Phase 2. The database is ready, clients exist, but no endpoint exposes them.
2. **MVP**: Complete Phase 3. Customers can upload slips and get a URL stored.
3. **Automation**: Complete Phase 4. Happy path payments are now auto-verified.
4. **Resilience**: Complete Phase 5. Edge cases and failures are properly routed to manual review.
5. **Support**: Complete Phase 6. Employees can intervene.
6. **Validation**: Complete Phase 7. Ensures constitutional compliance (Zero Warnings, Test-First).