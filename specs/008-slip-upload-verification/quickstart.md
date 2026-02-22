# Quick Start: Bank Transfer Slip Upload

**Feature**: 008-slip-upload-verification
**Estimated Time**: 2-3 hours

## Prerequisites

- .NET 10 SDK
- Docker (for Testcontainers)
- PostgreSQL 18 (local or via Docker)
- Redis 7 (local or via Docker)
- RabbitMQ (local or via Docker)
- Access to `Maliev.ChatbotService` repository

## Step 1: Verify Prerequisites

```bash
# Check .NET version
dotnet --version  # Should be 10.x.x

# Check Docker
docker ps

# Build existing project
dotnet build
```

## Step 2: ChatbotService Vision Endpoint (Blocking)

**If ChatbotService endpoint doesn't exist**, implement it first:

```bash
cd ../Maliev.ChatbotService
```

Create `Maliev.ChatbotService.Api/Controllers/V1/VisionController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

[ApiController]
[Route("chatbot/v1/vision")]
public class VisionController : ControllerBase
{
    private readonly IAnthropicClient _anthropicClient;
    private readonly ILogger<VisionController> _logger;

    public VisionController(IAnthropicClient anthropicClient, ILogger<VisionController> logger)
    {
        _anthropicClient = anthropicClient;
        _logger = logger;
    }

    [HttpPost("analyze-slip")]
    public async Task<ActionResult<SlipAnalysisResult>> AnalyzeSlip(
        [FromBody] AnalyzeSlipRequest request,
        CancellationToken cancellationToken)
    {
        // Call Anthropic Claude with vision capabilities
        // Return SlipAnalysisResult
    }
}
```

Build and verify:
```bash
dotnet build
```

## Step 3: PaymentService Changes

```bash
cd ../Maliev.PaymentService
```

### 3.1 Update PaymentStatus Enum

Edit `Maliev.PaymentService.Core/Enums/PaymentStatus.cs`:

```csharp
/// <summary>
/// A bank transfer slip has been uploaded; awaiting LLM or manual verification.
/// </summary>
PendingVerification = 6
```

### 3.2 Update PaymentTransaction Entity

Edit `Maliev.PaymentService.Core/Entities/PaymentTransaction.cs`:

```csharp
/// <summary>
/// GCS URL of the uploaded bank transfer slip image.
/// </summary>
public string? SlipUrl { get; set; }

/// <summary>
/// Amount extracted from slip by LLM verification.
/// </summary>
public decimal? SlipExtractedAmount { get; set; }

/// <summary>
/// Bank name extracted from slip.
/// </summary>
public string? SlipBankName { get; set; }

/// <summary>
/// Transfer date extracted from slip (ISO 8601).
/// </summary>
public string? SlipTransferDate { get; set; }

/// <summary>
/// Notes from LLM verification.
/// </summary>
public string? SlipVerificationNotes { get; set; }

/// <summary>
/// When slip verification was performed.
/// </summary>
public DateTime? SlipVerifiedAt { get; set; }
```

### 3.3 Update EF Core Configuration

Edit `Maliev.PaymentService.Infrastructure/Data/Configurations/PaymentTransactionConfiguration.cs`:

```csharp
builder.Property(p => p.SlipUrl)
    .HasColumnName("slip_url")
    .HasMaxLength(2000);

builder.Property(p => p.SlipExtractedAmount)
    .HasColumnName("slip_extracted_amount")
    .HasPrecision(18, 2);

builder.Property(p => p.SlipBankName)
    .HasColumnName("slip_bank_name")
    .HasMaxLength(100);

builder.Property(p => p.SlipTransferDate)
    .HasColumnName("slip_transfer_date")
    .HasMaxLength(20);

builder.Property(p => p.SlipVerificationNotes)
    .HasColumnName("slip_verification_notes")
    .HasMaxLength(500);

builder.Property(p => p.SlipVerifiedAt)
    .HasColumnName("slip_verified_at");
```

### 3.4 Generate Migration

```bash
dotnet ef migrations add AddSlipUrlToPaymentTransaction \
    --project Maliev.PaymentService.Infrastructure \
    --startup-project Maliev.PaymentService.Api
```

### 3.5 Create HTTP Clients

Create directory and files:
```bash
mkdir -p Maliev.PaymentService.Api/Clients
```

Create `IUploadServiceClient.cs`, `UploadServiceClient.cs`, `IChatbotServiceClient.cs`, `ChatbotServiceClient.cs` per spec.

### 3.6 Create DTOs

Create `Maliev.PaymentService.Api/Models/Responses/SlipAnalysisResult.cs` and `SlipUploadResponse.cs` per spec.

### 3.7 Register Clients in Program.cs

Add after existing client registrations:

```csharp
builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["UploadService:BaseUrl"]!))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IChatbotServiceClient, ChatbotServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ChatbotService:BaseUrl"]!))
    .AddStandardResilienceHandler();
```

### 3.8 Add Controller Action

Edit `Maliev.PaymentService.Api/Controllers/PaymentsController.cs`:
- Add `IUploadServiceClient` and `IChatbotServiceClient` to constructor
- Add `UploadSlip` action per spec

## Step 4: Configuration

Add to `appsettings.Development.json`:

```json
{
  "UploadService": {
    "BaseUrl": "http://localhost:5003"
  },
  "ChatbotService": {
    "BaseUrl": "http://localhost:5004"
  }
}
```

## Step 5: Build & Test

```bash
# Build - must have zero warnings
dotnet build

# Run unit tests
dotnet test

# Run integration tests (requires Docker)
dotnet test --filter "FullyQualifiedName~Integration"
```

## Step 6: Verify

1. Check no `PaymentStatus` switch statements broken:
   ```bash
   rg "PaymentStatus\." --type cs
   ```

2. Verify migration generated correctly:
   ```bash
   cat Maliev.PaymentService.Infrastructure/Migrations/*_AddSlipUrlToPaymentTransaction.cs
   ```

3. Run all tests:
   ```bash
   dotnet test
   ```

## Test Cases to Implement

Create `Maliev.PaymentService.Tests/Unit/Controllers/SlipUploadTests.cs`:

1. **Happy path — auto-verified**: Valid slip, amount matches → Completed
2. **Happy path — pending review**: Invalid slip → PendingVerification
3. **ChatbotService unavailable**: Exception → PendingVerification, 200 OK
4. **Payment not found**: Non-existent ID → 404
5. **Wrong status**: Completed payment → 409 Conflict
6. **Upload service failure**: Exception → 502 Bad Gateway

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Build warnings | Fix immediately - warnings are errors |
| Switch expression errors | Add `PendingVerification` case |
| Test failures | Check mock setup for new clients |
| Migration errors | Verify column names match configuration |

## Next Steps

After implementation:
1. Run `dotnet build` - verify zero warnings
2. Run `dotnet test` - all tests pass
3. Create PR with implementation checklist completed
