# Multi-stage Docker build for Maliev.PaymentService.Api
# Based on MALIEV Co. Ltd. standard .NET 10 microservice template

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and nuget.config (exclude Tests - filtered by .dockerignore)
COPY nuget.config ./
COPY Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj Maliev.PaymentService.Api/
COPY Maliev.PaymentService.Core/Maliev.PaymentService.Core.csproj Maliev.PaymentService.Core/
COPY Maliev.PaymentService.Infrastructure/Maliev.PaymentService.Infrastructure.csproj Maliev.PaymentService.Infrastructure/

# Restore dependencies with private NuGet feed credentials
RUN --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
    NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
    dotnet restore Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj

# Copy all source code
COPY . .

# Build and publish
WORKDIR /src/Maliev.PaymentService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Ensure 'app' user owns the workdir (app user already exists in ASP.NET runtime image)
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Copy published application (now owned by app)
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set ASP.NET Core environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/payments/liveness || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Maliev.PaymentService.Api.dll"]
