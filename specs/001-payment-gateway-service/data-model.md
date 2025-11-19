# Data Model: Payment Gateway Service

**Feature**: Payment Gateway Service | **Date**: 2025-11-18

## Overview

This document defines the database schema and entity model for the Payment Gateway Service using PostgreSQL 18 with Entity Framework Core 9.0.10. All tables use snake_case naming convention, and all entities include audit fields and optimistic concurrency control.

## Database Naming Conventions

- **Tables**: snake_case (e.g., `payment_transactions`)
- **Columns**: snake_case (e.g., `provider_transaction_id`)
- **Indexes**: `idx_{table}_{columns}` (e.g., `idx_payment_transactions_status_created_at`)
- **Foreign Keys**: `fk_{table}_{referenced_table}` (e.g., `fk_payment_transactions_payment_providers`)
- **Unique Constraints**: `uk_{table}_{columns}` (e.g., `uk_payment_transactions_idempotency_key`)

## Core Entities

### 1. PaymentTransaction

Represents a payment operation processed through the gateway.

**Table Name**: `payment_transactions`

**Properties**:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | NO | Primary key |
| idempotency_key | varchar(255) | NO | Client-provided idempotency key |
| amount | decimal(19,4) | NO | Payment amount with 4 decimal precision |
| currency | varchar(3) | NO | ISO 4217 currency code (e.g., USD, THB) |
| status | varchar(50) | NO | Payment status enum value |
| customer_id | varchar(255) | NO | External customer identifier |
| requesting_service | varchar(100) | NO | Name of microservice that initiated payment |
| provider_id | uuid | NO | Foreign key to payment_providers |
| provider_transaction_id | varchar(255) | YES | Provider's transaction identifier |
| provider_metadata | jsonb | YES | Provider-specific response data |
| payment_method | varchar(50) | YES | Payment method used (card, bank_transfer, qr, etc.) |
| description | varchar(500) | YES | Payment description |
| customer_email | varchar(255) | YES | Customer email address |
| customer_name | varchar(255) | YES | Customer name |
| failure_reason | varchar(500) | YES | Reason for failure if status is failed |
| failure_code | varchar(100) | YES | Standardized error code |
| completed_at | timestamptz | YES | UTC timestamp when payment completed |
| failed_at | timestamptz | YES | UTC timestamp when payment failed |
| correlation_id | uuid | NO | Distributed tracing correlation ID |
| refunded_amount | decimal(19,4) | NO | Total amount refunded (default 0) |
| is_archived | boolean | NO | Whether record is archived (default false) |
| created_at | timestamptz | NO | UTC timestamp of creation |
| updated_at | timestamptz | NO | UTC timestamp of last update |
| created_by | varchar(100) | YES | User/service that created record |
| updated_by | varchar(100) | YES | User/service that last updated record |
| row_version | bytea | NO | Optimistic concurrency token |

**Indexes**:
- `pk_payment_transactions` - PRIMARY KEY (id)
- `uk_payment_transactions_idempotency_key` - UNIQUE (idempotency_key)
- `idx_payment_transactions_status_created_at` - (status, created_at DESC)
- `idx_payment_transactions_customer_id` - (customer_id, created_at DESC)
- `idx_payment_transactions_provider_id` - (provider_id)
- `idx_payment_transactions_correlation_id` - (correlation_id)
- `idx_payment_transactions_provider_transaction_id` - (provider_transaction_id) WHERE provider_transaction_id IS NOT NULL
- `idx_payment_transactions_created_at` - (created_at DESC) WHERE is_archived = false
- `idx_payment_transactions_completed_at` - (completed_at DESC) WHERE completed_at IS NOT NULL

**Foreign Keys**:
- `fk_payment_transactions_payment_providers` - FOREIGN KEY (provider_id) REFERENCES payment_providers(id)

**Constraints**:
- `chk_payment_transactions_amount_positive` - CHECK (amount > 0)
- `chk_payment_transactions_currency_length` - CHECK (LENGTH(currency) = 3)
- `chk_payment_transactions_refunded_amount` - CHECK (refunded_amount >= 0 AND refunded_amount <= amount)
- `chk_payment_transactions_status` - CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'refunded', 'partially_refunded'))

**Entity Code Structure**:
```csharp
public class PaymentTransaction
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public PaymentStatus Status { get; set; }
    public string CustomerId { get; set; }
    public string RequestingService { get; set; }
    public Guid ProviderId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderMetadata { get; set; } // JSON string
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    public string? FailureReason { get; set; }
    public string? FailureCode { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public Guid CorrelationId { get; set; }
    public decimal RefundedAmount { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; }

    // Navigation properties
    public PaymentProvider Provider { get; set; }
    public ICollection<RefundTransaction> Refunds { get; set; }
    public ICollection<TransactionLog> Logs { get; set; }
}
```

---

### 2. PaymentProvider

Represents an external payment gateway configuration.

**Table Name**: `payment_providers`

**Properties**:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | NO | Primary key |
| name | varchar(100) | NO | Provider name (Stripe, PayPal, Omise, SCB) |
| display_name | varchar(255) | NO | Human-readable display name |
| status | varchar(50) | NO | Operational status enum value |
| provider_type | varchar(50) | NO | Type identifier for factory pattern |
| api_endpoint | varchar(500) | NO | Base API URL |
| webhook_endpoint | varchar(500) | YES | Public webhook URL for this provider |
| encrypted_credentials | text | NO | Encrypted API credentials (JSON) |
| supported_currencies | jsonb | NO | Array of supported ISO 4217 currency codes |
| supported_payment_methods | jsonb | YES | Array of supported payment methods |
| priority | integer | NO | Routing priority (lower = higher priority) |
| rate_limit_per_second | integer | YES | Maximum requests per second |
| timeout_seconds | integer | NO | Timeout for API calls (default 30) |
| max_retry_attempts | integer | NO | Maximum retry attempts (default 3) |
| enable_circuit_breaker | boolean | NO | Whether circuit breaker is enabled |
| circuit_breaker_threshold | decimal(3,2) | YES | Failure ratio threshold (default 0.5) |
| configuration_metadata | jsonb | YES | Additional provider-specific configuration |
| health_check_url | varchar(500) | YES | URL for health check endpoint |
| last_health_check | timestamptz | YES | Last successful health check timestamp |
| success_rate_15min | decimal(5,2) | YES | Success rate over last 15 minutes (%) |
| is_sandbox | boolean | NO | Whether using sandbox/test environment |
| is_archived | boolean | NO | Whether provider is archived (default false) |
| created_at | timestamptz | NO | UTC timestamp of creation |
| updated_at | timestamptz | NO | UTC timestamp of last update |
| created_by | varchar(100) | YES | User/service that created record |
| updated_by | varchar(100) | YES | User/service that last updated record |
| row_version | bytea | NO | Optimistic concurrency token |

**Indexes**:
- `pk_payment_providers` - PRIMARY KEY (id)
- `uk_payment_providers_name` - UNIQUE (name)
- `idx_payment_providers_status_priority` - (status, priority) WHERE status = 'active'
- `idx_payment_providers_supported_currencies` - USING GIN (supported_currencies)

**Constraints**:
- `chk_payment_providers_status` - CHECK (status IN ('active', 'disabled', 'degraded', 'maintenance'))
- `chk_payment_providers_priority` - CHECK (priority > 0)
- `chk_payment_providers_timeout` - CHECK (timeout_seconds > 0 AND timeout_seconds <= 120)
- `chk_payment_providers_rate_limit` - CHECK (rate_limit_per_second IS NULL OR rate_limit_per_second > 0)

**Entity Code Structure**:
```csharp
public class PaymentProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public ProviderStatus Status { get; set; }
    public string ProviderType { get; set; }
    public string ApiEndpoint { get; set; }
    public string? WebhookEndpoint { get; set; }
    public string EncryptedCredentials { get; set; }
    public string SupportedCurrencies { get; set; } // JSON array
    public string? SupportedPaymentMethods { get; set; } // JSON array
    public int Priority { get; set; }
    public int? RateLimitPerSecond { get; set; }
    public int TimeoutSeconds { get; set; }
    public int MaxRetryAttempts { get; set; }
    public bool EnableCircuitBreaker { get; set; }
    public decimal? CircuitBreakerThreshold { get; set; }
    public string? ConfigurationMetadata { get; set; } // JSON
    public string? HealthCheckUrl { get; set; }
    public DateTime? LastHealthCheck { get; set; }
    public decimal? SuccessRate15Min { get; set; }
    public bool IsSandbox { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; }

    // Navigation properties
    public ICollection<PaymentTransaction> Transactions { get; set; }
    public ICollection<RefundTransaction> Refunds { get; set; }
    public ICollection<WebhookEvent> WebhookEvents { get; set; }
}
```

---

### 3. RefundTransaction

Represents a refund operation for a completed payment.

**Table Name**: `refund_transactions`

**Properties**:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | NO | Primary key |
| payment_transaction_id | uuid | NO | Foreign key to payment_transactions |
| provider_id | uuid | NO | Foreign key to payment_providers |
| provider_refund_id | varchar(255) | YES | Provider's refund identifier |
| amount | decimal(19,4) | NO | Refund amount |
| currency | varchar(3) | NO | ISO 4217 currency code |
| status | varchar(50) | NO | Refund status enum value |
| reason | varchar(500) | YES | Reason for refund |
| refund_type | varchar(50) | NO | Type: full or partial |
| idempotency_key | varchar(255) | NO | Idempotency key for refund request |
| requesting_service | varchar(100) | NO | Service that requested refund |
| provider_metadata | jsonb | YES | Provider-specific refund data |
| failure_reason | varchar(500) | YES | Reason for failure if status is failed |
| failure_code | varchar(100) | YES | Standardized error code |
| completed_at | timestamptz | YES | UTC timestamp when refund completed |
| failed_at | timestamptz | YES | UTC timestamp when refund failed |
| correlation_id | uuid | NO | Distributed tracing correlation ID |
| is_archived | boolean | NO | Whether record is archived (default false) |
| created_at | timestamptz | NO | UTC timestamp of creation |
| updated_at | timestamptz | NO | UTC timestamp of last update |
| created_by | varchar(100) | YES | User/service that created record |
| updated_by | varchar(100) | YES | User/service that last updated record |
| row_version | bytea | NO | Optimistic concurrency token |

**Indexes**:
- `pk_refund_transactions` - PRIMARY KEY (id)
- `uk_refund_transactions_idempotency_key` - UNIQUE (idempotency_key)
- `idx_refund_transactions_payment_id` - (payment_transaction_id, created_at DESC)
- `idx_refund_transactions_status` - (status, created_at DESC)
- `idx_refund_transactions_provider_id` - (provider_id)
- `idx_refund_transactions_correlation_id` - (correlation_id)

**Foreign Keys**:
- `fk_refund_transactions_payment_transactions` - FOREIGN KEY (payment_transaction_id) REFERENCES payment_transactions(id)
- `fk_refund_transactions_payment_providers` - FOREIGN KEY (provider_id) REFERENCES payment_providers(id)

**Constraints**:
- `chk_refund_transactions_amount_positive` - CHECK (amount > 0)
- `chk_refund_transactions_currency_length` - CHECK (LENGTH(currency) = 3)
- `chk_refund_transactions_status` - CHECK (status IN ('pending', 'processing', 'completed', 'failed'))
- `chk_refund_transactions_type` - CHECK (refund_type IN ('full', 'partial'))

**Entity Code Structure**:
```csharp
public class RefundTransaction
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public Guid ProviderId { get; set; }
    public string? ProviderRefundId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public RefundStatus Status { get; set; }
    public string? Reason { get; set; }
    public string RefundType { get; set; }
    public string IdempotencyKey { get; set; }
    public string RequestingService { get; set; }
    public string? ProviderMetadata { get; set; } // JSON string
    public string? FailureReason { get; set; }
    public string? FailureCode { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public Guid CorrelationId { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; }

    // Navigation properties
    public PaymentTransaction PaymentTransaction { get; set; }
    public PaymentProvider Provider { get; set; }
}
```

---

### 4. WebhookEvent

Represents an asynchronous notification received from a payment provider.

**Table Name**: `webhook_events`

**Properties**:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | NO | Primary key |
| provider_id | uuid | NO | Foreign key to payment_providers |
| provider_event_id | varchar(255) | NO | Provider's unique event identifier |
| event_type | varchar(100) | NO | Type of event (payment.completed, etc.) |
| payment_transaction_id | uuid | YES | Related payment transaction if applicable |
| refund_transaction_id | uuid | YES | Related refund transaction if applicable |
| raw_payload | text | NO | Complete webhook payload as received |
| parsed_payload | jsonb | YES | Parsed webhook data |
| signature | varchar(500) | YES | Webhook signature for validation |
| signature_validated | boolean | NO | Whether signature validation passed |
| ip_address | varchar(45) | YES | Source IP address of webhook |
| user_agent | varchar(255) | YES | User agent from webhook request |
| processing_status | varchar(50) | NO | Processing status enum value |
| processing_attempts | integer | NO | Number of processing attempts (default 0) |
| processed_at | timestamptz | YES | UTC timestamp when successfully processed |
| failed_at | timestamptz | YES | UTC timestamp when processing failed |
| failure_reason | varchar(500) | YES | Reason for processing failure |
| next_retry_at | timestamptz | YES | Scheduled time for next retry |
| correlation_id | uuid | YES | Correlation ID if event relates to transaction |
| created_at | timestamptz | NO | UTC timestamp when webhook received |
| updated_at | timestamptz | NO | UTC timestamp of last update |
| row_version | bytea | NO | Optimistic concurrency token |

**Indexes**:
- `pk_webhook_events` - PRIMARY KEY (id)
- `uk_webhook_events_provider_event` - UNIQUE (provider_id, provider_event_id)
- `idx_webhook_events_payment_id` - (payment_transaction_id) WHERE payment_transaction_id IS NOT NULL
- `idx_webhook_events_refund_id` - (refund_transaction_id) WHERE refund_transaction_id IS NOT NULL
- `idx_webhook_events_processing_status` - (processing_status, created_at DESC)
- `idx_webhook_events_created_at` - (created_at DESC)
- `idx_webhook_events_next_retry` - (next_retry_at) WHERE next_retry_at IS NOT NULL

**Foreign Keys**:
- `fk_webhook_events_payment_providers` - FOREIGN KEY (provider_id) REFERENCES payment_providers(id)
- `fk_webhook_events_payment_transactions` - FOREIGN KEY (payment_transaction_id) REFERENCES payment_transactions(id)
- `fk_webhook_events_refund_transactions` - FOREIGN KEY (refund_transaction_id) REFERENCES refund_transactions(id)

**Constraints**:
- `chk_webhook_events_processing_status` - CHECK (processing_status IN ('pending', 'processing', 'completed', 'failed', 'duplicate'))
- `chk_webhook_events_attempts` - CHECK (processing_attempts >= 0)

**Data Retention**:
- Webhook events older than 30 days are automatically deleted (cleanup job)

**Entity Code Structure**:
```csharp
public class WebhookEvent
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderEventId { get; set; }
    public string EventType { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public Guid? RefundTransactionId { get; set; }
    public string RawPayload { get; set; }
    public string? ParsedPayload { get; set; } // JSON string
    public string? Signature { get; set; }
    public bool SignatureValidated { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public WebhookProcessingStatus ProcessingStatus { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; }

    // Navigation properties
    public PaymentProvider Provider { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    public RefundTransaction? RefundTransaction { get; set; }
}
```

---

### 5. TransactionLog

Represents an immutable audit log entry for all payment operations.

**Table Name**: `transaction_logs`

**Properties**:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | NO | Primary key |
| payment_transaction_id | uuid | YES | Related payment transaction |
| refund_transaction_id | uuid | YES | Related refund transaction |
| operation_type | varchar(50) | NO | Type of operation logged |
| provider_id | uuid | YES | Provider used for operation |
| requesting_service | varchar(100) | NO | Service that initiated operation |
| request_payload | jsonb | YES | Request data sent to provider |
| response_payload | jsonb | YES | Response received from provider |
| http_status_code | integer | YES | HTTP status code from provider |
| success | boolean | NO | Whether operation succeeded |
| error_code | varchar(100) | YES | Error code if failed |
| error_message | varchar(500) | YES | Error message if failed |
| latency_ms | integer | YES | Operation latency in milliseconds |
| retry_attempt | integer | YES | Retry attempt number (0 for first try) |
| correlation_id | uuid | NO | Distributed tracing correlation ID |
| user_agent | varchar(255) | YES | Client user agent |
| ip_address | varchar(45) | YES | Client IP address |
| created_at | timestamptz | NO | UTC timestamp of log entry (immutable) |

**Indexes**:
- `pk_transaction_logs` - PRIMARY KEY (id)
- `idx_transaction_logs_payment_id` - (payment_transaction_id, created_at DESC)
- `idx_transaction_logs_refund_id` - (refund_transaction_id, created_at DESC)
- `idx_transaction_logs_correlation_id` - (correlation_id)
- `idx_transaction_logs_created_at` - (created_at DESC)
- `idx_transaction_logs_operation_type` - (operation_type, created_at DESC)

**Foreign Keys**:
- `fk_transaction_logs_payment_transactions` - FOREIGN KEY (payment_transaction_id) REFERENCES payment_transactions(id)
- `fk_transaction_logs_refund_transactions` - FOREIGN KEY (refund_transaction_id) REFERENCES refund_transactions(id)
- `fk_transaction_logs_payment_providers` - FOREIGN KEY (provider_id) REFERENCES payment_providers(id)

**Constraints**:
- `chk_transaction_logs_operation_type` - CHECK (operation_type IN ('payment_create', 'payment_status_check', 'refund_create', 'webhook_received', 'provider_health_check'))
- `chk_transaction_logs_latency` - CHECK (latency_ms IS NULL OR latency_ms >= 0)
- `chk_transaction_logs_retry` - CHECK (retry_attempt IS NULL OR retry_attempt >= 0)

**Notes**:
- No `updated_at` or `row_version` - logs are immutable
- No soft delete - retention handled by archival process
- Partition by month for better query performance on large datasets

**Entity Code Structure**:
```csharp
public class TransactionLog
{
    public Guid Id { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public Guid? RefundTransactionId { get; set; }
    public string OperationType { get; set; }
    public Guid? ProviderId { get; set; }
    public string RequestingService { get; set; }
    public string? RequestPayload { get; set; } // JSON string
    public string? ResponsePayload { get; set; } // JSON string
    public int? HttpStatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int? LatencyMs { get; set; }
    public int? RetryAttempt { get; set; }
    public Guid CorrelationId { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public PaymentTransaction? PaymentTransaction { get; set; }
    public RefundTransaction? RefundTransaction { get; set; }
    public PaymentProvider? Provider { get; set; }
}
```

---

### 6. ProviderConfiguration (Optional - for multi-region support)

Stores environment-specific provider configurations for different deployment regions.

**Table Name**: `provider_configurations`

**Properties**:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | NO | Primary key |
| provider_id | uuid | NO | Foreign key to payment_providers |
| environment | varchar(50) | NO | Environment (dev, staging, production) |
| region | varchar(50) | YES | Geographic region (us-east, eu-west, asia-southeast) |
| is_active | boolean | NO | Whether this configuration is active |
| api_endpoint_override | varchar(500) | YES | Environment-specific API endpoint |
| encrypted_credentials_override | text | YES | Environment-specific credentials |
| configuration_overrides | jsonb | YES | Other environment-specific settings |
| created_at | timestamptz | NO | UTC timestamp of creation |
| updated_at | timestamptz | NO | UTC timestamp of last update |
| created_by | varchar(100) | YES | User/service that created record |
| updated_by | varchar(100) | YES | User/service that last updated record |
| row_version | bytea | NO | Optimistic concurrency token |

**Indexes**:
- `pk_provider_configurations` - PRIMARY KEY (id)
- `uk_provider_configurations_provider_env_region` - UNIQUE (provider_id, environment, region)
- `idx_provider_configurations_active` - (provider_id, is_active) WHERE is_active = true

**Foreign Keys**:
- `fk_provider_configurations_payment_providers` - FOREIGN KEY (provider_id) REFERENCES payment_providers(id)

**Constraints**:
- `chk_provider_configurations_environment` - CHECK (environment IN ('development', 'staging', 'production'))

**Entity Code Structure**:
```csharp
public class ProviderConfiguration
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string Environment { get; set; }
    public string? Region { get; set; }
    public bool IsActive { get; set; }
    public string? ApiEndpointOverride { get; set; }
    public string? EncryptedCredentialsOverride { get; set; }
    public string? ConfigurationOverrides { get; set; } // JSON string
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; }

    // Navigation properties
    public PaymentProvider Provider { get; set; }
}
```

---

## Enumerations

### PaymentStatus
```csharp
public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Refunded,
    PartiallyRefunded
}
```

### ProviderStatus
```csharp
public enum ProviderStatus
{
    Active,
    Disabled,
    Degraded,
    Maintenance
}
```

### RefundStatus
```csharp
public enum RefundStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
```

### WebhookProcessingStatus
```csharp
public enum WebhookProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Duplicate
}
```

---

## Entity Relationships

```
payment_providers (1) ──< (N) payment_transactions
payment_providers (1) ──< (N) refund_transactions
payment_providers (1) ──< (N) webhook_events
payment_providers (1) ──< (N) provider_configurations
payment_providers (1) ──< (N) transaction_logs

payment_transactions (1) ──< (N) refund_transactions
payment_transactions (1) ──< (N) webhook_events
payment_transactions (1) ──< (N) transaction_logs

refund_transactions (1) ──< (N) webhook_events
refund_transactions (1) ──< (N) transaction_logs
```

---

## Database Initialization Data

### Seed Data: Payment Providers

```sql
-- Stripe
INSERT INTO payment_providers (id, name, display_name, status, provider_type, api_endpoint, supported_currencies, priority, timeout_seconds, max_retry_attempts, enable_circuit_breaker, is_sandbox, created_at, updated_at, row_version)
VALUES
  (gen_random_uuid(), 'Stripe', 'Stripe', 'active', 'stripe', 'https://api.stripe.com', '["USD","EUR","GBP","THB","JPY","AUD","CAD","SGD"]', 1, 30, 3, true, true, NOW(), NOW(), E'\\x00000001');

-- PayPal
INSERT INTO payment_providers (id, name, display_name, status, provider_type, api_endpoint, supported_currencies, priority, timeout_seconds, max_retry_attempts, enable_circuit_breaker, is_sandbox, created_at, updated_at, row_version)
VALUES
  (gen_random_uuid(), 'PayPal', 'PayPal', 'active', 'paypal', 'https://api-m.sandbox.paypal.com', '["USD","EUR","GBP","CAD","AUD"]', 2, 30, 3, true, true, NOW(), NOW(), E'\\x00000001');

-- Omise
INSERT INTO payment_providers (id, name, display_name, status, provider_type, api_endpoint, supported_currencies, priority, timeout_seconds, max_retry_attempts, enable_circuit_breaker, is_sandbox, created_at, updated_at, row_version)
VALUES
  (gen_random_uuid(), 'Omise', 'Omise', 'active', 'omise', 'https://api.omise.co', '["THB","JPY","SGD","USD","EUR"]', 3, 30, 3, true, true, NOW(), NOW(), E'\\x00000001');

-- SCB API
INSERT INTO payment_providers (id, name, display_name, status, provider_type, api_endpoint, supported_currencies, priority, timeout_seconds, max_retry_attempts, enable_circuit_breaker, is_sandbox, created_at, updated_at, row_version)
VALUES
  (gen_random_uuid(), 'SCB', 'SCB Easy App', 'active', 'scb', 'https://api-sandbox.partners.scb', '["THB","USD"]', 4, 30, 3, true, true, NOW(), NOW(), E'\\x00000001');
```

---

## Database Migration Strategy

### Initial Migration
1. Create `payment_providers` table first (no dependencies)
2. Create `payment_transactions` table (depends on payment_providers)
3. Create `refund_transactions` table (depends on payment_transactions and payment_providers)
4. Create `webhook_events` table (depends on payment_providers, payment_transactions, refund_transactions)
5. Create `transaction_logs` table (depends on payment_transactions, refund_transactions, payment_providers)
6. Create `provider_configurations` table (depends on payment_providers)
7. Insert seed data for payment providers

### Schema Version Control
- Use EF Core migrations for all schema changes
- Never modify existing migrations
- Include migration SQL scripts in version control
- Test migrations against PostgreSQL 18 container

### Future Migration Considerations
- Add partitioning to `transaction_logs` by month after 6 months
- Consider separate archive database after 1 year
- Add JSONB indexes on frequently queried fields in provider_metadata

---

## Performance Optimization Notes

### Query Patterns
1. **List active payments for customer**: Use `idx_payment_transactions_customer_id`
2. **Find payment by idempotency key**: Use `uk_payment_transactions_idempotency_key` (unique index)
3. **Provider routing query**: Use `idx_payment_providers_status_priority`
4. **Audit log retrieval**: Use `idx_transaction_logs_correlation_id` or `idx_transaction_logs_payment_id`
5. **Webhook deduplication**: Use `uk_webhook_events_provider_event`

### Connection Pooling
- Configure EF Core with connection pooling (default 100 connections)
- Monitor connection pool exhaustion
- Use async/await for all database operations

### Batch Operations
- Use bulk inserts for transaction logs (high volume)
- Batch webhook event processing
- Consider temporal tables for audit if PostgreSQL 18 feature available

---

**Data Model Completed**: 2025-11-18
**Next Phase**: Contract Definition (OpenAPI specifications)
