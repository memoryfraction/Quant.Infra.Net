# ── Stage 1: Build ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all source (build context = src/)
COPY . .

# Restore & publish the MVC entry-point project
RUN dotnet publish Saas.Infra.MVC/Saas.Infra.MVC.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

# ── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

# .NET 8 ASP.NET container image defaults to port 8080 via ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Saas.Infra.MVC.dll"]
