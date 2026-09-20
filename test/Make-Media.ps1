<#
.SYNOPSIS
    Regenerates the test clips with varied technical properties, so card
    badges have something to show: resolutions from SD to 4K, HDR, several
    audio tracks with languages and codecs, subtitle tracks.

.DESCRIPTION
    Every movie and episode becomes a short .mkv (a few seconds of a flat
    frame) whose streams are tagged the way real rips are: video at the
    chosen resolution (H.264 or HEVC, HEVC "4K" ones flagged as HDR10-ish
    via color metadata), 1-3 audio tracks with ISO 639-2 languages and
    channel layouts, and 0-3 subtitle tracks. Existing .mp4 clips from
    Setup-Jellyfin.ps1 are removed so Jellyfin sees one version per item.

    Afterwards the libraries are refreshed through the API (admin / admin).
#>
[CmdletBinding()]
param(
    [string] $Server = 'http://localhost:8096',
    [string] $User = 'admin',
    [string] $Password = 'admin'
)

$ErrorActionPreference = 'Stop'
$ffmpeg = Join-Path $PSScriptRoot 'jellyfin\jellyfin\ffmpeg.exe'
$media = Join-Path $PSScriptRoot 'media'

# name -> resolution, codec, audio tracks (lang:channels), subtitle langs
$profiles = @{
    'Arrival (2016)'                             = @{ w = 3840; h = 2160; codec = 'hevc'; audio = @('eng:6', 'cze:2'); subs = @('cze', 'eng') }
    'Blade Runner 2049 (2017)'                   = @{ w = 3840; h = 2160; codec = 'hevc'; audio = @('eng:8', 'cze:6', 'slo:2'); subs = @('cze', 'slo', 'eng', 'ger') }
    'Dune (2021)'                                = @{ w = 1920; h = 1080; codec = 'h264'; audio = @('eng:6', 'cze:6'); subs = @('cze') }
    'Interstellar (2014)'                        = @{ w = 1920; h = 1080; codec = 'hevc'; audio = @('eng:8'); subs = @('eng', 'fre', 'ger', 'spa') }
    'The Matrix (1999)'                          = @{ w = 1920; h = 1080; codec = 'hevc'; audio = @('eng:8', 'cze:2'); subs = @('cze', 'eng', 'ger', 'fre') }   # the widest badges: DD+ 7.1 (same width as DD+ Atmos 5.1), two flags, four subtitle codes
    'Mad Max: Fury Road (2015)'                  = @{ w = 1920; h = 1080; codec = 'h264'; audio = @('eng:6', 'jpn:2', 'kor:2'); subs = @('cze', 'eng', 'jpn') }
    'Inception (2010)'                           = @{ w = 1920; h = 800;  codec = 'h264'; audio = @('eng:6'); subs = @('cze', 'eng') }
    'Her (2013)'                                 = @{ w = 1280; h = 720;  codec = 'h264'; audio = @('eng:2'); subs = @('cze') }
    'Parasite (2019)'                            = @{ w = 1920; h = 1080; codec = 'hevc'; audio = @('kor:6', 'eng:2'); subs = @('cze', 'eng', 'kor') }
    'Spirited Away (2001)'                       = @{ w = 1920; h = 1080; codec = 'h264'; audio = @('jpn:2', 'eng:2', 'cze:2'); subs = @('cze', 'eng') }
    'Whiplash (2014)'                            = @{ w = 720;  h = 576;  codec = 'h264'; audio = @('eng:2'); subs = @('cze') }
    'The Grand Budapest Hotel (2014)'            = @{ w = 1920; h = 1080; codec = 'h264'; audio = @('eng:6', 'ger:2', 'fre:2'); subs = @('cze', 'eng', 'ger', 'fre', 'ita', 'spa') }
    'Everything Everywhere All at Once (2022)'   = @{ w = 3840; h = 2160; codec = 'hevc'; audio = @('eng:6', 'chi:2'); subs = @('cze', 'eng', 'chi') }
    'Oppenheimer (2023)'                         = @{ w = 3840; h = 2160; codec = 'hevc'; audio = @('eng:6:truehd', 'cze:6', 'ger:2', 'fre:2'); subs = @('cze', 'eng', 'ger', 'fre') }   # the long sound name (TrueHD 5.1 - ffmpeg's encoder stops at 5.1(side)) next to four flags and four codes
    'Alien (1979)'                               = @{ w = 720;  h = 480;  codec = 'h264'; audio = @('eng:2', 'cze:2', 'slo:2', 'hun:2', 'pol:2'); subs = @('cze', 'slo', 'hun', 'pol', 'eng') }
}
$episodeProfiles = @(
    @{ w = 1920; h = 1080; codec = 'hevc'; audio = @('eng:6', 'cze:2'); subs = @('cze', 'eng') },
    @{ w = 1280; h = 720;  codec = 'h264'; audio = @('eng:2'); subs = @('cze') },
    @{ w = 3840; h = 2160; codec = 'hevc'; audio = @('eng:6', 'cze:6', 'slo:2'); subs = @('cze', 'slo', 'eng') }
)

function New-Clip([string] $Path, [hashtable] $P) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $args = @('-loglevel', 'error', '-y', '-f', 'lavfi', '-i', "color=c=0x202020:s=$($P.w)x$($P.h):d=3")
    $i = 0
    foreach ($a in $P.audio) {
        $parts = $a.Split(':')
        # lang:channels[:codec]; truehd wants the "(side)" 5.1 layout and -strict -2 (experimental encoder)
        $layout = switch ($parts[1]) { '8' { '7.1' } '6' { if ($parts.Count -gt 2 -and $parts[2] -eq 'truehd') { '5.1(side)' } else { '5.1' } } default { 'stereo' } }
        $args += @('-f', 'lavfi', '-i', "anullsrc=r=48000:cl=$layout")
        $i++
    }
    $subFiles = @()
    foreach ($s in $P.subs) {
        $f = [System.IO.Path]::GetTempFileName() + '.srt'
        "1`r`n00:00:00,000 --> 00:00:02,000`r`n$s subtitle`r`n" | Set-Content $f -Encoding utf8
        $subFiles += $f
        $args += @('-i', $f)
    }
    $args += @('-map', '0:v', '-shortest')
    # video: HEVC 4K tagged with HDR colour metadata, everything else plain H.264 / HEVC
    if ($P.codec -eq 'hevc') {
        $args += @('-c:v', 'libx265', '-preset', 'ultrafast', '-x265-params', 'log-level=error')
        if ($P.w -ge 3800) { $args += @('-color_primaries', 'bt2020', '-color_trc', 'smpte2084', '-colorspace', 'bt2020nc') }
    } else {
        $args += @('-c:v', 'libx264', '-preset', 'ultrafast')
    }
    $n = 0
    foreach ($a in $P.audio) {
        $parts = $a.Split(':')
        $codec = if ($parts.Count -gt 2) { $parts[2] } else { switch ($parts[1]) { '8' { 'eac3' } '6' { 'ac3' } default { 'aac' } } }
        $args += @('-map', "$($n + 1):a", "-c:a:$n", $codec, "-metadata:s:a:$n", "language=$($parts[0])")
        if ($codec -eq 'truehd') { $args += @('-strict', '-2') }
        $n++
    }
    $m = 0
    foreach ($s in $P.subs) {
        $args += @('-map', "$($P.audio.Count + 1 + $m):s", "-c:s:$m", 'srt', "-metadata:s:s:$m", "language=$s")
        $m++
    }
    $args += @($Path)
    & $ffmpeg @args
    $subFiles | ForEach-Object { Remove-Item $_ -ErrorAction SilentlyContinue }
}

Write-Host '== regenerating test media' -ForegroundColor Cyan
$fallback = @{ w = 1920; h = 1080; codec = 'h264'; audio = @('eng:6', 'cze:2'); subs = @('cze', 'eng') }
foreach ($dir in Get-ChildItem (Join-Path $media 'Movies') -Directory) {
    # folder names on disk have no colon ("Mad Max Fury Road (2015)")
    $key = $profiles.Keys | Where-Object { ($_ -replace ':', '') -eq $dir.Name } | Select-Object -First 1
    $p = if ($key) { $profiles[$key] } else { $fallback }
    Get-ChildItem $dir.FullName -Filter *.mp4 -ErrorAction SilentlyContinue | Remove-Item
    New-Clip (Join-Path $dir.FullName "$($dir.Name).mkv") $p
    Write-Host "   $($dir.Name) -> $($p.w)x$($p.h) $($p.codec) audio $($p.audio -join ',') subs $($p.subs -join ',')" -ForegroundColor DarkGray
}
$k = 0
foreach ($dir in Get-ChildItem (Join-Path $media 'Shows') -Directory) {
    foreach ($ep in Get-ChildItem $dir.FullName -Recurse -Include *.mp4, *.mkv) {
        $p = $episodeProfiles[$k % $episodeProfiles.Count]
        $k++
        $target = [System.IO.Path]::ChangeExtension($ep.FullName, '.mkv')
        if ($ep.Extension -eq '.mp4') { Remove-Item $ep.FullName }
        New-Clip $target $p
        Write-Host "   $($dir.Name) $($ep.BaseName)" -ForegroundColor DarkGray
    }
}

# --- refresh the libraries so Jellyfin picks up the new files -----------------
$auth = 'MediaBrowser Client="Jellycanvas test", Device="script", DeviceId="jellycanvas-media", Version="1.0"'
$login = Invoke-RestMethod -Method Post -Uri "$Server/Users/AuthenticateByName" -ContentType 'application/json' -Headers @{ Authorization = $auth } -Body (@{ Username = $User; Pw = $Password } | ConvertTo-Json)
$headers = @{ Authorization = "$auth, Token=`"$($login.AccessToken)`"" }
Invoke-RestMethod -Method Post -Uri "$Server/Library/Refresh" -Headers $headers | Out-Null
Write-Host 'Library refresh started - the badges show once Jellyfin has probed the new files (a minute or so).' -ForegroundColor Green
