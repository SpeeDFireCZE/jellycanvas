<#
.SYNOPSIS
    Adds a small music library to the test server: made-up artists and
    albums whose tracks are generated tones, so the music pages, the
    now-playing bar and the queue can be checked against a theme.

.DESCRIPTION
    Three artists with one album each (four or five tracks of 40 seconds:
    a chord that swells and fades), tagged the way real files are - title,
    artist, album artist, album, track number, year, genre - and an album
    cover (folder.jpg: a gradient with the album's name). One album is FLAC,
    the others MP3. The names are invented on purpose, so no metadata
    provider matches them to a real record.

    Then a "Music" library is created (or refreshed) through the API
    (admin / admin). Run it again to regenerate the files.
#>
[CmdletBinding()]
param(
    [string] $Server = 'http://localhost:8096',
    [string] $User = 'admin',
    [string] $Password = 'admin'
)

$ErrorActionPreference = 'Stop'
$ffmpeg = Join-Path $PSScriptRoot 'jellyfin\jellyfin\ffmpeg.exe'
$music = Join-Path $PSScriptRoot 'media\Music'
$font = 'C\:/Windows/Fonts/arialbd.ttf'   # drawtext wants the colon escaped

# artist -> album, year, genre, format, cover colors, base note (Hz), tracks
$albums = @(
    @{ Artist = 'Northern Lights Ensemble'; Album = 'Aurora Sessions'; Year = 2021; Genre = 'Ambient'; Format = 'flac'; C0 = '0x0f2027'; C1 = '0x2c7744'; Base = 220.0
       Tracks = @('First Light', 'Polar Drift', 'Magnetic North', 'Silent Fjord', 'Last Aurora') },
    @{ Artist = 'Pixel Harbor'; Album = 'Synth Tide'; Year = 2023; Genre = 'Synthwave'; Format = 'mp3'; C0 = '0x3a0ca3'; C1 = '0xf72585'; Base = 261.63
       Tracks = @('Neon Pier', 'Low Tide Drive', 'Arcade Lighthouse', 'Saltwater Grid') },
    @{ Artist = 'Velvet Static'; Album = 'Midnight Radio'; Year = 2019; Genre = 'Lo-fi'; Format = 'mp3'; C0 = '0x3d2c2e'; C1 = '0xe0a96d'; Base = 196.0
       Tracks = @('Dial Tone Lullaby', 'Rain on the Antenna', 'Late Night Request', 'Station Identification') }
)

# A chord (root, major third, fifth) on the given root, swelling in and out.
function New-Track([string] $Path, [double] $Root, [hashtable] $Tags, [string] $Format) {
    $third = [Math]::Round($Root * 1.2599, 2)
    $fifth = [Math]::Round($Root * 1.4983, 2)
    $expr = "0.16*sin(2*PI*$Root*t)+0.11*sin(2*PI*$third*t)+0.11*sin(2*PI*$fifth*t)"
    $expr = "($expr)*(0.75+0.25*sin(2*PI*0.25*t))"
    $args = @('-loglevel', 'error', '-y', '-f', 'lavfi', '-i', "aevalsrc=exprs='$expr|$expr':s=44100:d=40",
        '-af', 'afade=t=in:d=2,afade=t=out:st=37:d=3')
    foreach ($k in $Tags.Keys) { $args += @('-metadata', "$k=$($Tags[$k])") }
    if ($Format -eq 'flac') { $args += @('-c:a', 'flac') } else { $args += @('-c:a', 'libmp3lame', '-b:a', '160k', '-id3v2_version', '3') }
    $args += $Path
    & $ffmpeg @args
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed for $Path" }
}

function New-Cover([string] $Path, [string] $C0, [string] $C1, [string] $Title, [string] $Artist) {
    $t = $Title.Replace("'", '')
    $a = $Artist.Replace("'", '')
    $vf = "drawtext=fontfile='$font':text='$t':fontcolor=white:fontsize=46:x=(w-text_w)/2:y=h-150:shadowcolor=black@0.5:shadowx=2:shadowy=2," +
          "drawtext=fontfile='$font':text='$a':fontcolor=white@0.85:fontsize=28:x=(w-text_w)/2:y=h-90"
    & $ffmpeg -loglevel error -y -f lavfi -i "gradients=s=600x600:c0=$($C0):c1=$($C1):x0=0:y0=0:x1=600:y1=600:d=1" -vf $vf -frames:v 1 $Path
    if ($LASTEXITCODE -ne 0) {
        # no usable font: the gradient alone is still a cover
        & $ffmpeg -loglevel error -y -f lavfi -i "gradients=s=600x600:c0=$($C0):c1=$($C1):d=1" -frames:v 1 $Path
    }
}

Write-Host '== generating the music' -ForegroundColor Cyan
foreach ($al in $albums) {
    $dir = Join-Path $music "$($al.Artist)\$($al.Album) ($($al.Year))"
    New-Item -ItemType Directory -Force $dir | Out-Null
    New-Cover (Join-Path $dir 'folder.jpg') $al.C0 $al.C1 $al.Album $al.Artist
    $n = $al.Tracks.Count
    for ($i = 0; $i -lt $n; $i++) {
        $title = $al.Tracks[$i]
        # each track a step up the scale, so they do not all sound the same
        $root = [Math]::Round($al.Base * [Math]::Pow(2, (@(0, 2, 4, 5, 7)[$i % 5]) / 12.0), 2)
        $file = Join-Path $dir ('{0:D2} {1}.{2}' -f ($i + 1), $title, $al.Format)
        $tags = [ordered]@{ title = $title; artist = $al.Artist; album_artist = $al.Artist; album = $al.Album; track = "$($i + 1)/$n"; date = $al.Year; genre = $al.Genre }
        New-Track $file $root $tags $al.Format
    }
    Write-Host "   $($al.Artist) - $($al.Album): $n tracks" -ForegroundColor Green
}

# --- the library ------------------------------------------------------------
$authHeader = 'MediaBrowser Client="Jellycanvas Test", Device="setup script", DeviceId="jellycanvas-setup", Version="1.0"'
function Invoke-Jf {
    param([string] $Method, [string] $Path, $Body, [string] $Token)
    $h = @{ Authorization = $authHeader + $(if ($Token) { ", Token=`"$Token`"" } else { '' }) }
    $a = @{ Method = $Method; Uri = "$Server$Path"; Headers = $h; ContentType = 'application/json' }
    if ($null -ne $Body) { $a.Body = ($Body | ConvertTo-Json -Depth 8 -Compress) }
    return Invoke-RestMethod @a
}

$token = (Invoke-Jf POST '/Users/AuthenticateByName' @{ Username = $User; Pw = $Password }).AccessToken
$existing = Invoke-Jf GET '/Library/VirtualFolders' -Token $token
if ($existing | Where-Object { $_.Name -eq 'Music' }) {
    Invoke-Jf POST '/Library/Refresh' -Token $token | Out-Null
    Write-Host "== library 'Music' exists - refresh started" -ForegroundColor DarkGray
} else {
    $body = @{ LibraryOptions = @{ PathInfos = @(@{ Path = $music }); EnableRealtimeMonitor = $false } }
    Invoke-Jf POST "/Library/VirtualFolders?name=Music&collectionType=music&refreshLibrary=true" $body -Token $token | Out-Null
    Write-Host "== library 'Music' created - the scan runs in the background" -ForegroundColor Green
}
