# Maliev.PaymentService AGENTS.md

This document provides context and instructions for AI agents working on the `Maliev.PaymentService` repository.

## 1. Context & Tech Stack
- **Framework**: .NET 10 (C# 13)
- **Database**: PostgreSQL 18 (via Entity Framework Core 10)
- **Cache**: Redis 7
- **Messaging**: RabbitMQ (via MassTransit)
- **Architecture**: Clean Architecture / Hexagonal
  - `Maliev.PaymentService.Api`: Web API, Controllers, Consumers, Middleware
  - `Maliev.PaymentService.Core`: Domain Entities, Interfaces, Enums, DTOs, Handlers
  - `Maliev.PaymentService.Infrastructure`: EF Core DbContext, Repositories, External Adapters, HTTP Clients
  - `Maliev.PaymentService.Tests`: xUnit Tests (Unit & Integration)
  - `Directory.Build.props`: Central package versioning
  - `Maliev.PaymentService.slnx`: Solution file (.slnx preferred over .sln)

## 2. Build, Test & Lint Commands

All commands run from within `B:\maliev\Maliev.PaymentService`.

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.PaymentService.slnx

# Run all tests
dotnet test Maliev.PaymentService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~PaymentStatusServiceTests.GetPaymentStatus_ShouldReturnCorrectStatus"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~PaymentStatusServiceTests"

# Run with code coverage
dotnet test Maliev.PaymentService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.PaymentService.slnx

# EF Core migrations (Infrastructure project only)
dotnet ef migrations add <Name> --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Infrastructure
```

### Integration Tests

Integration tests use **Testcontainers** (PostgreSQL, Redis, RabbitMQ). Ensure Docker is running.

```powershell
dotnet test --filter "FullyQualifiedName~Integration"
```

## 3. Code Style & Conventions

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.PaymentService.Core.Entities;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `ProcessPaymentAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IPaymentProviderAdapter`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `payment.transactions.create`, `payment.payments.read`
  - Invalid: `payment.transaction.create` (singular), `payment.read` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("payment/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {PaymentId}", paymentId)`
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned

### PaymentService-Specific Patterns
- **Idempotency**: All state-changing operations must check/store idempotency keys (Redis)
- **Configuration**: NO secrets in code. Use `IConfiguration` / `IOptions<T>` patterns
- **IAM Integration**: Permissions follow `{domain}.{plural-resource}.{action}` format (e.g., `payment.transactions.read`)
- **Webhook signatures**: Provider webhooks fail closed when signing material is missing. PayPal must verify `PAYPAL-TRANSMISSION-SIG` cryptographically with configured `WebhookCertificatePem`, `WebhookCertificate`, or `WebhookPublicKeyPem`; never accept headers by presence only.
- **Test/simulation endpoints**: Non-production-only checks must be paired with `[RequirePermission]` when an endpoint publishes events or mutates payment state. The manual `PaymentCompletedEvent` publisher requires `payment.payments.process`.

## 4. Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/payment/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

## 5. Testing Rules

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class. Tested in `Maliev.Aspire.Tests/`
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

This service covers **Tier 1 (Unit)** and **Tier 2 (Service Integration)**:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

## 6. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("domain.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (e.g., `/payment`)
- **Scalar docs**: Configured at `/payment/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

## 7. External Dependencies (Local Dev)

This project references sibling repositories (`Maliev.Aspire`, `Maliev.MessagingContracts`) in the parent directory.
- If build fails due to missing projects, ensure the folder structure is:
  ```text
  ../Maliev.Aspire
  ../Maliev.MessagingContracts
  ../Maliev.PaymentService (Current)
  ```

## 8. Common Tasks

### Database Migrations
Always create migrations in the Infrastructure project.
```powershell
dotnet ef migrations add <MigrationName> --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Infrastructure
dotnet ef database update --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Infrastructure
```

### Adding a New Provider
1. Create `ProviderNameProvider.cs` in `Infrastructure/Providers`.
2. Implement `IPaymentProviderAdapter`.
3. Add configuration class in `Core/Entities` or `Infrastructure/Data/Configurations`.
4. Register in `Program.cs`.

## 9. Git Rules

- Each `Maliev.*` folder is an independent git repo. `cd` into it before git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked

## 10. Database & EF Core — Mandatory Rules

### EF Core Design Package
- `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```powershell
  dotnet ef migrations add <Name> --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
