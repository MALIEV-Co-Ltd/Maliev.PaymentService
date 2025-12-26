# PaymentService Specification - Permission-Based Authorization Migration

## Overview
Migrate PaymentService from policy-based authorization to fine-grained permission-based authorization using the IAM service.

## Current State
- Uses policy-based authorization
- Has JwtAuthenticationMiddleware with ServiceId/ServiceName claims
- No fine-grained permission control

## Target State
- Permission-based authorization with format: `payment.{resource}.{action}`
- Authorization checks: `[RequirePermission(PaymentPermissions.PaymentsProcess)]`

## Permissions to Define

### Payment Operations
```
payment.payments.create      - Create payment records
payment.payments.read        - Read payment details
payment.payments.update      - Update payment information
payment.payments.process     - Process payments (critical)
payment.payments.refund      - Refund payments (critical)
payment.payments.void        - Void payments (critical)
payment.payments.reconcile   - Reconcile payment transactions
```

### Transaction Operations
```
payment.transactions.read    - Read transaction details
payment.transactions.query   - Query transaction history
payment.transactions.export  - Export transaction data
```

### Provider Operations
```
payment.providers.manage     - Manage payment providers
payment.providers.view       - View provider configurations
payment.providers.test       - Test provider connections
```

### Gateway Operations
```
payment.gateway.configure    - Configure payment gateway
payment.gateway.monitor      - Monitor gateway health
```

## Predefined Roles

### payment-admin
**Description**: Full administrative access to all payment operations
**Permissions**: All payment.* permissions

### payment-processor
**Description**: Can process, refund, and void payments
**Permissions**:
- payment.payments.create
- payment.payments.read
- payment.payments.process (critical)
- payment.payments.refund (critical)
- payment.payments.void (critical)
- payment.transactions.read

### payment-accountant
**Description**: Can reconcile and export payment data
**Permissions**:
- payment.payments.read
- payment.payments.reconcile
- payment.transactions.read
- payment.transactions.query
- payment.transactions.export

### payment-viewer
**Description**: Read-only access to payments
**Permissions**:
- payment.payments.read
- payment.transactions.read

### payment-operations
**Description**: Can manage providers and monitor gateway
**Permissions**:
- payment.payments.read
- payment.providers.manage
- payment.providers.view
- payment.providers.test
- payment.gateway.configure
- payment.gateway.monitor

## Critical Permissions
Mark as `IsCritical = true`:
- payment.payments.process
- payment.payments.refund
- payment.payments.void

These require active revocation checking for immediate effect.

## Implementation Files

**Create**:
- `Maliev.PaymentService.Api/Authorization/PaymentPermissions.cs`
- `Maliev.PaymentService.Api/Authorization/PaymentPredefinedRoles.cs`
- `Maliev.PaymentService.Api/Services/PaymentIAMRegistrationService.cs`

**Update**:
- All controller files
- `Program.cs`
- Remove JwtAuthenticationMiddleware (replaced by RequirePermission)
- All integration tests

## Success Criteria
- [ ] ~18 permissions registered with IAM
- [ ] 5 predefined roles registered
- [ ] 3 critical permissions marked
- [ ] All endpoints have [RequirePermission]
- [ ] All tests pass
