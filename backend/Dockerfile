# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Motivators.sln ./
COPY src/Motivators.Domain/Motivators.Domain.csproj src/Motivators.Domain/
COPY src/Motivators.Application/Motivators.Application.csproj src/Motivators.Application/
COPY src/Motivators.Infrastructure/Motivators.Infrastructure.csproj src/Motivators.Infrastructure/
COPY src/Motivators.Api/Motivators.Api.csproj src/Motivators.Api/
RUN dotnet restore src/Motivators.Api/Motivators.Api.csproj
COPY src/ src/
RUN dotnet publish src/Motivators.Api/Motivators.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Motivators.Api.dll"]
