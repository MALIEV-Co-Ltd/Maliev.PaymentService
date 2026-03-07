# Maliev.PaymentService AGENTS.md

This document provides context and instructions for AI agents working on the `Maliev.PaymentService` repository.

## 1. Context & Tech Stack
- **Framework**: .NET 10 (C# 13)
- **Database**: PostgreSQL 18 (via Entity Framework Core 10)
- **Cache**: Redis 7
- **Messaging**: RabbitMQ (via MassTransit)
- **Architecture**: Clean Architecture / Hexagonal
  - `Maliev.PaymentService.Api`: Web API, Controllers, Consumers
  - `Maliev.PaymentService.Core`: Domain Entities, Interfaces, Enums
  - `Maliev.PaymentService.Infrastructure`: EF Core, Repositories, External Adapters
  - `Maliev.PaymentService.Tests`: xUnit Tests (Unit & Integration)

## 2. Build & Test Commands

### Build
The project uses standard .NET CLI commands. **Note**: Warnings are treated as errors.
```bash
dotnet build
```

### Testing
**Run All Tests:**
```bash
dotnet test
```

**Run a Single Test:**
Use the `--filter` option with the fully qualified name.
```bash
# Syntax: dotnet test --filter "FullyQualifiedName~{Namespace}.{Class}.{Method}"
dotnet test --filter "FullyQualifiedName~Maliev.PaymentService.Tests.Unit.Services.PaymentStatusServiceTests.GetPaymentStatus_ShouldReturnCorrectStatus"
```

**Run Integration Tests:**
Integration tests use **Testcontainers**. Ensure Docker is running.
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

## 3. Code Style & Guidelines

### Mandatory Rules
- **No Banned Libraries**:
  - ❌ **AutoMapper**: Use manual mapping (extension methods or static factories).
  - ❌ **FluentValidation**: Use standard Data Annotations (`[Required]`, `[EmailAddress]`).
  - ❌ **FluentAssertions**: Use standard xUnit `Assert` methods (`Assert.Equal`, `Assert.NotNull`).
  - ❌ **Moq/NSubstitute (excessive)**: Prefer real instances or simple stubs where possible, though Moq is present in dependencies.
- **Documentation**: All public methods and properties MUST have XML documentation (`/// <summary>`).
- **Async/Await**: All I/O operations (DB, Http, Message Bus) must be asynchronous.

### Naming Conventions
- **Classes/Methods**: `PascalCase`
- **Private Fields**: `_camelCase`
- **Variables/Parameters**: `camelCase`
- **Interfaces**: `IPascalCase`
- **Tests**: `MethodName_StateUnder_ExpectedBehavior` (e.g., `ProcessPayment_WithValidData_ReturnsSuccess`)

### Architecture Patterns
- **Dependency Injection**: Explicit constructor injection.
- **IAM Integration**: Permissions must follow `{service}.{resource}.{action}` format (e.g., `payment.transaction.read`).
- **Idempotency**: All state-changing operations must check/store idempotency keys (Redis).
- **Configuration**: NO secrets in code. Use `IConfiguration` / `IOptions<T>` patterns.

## 4. External Dependencies (Local Dev)
This project references sibling repositories (`Maliev.Aspire`, `Maliev.MessagingContracts`) in the parent directory.
- If build fails due to missing projects, ensure the folder structure is:
  ```text
  ../Maliev.Aspire
  ../Maliev.MessagingContracts
  ../Maliev.PaymentService (Current)
  ```

## 5. Common Tasks

### Database Migrations
Always create migrations in the Infrastructure project.
```bash
dotnet ef migrations add <MigrationName> --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Infrastructure
dotnet ef database update --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Infrastructure
```

### Adding a New Provider
1. Create `ProviderNameProvider.cs` in `Infrastructure/Providers`.
2. Implement `IPaymentProviderAdapter`.
3. Add configuration class in `Core/Entities` or `Infrastructure/Data/Configurations`.
4. Register in `Program.cs`.

## 6. Verification
Before declaring a task complete:
1. Run `dotnet build` (ensure no warnings).
2. Run relevant tests.
3. If modifying DB schema, ensure a migration script is generated.


## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
