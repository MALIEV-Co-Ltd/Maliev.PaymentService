# Research: Payment Gateway Service

**Feature**: Payment Gateway Service | **Date**: 2025-11-18

## Overview

This document contains research findings for implementing a payment gateway service that integrates with multiple payment providers (Stripe, PayPal, Omise, SCB API) using .NET 10, Entity Framework Core 9.0, and resilience patterns with Polly 8.5.0.

## 1. Payment Provider Integration Patterns

### 1.1 Provider Abstraction Strategy

**Adapter Pattern Implementation**:
- Define `IPaymentProviderAdapter` interface with common payment operations
- Implement provider-specific adapters (StripeProvider, PayPalProvider, etc.)
- Use factory pattern (`ProviderFactory`) to instantiate correct provider based on configuration
- Normalize provider responses to internal domain models

**Key Interface Methods**:
```csharp
Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken ct);
Task<PaymentStatus> GetPaymentStatusAsync(string providerTransactionId, CancellationToken ct);
Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken ct);
bool ValidateWebhookSignature(string payload, string signature);
```

**Provider Selection Logic**:
- Currency-based routing (primary strategy)
- Provider health status consideration
- Priority-based fallback for multi-provider currencies
- Circuit breaker state awareness

### 1.2 Provider-Specific Research

#### Stripe Integration
- **SDK**: Stripe.net NuGet package (latest stable)
- **Authentication**: Secret key in headers
- **Webhook Signature**: HMAC SHA-256 with signing secret
- **Idempotency**: `Idempotency-Key` header support
- **Rate Limits**: 100 requests/second per API key
- **Supported Currencies**: 135+ currencies
- **Timeout Recommendations**: 30-80 seconds
- **Webhook Events**: `payment_intent.succeeded`, `payment_intent.failed`, `charge.refunded`

#### PayPal Integration
- **SDK**: PayPal REST SDK or direct HTTP API
- **Authentication**: OAuth 2.0 bearer tokens (expires in 9 hours)
- **Webhook Signature**: CERT-URL and TRANSMISSION-SIG headers validation
- **Rate Limits**: 500 requests/minute for REST API
- **Supported Currencies**: 25+ major currencies
- **Order Flow**: Create Order -> Capture Order (two-step process)
- **Webhook Events**: `PAYMENT.CAPTURE.COMPLETED`, `PAYMENT.CAPTURE.DENIED`, `PAYMENT.CAPTURE.REFUNDED`

#### Omise Integration
- **SDK**: Omise.Net or direct HTTP API
- **Authentication**: Basic auth with secret key
- **Webhook Signature**: Not provided - use IP whitelist validation
- **Rate Limits**: Not publicly documented (monitor 429 responses)
- **Supported Currencies**: THB, JPY, SGD, USD, GBP, EUR, AUD, CAD
- **3D Secure Support**: Required for certain card types
- **Webhook Events**: `charge.complete`, `charge.failed`, `refund.created`

#### SCB API (Siam Commercial Bank) Integration
- **Authentication**: OAuth 2.0 with client credentials grant
- **Webhook Signature**: HMAC signature verification
- **Rate Limits**: Tiered based on merchant agreement
- **Supported Currencies**: THB primarily, USD for specific merchants
- **QR Code Support**: Thai QR Payment (PromptPay)
- **Webhook Events**: Transaction status updates, settlement notifications
- **Compliance**: Requires Thai regulatory compliance documentation

### 1.3 Common Integration Challenges

**Challenge 1: Authentication Token Management**
- **Solution**: Implement token caching with Redis
- **Refresh Strategy**: Proactive refresh 5 minutes before expiry
- **Fallback**: Retry with new token on 401 responses

**Challenge 2: Webhook Replay Attacks**
- **Solution**: Store webhook event IDs with timestamp in database
- **Deduplication Window**: 24 hours
- **Cleanup**: Automated job to remove old webhook records

**Challenge 3: Provider Timezone Differences**
- **Solution**: Always store UTC timestamps internally
- **Conversion**: Convert provider timestamps to UTC on ingestion
- **Display**: Convert to user timezone only for presentation

## 2. Circuit Breaker and Retry Patterns with Polly v8

### 2.1 Polly v8 Migration Considerations

**Breaking Changes from v7**:
- New resilience pipeline API (replaces Policy)
- Telemetry integration built-in
- Improved DI integration with `AddResiliencePipeline`
- Cancellation token propagation improved

### 2.2 Retry Strategy

**Configuration**:
```csharp
var retryOptions = new RetryStrategyOptions
{
    MaxRetryAttempts = 3,
    BackoffType = DelayBackoffType.Exponential,
    Delay = TimeSpan.FromSeconds(2),
    UseJitter = true,
    ShouldHandle = new PredicateBuilder()
        .Handle<HttpRequestException>()
        .Handle<TimeoutException>()
        .HandleResult<HttpResponseMessage>(r =>
            r.StatusCode == HttpStatusCode.RequestTimeout ||
            r.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)r.StatusCode >= 500)
};
```

**Retry Delays**:
- Attempt 1: 2 seconds (+ jitter)
- Attempt 2: 4 seconds (+ jitter)
- Attempt 3: 8 seconds (+ jitter)

**When NOT to Retry**:
- 400 Bad Request (client error)
- 401 Unauthorized (authentication issue)
- 403 Forbidden (authorization issue)
- 404 Not Found (resource doesn't exist)

### 2.3 Circuit Breaker Strategy

**Configuration**:
```csharp
var circuitBreakerOptions = new CircuitBreakerStrategyOptions
{
    FailureRatio = 0.5,           // 50% failure rate
    MinimumThroughput = 10,       // Minimum 10 requests in sampling duration
    SamplingDuration = TimeSpan.FromSeconds(30),
    BreakDuration = TimeSpan.FromSeconds(60),
    ShouldHandle = new PredicateBuilder()
        .Handle<HttpRequestException>()
        .Handle<TimeoutException>()
        .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
};
```

**States**:
- **Closed**: Normal operation, tracking failures
- **Open**: Circuit tripped, fail fast for break duration (60s)
- **Half-Open**: Testing if service recovered (1 test request)

**Automatic Failover**:
- Monitor circuit breaker state in `ProviderHealthService`
- If circuit opens, mark provider as degraded
- Route new requests to backup provider if available
- Attempt recovery when circuit transitions to half-open

### 2.4 Timeout Strategy

**Configuration**:
```csharp
var timeoutOptions = new TimeoutStrategyOptions
{
    Timeout = TimeSpan.FromSeconds(30),
    OnTimeout = args =>
    {
        _logger.LogWarning(
            "Payment provider request timeout after {Timeout}s for {Provider}",
            args.Timeout.TotalSeconds,
            args.Context.Properties["Provider"]);
        return ValueTask.CompletedTask;
    }
};
```

**Timeout Hierarchy**:
- HttpClient timeout: 35 seconds (outer boundary)
- Polly timeout: 30 seconds (per specification)
- Total max time with 3 retries: ~90 seconds

### 2.5 Combined Resilience Pipeline

**Recommended Order** (outer to inner):
1. Timeout (30s per request)
2. Retry (3 attempts with exponential backoff)
3. Circuit Breaker (protect provider)
4. Rate Limiter (respect provider limits)

```csharp
services.AddResiliencePipeline("payment-provider", builder =>
{
    builder
        .AddTimeout(timeoutOptions)
        .AddRetry(retryOptions)
        .AddCircuitBreaker(circuitBreakerOptions)
        .AddConcurrencyLimiter(concurrencyOptions);
});
```

## 3. Webhook Signature Validation Approaches

### 3.1 HMAC Signature Validation (Stripe, SCB)

**Algorithm**: HMAC-SHA256

**Implementation**:
```csharp
public bool ValidateHmacSignature(string payload, string signature, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    var computedSignature = Convert.ToBase64String(computedHash);
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(computedSignature),
        Encoding.UTF8.GetBytes(signature)
    );
}
```

**Stripe-Specific**:
- Signature format: `t=timestamp,v1=signature`
- Verify timestamp within 5 minutes tolerance (replay protection)
- Construct payload: `{timestamp}.{request_body}`

### 3.2 Certificate Verification (PayPal)

**Algorithm**: RSA signature with CERT-URL validation

**Implementation Steps**:
1. Extract CERT-URL from headers
2. Download certificate from PayPal (cache for 24 hours)
3. Verify certificate chain against PayPal root CA
4. Extract public key from certificate
5. Verify signature using public key

**Security Considerations**:
- Only allow CERT-URLs from `*.paypal.com` domain
- Implement certificate caching to avoid repeated downloads
- Set certificate cache TTL to 24 hours

### 3.3 IP Whitelist Validation (Omise)

**Fallback Strategy** (when signature not available):
```csharp
public bool ValidateIpWhitelist(IPAddress clientIp, string[] allowedIpRanges)
{
    foreach (var range in allowedIpRanges)
    {
        if (IPAddressRange.Parse(range).Contains(clientIp))
            return true;
    }
    return false;
}
```

**Omise IP Ranges** (example, verify with provider):
- `52.77.86.232/32`
- `54.255.188.165/32`

**Limitations**:
- Less secure than cryptographic signatures
- Requires updating configuration when IPs change
- Combine with timestamp validation and event deduplication

### 3.4 Webhook Validation Middleware

**Recommended Approach**:
- Create middleware that validates before MVC routing
- Fail fast on invalid signatures (return 401)
- Log all validation failures for security monitoring
- Include provider-specific validation in `WebhooksController`

## 4. Idempotency Key Implementation Strategies

### 4.1 Redis-Based Idempotency

**Key Structure**: `idempotency:{key}:{operation}`

**Implementation**:
```csharp
public async Task<PaymentResult> ProcessPaymentIdempotentlyAsync(
    string idempotencyKey,
    PaymentRequest request,
    CancellationToken ct)
{
    var cacheKey = $"idempotency:{idempotencyKey}:payment";

    // Check if already processed
    var cachedResult = await _redis.GetAsync<PaymentResult>(cacheKey, ct);
    if (cachedResult != null)
    {
        _logger.LogInformation("Duplicate request detected: {Key}", idempotencyKey);
        return cachedResult;
    }

    // Acquire distributed lock
    await using var lockHandle = await _redis.AcquireLockAsync(
        $"lock:{cacheKey}",
        TimeSpan.FromSeconds(30),
        ct);

    if (lockHandle == null)
    {
        throw new ConcurrentRequestException("Another request with same idempotency key is processing");
    }

    // Process payment
    var result = await _paymentService.ProcessPaymentAsync(request, ct);

    // Cache result for 24 hours
    await _redis.SetAsync(cacheKey, result, TimeSpan.FromHours(24), ct);

    return result;
}
```

**TTL Strategy**:
- Standard: 24 hours
- Failed payments: 1 hour (allow retry sooner)
- Completed payments: 24 hours (prevent immediate duplicates)

### 4.2 Database-Based Deduplication

**Alternative Approach** (when Redis unavailable):
- Add unique index on `idempotency_key` column
- Catch unique constraint violations
- Return existing transaction on duplicate key

**Trade-offs**:
- Slower than Redis (database round-trip)
- No automatic expiry (requires cleanup job)
- Better for compliance/audit requirements

### 4.3 Idempotency Key Format

**Recommended**: UUID v4 or client-generated

**Validation**:
- Minimum length: 16 characters
- Maximum length: 255 characters
- Allowed characters: alphanumeric, dash, underscore

**Client Responsibilities**:
- Generate unique key per logical operation
- Reuse same key for retries of same operation
- Include in `Idempotency-Key` request header

## 5. Provider Failover and Routing Logic

### 5.1 Routing Decision Tree

**Primary Factors** (in order of priority):
1. Currency support (MUST match)
2. Provider operational status (active/degraded/disabled)
3. Circuit breaker state (closed/half-open/open)
4. Provider priority/preference
5. Rate limit headroom
6. Historical success rate (last 15 minutes)

**Implementation**:
```csharp
public async Task<IPaymentProviderAdapter> SelectProviderAsync(
    string currency,
    CancellationToken ct)
{
    var eligibleProviders = await _providerRepository
        .GetActiveByCurrencyAsync(currency, ct);

    if (!eligibleProviders.Any())
        throw new NoProviderAvailableException($"No provider supports {currency}");

    // Filter by circuit breaker state
    var availableProviders = eligibleProviders
        .Where(p => !_circuitBreakerManager.IsOpen(p.Id))
        .OrderBy(p => p.Priority)
        .ThenByDescending(p => p.SuccessRate)
        .ToList();

    if (!availableProviders.Any())
    {
        _logger.LogError("All providers for {Currency} are unavailable", currency);
        throw new AllProvidersDownException();
    }

    var selectedProvider = availableProviders.First();
    return _providerFactory.Create(selectedProvider);
}
```

### 5.2 Automatic Failover Strategy

**Trigger Conditions**:
- Circuit breaker opens (50% failure rate over 30s)
- Provider explicitly disabled by admin
- Rate limit exceeded (429 responses)
- Timeout threshold exceeded (3 consecutive timeouts)

**Failover Process**:
1. Log provider degradation event
2. Publish `ProviderDegradedEvent` to message queue
3. Select backup provider using routing logic
4. Retry payment operation with backup provider
5. Update metrics to track failover events

**Recovery Detection**:
- Circuit breaker transitions to half-open
- Test request succeeds
- Publish `ProviderRecoveredEvent`
- Restore provider to normal priority

### 5.3 Multi-Provider Currency Handling

**Example**: USD supported by Stripe, PayPal, Omise

**Configuration**:
```json
{
  "providers": [
    {
      "name": "Stripe",
      "priority": 1,
      "supportedCurrencies": ["USD", "EUR", "GBP", "THB", ...]
    },
    {
      "name": "PayPal",
      "priority": 2,
      "supportedCurrencies": ["USD", "EUR", "GBP", ...]
    },
    {
      "name": "Omise",
      "priority": 3,
      "supportedCurrencies": ["THB", "USD", "SGD", ...]
    }
  ]
}
```

**Sticky Provider Strategy** (optional enhancement):
- Track customer's previously used provider
- Prefer same provider for same customer (better UX)
- Override if provider unavailable

## 6. Transaction Reconciliation Patterns

### 6.1 Daily Reconciliation Process

**Scheduled Job**: Daily at 2:00 AM UTC

**Algorithm**:
1. Query all transactions from previous day (UTC)
2. For each provider, fetch settlement report
3. Match internal transactions with provider records
4. Identify discrepancies:
   - Transactions in gateway but not in provider report
   - Transactions in provider report but not in gateway
   - Amount mismatches
   - Status mismatches

**Implementation**:
```csharp
public async Task ReconcileDailyTransactionsAsync(DateTime date, CancellationToken ct)
{
    var startOfDay = date.Date;
    var endOfDay = startOfDay.AddDays(1);

    var internalTransactions = await _paymentRepository
        .GetByDateRangeAsync(startOfDay, endOfDay, ct);

    var discrepancies = new List<ReconciliationDiscrepancy>();

    foreach (var provider in await _providerRepository.GetAllActiveAsync(ct))
    {
        var providerReport = await provider.GetSettlementReportAsync(date, ct);

        var discrepancy = await CompareRecordsAsync(
            internalTransactions.Where(t => t.ProviderId == provider.Id),
            providerReport,
            ct);

        discrepancies.AddRange(discrepancy);
    }

    if (discrepancies.Any())
    {
        await _alertService.SendReconciliationAlertAsync(discrepancies, ct);
        await _reconciliationRepository.SaveDiscrepanciesAsync(discrepancies, ct);
    }
}
```

### 6.2 Discrepancy Handling

**Severity Levels**:
- **Critical**: Amount mismatch, missing completed transaction
- **High**: Status mismatch (completed vs failed)
- **Medium**: Missing pending transaction
- **Low**: Metadata differences

**Resolution Workflow**:
1. Log discrepancy with severity
2. Send alert to operations team (critical/high only)
3. Create reconciliation ticket in tracking system
4. Manual investigation required for critical discrepancies
5. Automated retry for certain low-severity cases

### 6.3 Real-Time Reconciliation

**Trigger**: On webhook receipt

**Process**:
1. Webhook updates transaction status
2. Compare webhook data with internal record
3. If mismatch detected:
   - Query provider API for authoritative status
   - Update internal record
   - Log reconciliation event

**Benefits**:
- Faster discrepancy detection
- Reduced window for inconsistency
- Better customer experience (accurate status)

## 7. Message Queue Event Publishing with MassTransit

### 7.1 MassTransit 8.3 Configuration

**RabbitMQ Integration**:
```csharp
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.Message<PaymentCompletedEvent>(e =>
        {
            e.SetEntityName("payment.completed");
        });

        cfg.Publish<PaymentCompletedEvent>(p =>
        {
            p.Durable = true;
            p.AutoDelete = false;
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

### 7.2 Event Schema Design

**Base Event Interface**:
```csharp
public interface IPaymentEvent
{
    Guid EventId { get; }
    Guid TransactionId { get; }
    DateTime OccurredAt { get; }
    string ServiceName { get; }
    string CorrelationId { get; }
}
```

**Event Types**:
1. `PaymentCreatedEvent` - Payment initiated
2. `PaymentCompletedEvent` - Payment successful
3. `PaymentFailedEvent` - Payment failed
4. `RefundInitiatedEvent` - Refund started
5. `RefundCompletedEvent` - Refund successful
6. `ProviderDegradedEvent` - Provider health degraded
7. `ProviderRecoveredEvent` - Provider health restored

### 7.3 Publishing Strategy

**Transactional Outbox Pattern**:
- Store events in database within same transaction as entity changes
- Background service publishes events from outbox table
- Mark events as published after successful delivery
- Ensures exactly-once delivery semantics

**Implementation**:
```csharp
public async Task PublishPaymentCompletedAsync(PaymentTransaction transaction, CancellationToken ct)
{
    var evt = new PaymentCompletedEvent
    {
        EventId = Guid.NewGuid(),
        TransactionId = transaction.Id,
        Amount = transaction.Amount,
        Currency = transaction.Currency,
        CustomerId = transaction.CustomerId,
        ProviderId = transaction.ProviderId,
        ProviderTransactionId = transaction.ProviderTransactionId,
        CompletedAt = transaction.CompletedAt.Value,
        OccurredAt = DateTime.UtcNow,
        ServiceName = "PaymentGatewayService",
        CorrelationId = transaction.CorrelationId
    };

    // Save to outbox
    await _outboxRepository.AddEventAsync(evt, ct);

    // Publish via MassTransit (background service)
    await _publishEndpoint.Publish(evt, ct);
}
```

### 7.4 Message Durability and Reliability

**RabbitMQ Configuration**:
- Durable queues and exchanges
- Persistent messages
- Publisher confirms enabled
- Consumer acknowledgments manual

**Retry Policy**:
- Exponential backoff: 1s, 5s, 30s, 2m, 10m
- Maximum 5 retries
- Dead letter queue for failed messages

**Monitoring**:
- Track publish success/failure rates
- Monitor queue depths
- Alert on growing dead letter queue

## 8. Data Retention and Archival Strategies

### 8.1 Retention Requirements

**Active Storage** (PostgreSQL - 1 year):
- All transaction records
- Provider configurations
- Webhook events (last 30 days only)
- Transaction logs
- Full query performance

**Archived Storage** (Cold storage - 3 years):
- Completed transactions older than 1 year
- Associated logs and audit trails
- Compressed and encrypted
- Slower access acceptable

**Permanent Deletion** (after 4 years):
- All transaction data
- Associated logs
- Comply with GDPR right to erasure
- Retain summary statistics only

### 8.2 Archival Process

**Monthly Job** (1st of each month):
1. Select records older than 1 year
2. Export to compressed JSON/Parquet files
3. Upload to cloud object storage (S3/Azure Blob)
4. Encrypt files at rest
5. Delete from PostgreSQL
6. Update archival index

**File Format**:
```
archives/
├── 2024/
│   ├── 01/
│   │   ├── transactions_2024-01.parquet.gz.enc
│   │   ├── logs_2024-01.parquet.gz.enc
│   │   └── manifest.json
│   ├── 02/
│   └── ...
└── 2025/
```

**Restoration Process**:
- Rarely needed
- Manual process triggered by compliance request
- Download encrypted archive
- Decrypt and load into temporary database
- Provide read-only access
- Delete temporary data after request fulfilled

### 8.3 Webhook Event Cleanup

**Strategy**: 30-day rolling window

**Justification**:
- Webhooks are idempotent - duplicates handled
- Transaction status is source of truth
- Debugging typically needs recent data only
- Reduces database size significantly

**Implementation**:
```csharp
public async Task CleanupOldWebhooksAsync(CancellationToken ct)
{
    var cutoffDate = DateTime.UtcNow.AddDays(-30);
    var deletedCount = await _webhookRepository.DeleteOlderThanAsync(cutoffDate, ct);

    _logger.LogInformation(
        "Deleted {Count} webhook events older than {CutoffDate}",
        deletedCount,
        cutoffDate);

    _metrics.RecordWebhookCleanup(deletedCount);
}
```

### 8.4 Compliance Considerations

**GDPR Requirements**:
- Right to erasure: Provide mechanism to delete customer data
- Data minimization: Only retain what's necessary
- Purpose limitation: Archive only for compliance, not analytics

**Thai Regulations**:
- Tax records: Minimum 5 years (exceeds our 4-year policy)
- Recommendation: Extend to 5 years or consult legal team

**Audit Trail**:
- Log all archival operations
- Track who accessed archived data
- Maintain deletion certificates

## 9. Key Research Findings Requiring Attention

### 9.1 Critical Decisions Needed

**Decision 1: Provider SDK vs Direct HTTP**
- **Recommendation**: Use official SDKs for Stripe and PayPal (well-maintained, typed)
- **Concern**: Omise and SCB may need direct HTTP implementation
- **Action**: Evaluate SDK quality before implementation

**Decision 2: Synchronous vs Asynchronous Payment Flow**
- **Recommendation**: Hybrid approach
  - Return immediately with "pending" status
  - Update via webhook or polling
  - Support synchronous wait with timeout (30s max)
- **Concern**: Client expectation management
- **Action**: Document expected behavior in API contracts

**Decision 3: Data Retention Period**
- **Specification**: 1 year active + 3 years archived = 4 years total
- **Thai Regulation**: Minimum 5 years for tax records
- **Recommendation**: Extend total retention to 5 years (1 active + 4 archived)
- **Action**: Confirm with legal/compliance team before finalizing

### 9.2 Performance Optimization Opportunities

**Opportunity 1: Connection Pooling**
- Configure HttpClient with connection pooling
- Reuse connections to providers
- Monitor connection statistics

**Opportunity 2: Parallel Provider Queries**
- When checking multiple providers for status
- Use `Task.WhenAll` for concurrent requests
- Respect rate limits

**Opportunity 3: Database Indexing**
- Index on `(currency, status, created_at)` for routing queries
- Index on `idempotency_key` for fast lookup
- Partial index on active transactions only

### 9.3 Security Hardening

**Hardening 1: Credential Rotation**
- Implement automated credential rotation every 90 days
- Zero-downtime rotation using dual-key approach
- Monitor for failed auth after rotation

**Hardening 2: Rate Limiting**
- Implement API rate limiting (per client token)
- Prevent abuse and DDoS
- Use Redis for distributed rate limit tracking

**Hardening 3: Input Validation**
- Strict validation on all incoming requests
- FluentValidation library for complex rules
- Reject malformed requests early

### 9.4 Testing Strategy Considerations

**Integration Testing with Providers**:
- Use provider sandbox environments
- Create test fixtures with known outcomes
- Mock provider responses for unit tests
- Testcontainers for ALL infrastructure: PostgreSQL, RabbitMQ, Redis (per Constitution Principle IV)

**Load Testing Targets**:
- 10,000 transactions/hour = ~2.78 TPS sustained
- 1,000 concurrent requests peak
- Use k6 or NBomber for load testing
- Test circuit breaker activation under load

**Chaos Engineering**:
- Simulate provider outages
- Test failover mechanisms
- Verify circuit breaker behavior
- Validate data consistency under failures

## 10. Real Infrastructure Testing with Testcontainers

### 10.1 Constitutional Requirement

**Principle IV: Real Infrastructure Testing (NON-NEGOTIABLE)**
- ALL tests MUST use real infrastructure via Testcontainers
- No in-memory substitutes allowed (databases, message queues, caches)
- Test isolation through transactions, queue purging, cleanup scripts
- Production configuration mirrored in tests

**Rationale**: In-memory substitutes hide real-world issues:
- Database: Concurrency, transaction isolation, constraint behavior
- Message Queue: Serialization, dead-letter handling, consumer concurrency
- Cache: Distributed locking race conditions, eviction policies, TTL behavior

### 10.2 Testcontainers.PostgreSQL 3.10.0

**Usage Pattern**:
```csharp
public class TestContainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("paymentservice_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    public async Task InitializeAsync()
        => await _postgresContainer.StartAsync();

    public string ConnectionString
        => _postgresContainer.GetConnectionString();

    public async Task DisposeAsync()
        => await _postgresContainer.DisposeAsync();
}
```

**Key Features**:
- Automatic port mapping (avoids conflicts)
- Wait strategies for database readiness
- Automatic cleanup after tests
- Snapshot/restore for fast test isolation

**Test Isolation Strategy**:
- Use database transactions with rollback
- Or: Truncate tables between tests
- Run migrations on container startup
- Seed test data in fixture

### 10.3 Testcontainers.RabbitMQ 3.10.0

**Usage Pattern**:
```csharp
private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
    .WithImage("rabbitmq:7.0-management-alpine")
    .WithUsername("guest")
    .WithPassword("guest")
    .Build();
```

**Why Real RabbitMQ vs In-Memory**:
- **Serialization**: Catches JSON serialization issues with MassTransit
- **Dead-Letter Exchanges**: Validates error handling configuration
- **Message TTL**: Tests time-based expiration behavior
- **Consumer Concurrency**: Tests parallel message processing
- **Acknowledgment**: Validates ack/nack behavior under failures

**Test Scenarios**:
- Event publishing from PaymentService
- Verify events appear in correct exchanges/queues
- Test consumer behavior (if service consumes events)
- Validate retry policies with dead-letter queues
- Simulate broker unavailability and reconnection

**Integration with MassTransit**:
```csharp
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(_rabbitMqContainer.GetConnectionString());
        cfg.ConfigureEndpoints(context);
    });
});
```

### 10.4 Testcontainers.Redis 3.10.0

**Usage Pattern**:
```csharp
private readonly RedisContainer _redisContainer = new RedisBuilder()
    .WithImage("redis:7.2-alpine")
    .Build();
```

**Why Real Redis vs In-Memory**:
- **Distributed Locking**: Catches race conditions in idempotency checks
- **Expiration**: Tests TTL behavior for idempotency keys
- **Connection Pooling**: Validates StackExchange.Redis configuration
- **Keyspace Events**: Tests expiration notifications
- **Atomic Operations**: Validates SETNX, GETSET behavior

**Critical Test Scenarios**:
1. **Idempotency Key Race Condition**:
   - Two concurrent requests with same idempotency key
   - Verify only one payment is created
   - Requires real Redis SETNX atomic operation

2. **Distributed Lock Timeout**:
   - Long-running operation holds lock
   - Second request waits then times out
   - Verify lock release on timeout

3. **Circuit Breaker State Sharing**:
   - Multiple service instances share circuit breaker state
   - Verify state synchronized via Redis

4. **Rate Limiting**:
   - Concurrent requests increment counters
   - Verify accurate rate limit enforcement
   - Test sliding window algorithms

**Integration with RedisIdempotencyService**:
```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = _redisContainer.GetConnectionString();
    options.InstanceName = "PaymentService_";
});
```

### 10.5 Comprehensive Test Fixture

**Complete Integration Test Setup**:
```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly RabbitMqContainer _rabbitMq;
    private readonly RedisContainer _redis;

    public IntegrationTestFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .Build();

        _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:7.0-management-alpine")
            .Build();

        _redis = new RedisBuilder()
            .WithImage("redis:7.2-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start all containers in parallel
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbitMq.StartAsync(),
            _redis.StartAsync()
        );

        // Run EF Core migrations
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask()
        );
    }
}
```

### 10.6 CI/CD Considerations

**Docker-in-Docker Requirements**:
- CI agents MUST have Docker daemon access
- Use `docker:dind` service in GitLab CI
- GitHub Actions: Pre-configured Docker support
- Azure Pipelines: Enable Docker service

**Performance Optimization**:
- Cache container images in CI
- Reuse containers across test classes (xUnit collection fixtures)
- Parallel test execution with port randomization
- Container startup typically <5 seconds

**Troubleshooting**:
- Check Docker daemon availability: `docker ps`
- Increase test timeouts for container startup
- Monitor port conflicts in parallel test runs
- Enable Testcontainers logging for debugging

### 10.7 Research Findings

**Benefits Observed**:
- ✅ Catches PostgreSQL-specific constraint violations missed by in-memory DBs
- ✅ Validates MassTransit serialization configuration
- ✅ Tests Redis distributed locking race conditions
- ✅ Ensures production configuration parity
- ✅ Fast container startup (<5s per container)

**Challenges**:
- ⚠️ Requires Docker daemon (developer setup step)
- ⚠️ Slightly slower than in-memory (acceptable tradeoff)
- ⚠️ Port conflicts in parallel execution (Testcontainers handles this)

**Recommendation**:
- **ENFORCE** Testcontainers for ALL infrastructure dependencies
- Update Constitution Principle IV to mandate this approach
- NO exceptions for "faster" in-memory alternatives
- Trade minimal performance cost for maximum test fidelity

## 11. Technology Stack Validation

### 11.1 NuGet Package Versions

**Confirmed Compatible Versions**:
- ✅ Entity Framework Core 9.0.10 + Npgsql 9.0.4 (compatible)
- ✅ MassTransit 8.3.4 (latest stable for .NET 8+)
- ✅ Polly 8.5.0 (latest v8 with resilience pipeline API)
- ✅ StackExchange.Redis 2.8.16 (latest stable)
- ✅ Prometheus-net.AspNetCore 8.2.1 (latest stable)

**Additional Required Packages**:
- FluentValidation.AspNetCore 11.3.0
- Scalar.AspNetCore 1.2.42
- Serilog.AspNetCore 8.0.3
- Testcontainers.PostgreSQL 3.10.0
- Testcontainers.RabbitMQ 3.10.0
- Testcontainers.Redis 3.10.0
- xUnit 2.9.3
- Moq 4.20.72

### 11.2 PostgreSQL 18 Feature Usage

**New Features to Leverage**:
- JSON improvements for webhook payload storage
- Improved performance for JSONB indexing
- Better query parallelization (partition-wise joins)
- Enhanced monitoring views

**Compatibility Notes**:
- Npgsql 9.0.4 fully supports PostgreSQL 18
- No migration concerns from earlier versions
- Use advisory locks for distributed locking if needed

### 11.3 Redis Usage Patterns

**Use Cases**:
1. Idempotency key caching
2. Distributed locks for concurrent request handling
3. Circuit breaker state storage (shared across instances)
4. Provider authentication token caching
5. Rate limiting counters

**Configuration**:
- Enable keyspace notifications for expiry events
- Use Redis Cluster for high availability
- Configure maxmemory-policy: allkeys-lru

## 12. Next Steps

1. Review and approve research findings
2. Proceed to Phase 1: Design & Architecture
3. Create `data-model.md` with complete entity definitions
4. Define OpenAPI contracts in `contracts/` directory
5. Write `quickstart.md` for developer onboarding
6. Generate tasks with `/speckit.tasks` command
7. Begin Test-First Development workflow

---

**Research Completed**: 2025-11-18
**Next Phase**: Design & Architecture (Phase 1)
