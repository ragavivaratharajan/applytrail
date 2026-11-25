# syntax=docker/dockerfile:1
# Build context: the web/ folder.   docker build -t applytrail-web web

FROM node:26-alpine AS build
WORKDIR /app

# Install first, from the lockfile only, so this layer is cached until a dependency changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/web/browser /usr/share/nginx/html

EXPOSE 8080
