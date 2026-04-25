FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first (better Docker layer caching)
COPY vfr-backend.sln ./
COPY API/API.csproj API/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Shared/Shared.csproj Shared/
COPY Tests.Unit/Tests.Unit.csproj Tests.Unit/

# Restore NuGet packages
RUN dotnet restore vfr-backend.sln

# Copy everything else
COPY . .

# Publish the API project in Release mode
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ═══════════════════════════════════════════════════════════════
# Stage 2: Runtime
# ═══════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

# Switch to non-root user
USER appuser

# Expose port (Render will set PORT env var)
EXPOSE 8080

# Set environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "API.dll"]