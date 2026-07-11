# syntax=docker/dockerfile:1.7

ARG dependency_restore_stage=restore-private

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-base
ARG BUILD_CONFIGURATION=Release
ARG SERVICE_DEFAULTS_VERSION=1.0.81-alpha
ARG MESSAGING_CONTRACTS_VERSION=1.0.91-alpha
ENV GITHUB_ACTIONS=true
WORKDIR /src

COPY ["nuget.config", "."]
COPY ["NuGet.PRValidation.Config", "."]
COPY ["Directory.Build.props", "."]
COPY ["Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj", "Maliev.PaymentService.Api/"]
COPY ["Maliev.PaymentService.Domain/Maliev.PaymentService.Domain.csproj", "Maliev.PaymentService.Domain/"]
COPY ["Maliev.PaymentService.Application/Maliev.PaymentService.Application.csproj", "Maliev.PaymentService.Application/"]
COPY ["Maliev.PaymentService.Infrastructure/Maliev.PaymentService.Infrastructure.csproj", "Maliev.PaymentService.Infrastructure/"]
COPY [".ci-packages/", ".ci-packages/"]

FROM build-base AS restore-private
RUN --mount=type=secret,id=nuget_username,required=true \
    --mount=type=secret,id=nuget_password,required=true \
    NUGET_USERNAME="$(cat /run/secrets/nuget_username)" \
    NUGET_PASSWORD="$(cat /run/secrets/nuget_password)" \
    dotnet restore "./Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj" \
      -p:ServiceDefaultsVersion=$SERVICE_DEFAULTS_VERSION \
      -p:MessagingContractsVersion=$MESSAGING_CONTRACTS_VERSION

FROM build-base AS restore-local
RUN dotnet restore "./Maliev.PaymentService.Api/Maliev.PaymentService.Api.csproj" \
      --configfile "NuGet.PRValidation.Config" \
      -p:ServiceDefaultsVersion=$SERVICE_DEFAULTS_VERSION \
      -p:MessagingContractsVersion=$MESSAGING_CONTRACTS_VERSION

FROM ${dependency_restore_stage} AS build
COPY . .
WORKDIR "/src/Maliev.PaymentService.Api"
RUN dotnet publish "./Maliev.PaymentService.Api.csproj" \
      -c $BUILD_CONFIGURATION \
      --no-restore \
      -o /app/publish \
      /p:UseAppHost=false \
      -p:ServiceDefaultsVersion=$SERVICE_DEFAULTS_VERSION \
      -p:MessagingContractsVersion=$MESSAGING_CONTRACTS_VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .

USER app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "Maliev.PaymentService.Api.dll"]
