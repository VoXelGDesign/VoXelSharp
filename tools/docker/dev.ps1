# VoXelSharp container dev helper (Windows / Docker Desktop PowerShell, PS 5.1+).
#
# Usage:
#   .\dev.ps1 build          Build the server image only
#   .\dev.ps1 up             Build + run the container (foreground)
#   .\dev.ps1 up -d          Build + run detached, then print hints (logs / down)
#   .\dev.ps1 down           Stop and remove the container
#   .\dev.ps1 logs           Stream container logs (follow)
#   .\dev.ps1 exec <cmd...>  Run a command inside the running container
#
# The container runs as UID 1654 (chiseled non-root `app`). On Docker Desktop the engine
# (WSL/VM) owns the bind mounts, so `data\world` and `data\plugins` are created here and
# chowned to 1654 as a best effort (usually a no-op on the VM-backed filesystem).

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..\..")   # repo root

$Image = "voxelsharp:dev"
$DataDir = "data"
if ($env:DATA_DIR) { $DataDir = $env:DATA_DIR }
$Container = "voxelsharp-dev"

function Ensure-DataDirs {
    New-Item -ItemType Directory -Force -Path "$DataDir\world", "$DataDir\plugins" | Out-Null
    # Best effort chown; harmless if the engine filesystem ignores it. Under
    # $ErrorActionPreference="Stop", PS 5.1 turns redirected native stderr (e.g. a first-time
    # alpine pull) into a terminating error, so swallow it in a try/catch.
    try {
        docker run --rm -v "${PWD}\${DataDir}:/data" alpine:3.20 sh -c "chown -R 1654:1654 /data/world /data/plugins" 2>&1 | Out-Null
    } catch {
        # chown is best effort only; ignore failures.
    }
}

function Build-Image {
    Write-Host "==> Building $Image"
    docker build -t $Image .
}

function Spin-Up($Detach) {
    Ensure-DataDirs
    Build-Image
    $run = @("run")
    $flags = @("--rm", "--name", $Container, "-p", "25565:25565")
    $tail = @("-v", "${PWD}\${DataDir}\world:/app/data/world",
              "-v", "${PWD}\${DataDir}\plugins:/app/data/plugins:ro",
              "--read-only", "--tmpfs", "/tmp",
              "--cap-drop", "ALL",
              "--security-opt", "no-new-privileges:true",
              "--stop-timeout", "30",
              $Image)
    # `-d` must come directly after the `run` subcommand, before docker run's options.
    if ($Detach) { $flags = @("-d") + $flags }
    $cmdArgs = $run + $flags + $tail
    & docker @cmdArgs
    if ($LASTEXITCODE -ne 0) { throw "docker run failed ($LASTEXITCODE)" }
    if ($Detach) {
        Write-Host "Container $Container is running. Use:"
        Write-Host "   tools\docker\dev.ps1 logs"
        Write-Host "   tools\docker\dev.ps1 down"
    }
}

function Spin-Down {
    Write-Host "==> Stopping $Container"
    # Stopping an already-stopped container is a no-op, not an error.
    try {
        docker rm -f $Container 2>&1 | Out-Null
    } catch {
        # Container already gone; nothing to do.
    }
}

function Logs {
    docker logs -f $Container
}

function Exec($Rest) {
    $cmd = @($Container)
    foreach ($r in $Rest) { $cmd = $cmd + @($r) }
    & docker exec @cmd
}

$cmd = "up"
if ($args.Count -gt 0) { $cmd = $args[0] }

$detach = $false
$rest = @()
for ($i = 1; $i -lt $args.Count; $i++) {
    if ($args[$i] -eq "-d") { $detach = $true } else { $rest += ,$args[$i] }
}

switch ($cmd) {
    "build" { Build-Image }
    "up" { Spin-Up $detach }
    "down" { Spin-Down }
    "logs" { Logs }
    "exec" { Exec @rest }
    default { Write-Host "unknown command: $cmd (build|up|down|logs|exec)"; exit 2 }
}