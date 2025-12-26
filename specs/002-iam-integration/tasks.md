# Task List: IAM Integration Migration

**Feature Branch**: `002-iam-integration`
**Status**: Draft
**Specification**: [spec.md](./spec.md)
**Implementation Plan**: [plan.md](./plan.md)

## Phase 1: Setup

Goal: Initialize the environment for the new authorization framework.

- [x] T001 Create Authorization directory in Maliev.PaymentService.Api/Authorization

## Phase 2: Foundational

Goal: Define the core permission model and the base authorization logic.

- [x] T002 [P] Define granular permissions in Maliev.PaymentService.Api/Authorization/PaymentPermissions.cs
- [x] T003 [P] Define predefined roles and mappings in Maliev.PaymentService.Api/Authorization/PaymentPredefinedRoles.cs
- [x] T004 Create PermissionRequirement in Maliev.PaymentService.Api/Authorization/PermissionRequirement.cs
- [x] T005 Create RequirePermissionAttribute in Maliev.PaymentService.Api/Authorization/RequirePermissionAttribute.cs
- [x] T006 Implement core logic in Maliev.PaymentService.Api/Authorization/PermissionAuthorizationHandler.cs
- [x] T007 Implement short-term caching for non-critical permissions in Maliev.PaymentService.Api/Authorization/PermissionAuthorizationHandler.cs
- [x] T008 Implement real-time revocation for critical permissions in Maliev.PaymentService.Api/Authorization/PermissionAuthorizationHandler.cs
- [x] T009 Implement structured logging for authorization failures in Maliev.PaymentService.Api/Authorization/PermissionAuthorizationHandler.cs

## Phase 3: User Story 1 - Secure Payment Processing

**Story Goal**: Secure payment and refund operations with mandatory permission checks.
**Priority**: P1
**Independent Test**: Attempt processing a payment with and without the `payment.payments.process` permission.

- [x] T010 [US1] Update integration tests to use permission-based tokens in Maliev.PaymentService.Tests/Integration/Controllers/PaymentsControllerIntegrationTests.cs
- [x] T011 [US1] Apply [RequirePermission] to class and process methods in Maliev.PaymentService.Api/Controllers/PaymentsController.cs
- [x] T012 [US1] Apply [RequirePermission] to refund methods in Maliev.PaymentService.Api/Controllers/PaymentsController.cs

## Phase 4: User Story 2 - Granular Role Assignment

**Story Goal**: Synchronize permissions and roles with IAM service on startup.
**Priority**: P2
**Independent Test**: Verify IAM service receives registration request on application startup.

- [x] T013 [US2] Verify role-based access in integration tests (create if missing) in Maliev.PaymentService.Tests/Integration/Authorization/RoleAccessTests.cs
- [x] T014 [US2] Create IAM registration service in Maliev.PaymentService.Api/Services/PaymentIAMRegistrationService.cs
- [x] T015 [US2] Implement Push-on-Startup synchronization logic in Maliev.PaymentService.Api/Services/PaymentIAMRegistrationService.cs
- [x] T016 [US2] Register and trigger registration service in Maliev.PaymentService.Api/Program.cs

## Phase 5: User Story 3 - Infrastructure and Gateway Monitoring

**Story Goal**: Secure provider management and health monitoring.
**Priority**: P3
**Independent Test**: Verify operations role can access providers but not payments.

- [x] T017 [US3] Apply [RequirePermission] to Maliev.PaymentService.Api/Controllers/ProvidersController.cs
- [x] T018 [US3] Apply [RequirePermission] to Maliev.PaymentService.Api/Controllers/TestController.cs
- [x] T019 [US3] Secure health and monitoring endpoints in Maliev.PaymentService.Api/Controllers/WebhooksController.cs

## Phase 6: Polish & Cross-Cutting Concerns

Goal: Clean up legacy code and perform final validation.

- [x] T020 Remove legacy JwtAuthenticationMiddleware from Maliev.PaymentService.Api/Middleware/JwtAuthenticationMiddleware.cs
- [x] T021 Cleanup legacy authorization configuration in Maliev.PaymentService.Api/Program.cs
- [x] T022 [P] Verify authorization latency for non-critical requests (SC-003)
- [x] T023 [P] Verify revocation speed for critical permissions (SC-004)
- [x] T024 Final verification of all success criteria (SC-001 to SC-005)

## Implementation Strategy

1. **Foundations First**: Complete Phase 2 to have a working authorization attribute and handler.
2. **MVP Delivery**: Focus on User Story 1 (Phase 3) to secure the most critical part of the system (payments). Ensure tests are updated BEFORE applying attributes.
3. **IAM Sync**: Implement Phase 4 to ensure permissions are registered correctly. Authorize tests first.
4. **Complete Coverage**: Secure remaining controllers in Phase 5.
5. **Clean & Verify**: Perform final cleanup and verification in Phase 6, including performance benchmarking.

## Dependencies

- **US1** depends on **Foundational** (T002-T009)
- **US2** depends on **Foundational** (T002-T003)
- **US3** depends on **Foundational** (T002-T009)
- **Polish** depends on **US1, US2, US3**

## Parallel Execution Opportunities

- T002 and T003 can be done in parallel.
- US1, US2, and US3 implementation tasks can be done in parallel once Foundational phase is complete.
- Performance verification tasks (T022, T023) can be done in parallel.
