# ==============================================================================
# Stage 1: Build & Publish (Multi-Stage Build para Otimização em Produção)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/GameStore.Payments.Domain/GameStore.Payments.Domain.csproj", "src/GameStore.Payments.Domain/"]
COPY ["src/GameStore.Payments.Application/GameStore.Payments.Application.csproj", "src/GameStore.Payments.Application/"]
COPY ["src/GameStore.Payments.Infrastructure/GameStore.Payments.Infrastructure.csproj", "src/GameStore.Payments.Infrastructure/"]
COPY ["src/GameStore.Payments.Api/GameStore.Payments.Api.csproj", "src/GameStore.Payments.Api/"]

RUN dotnet restore "src/GameStore.Payments.Api/GameStore.Payments.Api.csproj"

COPY src/ src/
WORKDIR "/src/src/GameStore.Payments.Api"
RUN dotnet publish "GameStore.Payments.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==============================================================================
# Stage 2: Runtime enxuto (ASP.NET Core Runtime)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GameStore.Payments.Api.dll"]

