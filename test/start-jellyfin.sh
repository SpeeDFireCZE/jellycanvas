#!/usr/bin/env bash
# Builds the plugin, installs it into the Linux test instance and (re)starts
# a portable Jellyfin 12 on port 3004 - the Linux counterpart of
# Start-Jellyfin.ps1.
#
#   ./test/start-jellyfin.sh              build, install, restart
#   ./test/start-jellyfin.sh --no-build   restart with the DLL already built
#   ./test/start-jellyfin.sh --stop       stop the server and exit
#
# First time (or with an empty data-linux): run ./test/setup-jellyfin.sh
# afterwards - wizard, admin/admin, the Movies / Shows / Music libraries.
#
# Server:  test/jellyfin-linux/jellyfin  (jellyfin_12.x-amd64.tar.xz from
#          https://repo.jellyfin.org/files/server/linux/stable/)
# ffmpeg:  test/jellyfin-linux/ffmpeg    (jellyfin-ffmpeg portable_linux64)
# Data:    test/data-linux               (the Windows test/data stays as it was)
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"
server="$here/jellyfin-linux/jellyfin"
ffmpeg="$here/jellyfin-linux/ffmpeg/ffmpeg"
data="$here/data-linux"
project="$root/Jellyfin.Plugin.Jellycanvas/Jellyfin.Plugin.Jellycanvas.csproj"
pidfile="$data/jellyfin.pid"
log="$here/jellyfin-linux.log"
port="${JF_PORT:-3004}"

build=1
case "${1:-}" in
    --no-build) build=0 ;;
    --stop) ;;
    "") ;;
    *) echo "usage: $0 [--no-build|--stop]" >&2; exit 2 ;;
esac

stop_server() {
    if [[ -f "$pidfile" ]] && kill -0 "$(cat "$pidfile")" 2>/dev/null; then
        echo "== stopping running Jellyfin (pid $(cat "$pidfile"))"
        kill "$(cat "$pidfile")"
        for _ in $(seq 1 30); do
            kill -0 "$(cat "$pidfile")" 2>/dev/null || break
            sleep 0.5
        done
        kill -9 "$(cat "$pidfile")" 2>/dev/null || true
    fi
    rm -f "$pidfile"
}

if [[ "${1:-}" == "--stop" ]]; then
    stop_server
    exit 0
fi

[[ -x "$server/jellyfin" ]] || { echo "Jellyfin server not found in $server" >&2; exit 1; }
[[ -x "$ffmpeg" ]] || { echo "jellyfin-ffmpeg not found at $ffmpeg" >&2; exit 1; }

# --- 1. build ----------------------------------------------------------------
if (( build )); then
    echo "== dotnet build"
    dotnet build "$project" -c Release --nologo -v quiet
fi
dll="$root/Jellyfin.Plugin.Jellycanvas/bin/Release/net10.0/Jellyfin.Plugin.Jellycanvas.dll"
# The plugin folder carries the three-part version from the project file.
version="$(grep -oP '(?<=<AssemblyVersion>)[^<]+' "$root/Jellyfin.Plugin.Jellycanvas/Jellyfin.Plugin.Jellycanvas.csproj")"

# --- 2. stop -----------------------------------------------------------------
stop_server

# --- 3. install the plugin ---------------------------------------------------
# Jellyfin loads <datadir>/plugins/<name>_<version>/; an older copy would
# load as well and conflict, so it goes.
mkdir -p "$data/plugins" "$data/config"
rm -rf "$data"/plugins/Jellycanvas_*
target="$data/plugins/Jellycanvas_$version"
mkdir -p "$target"
cp "$dll" "$target/"
echo "== plugin installed to $target"

# The port: Jellyfin reads it from network.xml (no command-line switch).
net="$data/config/network.xml"
if [[ -f "$net" ]]; then
    sed -i -E "s#<InternalHttpPort>[0-9]+</InternalHttpPort>#<InternalHttpPort>$port</InternalHttpPort>#; s#<PublicHttpPort>[0-9]+</PublicHttpPort>#<PublicHttpPort>$port</PublicHttpPort>#" "$net"
else
    printf '<?xml version="1.0" encoding="utf-8"?>\n<NetworkConfiguration>\n  <InternalHttpPort>%s</InternalHttpPort>\n  <PublicHttpPort>%s</PublicHttpPort>\n  <EnableRemoteAccess>true</EnableRemoteAccess>\n</NetworkConfiguration>\n' "$port" "$port" > "$net"
fi

# --- 4. start ----------------------------------------------------------------
# The previous run's log stays: when the server crashed, the reason is in it.
[[ -f "$log" ]] && mv -f "$log" "$log.prev"
echo "== starting Jellyfin on port $port (log: $log)"
# setsid + nohup: the server outlives the shell that started it.
setsid nohup "$server/jellyfin" \
    --datadir "$data" \
    --cachedir "$data/cache" \
    --webdir "$server/jellyfin-web" \
    --ffmpeg "$ffmpeg" \
    > "$log" 2>&1 < /dev/null &
echo $! > "$pidfile"

# Wait until it answers: /health says "Degraded" while starting, only
# "Healthy" means the API (and the plugin) take requests.
for _ in $(seq 1 60); do
    sleep 2
    kill -0 "$(cat "$pidfile")" 2>/dev/null || { echo "Jellyfin exited early - see $log" >&2; tail -20 "$log" >&2; exit 1; }
    if [[ "$(curl -s -m 3 "http://localhost:$port/health" || true)" == "Healthy" ]]; then
        info="$(curl -s "http://localhost:$port/System/Info/Public")"
        echo "== Jellyfin $(jq -r .Version <<<"$info") is up at http://localhost:$port  (wizard done: $(jq -r .StartupWizardCompleted <<<"$info"))"
        [[ "$(jq -r .StartupWizardCompleted <<<"$info")" == "true" ]] || echo "   run ./test/setup-jellyfin.sh to finish the wizard and add the libraries"
        exit 0
    fi
done
echo "Jellyfin did not come up in 120 s - see $log" >&2
exit 1
