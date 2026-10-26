# Backoffice (Angular) za nginxem. Staví se z kořene repa jako ostatní obrazy:
#   docker build -f src/frontend/wakepark-web/Dockerfile -t wakepark-backoffice .
#
# Obraz obsahuje jen přeložené statické soubory a nginx, který /api posílá na API
# (http://api:8080) ve stejné síti Dockeru — ADR 0010.

# --- build: Node jen pro překlad ---
FROM node:24-alpine AS build
WORKDIR /src

# Nejdřív jen manifesty, aby se npm ci bral z cache, dokud se závislosti nezmění.
COPY src/frontend/wakepark-web/package.json src/frontend/wakepark-web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY src/frontend/wakepark-web/ ./
RUN npm run build

# --- runtime: nginx bez práv roota, poslouchá na 8080 ---
FROM nginxinc/nginx-unprivileged:1.31-alpine AS runtime

COPY src/frontend/wakepark-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/wakepark-web/browser/ /usr/share/nginx/html/

EXPOSE 8080
