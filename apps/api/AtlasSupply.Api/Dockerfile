FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY apps/api/AtlasSupply.Api/AtlasSupply.Api.csproj apps/api/AtlasSupply.Api/
COPY libs/AtlasSupply.Application/AtlasSupply.Application.csproj libs/AtlasSupply.Application/
COPY libs/AtlasSupply.Domain/AtlasSupply.Domain.csproj libs/AtlasSupply.Domain/
COPY libs/AtlasSupply.Infrastructure/AtlasSupply.Infrastructure.csproj libs/AtlasSupply.Infrastructure/
RUN dotnet restore apps/api/AtlasSupply.Api/AtlasSupply.Api.csproj

COPY apps/api/AtlasSupply.Api/ apps/api/AtlasSupply.Api/
COPY libs/AtlasSupply.Application/ libs/AtlasSupply.Application/
COPY libs/AtlasSupply.Domain/ libs/AtlasSupply.Domain/
COPY libs/AtlasSupply.Infrastructure/ libs/AtlasSupply.Infrastructure/
RUN dotnet publish apps/api/AtlasSupply.Api/AtlasSupply.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet AtlasSupply.Api.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
