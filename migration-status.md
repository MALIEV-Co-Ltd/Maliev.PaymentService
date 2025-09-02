# Maliev.PaymentService Migration Status

## Part 1: Triage and Mode Selection

**Mode:** Initial Migration

## Part 2: Mandatory Execution Plan

### Step 1: Plan and Dynamic Discovery

#### Inferred Project Structure and Dependencies:

Based on the provided directory listing and the `reference_project` blueprint, the `Maliev.PaymentService` solution is assumed to have the following projects and dependencies:

*   **Maliev.PaymentService.Api** (API project)
    *   Depends on: `Maliev.PaymentService.Data`, `Maliev.PaymentService.Common`
*   **Maliev.PaymentService.Common** (Common library)
    *   No external dependencies within the solution.
*   **Maliev.PaymentService.Data** (Data access layer)
    *   Depends on: `Maliev.PaymentService.Common`
*   **Maliev.PaymentService.Tests** (Unit/Integration tests)
    *   Depends on: `Maliev.PaymentService.Api`, `Maliev.PaymentService.Data`

#### Update `.gitignore`:

Confirmed that `migration_source/` and `reference_project/` are already present in the root `.gitignore`.

### Step 2: Create and Clean Project Skeletons

For each inferred project, a new .NET 9 project will be created, all boilerplate files will be deleted, and the project will be added to the solution.

*   Create `Maliev.PaymentService.Api` (ASP.NET Core Web API)
*   Create `Maliev.PaymentService.Common` (.NET Class Library)
*   Create `Maliev.PaymentService.Data` (.NET Class Library)
*   Create `Maliev.PaymentService.Tests` (xUnit Test Project)

### Step 3: Establish Project References

Based on the inferred dependency graph, the correct `<ProjectReference>` tags will be added to all new `.csproj` files.

### Step 4: Re-implement Supporting Libraries

Analyze the source of supporting libraries (e.g., `Maliev.PaymentService.Common`) and write new, modernized code that replicates their functionality.

### Step 5: Implement Core Functionality and Replicate `Program.cs`

#### 5.1 - Code Generation

*   **DbContext as Source of Truth**: Analyze the source `DbContext`'s `OnModelCreating` method (from `migration_source/Maliev.PaymentService.Data/Database/PaymentContext/PaymentContext.cs`) to determine `required` and nullable (`?`) properties for new entities.
*   **Generate Code**: Write new, modern code for:
    *   Entities (based on `PaymentContext.cs` and related entity files in `migration_source/Maliev.PaymentService.Data/Database/PaymentContext/`)
    *   DTOs (based on `migration_source/Maliev.PaymentService.Api/Models/`)
    *   `IPaymentServiceService` and `PaymentServiceService` (based on `migration_source/Maliev.PaymentService.Api/Services/`)
    *   "Thin" Controllers (based on `migration_source/Maliev.PaymentService.Api/Controllers/`)

#### 5.2 - Replicate `Program.cs` from the Reference Project

Replicate the `Program.cs` file from `reference_project/Maliev.JobService/Maliev.JobService.Api/Program.cs` (or `Maliev.CountryService`) adapting it with `PaymentService` specific names and types. This includes:

*   Service Registration Order (`builder.Services.Add...()`)
*   Authentication (`AddAuthentication()`, `AddJwtBearer()`)
*   API Versioning (`.AddApiExplorer()`)
*   Swagger Configuration (using `IApiVersionDescriptionProvider`)
*   CORS (named policy, specific origins)
*   Exception Handling (`if/else` for `IsDevelopment()`, `UseExceptionHandler`)
*   Middleware Pipeline Order (`UsePathBase`, `UseHttpsRedirection`, `UseCors`, `UseAuthentication`, `UseAuthorization`, etc.)
*   Health Checks (if present in reference)

### Step 6: Write Comprehensive Unit Tests

Write new tests for the implemented functionality.

#### 6.1 - Service Layer Tests

*   For each public method in `PaymentServiceService`, create unit tests in `Maliev.PaymentService.Tests`.
*   Use a mocking framework (e.g., Moq) to mock `DbContext`.
*   Cover success, failure, and edge cases.

#### 6.2 - Controller Tests

*   For each controller action, create unit tests in `Maliev.PaymentService.Tests`.
*   Mock `IPaymentServiceService`.
*   Verify correct `IActionResult` returns (e.g., `OkObjectResult`, `NotFoundResult`, `CreatedAtActionResult`).

### Step 7: Configure Local Secrets

Automate the setup of local development secrets by executing all required `dotnet user-secrets set` commands for `Jwt:Issuer`, `Jwt:Audience`, `JwtSecurityKey`, and `ConnectionStrings:PaymentServiceDbContext`.

### Step 8: Final Verification

1.  **Build the Solution**: Run `dotnet build` and resolve all build errors and warnings.
2.  **Run All Tests**: Run `dotnet test`. All new tests for both the service and controllers must pass.

### Step 9: API Standardization and Documentation

1.  Standardize all API routes.
2.  Generate `GEMINI.md` and update `README.md`.
3.  Present the user with the final `ACTION REQUIRED` block containing the `gcloud secrets` commands.