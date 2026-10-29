#!/usr/bin/env bash
# First-run setup of the Linux test instance (start-jellyfin.sh first):
# completes the startup wizard (admin / admin) and adds the Movies, Shows and
# Music libraries over test/media. The media themselves come from the
# Windows scripts (Setup-, Make-Media.ps1, Make-Music.ps1) and are already
# in test/media.
#
#   ./test/setup-jellyfin.sh
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
server="http://localhost:${JF_PORT:-3004}"
user=admin
password=admin
media="$here/media"
auth='MediaBrowser Client="Jellycanvas Test", Device="setup script", DeviceId="jellycanvas-setup", Version="1.0"'

jf() { # method path [json] [token]
    local h="$auth"
    [[ -n "${4:-}" ]] && h="$h, Token=\"$4\""
    curl -sf -X "$1" "$server$2" -H "Authorization: $h" -H 'Content-Type: application/json' ${3:+-d "$3"}
}

# --- 1. wizard ---------------------------------------------------------------
if [[ "$(curl -sf "$server/System/Info/Public" | jq -r .StartupWizardCompleted)" != "true" ]]; then
    echo "== completing the startup wizard"
    jf POST /Startup/Configuration '{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}' > /dev/null
    jf GET /Startup/User > /dev/null   # makes the server create the first user
    jf POST /Startup/User "{\"Name\":\"$user\",\"Password\":\"$password\"}" > /dev/null
    jf POST /Startup/RemoteAccess '{"EnableRemoteAccess":true,"EnableAutomaticPortMapping":false}' > /dev/null
    jf POST /Startup/Complete > /dev/null
    sleep 2
else
    echo "== wizard already completed"
fi

# --- 2. login ------------------------------------------------------------------
token="$(jf POST /Users/AuthenticateByName "{\"Username\":\"$user\",\"Pw\":\"$password\"}" | jq -r .AccessToken)"
echo "== logged in as $user"

# --- 3. libraries --------------------------------------------------------------
existing="$(jf GET /Library/VirtualFolders "" "$token")"
add_library() { # name type folder
    if jq -e --arg n "$1" 'any(.[]; .Name == $n)' <<<"$existing" > /dev/null; then
        echo "   library '$1' exists"
        return
    fi
    jf POST "/Library/VirtualFolders?name=$1&collectionType=$2&refreshLibrary=true" \
        "{\"LibraryOptions\":{\"PathInfos\":[{\"Path\":\"$3\"}],\"EnableRealtimeMonitor\":false}}" "$token" > /dev/null
    echo "   library '$1' created"
}
add_library Movies movies "$media/Movies"
add_library Shows tvshows "$media/Shows"
add_library Music music "$media/Music"

echo
echo "Done. Open $server, log in as $user / $password, then Dashboard -> My Plugins -> Jellycanvas."
echo "Metadata download (posters from TMDB) runs in the background for a minute or two."
