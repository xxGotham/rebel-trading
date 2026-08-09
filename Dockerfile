FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY backend/Directory.Build.props backend/

COPY backend/src/Rebel.Core/Rebel.Core.csproj backend/src/Rebel.Core/
COPY backend/src/Rebel.Infrastructure/Rebel.Infrastructure.csproj backend/src/Rebel.Infrastructure/
COPY backend/src/Rebel.Api/Rebel.Api.csproj backend/src/Rebel.Api/

RUN dotnet restore backend/src/Rebel.Api/Rebel.Api.csproj

COPY backend/src/ backend/src/

RUN dotnet publish backend/src/Rebel.Api/Rebel.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Rebel.Api.dll"]
