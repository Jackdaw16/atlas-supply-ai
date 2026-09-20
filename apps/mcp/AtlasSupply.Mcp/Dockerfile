FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY apps/mcp/AtlasSupply.Mcp/AtlasSupply.Mcp.csproj apps/mcp/AtlasSupply.Mcp/
COPY libs/AtlasSupply.Application/AtlasSupply.Application.csproj libs/AtlasSupply.Application/
COPY libs/AtlasSupply.Domain/AtlasSupply.Domain.csproj libs/AtlasSupply.Domain/
COPY libs/AtlasSupply.Infrastructure/AtlasSupply.Infrastructure.csproj libs/AtlasSupply.Infrastructure/
RUN dotnet restore apps/mcp/AtlasSupply.Mcp/AtlasSupply.Mcp.csproj

COPY apps/mcp/AtlasSupply.Mcp/ apps/mcp/AtlasSupply.Mcp/
COPY libs/AtlasSupply.Application/ libs/AtlasSupply.Application/
COPY libs/AtlasSupply.Domain/ libs/AtlasSupply.Domain/
COPY libs/AtlasSupply.Infrastructure/ libs/AtlasSupply.Infrastructure/
RUN dotnet publish apps/mcp/AtlasSupply.Mcp/AtlasSupply.Mcp.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet AtlasSupply.Mcp.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
