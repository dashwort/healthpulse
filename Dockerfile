# Build stage
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
RUN dotnet publish "src/HealthTracker.Web/HealthTracker.Web.csproj" --configuration Release --output /app/publish --property UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HealthTracker.Web.dll"]
