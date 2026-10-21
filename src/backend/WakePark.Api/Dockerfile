# Backendové API. Staví se z kořene repa kvůli společným souborům buildu:
#   docker build -f src/backend/WakePark.Api/Dockerfile -t wakepark-api .
#
# Stejný obraz dělá i kroky nasazení — nahradí se jen argument:
#   docker compose run --rm api migrate          migrace všech modulů
#   docker compose run --rm api import-catalog   nabídka e-shopu z data/katalog.json

# --- build: SDK jen pro překlad, do výsledného obrazu se nedostane ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Nejdřív jen projekty a verze balíčků, aby se restore bral z cache, dokud se nezmění.
# Projektů je hodně a Docker neumí kopírovat se zachováním složek podle masky, proto
# se zkopíruje celý backend a smažou se zdrojáky — zbudou jen *.csproj ve správných složkách.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/backend/ /tmp/backend/
RUN cd /tmp/backend \
    && find . -name '*.csproj' -exec install -D {} /src/src/backend/{} \; \
    && rm -rf /tmp/backend
RUN dotnet restore src/backend/WakePark.Api/WakePark.Api.csproj

COPY src/backend/ src/backend/
RUN dotnet publish src/backend/WakePark.Api/WakePark.Api.csproj \
    --configuration Release --no-restore --output /app

# --- runtime: jen ASP.NET Core runtime, bez SDK a zdrojáků ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app .

# Uživatel bez práv roota — když se aplikace nechá zneužít, útočník v kontejneru není root.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "WakePark.Api.dll"]
