# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore layer: only project files and the shared build props, so editing source does not
# invalidate the NuGet cache.
COPY Directory.Build.props ./
COPY src/ProviderOptimizer.Domain/ProviderOptimizer.Domain.csproj src/ProviderOptimizer.Domain/
COPY src/ProviderOptimizer.Application/ProviderOptimizer.Application.csproj src/ProviderOptimizer.Application/
COPY src/ProviderOptimizer.Infrastructure/ProviderOptimizer.Infrastructure.csproj src/ProviderOptimizer.Infrastructure/
COPY src/ProviderOptimizer.Api/ProviderOptimizer.Api.csproj src/ProviderOptimizer.Api/
RUN dotnet restore src/ProviderOptimizer.Api/ProviderOptimizer.Api.csproj

COPY src/ src/
RUN dotnet publish src/ProviderOptimizer.Api/ProviderOptimizer.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app ./

USER $APP_UID

ENTRYPOINT ["dotnet", "ProviderOptimizer.Api.dll"]
