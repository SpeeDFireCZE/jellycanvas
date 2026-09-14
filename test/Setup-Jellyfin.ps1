<#
.SYNOPSIS
    Completes the test instance's setup wizard and creates test libraries.

.DESCRIPTION
    Jellyfin's wizard can be walked entirely through the API (/Startup/*).
    Then an admin / admin user is created, a test\media\ folder with a few
    "movies" and "shows" (3 seconds of a dark frame from the bundled ffmpeg -
    enough for testing the look; what matters are the titles, which Jellyfin
    uses to fetch posters), and Movies and Shows libraries pointing at it.

    Run after Start-Jellyfin.ps1. Safe to run again - whatever exists is skipped.
#>
[CmdletBinding()]
param(
    [string] $Server = 'http://localhost:8096',
    [string] $User = 'admin',
    [string] $Password = 'admin'
)

$ErrorActionPreference = 'Stop'
$media = Join-Path $PSScriptRoot 'media'
$ffmpeg = Join-Path $PSScriptRoot 'jellyfin\jellyfin\ffmpeg.exe'

# The header Jellyfin requires on every call (even without a token).
$authHeader = 'MediaBrowser Client="Jellycanvas Test", Device="setup script", DeviceId="jellycanvas-setup", Version="1.0"'
function Invoke-Jf {
    param([string] $Method, [string] $Path, $Body, [string] $Token)
    $h = @{ Authorization = $authHeader + $(if ($Token) { ", Token=`"$Token`"" } else { '' }) }
    $args = @{ Method = $Method; Uri = "$Server$Path"; Headers = $h; ContentType = 'application/json' }
    if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Depth 8 -Compress) }
    return Invoke-RestMethod @args
}

# --- 1. wizard ------------------------------------------------------------
$info = Invoke-RestMethod "$Server/System/Info/Public"
if (-not $info.StartupWizardCompleted) {
    Write-Host '== completing the startup wizard' -ForegroundColor Cyan
    Invoke-Jf POST '/Startup/Configuration' @{ UICulture = 'en-US'; MetadataCountryCode = 'US'; PreferredMetadataLanguage = 'en' } | Out-Null
    Invoke-Jf GET '/Startup/User' | Out-Null      # makes the server create the first user
    Invoke-Jf POST '/Startup/User' @{ Name = $User; Password = $Password } | Out-Null
    Invoke-Jf POST '/Startup/RemoteAccess' @{ EnableRemoteAccess = $true; EnableAutomaticPortMapping = $false } | Out-Null
    Invoke-Jf POST '/Startup/Complete' | Out-Null
    Start-Sleep -Seconds 2
} else {
    Write-Host '== wizard already completed' -ForegroundColor DarkGray
}

# --- 2. login -------------------------------------------------------------
$auth = Invoke-Jf POST '/Users/AuthenticateByName' @{ Username = $User; Pw = $Password }
$token = $auth.AccessToken
Write-Host "== logged in as $User" -ForegroundColor Cyan

# --- 3. test media ----------------------------------------------------------
# Real titles so TMDB finds posters and backdrops (when there is internet).
$movies = @(
    'Inception (2010)', 'The Matrix (1999)', 'Interstellar (2014)', 'Blade Runner 2049 (2017)',
    'Dune (2021)', 'Arrival (2016)', 'The Dark Knight (2008)', 'Spirited Away (2001)',
    'Mad Max Fury Road (2015)', 'Parasite (2019)', 'Whiplash (2014)', 'Her (2013)',
    'The Grand Budapest Hotel (2014)', 'Everything Everywhere All at Once (2022)', 'Oppenheimer (2023)', 'Alien (1979)'
)
$shows = @{
    'Breaking Bad' = @('S01E01', 'S01E02', 'S01E03')
    'The Expanse' = @('S01E01', 'S01E02')
    'Severance' = @('S01E01', 'S01E02')
}

function New-Clip([string] $Path) {
    if (Test-Path $Path) { return }
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    & $ffmpeg -loglevel error -y -f lavfi -i 'color=c=0x202020:s=640x360:d=3' -f lavfi -i 'anullsrc=r=44100:cl=stereo' -shortest -c:v libx264 -preset ultrafast -c:a aac $Path
}

Write-Host '== creating test media' -ForegroundColor Cyan
foreach ($m in $movies) { New-Clip (Join-Path $media "Movies\$m\$m.mp4") }
foreach ($s in $shows.Keys) {
    foreach ($e in $shows[$s]) { New-Clip (Join-Path $media "Shows\$s\Season 01\$s $e.mp4") }
}

# --- 4. libraries -----------------------------------------------------------
$existing = Invoke-Jf GET '/Library/VirtualFolders' -Token $token
function Add-Library([string] $Name, [string] $Type, [string] $Path) {
    if ($existing | Where-Object { $_.Name -eq $Name }) {
        Write-Host "   library '$Name' exists" -ForegroundColor DarkGray
        return
    }
    $body = @{ LibraryOptions = @{ PathInfos = @(@{ Path = $Path }); EnableRealtimeMonitor = $false } }
    $q = "name=$([uri]::EscapeDataString($Name))&collectionType=$Type&refreshLibrary=true"
    Invoke-Jf POST "/Library/VirtualFolders?$q" $body -Token $token | Out-Null
    Write-Host "   library '$Name' created" -ForegroundColor Green
}
Add-Library 'Movies' 'movies' (Join-Path $media 'Movies')
Add-Library 'Shows' 'tvshows' (Join-Path $media 'Shows')

Write-Host ''
Write-Host "Done. Open $Server, log in as $User / $Password," -ForegroundColor Green
Write-Host 'then Dashboard -> My Plugins -> Jellycanvas. Metadata download runs in the background for a minute or two.' -ForegroundColor Green
