# PaymentService Implementation Plan - Permission-Based Authorization Migration

## Phase 1: Define Permissions & Roles (2 hours)
- Create PaymentPermissions.cs
- Create PaymentPredefinedRoles.cs
- Define ~18 permissions (3 critical)
- Define 5 roles

## Phase 2: IAM Registration (2 hours)
- Create PaymentIAMRegistrationService.cs
- Update Program.cs
- Add IAM configuration
- Mark critical permissions

## Phase 3: Update Controllers (3 hours)
- Update PaymentsController
- Update TransactionsController
- Update ProvidersController
- Replace JwtAuthenticationMiddleware with RequirePermission
- Remove custom middleware

## Phase 4: Update Tests (4 hours)
- Update integration tests
- Add critical permission tests
- Test refund/void scenarios

## Phase 5: Deploy & Verify (2 hours)
- Deploy with feature flag OFF
- Test payment processing
- Enable feature flag
- Production rollout

**Total: ~13 hours (~2 days)**
