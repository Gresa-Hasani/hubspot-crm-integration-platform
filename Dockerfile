FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CrmIntegration.sln .
COPY src/CrmIntegration.Api/CrmIntegration.Api.csproj src/CrmIntegration.Api/
COPY src/CrmIntegration.Application/CrmIntegration.Application.csproj src/CrmIntegration.Application/
COPY src/CrmIntegration.Domain/CrmIntegration.Domain.csproj src/CrmIntegration.Domain/
COPY src/CrmIntegration.Infrastructure/CrmIntegration.Infrastructure.csproj src/CrmIntegration.Infrastructure/
RUN dotnet restore src/CrmIntegration.Api/CrmIntegration.Api.csproj

COPY src/ src/
RUN dotnet publish src/CrmIntegration.Api/CrmIntegration.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "CrmIntegration.Api.dll"]
