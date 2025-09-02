# Maliev.PaymentService

This repository contains the `Maliev.PaymentService` application, a .NET 9 service for managing payment-related operations.

## Migration to .NET 9

This service has been migrated to .NET 9, incorporating modern development standards, improved maintainability, testability, and security. For a detailed overview of the changes made during the migration, please refer to the [GEMINI.md](GEMINI.md) document.

## Getting Started

(Add instructions on how to set up and run the project locally, e.g., prerequisites, `dotnet run` commands, etc.)

## Local Development Secrets

Sensitive information such as JWT keys and database connection strings are managed using .NET User Secrets for local development. To set up your local secrets, run the following commands from the project root directory:

```bash
dotnet user-secrets set "Jwt:Issuer" "https://localhost:5001" --project Maliev.PaymentService.Api
dotnet user-secrets set "Jwt:Audience" "https://localhost:5001" --project Maliev.PaymentService.Api
dotnet user-secrets set "JwtSecurityKey" "YOUR_SECURE_JWT_KEY_HERE" --project Maliev.PaymentService.Api
dotnet user-secrets set "ConnectionStrings:PaymentServiceDbContext" "Server=(localdb)\mssqllocaldb;Database=PaymentServiceDb;Trusted_Connection=True;MultipleActiveResultSets=true" --project Maliev.PaymentService.Api
```

**Note:** Replace `YOUR_SECURE_JWT_KEY_HERE` with a strong, randomly generated key.

## Deployment

For production deployments, secrets are managed via Google Secret Manager. Ensure the `maliev-shared-secrets` `SecretProviderClass` is correctly configured in your Kubernetes cluster to fetch the necessary secrets.

## API Documentation

The API documentation is available via Swagger UI when running the application in the development environment. Navigate to `/swagger` in your browser after starting the application.

```