# syntax=docker/dockerfile:1
# Build context is the repository root: docker build -f src/StayHost.Web/Dockerfile .

# The React SPA is built first and its output is copied into wwwroot, so the
# .NET publish step ships the compiled bundle rather than the source.
FROM node:22-alpine AS client
WORKDIR /client

COPY src/StayHost.Web/ClientApp/package.json src/StayHost.Web/ClientApp/package-lock.json ./
RUN npm ci

COPY src/StayHost.Web/ClientApp/ ./
# vite.config.js writes to ../wwwroot; give it a directory to land in.
RUN mkdir -p /wwwroot && npm run build -- --outDir /wwwroot --emptyOutDir

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Restore first so the layer caches on dependency changes only.
COPY StayHost.sln ./
COPY src/StayHost.Domain/StayHost.Domain.csproj src/StayHost.Domain/
COPY src/StayHost.Infrastructure/StayHost.Infrastructure.csproj src/StayHost.Infrastructure/
COPY src/StayHost.Web/StayHost.Web.csproj src/StayHost.Web/
RUN dotnet restore src/StayHost.Web/StayHost.Web.csproj

COPY . .
COPY --from=client /wwwroot/ src/StayHost.Web/wwwroot/
RUN dotnet publish src/StayHost.Web/StayHost.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# The aspnet:9.0-alpine base already ships a non-root `app` user; reuse it.
RUN apk add --no-cache icu-libs tzdata wget
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    TZ=Asia/Ho_Chi_Minh

COPY --from=build /app/publish .
RUN chown -R app:app /app
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "StayHost.Web.dll"]
