# Multi-stage build for the Soarscore API (net10.0).
# The store is selected at runtime by Soarscore:Store (postgres | sqlite);
# the image defaults to sqlite and persists its database in /data.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better layer caching on restore
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Soarscore.Domain/Soarscore.Domain.csproj src/Soarscore.Domain/
COPY src/Soarscore.Application/Soarscore.Application.csproj src/Soarscore.Application/
COPY src/Soarscore.Infrastructure/Soarscore.Infrastructure.csproj src/Soarscore.Infrastructure/
COPY src/Soarscore.Api/Soarscore.Api.csproj src/Soarscore.Api/
RUN dotnet restore src/Soarscore.Api

COPY src/ src/
RUN dotnet publish src/Soarscore.Api -c Release -o /app --no-restore --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# The frozen seed corpus — the sixteen FAI and NZ class definitions the seed
# tool emits and byte-verifies. ClassCorpusSeederHost publishes them through
# the ordinary PublishClassDefinition command at startup (idempotent by
# content hash, so every boot is safe). Tapes are test fixtures, not
# deployment data, and are excluded by .dockerignore.
COPY tools/Soarscore.SeedData/json/ /app/seed/

# SQLite database location (default store). For postgres instead:
#   docker run -e SOARSCORE_STORE=postgres \
#     -e SOARSCORE_CONNECTION_STRING="Host=...;Database=...;Username=...;Password=..." ...
ENV SOARSCORE_STORE=sqlite \
    ConnectionStrings__Soarscore="Data Source=/data/soarscore.db"
RUN mkdir -p /data && chown app:app /data
VOLUME /data

EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Soarscore.Api.dll"]
