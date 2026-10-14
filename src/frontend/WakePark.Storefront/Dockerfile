# Veřejný web (Storefront). Staví se z kořene repa kvůli společným souborům buildu:
#   docker build -f src/frontend/WakePark.Storefront/Dockerfile -t wakepark-storefront .

# --- build: SDK jen pro překlad, do výsledného obrazu se nedostane ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Nejdřív jen to, co ovlivňuje restore. Dokud se nezmění projekt ani verze balíčků,
# Docker použije vrstvu z cache a balíčky znovu nestahuje.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/frontend/WakePark.Storefront/WakePark.Storefront.csproj src/frontend/WakePark.Storefront/
RUN dotnet restore src/frontend/WakePark.Storefront/WakePark.Storefront.csproj

COPY src/frontend/WakePark.Storefront/ src/frontend/WakePark.Storefront/
RUN dotnet publish src/frontend/WakePark.Storefront/WakePark.Storefront.csproj \
    --configuration Release --no-restore --output /app

# --- runtime: jen ASP.NET Core runtime, bez SDK a zdrojáků ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app .

# Obraz má připraveného uživatele bez práv roota. Když se aplikace někdy nechá
# zneužít, útočník v kontejneru není root.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "WakePark.Storefront.dll"]
