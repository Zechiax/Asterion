# Self-hosting

## Docker

Images for `linux/amd64` and `linux/arm64` are published to `ghcr.io/zechiax/asterion`:
- `latest` - the newest release, `4` / `4.0` / `4.0.0` - pinned releases
- `next` - the current `main` branch

Everything the bot stores (`config.toml`, `data.sqlite`, `logs/`) lives in `./data` next to `compose.yml`.

```bash
cp .env.example .env      # fill in ASTERION_TOKEN (and PUID/PGID if not 1000)
docker compose up -d
```

Any value from `config.toml` can also be set as an environment variable with the `ASTERION_` prefix, e.g. `ASTERION_TOKEN`.

## Configuration

<!-- notes: config.toml options -->
