# Technical Implementation Plan: IAM Integration Migration

This plan outlines the migration from policy-based authorization to permission-based authorization using an external IAM service.

## Architecture Overview

We will implement a granular permission-based authorization system where:
1. Permissions are defined as constants in the code.
2. Roles are predefined groups of these permissions.
3. A registration service synchronizes these definitions with the IAM service on startup.
4. Controllers use a custom `[RequirePermission]` attribute.
5. An authorization handler validates permissions against JWT claims, with a short-term cache for performance and special handling for "critical" permissions.

## Proposed Changes

### 1. Define Permissions and Roles
- Create `Maliev.PaymentService.Api/Authorization/PaymentPermissions.cs`: Define all 18 permissions as constants.
- Create `Maliev.PaymentService.Api/Authorization/PaymentPredefinedRoles.cs`: Define the 5 roles and their permission mappings.

### 2. Custom Authorization Attribute and Handler
- Create `Maliev.PaymentService.Api/Authorization/RequirePermissionAttribute.cs`: Inherits from `AuthorizeAttribute`.
- Create `Maliev.PaymentService.Api/Authorization/PermissionRequirement.cs`: Implements `IAuthorizationRequirement`.
- Create `Maliev.PaymentService.Api/Authorization/PermissionAuthorizationHandler.cs`:
    - Validates if the user has the required permission.
    - Implements short-term caching (5-10m) for non-critical permissions.
    - Implements real-time revocation checking for "critical" permissions (process, refund, void).
    - Logs failures with structured detail (User ID, Permission, Reason).

### 3. IAM Registration Service
- Create `Maliev.PaymentService.Api/Services/PaymentIAMRegistrationService.cs`:
    - Scans defined permissions and roles.
    - Pushes/Synchronizes them to the IAM service on startup.
    - Overwrites existing metadata to ensure code remains the source of truth.

### 4. Middleware and Configuration
- Update `Program.cs`:
    - Register `PermissionAuthorizationHandler` and `PaymentIAMRegistrationService`.
    - Configure the authorization policy to use the custom requirement.
    - Remove `JwtAuthenticationMiddleware` (logic moved to authorization handler/standard flow).
- Update `Maliev.PaymentService.Api/Middleware/JwtAuthenticationMiddleware.cs`: Mark as deprecated or remove.

### 5. Controller Updates
- Update all Controllers (`PaymentsController`, `ProvidersController`, etc.):
    - Replace `[Authorize]` with specific `[RequirePermission(...)]` attributes on class or action level.

## Verification Plan

### Automated Tests
- **Unit Tests**:
    - Test `PermissionAuthorizationHandler` with various scenarios (authorized, unauthorized, critical revocation).
    - Test `PaymentIAMRegistrationService` mock synchronization.
- **Integration Tests**:
    - Update existing integration tests to use tokens with appropriate permissions.
    - Verify 403 Forbidden is returned for insufficient permissions.
    - Verify synchronization logic on app startup.

### Manual Verification
- Start the application and check logs for successful IAM synchronization.
- Test endpoints using tools like Postman/cURL with tokens containing different permission sets.
- Verify "critical" permission revocation is immediate (simulated via IAM mock/service).
