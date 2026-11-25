# Deploying ApplyTrail

ApplyTrail is two containers (nginx serving the Angular app, and the .NET API) plus one volume for the SQLite file. Anything that can run Docker Compose can host it.

## Before you expose it to the internet

1. **Serve it over https.** Put a TLS-terminating proxy in front (Caddy, a platform load balancer, Cloudflare). Then set `AUTH_COOKIE_SECURE=true`.
2. **Set a real `JWT_SIGNING_KEY`**, at least 32 random characters, and keep it out of git.
3. **Create your account, then set `ALLOW_REGISTRATION=false`** and restart, so strangers cannot sign up.
4. **Leave `SEED_DEMO_DATA=false`.** The demo account has a published password.
5. **Plan a backup** (below).

## A small server (VPS or a free-tier VM)

Any Linux machine with Docker works. Free-tier offers from cloud providers change often, so check the current terms before relying on one.

```bash
git clone <your-repo-url> applytrail && cd applytrail
cp .env.example .env     # set JWT_SIGNING_KEY, AUTH_COOKIE_SECURE=true, ALLOW_REGISTRATION=false afterwards
docker compose up -d --build
```

### HTTPS with Caddy

Caddy gets and renews certificates by itself. Point a DNS record at the server, then run Caddy next to the stack, for example in a file `docker-compose.override.yml`:

```yaml
services:
  web:
    ports: !reset []     # only Caddy is public (needs Docker Compose 2.24 or newer)
  caddy:
    image: caddy:2
    restart: unless-stopped
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
    depends_on: [web]
volumes:
  caddy-data:
```

and a `Caddyfile`:

```
applytrail.example.com {
    reverse_proxy web:8080
}
```

nginx already forwards the original scheme (`X-Forwarded-Proto`) and client address to the API, so rate limiting and secure cookies work behind Caddy.

## Container platforms

Deploy the two images (`src/ApplyTrail.Api/Dockerfile` with the repository root as build context, and `web/Dockerfile` with `web` as context). The web container proxies to a host named `api` on port 8080, so give the API that name on the platform's private network, or edit `web/nginx.conf`. Mount a persistent volume at `/data` on the API container. SQLite needs a real disk, so a platform with only ephemeral storage will lose your data on every restart.

## Backups

- **Easiest:** Settings, then Download JSON backup, in the app.
- **Whole database:** stop the stack, copy the volume, start again.

  ```bash
  docker compose stop api
  docker run --rm -v applytrail_applytrail-data:/data -v "$PWD":/backup alpine sh -c 'cp /data/applytrail.db* /backup/'
  docker compose start api
  ```

  The database runs in write-ahead-log mode, so copy the `-wal` and `-shm` files with it, and do it while the API is stopped.

## Upgrading

```bash
git pull
docker compose up -d --build
```

If a release includes a database migration it is applied automatically at startup. Back up first.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `JWT_SIGNING_KEY` error when starting | The variable is missing from `.env`, or shorter than 32 characters. |
| Signed in, then immediately signed out | `AUTH_COOKIE_SECURE=true` while using plain `http://`. Use https, or set it to `false` for local use only. |
| `502 Bad Gateway` on `/api` | The API container is still starting or has stopped. Check `docker compose logs api`. |
| "Too many attempts" | The sign-in rate limit. Wait a minute. |
