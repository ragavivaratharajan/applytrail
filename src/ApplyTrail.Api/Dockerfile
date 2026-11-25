# syntax=docker/dockerfile:1
# Build context is the repository root:  docker build -f src/ApplyTrail.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Restore first, using only the project files, so this layer is cached until a dependency changes.
COPY Directory.Build.props ./
COPY src/ApplyTrail.Domain/*.csproj src/ApplyTrail.Domain/
COPY src/ApplyTrail.Application/*.csproj src/ApplyTrail.Application/
COPY src/ApplyTrail.Infrastructure/*.csproj src/ApplyTrail.Infrastructure/
COPY src/ApplyTrail.Api/*.csproj src/ApplyTrail.Api/
RUN dotnet restore src/ApplyTrail.Api/ApplyTrail.Api.csproj

COPY src/ src/
RUN dotnet publish src/ApplyTrail.Api/ApplyTrail.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    ConnectionStrings__Default="Data Source=/data/applytrail.db"

# The built-in non-root "app" user owns the data folder, which is mounted as a volume.
RUN mkdir -p /data && chown app:app /data
USER app

COPY --from=build /app .

VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApplyTrail.Api.dll"]
