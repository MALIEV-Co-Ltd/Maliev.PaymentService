# syntax=docker/dockerfile:1.4
# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG SERVICE_DEFAULTS_VERSION=1.0.81-alpha
ARG MESSAGING_CONTRACTS_VERSION=1.0.91-alpha
ENV GITHUB_ACTIONS=true
WORKDIR /src

# Copy nuget.config and project files
COPY ["nuget.config", "."]
COPY ["Directory.Build.props", "."]
COPY ["Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj", "Maliev.PaymentService.Api/"]
COPY ["Maliev.PaymentService.Domain/Maliev.PaymentService.Domain.csproj", "Maliev.PaymentService.Domain/"]
COPY ["Maliev.PaymentService.Application/Maliev.PaymentService.Application.csproj", "Maliev.PaymentService.Application/"]
COPY ["Maliev.PaymentService.Infrastructure/Maliev.PaymentService.Infrastructure.csproj", "Maliev.PaymentService.Infrastructure/"]

# Restore with GitHub Packages authentication using BuildKit secrets
RUN --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
    NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
    dotnet restore "./Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj" \
      -p:ServiceDefaultsVersion=$SERVICE_DEFAULTS_VERSION \
      -p:MessagingContractsVersion=$MESSAGING_CONTRACTS_VERSION

COPY . .
WORKDIR "/src/Maliev.PaymentService.Api"
RUN dotnet build "./Maliev.PaymentService.Api.csproj" \
      -c $BUILD_CONFIGURATION \
      --no-restore \
      -o /app/build \
      -p:ServiceDefaultsVersion=$SERVICE_DEFAULTS_VERSION \
      -p:MessagingContractsVersion=$MESSAGING_CONTRACTS_VERSION

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG SERVICE_DEFAULTS_VERSION=1.0.81-alpha
ARG MESSAGING_CONTRACTS_VERSION=1.0.91-alpha
RUN dotnet publish "./Maliev.PaymentService.Api.csproj" \
      -c $BUILD_CONFIGURATION \
      --no-restore \
      -o /app/publish \
      /p:UseAppHost=false \
      -p:ServiceDefaultsVersion=$SERVICE_DEFAULTS_VERSION \
      -p:MessagingContractsVersion=$MESSAGING_CONTRACTS_VERSION

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app

# Ensure 'app' owns the workdir (app user already exists in ASP.NET runtime image)
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Copy published application (now owned by app)
COPY --chown=app:app --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/payment/liveness || exit 1

ENTRYPOINT ["dotnet", "Maliev.PaymentService.Api.dll"]
