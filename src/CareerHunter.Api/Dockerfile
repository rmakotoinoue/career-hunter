FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/CareerHunter.Domain/CareerHunter.Domain.csproj src/CareerHunter.Domain/
COPY src/CareerHunter.Application/CareerHunter.Application.csproj src/CareerHunter.Application/
COPY src/CareerHunter.Infrastructure/CareerHunter.Infrastructure.csproj src/CareerHunter.Infrastructure/
COPY src/CareerHunter.Api/CareerHunter.Api.csproj src/CareerHunter.Api/

RUN dotnet restore src/CareerHunter.Api/CareerHunter.Api.csproj

COPY src/ src/
RUN dotnet publish src/CareerHunter.Api/CareerHunter.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CareerHunter.Api.dll"]
