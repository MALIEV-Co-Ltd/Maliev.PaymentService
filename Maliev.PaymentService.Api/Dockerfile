# Multi-stage Dockerfile for Payment Gateway Service
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies (layer caching)
COPY ["Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj", "Maliev.PaymentService.Api/"]
COPY ["Maliev.PaymentService.Core/Maliev.PaymentService.Core.csproj", "Maliev.PaymentService.Core/"]
COPY ["Maliev.PaymentService.Infrastructure/Maliev.PaymentService.Infrastructure.csproj", "Maliev.PaymentService.Infrastructure/"]
RUN dotnet restore "Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/Maliev.PaymentService.Api"
RUN dotnet build "Maliev.PaymentService.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "Maliev.PaymentService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r app && useradd -r -g app app

# Copy published application
COPY --from=publish /app/publish .

# Set ownership to app user
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Configure health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/payments/readiness || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "Maliev.PaymentService.Api.dll"]
