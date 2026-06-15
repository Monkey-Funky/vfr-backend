# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for NuGet restore layer caching.
# This means NuGet restore only re-runs when a .csproj or .sln file changes.
COPY vfr-backend.sln .
COPY API/API.csproj API/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Shared/Shared.csproj Shared/
COPY Tests.Unit/Tests.Unit.csproj Tests.Unit/
COPY Tests.Integration/Tests.Integration.csproj Tests.Integration/

RUN dotnet restore vfr-backend.sln

# Copy everything else and publish the API project.
COPY . .
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Security: run as non-root user.
RUN useradd --no-create-home --home-dir /app appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "API.dll"]
