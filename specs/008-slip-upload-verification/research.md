# Research: Bank Transfer Slip Upload & LLM Verification

**Feature**: 008-slip-upload-verification
**Date**: 2026-02-22

## Research Topics

### 1. HTTP Client Pattern for External Services

**Question**: How should we implement HTTP clients for UploadService and ChatbotService?

**Decision**: Use `IHttpClientFactory` with typed clients, following the existing pattern in `Program.cs`.

**Rationale**:
- Consistent with existing codebase (PaymentProviders HttpClient)
- Built-in resilience via `AddStandardResilienceHandler()` from ServiceDefaults
- Proper lifetime management (typed clients are transient)
- Configuration via `IOptions<T>` pattern

**Alternatives Considered**:
- Refit: Rejected - adds unnecessary dependency, simple POST requests don't justify it
- Singleton HttpClient: Rejected - socket exhaustion risk, no DNS refresh
- Direct HttpClient per request: Rejected - no connection pooling

**Implementation**:
```csharp
// Program.cs
builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>(client =>
    client.BaseAddress = new Uri(configuration["UploadService:BaseUrl"]!))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IChatbotServiceClient, ChatbotServiceClient>(client =>
    client.BaseAddress = new Uri(configuration["ChatbotService:BaseUrl"]!))
    .AddStandardResilienceHandler();
```

---

### 2. LLM Verification Graceful Degradation

**Question**: How should we handle LLM service failures?

**Decision**: Catch all exceptions in `ChatbotServiceClient`, log warning, return fallback `SlipAnalysisResult` with `IsValid = false`.

**Rationale**:
- User experience: Upload succeeds, payment enters PendingVerification
- Operations: No alerts for transient LLM issues, manual review catches failures
- Spec requirement (FR-008): "gracefully handle LLM verification service failures"

**Alternatives Considered**:
- Rethrow with custom exception: Rejected - caller must handle, violates graceful degradation
- Return null: Rejected - requires null checks, less explicit
- Circuit breaker pattern: Rejected - over-engineering for MVP, can add later

**Implementation**:
```csharp
public async Task<SlipAnalysisResult> AnalyzeSlipAsync(string imageUrl, CancellationToken ct)
{
    try
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/chatbot/v1/vision/analyze-slip",
            new { imageUrl },
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SlipAnalysisResult>(ct)
            ?? new SlipAnalysisResult { IsValid = false, Notes = "Empty response from verification service." };
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        _logger.LogWarning(ex, "Slip verification service unavailable for image {ImageUrl}", imageUrl);
        return new SlipAnalysisResult { IsValid = false, Notes = "Verification service unavailable." };
    }
}
```

---

### 3. Slip Data Persistence Strategy

**Question**: Where should extracted slip data be stored?

**Decision**: Store directly on `PaymentTransaction` entity with nullable columns.

**Rationale**:
- Clarification Q3 confirmed: "Store extracted verification data with payment record"
- Simpler queries for manual review (single table lookup)
- Audit trail naturally preserved with payment history
- No separate migration complexity

**Alternatives Considered**:
- Separate `SlipVerification` table: Rejected - adds join complexity for common queries
- JSON column: Rejected - loses type safety, harder to query
- Audit log only: Rejected - manual review needs direct access

**Fields to Add**:
| Property | Type | Max Length | Nullable |
|----------|------|------------|----------|
| SlipUrl | string | 2000 | Yes |
| SlipExtractedAmount | decimal | (18,2) | Yes |
| SlipBankName | string | 100 | Yes |
| SlipTransferDate | string | 20 | Yes |
| SlipVerificationNotes | string | 500 | Yes |
| SlipVerifiedAt | DateTime | - | Yes |

---

### 4. Concurrent Upload Handling

**Question**: How should we handle concurrent slip uploads for the same payment?

**Decision**: Last-write-wins with no distributed locking.

**Rationale**:
- Clarification Q2 confirmed: "Last write wins - accept final upload result"
- Concurrent uploads for same payment are rare (same user double-clicking)
- Re-upload explicitly allowed per FR-013
- No customer impact - newer slip replaces older

**Alternatives Considered**:
- Optimistic concurrency (RowVersion): Rejected - would throw on conflict, bad UX
- Distributed lock (Redis): Rejected - over-engineering for rare edge case
- Reject second upload (409): Rejected - explicit clarification chose last-write-wins

---

### 5. File Upload Path Convention

**Question**: What path structure should be used for slip uploads?

**Decision**: `payment-slips/{paymentId}/{timestamp}_{originalFilename}`

**Rationale**:
- Organized by payment ID for easy cleanup/migration
- Timestamp prevents filename collisions
- Original filename preserved for audit/debugging
- Consistent with spec suggestion

**Implementation**:
```csharp
var path = $"payment-slips/{paymentId}/{DateTime.UtcNow:yyyyMMddHHmmss}_{sanitizedFilename}";
```

---

### 6. ChatbotService Authorization

**Question**: How should the VisionController endpoint be secured?

**Decision**: Use internal service-to-service authentication via network isolation or shared JWT validation.

**Rationale**:
- ChatbotService is internal (not user-facing)
- Service mesh / Kubernetes network policies can restrict access
- If JWT required, use service account tokens

**Alternatives Considered**:
- `[AllowAnonymous]`: Acceptable if network-isolated
- API Key header: Simple but adds secret management
- mTLS: Over-engineering for current scale

---

## Resolved Clarifications

All NEEDS CLARIFICATION items from technical context have been resolved:

1. HTTP client pattern → Typed clients with IHttpClientFactory
2. LLM failure handling → Graceful degradation, never throw
3. Slip data storage → Direct on PaymentTransaction entity
4. Concurrency → Last-write-wins
5. Upload path → payment-slips/{paymentId}/{timestamp}_{filename}
6. Service auth → Network isolation or shared JWT validation

## Dependencies Identified

| Dependency | Status | Notes |
|------------|--------|-------|
| ChatbotService vision endpoint | BLOCKING | Must exist before PaymentService integration |
| UploadService existing API | READY | Assumed existing per spec assumptions |
| PaymentCompletedEvent | READY | Already exists in Maliev.MessagingContracts |
| IAM permissions | READY | payments.slip.upload permission to be registered |
