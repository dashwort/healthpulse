# Build stage
FROM node:24-alpine AS frontend
WORKDIR /src/src/HealthTracker.Web/ClientApp
RUN npm install --global pnpm@11.24.0
COPY ["src/HealthTracker.Web/ClientApp/package.json", "src/HealthTracker.Web/ClientApp/pnpm-lock.yaml", "./"]
RUN pnpm install --frozen-lockfile
COPY ["src/HealthTracker.Web/ClientApp", "./"]
RUN pnpm test && pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["HealthTracker.slnx", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/HealthTracker.Domain/HealthTracker.Domain.csproj", "src/HealthTracker.Domain/"]
COPY ["src/HealthTracker.Application/HealthTracker.Application.csproj", "src/HealthTracker.Application/"]
COPY ["src/HealthTracker.Infrastructure/HealthTracker.Infrastructure.csproj", "src/HealthTracker.Infrastructure/"]
COPY ["src/HealthTracker.Web/HealthTracker.Web.csproj", "src/HealthTracker.Web/"]
RUN dotnet restore "src/HealthTracker.Web/HealthTracker.Web.csproj"

COPY src ./src
COPY --from=frontend /src/src/HealthTracker.Web/wwwroot/app ./src/HealthTracker.Web/wwwroot/app
RUN dotnet publish "src/HealthTracker.Web/HealthTracker.Web.csproj" --configuration Release --output /app/publish --property UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ARG DEPLOYMENT_VERSION=development
ARG DEPLOYMENT_BUILD=local
ARG DEPLOYMENT_COMMIT=local
ARG DEPLOYMENT_BUILT_AT_UTC=Not_available
RUN mkdir -p /app/App_Data/Logs && chown -R $APP_UID:$APP_UID /app/App_Data
# Configuration values that are safe to publish as image defaults. The empty
# values are deliberate: they advertise the required runtime settings without
# embedding credentials or an installation-specific administrator address.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__HealthTracker="Data Source=/app/App_Data/healthtracker.db" \
    AccessControl__InitialAdministratorEmail="" \
    Authentication__OpenIdConnect__Authority="" \
    Authentication__OpenIdConnect__ClientId="" \
    Authentication__OpenIdConnect__ClientSecret="" \
    AccessActivity__TrustedProxyNetworks__0="" \
    Mobile__Android__LatestVersion="" \
    Mobile__Android__ApkUrl="" \
    Mobile__Android__ReleaseNotes="" \
    Mobile__Android__ReleaseRepository="dashwort/healthpulse" \
    Deployment__Version="${DEPLOYMENT_VERSION}" \
    Deployment__Build="${DEPLOYMENT_BUILD}" \
    Deployment__Commit="${DEPLOYMENT_COMMIT}" \
    Deployment__BuiltAtUtc="${DEPLOYMENT_BUILT_AT_UTC}"
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HealthTracker.Web.dll"]
