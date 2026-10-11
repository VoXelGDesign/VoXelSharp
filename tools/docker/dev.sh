#!/usr/bin/env bash
#
# VoXelSharp container dev helper.
#
# Usage:
#   ./dev.sh build            Build the server image only
#   ./dev.sh up               Build + run the container (foreground)
#   ./dev.sh up -d            Build + run detached, then print hints (logs / down)
#   ./dev.sh down             Stop and remove the container
#   ./dev.sh logs             Stream container logs (follow)
#   ./dev.sh exec <cmd...>    Run a command inside the running container (no shell in chiseled image)
#
# Requirements:
#   - Docker (daemon running)
#   - On Docker Desktop / Linux, the container runs as UID 1654 (chiseled non-root `app`).
#     Bind-mounted host dirs `data/world` and `data/plugins` are created here and chowned to
#     1654 so the server can write the world back out (M6 persistence).
set -euo pipefail
cd "$(dirname "$0")/../.."   # repo root

IMAGE="voxelsharp:dev"
DATA_DIR="${DATA_DIR:-data}"

ensure_data_dirs() {
  mkdir -p "$DATA_DIR/world" "$DATA_DIR/plugins"
  # Best effort: make the dirs writable by the container's UID 1654. On Docker Desktop's
  # WSL/VM filesystems chown may be a no-op, which is fine (the mount is writable through the
  # engine anyway). If the server can't write the world, run this step with sudo on Linux.
  if command -v docker >/dev/null 2>&1; then
    docker run --rm -v "$PWD/$DATA_DIR:/data" alpine:3.20 sh -c 'chown -R 1654:1654 /data/world /data/plugins' >/dev/null 2>&1 || true
  fi
}

build() {
  echo "==> Building $IMAGE"
  docker build -t "$IMAGE" .
}

up() {
  ensure_data_dirs
  build
  # We can't easily pass DATA_DIR into compose's static binds, so run directly with the same
  # hardening flags the compose file uses (read-only fs, ports, restart, cap drop).
  local detach=""
  if [[ "$1" == "-d" ]]; then detach="-d"; shift || true; fi
  docker run $detach --rm --name voxelsharp-dev \
    -p 25565:25565 \
    -v "$PWD/$DATA_DIR/world:/app/data/world" \
    -v "$PWD/$DATA_DIR/plugins:/app/data/plugins:ro" \
    --read-only \
    --tmpfs /tmp \
    --cap-drop ALL \
    --security-opt no-new-privileges:true \
    --stop-timeout 30 \
    "$IMAGE"
  if [[ -n "$detach" ]]; then
    echo "Container voxelsharp-dev is running. Use:"
    echo "   tools/docker/dev.sh logs"
    echo "   tools/docker/dev.sh down"
  fi
}

down() {
  echo "==> Stopping voxelsharp-dev"
  docker rm -f voxelsharp-dev >/dev/null 2>&1 || true
}

logs() {
  docker logs -f voxelsharp-dev
}

exec() {
  docker exec voxelsharp-dev "$@"
}

case "${1:-up}" in
  build) shift; build "$@";;
  up)    shift; up "${1:-}";;
  down)  down;;
  logs)  logs;;
  exec)  shift; exec "$@";;
  *) echo "unknown command: $1 (build|up|down|logs|exec)"; exit 2;;
esac