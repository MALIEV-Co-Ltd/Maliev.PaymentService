# Quickstart Guide: Payment Gateway Service

**Feature**: Payment Gateway Service | **Date**: 2025-11-18

## Overview

This guide helps developers get started with the Payment Gateway Service locally. It covers prerequisites, setup, database migrations, running tests, and making API calls.

## Prerequisites

### Required Software

- **.NET 10 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
  ```bash
  dotnet --version
  # Should output: 10.0.x
  ```

- **PostgreSQL 18**: Can run via Docker (recommended) or local installation
  ```bash
  # Check version
  psql --version
  # Should output: psql (PostgreSQL) 18.x
  ```

- **Docker Desktop**: For running infrastructure services
  ```bash
  docker --version
  docker compose --version
  ```

- **Redis**: For distributed caching and idempotency
  ```bash
  # Via Docker (see docker-compose.yml)
  ```

- **RabbitMQ**: For message queue event publishing
  ```bash
  # Via Docker (see docker-compose.yml)
  ```

### Optional Tools

- **Postman** or **Insomnia**: For API testing
- **pgAdmin** or **DBeaver**: For database management
- **k6** or **NBomber**: For load testing

## Local Development Setup

### 1. Clone Repository

```bash
git clone https://github.com/maliev/Maliev.PaymentService.git
cd Maliev.PaymentService
```

### 2. Start Infrastructure Services

Create `docker-compose.yml` in the repository root:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:18-alpine
    container_name: payment-gateway-postgres
    environment:
      POSTGRES_DB: payment_gateway
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: payment-gateway-redis
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  rabbitmq:
    image: rabbitmq:3-management-alpine
    container_name: payment-gateway-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management UI
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
```

Start all services:

```bash
docker compose up -d
```

Verify services are running:

```bash
docker compose ps

# Expected output:
# NAME                          STATUS      PORTS
# payment-gateway-postgres      Up          0.0.0.0:5432->5432/tcp
# payment-gateway-redis         Up          0.0.0.0:6379->6379/tcp
# payment-gateway-rabbitmq      Up          0.0.0.0:5672->5672/tcp, 0.0.0.0:15672->15672/tcp
```

### 3. Configure Application Settings

Create `appsettings.Development.json` in `Maliev.PaymentService.Api/`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "PaymentDatabase": "Host=localhost;Port=5432;Database=payment_gateway;Username=postgres;Password=postgres;Include Error Detail=true"
  },
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "PaymentGateway:"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  },
  "JwtAuthentication": {
    "Authority": "https://localhost:5001",
    "Audience": "payment-gateway-api",
    "RequireHttpsMetadata": false
  },
  "PaymentProviders": {
    "Stripe": {
      "ApiKey": "sk_test_YOUR_STRIPE_TEST_KEY",
      "WebhookSecret": "whsec_YOUR_WEBHOOK_SECRET",
      "Sandbox": true
    },
    "PayPal": {
      "ClientId": "YOUR_PAYPAL_CLIENT_ID",
      "ClientSecret": "YOUR_PAYPAL_CLIENT_SECRET",
      "Sandbox": true
    },
    "Omise": {
      "SecretKey": "skey_test_YOUR_OMISE_KEY",
      "Sandbox": true
    },
    "SCB": {
      "ApiKey": "YOUR_SCB_API_KEY",
      "ApiSecret": "YOUR_SCB_API_SECRET",
      "Sandbox": true
    }
  },
  "Polly": {
    "RetryCount": 3,
    "TimeoutSeconds": 30,
    "CircuitBreakerFailureThreshold": 0.5,
    "CircuitBreakerSamplingDurationSeconds": 30,
    "CircuitBreakerBreakDurationSeconds": 60
  }
}
```

**Important**: Never commit real credentials. Use placeholders for public repositories.

### 4. Restore NuGet Packages

```bash
dotnet restore
```

## Database Migration Steps

### 1. Install EF Core Tools (if not already installed)

```bash
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef

# Verify installation
dotnet ef --version
```

### 2. Create Initial Migration

```bash
cd Maliev.PaymentService.Infrastructure

dotnet ef migrations add InitialCreate \
  --startup-project ../Maliev.PaymentService.Api \
  --context PaymentDbContext \
  --output-dir Data/Migrations
```

### 3. Apply Migration to Database

```bash
dotnet ef database update \
  --startup-project ../Maliev.PaymentService.Api \
  --context PaymentDbContext
```

### 4. Verify Database Schema

Connect to PostgreSQL and verify tables:

```bash
psql -h localhost -U postgres -d payment_gateway -c "\dt"

# Expected tables:
# payment_transactions
# payment_providers
# refund_transactions
# webhook_events
# transaction_logs
# provider_configurations
```

### 5. Seed Initial Data

The migration should automatically seed initial payment providers (Stripe, PayPal, Omise, SCB).

Verify seed data:

```sql
SELECT id, name, display_name, status, priority
FROM payment_providers
ORDER BY priority;
```

## Running the Application

### 1. Build the Solution

```bash
cd ..
dotnet build
```

### 2. Run the API

```bash
cd Maliev.PaymentService.Api
dotnet run

# Or use watch mode for auto-reload during development
dotnet watch run
```

The API will start on:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### 3. Verify Service is Running

Open browser or curl:

```bash
# Health check
curl http://localhost:5000/health

# Expected response:
# {"status":"healthy","timestamp":"2025-11-18T10:30:00Z","version":"1.0.0","uptime":5}

# Readiness check
curl http://localhost:5000/health/ready

# Scalar API Documentation
# Open browser: http://localhost:5000/scalar
```

## Running Tests

### 1. Run All Tests

```bash
cd Maliev.PaymentService.Tests
dotnet test
```

### 2. Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### 3. Run Specific Test Categories

```bash
# Unit tests only
dotnet test --filter Category=Unit

# Integration tests only
dotnet test --filter Category=Integration

# Contract tests only
dotnet test --filter Category=Contract
```

### 4. Run Tests in Watch Mode

```bash
dotnet watch test
```

### Test Infrastructure

Tests use **Testcontainers** to spin up real infrastructure containers:
- **PostgreSQL 18**: Real database for EF Core integration tests
- **RabbitMQ 7.0**: Real message broker for MassTransit event publishing tests
- **Redis 7.2**: Real cache for distributed locking and idempotency tests

This ensures:
- No in-memory database/cache/queue inconsistencies
- Production-like test environment
- Accurate testing of distributed locking, message serialization, and transaction isolation
- Isolation between test runs

**Note**: Docker must be running for integration tests to pass. Constitution Principle IV (Real Infrastructure Testing) enforces Testcontainers for ALL infrastructure dependencies.

## API Endpoint Examples

### 1. Initiate Payment

**Note**: All endpoints require JWT authentication except webhooks and health checks.

For local development, you can disable authentication temporarily in `Program.cs` or use a test JWT token.

```bash
# POST /v1/payments
curl -X POST http://localhost:5000/v1/payments \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Idempotency-Key: test-payment-001" \
  -d '{
    "amount": 99.99,
    "currency": "USD",
    "customerId": "cust_test_123",
    "customerEmail": "customer@example.com",
    "customerName": "John Doe",
    "description": "Test order #12345"
  }'

# Expected Response (201 Created):
# {
#   "transactionId": "550e8400-e29b-41d4-a716-446655440000",
#   "status": "pending",
#   "amount": 99.99,
#   "currency": "USD",
#   "customerId": "cust_test_123",
#   "providerName": "Stripe",
#   "createdAt": "2025-11-18T10:30:00Z"
# }
```

### 2. Get Payment Status

```bash
# GET /v1/payments/{transactionId}
curl -X GET http://localhost:5000/v1/payments/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"

# Expected Response (200 OK):
# {
#   "transactionId": "550e8400-e29b-41d4-a716-446655440000",
#   "status": "completed",
#   "amount": 99.99,
#   "currency": "USD",
#   "customerId": "cust_test_123",
#   "providerName": "Stripe",
#   "providerTransactionId": "pi_1234567890",
#   "completedAt": "2025-11-18T10:30:25Z",
#   "createdAt": "2025-11-18T10:30:00Z"
# }
```

### 3. Refund Payment

```bash
# POST /v1/payments/{transactionId}/refund
curl -X POST http://localhost:5000/v1/payments/550e8400-e29b-41d4-a716-446655440000/refund \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Idempotency-Key: test-refund-001" \
  -d '{
    "amount": 49.99,
    "reason": "Customer requested partial refund"
  }'

# Expected Response (201 Created):
# {
#   "refundId": "650e8400-e29b-41d4-a716-446655440001",
#   "transactionId": "550e8400-e29b-41d4-a716-446655440000",
#   "status": "pending",
#   "amount": 49.99,
#   "currency": "USD",
#   "refundType": "partial",
#   "providerName": "Stripe",
#   "createdAt": "2025-11-18T11:00:00Z"
# }
```

### 4. List Payment Providers

```bash
# GET /v1/providers
curl -X GET http://localhost:5000/v1/providers \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"

# Expected Response (200 OK):
# {
#   "providers": [
#     {
#       "id": "450e8400-e29b-41d4-a716-446655440005",
#       "name": "Stripe",
#       "displayName": "Stripe Payments",
#       "status": "active",
#       "supportedCurrencies": ["USD", "EUR", "GBP", "THB"],
#       "priority": 1,
#       "successRate15Min": 99.8,
#       "isSandbox": true
#     },
#     ...
#   ],
#   "total": 4
# }
```

### 5. Test Webhook (Sandbox Only)

```bash
# POST /v1/webhooks/stripe/test
curl -X POST http://localhost:5000/v1/webhooks/stripe/test \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "payment.completed",
    "transactionId": "550e8400-e29b-41d4-a716-446655440000"
  }'

# Expected Response (200 OK):
# {
#   "success": true,
#   "message": "Test webhook event sent"
# }
```

### 6. Prometheus Metrics

```bash
# GET /metrics
curl -X GET http://localhost:5000/metrics

# Expected Response (200 OK):
# # HELP payment_transactions_total Total number of payment transactions
# # TYPE payment_transactions_total counter
# payment_transactions_total{service_name="payment-gateway-service",provider="Stripe",status="completed",environment="development",version="1.0.0"} 125
# ...
```

## Provider Configuration Examples

### Stripe Configuration

1. Create Stripe test account: https://dashboard.stripe.com/test/dashboard
2. Get API keys from: https://dashboard.stripe.com/test/apikeys
3. Set up webhook endpoint: https://dashboard.stripe.com/test/webhooks
   - URL: `https://your-ngrok-url/v1/webhooks/stripe`
   - Events: `payment_intent.succeeded`, `payment_intent.failed`, `charge.refunded`

### PayPal Configuration

1. Create PayPal sandbox account: https://developer.paypal.com/
2. Get credentials from: https://developer.paypal.com/dashboard/applications/sandbox
3. Configure webhook: https://developer.paypal.com/dashboard/webhooks/sandbox
   - URL: `https://your-ngrok-url/v1/webhooks/paypal`
   - Events: `PAYMENT.CAPTURE.COMPLETED`, `PAYMENT.CAPTURE.DENIED`, `PAYMENT.CAPTURE.REFUNDED`

### Local Webhook Testing with ngrok

For local development, use ngrok to expose your local API to the internet:

```bash
# Install ngrok
# Download from https://ngrok.com/download

# Expose local API
ngrok http 5000

# Copy the HTTPS URL (e.g., https://abc123.ngrok.io)
# Use this URL in provider webhook configurations
```

## Troubleshooting

### Database Connection Issues

```bash
# Check PostgreSQL is running
docker compose ps postgres

# View PostgreSQL logs
docker compose logs postgres

# Restart PostgreSQL
docker compose restart postgres
```

### Redis Connection Issues

```bash
# Check Redis is running
docker compose ps redis

# Test Redis connection
redis-cli -h localhost -p 6379 ping
# Expected: PONG

# View Redis logs
docker compose logs redis
```

### RabbitMQ Connection Issues

```bash
# Check RabbitMQ is running
docker compose ps rabbitmq

# Access RabbitMQ Management UI
# Open browser: http://localhost:15672
# Username: guest, Password: guest

# View RabbitMQ logs
docker compose logs rabbitmq
```

### Migration Issues

```bash
# Drop and recreate database
docker compose down -v
docker compose up -d postgres

# Wait for PostgreSQL to start
sleep 5

# Reapply migrations
cd Maliev.PaymentService.Infrastructure
dotnet ef database update --startup-project ../Maliev.PaymentService.Api
```

### Test Failures

```bash
# Ensure Docker is running (required for Testcontainers)
docker ps

# Clean and rebuild
dotnet clean
dotnet build

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Development Workflow

### 1. Create Feature Branch

```bash
git checkout -b feature/001-payment-gateway-service
```

### 2. Implement Changes Following TDD

1. Write failing test (Red)
2. Implement minimum code to pass test (Green)
3. Refactor while keeping tests green (Refactor)

### 3. Run Tests and Linting

```bash
# Run all tests
dotnet test

# Check for warnings (should be zero per constitution)
dotnet build --no-incremental

# Format code
dotnet format
```

### 4. Commit and Push

```bash
git add .
git commit -m "feat: implement payment initiation endpoint"
git push origin feature/001-payment-gateway-service
```

## Next Steps

1. Review the [Data Model](./data-model.md) for entity relationships
2. Explore [API Contracts](./contracts/) for detailed endpoint specifications
3. Read [Research Document](./research.md) for implementation patterns
4. Check [Implementation Plan](./plan.md) for architecture decisions
5. Generate tasks with `/speckit.tasks` command

## Additional Resources

- [.NET 10 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [PostgreSQL 18 Documentation](https://www.postgresql.org/docs/18/)
- [MassTransit Documentation](https://masstransit.io/)
- [Polly Resilience](https://www.pollydocs.org/)
- [Prometheus Metrics](https://prometheus.io/docs/introduction/overview/)

## Support

For questions or issues:
- Internal: #payment-gateway-service Slack channel
- Email: platform@maliev.com
- GitHub Issues: https://github.com/maliev/Maliev.PaymentService/issues

---

**Last Updated**: 2025-11-18
**Maintained By**: MALIEV Platform Team
