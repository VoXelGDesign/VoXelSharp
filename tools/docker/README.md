# Container dev scripts

Quick helpers for iterating on the server in Docker, so you don't hand-type the long
`docker build` / `docker run` flags.

## Usage

Windows / Docker Desktop (PowerShell):

```powershell
tools\docker\dev.ps1 build
tools\docker\dev.ps1 up          # foreground
tools\docker\dev.ps1 up -d       # detached, then print hints (logs / down)
tools\docker\dev.ps1 logs
tools\docker\dev.ps1 down
tools\docker\dev.ps1 exec ...
```

Unix shell (Linux / WSL / Git Bash):

```bash
tools/docker/dev.sh build
tools/docker/dev.sh up
tools/docker/dev.sh up -d
tools/docker/dev.sh logs
tools/docker/dev.sh down
tools/docker/dev.sh exec ...
```

The scripts run from the repository root and use the same hardening the compose file does
(read-only root fs, dropped capabilities, no-new-privileges, 30s stop timeout).

## Data directory

The container binds:

| Host               | Container              |
|--------------------|------------------------|
| `data/world`       | `/app/data/world`      |
| `data/plugins`     | `/app/data/plugins` (ro) |

The chiseled runtime image has no shell and runs as non-root UID **1654** (`app`). The
scripts create `data/world` and `data/plugins` on the host and chown them to 1654 as a best
effort. On Docker Desktop the engine filesystem usually lets the non-root process write
through the mount regardless; on plain Linux, if the server ever fails to write the world,
run `sudo chown -R 1654:1654 data/world data/plugins` once.

## Notes

- Manual equivalent (compose):

  ```bash
  docker compose up --build -d
  docker compose down
  ```

- `up` runs in the foreground: Ctrl+C stops and removes the container (`--rm`). Use `up -d` if
  you want it to keep running in the background — it returns immediately, so call `logs` to
  follow output and `down` to stop it. `exec` only works with commands present in the chiseled
  image (it has no shell, no coreutils).

- There is intentionally no shell inside the container (chiseled + `read_only`), so
  the scripts' `exec` is mainly useful for debugging with the image's own entrypoint, not for
  interactive shells.