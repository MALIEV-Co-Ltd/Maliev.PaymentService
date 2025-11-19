# Payment Gateway Service

A production-ready payment gateway service built with .NET 10, featuring multi-provider support, intelligent routing, comprehensive monitoring, and enterprise-grade resilience patterns.

## Features

### Core Capabilities
- **Multi-Provider Support**: Seamlessly integrate with multiple payment providers (Stripe, PayPal, Square, Braintree, etc.)
- **Intelligent Routing**: Automatic provider selection based on health, cost, and performance metrics
- **Idempotency**: Built-in duplicate request detection using Redis-backed idempotency keys
- **Circuit Breaker**: Automatic provider health monitoring with circuit breaker pattern
- **Webhook Processing**: Secure webhook handling with signature verification and retry logic
- **Refund Management**: Full and partial refund support with validation
- **Payment Status Caching**: Redis-backed caching for improved performance

### Architecture
- **Clean Architecture**: Domain-driven design with clear separation of concerns (Core → Infrastructure → API)
- **Repository Pattern**: Abstracted data access layer
- **CQRS-lite**: Command/query separation for payment operations
- **Event-Driven**: Asynchronous event publishing via RabbitMQ/MassTransit
- **Resilience**: Polly-based retry policies, circuit breakers, and timeouts

### Monitoring & Observability
- **Prometheus Metrics**: Comprehensive metrics for payments, refunds, webhooks, and provider health
- **Structured Logging**: Serilog with correlation ID tracking
- **Health Checks**: Liveness and readiness probes for Kubernetes deployments
- **Distributed Tracing**: Correlation ID propagation across service boundaries

### Security
- **JWT Authentication**: OAuth 2.0 / OpenID Connect support
- **Credential Encryption**: ASP.NET Core Data Protection for sensitive provider credentials
- **Rate Limiting**: Webhook endpoint protection
- **Non-Root Docker**: Security-hardened container images

## Technology Stack

- **.NET 10 (C# 13)**: Latest .NET framework with modern language features
- **ASP.NET Core 10.0**: Web API framework
- **Entity Framework Core 9.0**: ORM with PostgreSQL provider
- **PostgreSQL 18**: Primary database
- **Redis 7.2**: Caching and idempotency storage
- **RabbitMQ 3.13 / MassTransit**: Event messaging
- **Prometheus**: Metrics collection
- **Serilog**: Structured logging
- **FluentValidation**: Request validation
- **Polly**: Resilience and transient fault handling
- **xUnit**: Unit and integration testing

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started) and Docker Compose
- [PostgreSQL 18](https://www.postgresql.org/download/) (or use Docker Compose)
- [Redis 7.2](https://redis.io/download/) (or use Docker Compose)
- [RabbitMQ 3.13](https://www.rabbitmq.com/download.html) (or use Docker Compose)

### Quick Start with Docker Compose

The fastest way to get started is using Docker Compose, which sets up all required services:

```bash
# Clone the repository
git clone https://github.com/yourusername/Maliev.PaymentService.git
cd Maliev.PaymentService

# Start all services (PostgreSQL, Redis, RabbitMQ, API)
docker-compose up -d

# View logs
docker-compose logs -f payment-gateway-api

# Access the API
curl http://localhost:8080/payments/liveness

# Access Prometheus metrics
curl http://localhost:8080/metrics

# Access RabbitMQ Management UI
# http://localhost:15672 (user: payment_user, password: dev_rabbitmq_password)
```

The API will be available at:
- **API**: http://localhost:8080/payments/v1/payments
- **OpenAPI Spec**: http://localhost:8080/payments/openapi/v1.json (development only)
- **Metrics**: http://localhost:8080/payments/metrics
- **Health (Liveness)**: http://localhost:8080/payments/liveness
- **Health (Readiness)**: http://localhost:8080/payments/readiness

### Local Development Setup

#### 1. Install Dependencies

```bash
# Restore NuGet packages
dotnet restore
```

#### 2. Configure Services

Update `appsettings.Development.json` with your local service endpoints:

```json
{
  "ConnectionStrings": {
    "PaymentDatabase": "Host=localhost;Port=5432;Database=payment_gateway;Username=your_user;Password=your_password"
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
  }
}
```

#### 3. Run Database Migrations

```bash
# Navigate to the API project
cd Maliev.PaymentService.Api

# Apply migrations
dotnet ef database update

# Or create a new migration
dotnet ef migrations add YourMigrationName --project ../Maliev.PaymentService.Infrastructure
```

#### 4. Run the Application

```bash
# Run from the API project
dotnet run --project Maliev.PaymentService.Api

# Or with hot reload
dotnet watch --project Maliev.PaymentService.Api
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category!=Integration"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## API Documentation

### Payment Processing

#### Process a Payment

```http
POST /payments/v1/payments
Content-Type: application/json
Authorization: Bearer {your-jwt-token}
Idempotency-Key: {unique-request-id}
X-Correlation-Id: {optional-correlation-id}

{
  "amount": 99.99,
  "currency": "USD",
  "customerId": "cust_12345",
  "orderId": "order_67890",
  "description": "Premium subscription",
  "returnUrl": "https://example.com/payment/success",
  "cancelUrl": "https://example.com/payment/cancel",
  "preferredProvider": "stripe",
  "metadata": {
    "plan": "premium",
    "period": "annual"
  }
}
```

**Response (201 Created):**

```json
{
  "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 99.99,
  "currency": "USD",
  "status": "pending",
  "customerId": "cust_12345",
  "orderId": "order_67890",
  "description": "Premium subscription",
  "selectedProvider": "stripe",
  "providerTransactionId": "pi_3L8K9M2eZvKYlo2C0w8TZ9Gh",
  "paymentUrl": "https://checkout.stripe.com/pay/cs_test_...",
  "createdAt": "2025-11-19T10:30:00Z",
  "updatedAt": "2025-11-19T10:30:00Z"
}
```

#### Get Payment Status

```http
GET /payments/v1/payments/{transactionId}
Authorization: Bearer {your-jwt-token}
```

**Response (200 OK):**

```json
{
  "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 99.99,
  "currency": "USD",
  "status": "completed",
  "selectedProvider": "stripe",
  "completedAt": "2025-11-19T10:35:00Z"
}
```

#### Process a Refund

```http
POST /payments/v1/payments/{transactionId}/refund
Content-Type: application/json
Authorization: Bearer {your-jwt-token}
Idempotency-Key: {unique-refund-request-id}

{
  "amount": 99.99,
  "reason": "Customer requested refund",
  "refundType": "full"
}
```

**Response (200 OK):**

```json
{
  "refundId": "7fa85f64-5717-4562-b3fc-2c963f66afa8",
  "paymentTransactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 99.99,
  "currency": "USD",
  "status": "completed",
  "refundType": "full",
  "reason": "Customer requested refund",
  "initiatedAt": "2025-11-19T11:00:00Z",
  "completedAt": "2025-11-19T11:00:15Z"
}
```

### Webhook Endpoints

Payment providers send webhooks to notify about payment status changes:

```http
POST /payments/v1/webhooks/{providerName}
Content-Type: application/json
X-Provider-Signature: {signature}

{
  "event": "payment.succeeded",
  "data": { ... }
}
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Production |
| `ASPNETCORE_URLS` | HTTP listening URLs | http://+:8080 |
| `ConnectionStrings__PaymentDatabase` | PostgreSQL connection string | - |
| `Redis__Configuration` | Redis connection string | localhost:6379 |
| `Redis__InstanceName` | Redis key prefix | PaymentGateway: |
| `RabbitMQ__Host` | RabbitMQ hostname | localhost |
| `RabbitMQ__Port` | RabbitMQ port | 5672 |
| `JwtAuthentication__Authority` | OAuth authority URL | - |
| `JwtAuthentication__Audience` | JWT audience | payment-gateway-api |

### Provider Configuration

Configure payment providers in the database using the Provider Management API:

```sql
INSERT INTO payment_providers (id, name, is_enabled, priority, configuration)
VALUES (
  gen_random_uuid(),
  'stripe',
  true,
  1,
  '{"apiKey": "encrypted_key", "webhookSecret": "encrypted_secret"}'::jsonb
);
```

## Monitoring

### Prometheus Metrics

The service exposes Prometheus metrics at `/payments/metrics`:

**Key Metrics:**
- `payment_transactions_total`: Total payment transactions by provider, status, currency
- `refund_transactions_total`: Total refund transactions by provider, status
- `webhooks_processed_total`: Total webhooks processed by provider, event type, success
- `payment_duration_seconds`: Payment processing duration histogram
- `webhook_duration_seconds`: Webhook processing duration histogram
- `active_requests`: Current number of active requests
- `provider_health`: Provider health score (0.0 to 1.0)

### Health Checks

- **Liveness**: `/payments/liveness` - Returns 200 if the service is running
- **Readiness**: `/payments/readiness` - Returns 200 if all dependencies (PostgreSQL, Redis, RabbitMQ) are healthy

### Logging

Structured logs are written to:
- **Console**: JSON format for container environments
- **Files**: `logs/payment-gateway-{date}.txt` (30-day retention)

Logs include:
- Correlation IDs for distributed tracing
- Payment transaction details (amounts, providers, statuses)
- Webhook processing events
- Provider health changes
- Error details with stack traces

## Architecture

### Clean Architecture Layers

```
┌─────────────────────────────────────────┐
│         Maliev.PaymentService.Api       │  ← Controllers, Middleware, DTOs
├─────────────────────────────────────────┤
│   Maliev.PaymentService.Infrastructure  │  ← Repositories, Services, Providers
├─────────────────────────────────────────┤
│      Maliev.PaymentService.Core         │  ← Entities, Interfaces, Enums
└─────────────────────────────────────────┘
```

### Data Flow

```
Client Request
    ↓
JWT Middleware → Authentication
    ↓
Correlation ID Middleware → Tracking
    ↓
Request Logging Middleware → Observability
    ↓
PaymentsController
    ↓
PaymentService → Business Logic
    ↓
Idempotency Check (Redis) → Duplicate Detection
    ↓
PaymentRoutingService → Provider Selection
    ↓
Provider Adapter (Stripe/PayPal/etc.) → External API
    ↓
Circuit Breaker → Resilience
    ↓
PaymentRepository → Database Persistence
    ↓
EventPublisher (RabbitMQ) → Async Events
    ↓
Response to Client
```

## Deployment

### Kubernetes

Example deployment manifest:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: payment-gateway-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: payment-gateway-api
  template:
    metadata:
      labels:
        app: payment-gateway-api
    spec:
      containers:
      - name: api
        image: ghcr.io/yourusername/maliev.paymentservice:latest
        ports:
        - containerPort: 8080
        env:
        - name: ConnectionStrings__PaymentDatabase
          valueFrom:
            secretKeyRef:
              name: payment-gateway-secrets
              key: database-url
        livenessProbe:
          httpGet:
            path: /payments/liveness
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /payments/readiness
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
```

### Docker

Build and run the Docker image:

```bash
# Build the image
docker build -t payment-gateway-api -f Maliev.PaymentService.Api/Dockerfile .

# Run the container
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__PaymentDatabase="Host=postgres;Database=payment_gateway;..." \
  -e Redis__Configuration="redis:6379" \
  payment-gateway-api
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Quality Standards

- All code must pass `dotnet format` verification
- Build with `/p:TreatWarningsAsErrors=true`
- Unit test coverage > 80%
- Integration tests for all API endpoints
- Follow Clean Architecture principles

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues and questions:
- **GitHub Issues**: https://github.com/yourusername/Maliev.PaymentService/issues
- **Email**: payment-gateway@example.com

## Acknowledgments

Built with:
- [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [MassTransit](https://masstransit-project.com/)
- [Polly](https://github.com/App-vNext/Polly)
- [Serilog](https://serilog.net/)
- [Prometheus](https://prometheus.io/)
