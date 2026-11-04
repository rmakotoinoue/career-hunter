FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/CareerHunter.Domain/CareerHunter.Domain.csproj src/CareerHunter.Domain/
COPY src/CareerHunter.Application/CareerHunter.Application.csproj src/CareerHunter.Application/
COPY src/CareerHunter.Infrastructure/CareerHunter.Infrastructure.csproj src/CareerHunter.Infrastructure/
COPY src/CareerHunter.Worker/CareerHunter.Worker.csproj src/CareerHunter.Worker/

RUN dotnet restore src/CareerHunter.Worker/CareerHunter.Worker.csproj

COPY src/ src/
RUN dotnet publish src/CareerHunter.Worker/CareerHunter.Worker.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CareerHunter.Worker.dll"]
