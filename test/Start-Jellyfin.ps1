<#
.SYNOPSIS
    Builds the plugin, installs it into the local Jellyfin 12 test instance and starts it.

.DESCRIPTION
    The test instance is a portable Jellyfin unpacked in test\jellyfin\jellyfin\
    (downloaded from repo.jellyfin.org, see README). Its data lives in
    test\data\, so nothing on the system is touched and it can be deleted at
    any time.

    It listens on http://localhost:8096 - the same port as any Jellyfin, so
    two cannot run at once.

.PARAMETER NoBuild
    Skip dotnet build - just restart the server with what is already in bin\.

.PARAMETER Fresh
    Delete test\data\ and start with a clean install (the wizard runs again -
    then run Setup-Jellyfin.ps1).

.EXAMPLE
    .\test\Start-Jellyfin.ps1
    .\test\Setup-Jellyfin.ps1      # first time only (or after -Fresh)
#>
[CmdletBinding()]
param(
    [switch] $NoBuild,
    [switch] $Fresh
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot           # repository root
$server = Join-Path $PSScriptRoot 'jellyfin\jellyfin'
$data = Join-Path $PSScriptRoot 'data'
$project = Join-Path $root 'Jellyfin.Plugin.Jellycanvas\Jellyfin.Plugin.Jellycanvas.csproj'

if (-not (Test-Path (Join-Path $server 'jellyfin.exe'))) {
    throw "Jellyfin server not found in $server - download jellyfin_12.x-amd64.zip from https://repo.jellyfin.org/files/server/windows/latest-stable/amd64/ and unpack it into test\jellyfin\"
}

# --- 1. build ---------------------------------------------------------------
if (-not $NoBuild) {
    Write-Host '== dotnet build' -ForegroundColor Cyan
    dotnet build $project -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}

$dll = Get-ChildItem (Join-Path $root 'Jellyfin.Plugin.Jellycanvas\bin\Release\net10.0\Jellyfin.Plugin.Jellycanvas.dll')
$version = (Get-Item $dll).VersionInfo.FileVersion

# --- 2. stop the running server -------------------------------------------
$running = Get-Process -Name jellyfin -ErrorAction SilentlyContinue
if ($running) {
    Write-Host '== stopping running Jellyfin' -ForegroundColor Cyan
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
}

if ($Fresh -and (Test-Path $data)) {
    Write-Host '== removing test data (fresh start)' -ForegroundColor Yellow
    Remove-Item -Recurse -Force $data
}

# --- 3. install the plugin ---------------------------------------------------
# Jellyfin looks for plugins in <datadir>\plugins\<name>_<version>\. Older
# versions of the same plugin are removed, otherwise the server would load
# both and report a conflict.
$pluginsDir = Join-Path $data 'plugins'
New-Item -ItemType Directory -Force $pluginsDir | Out-Null
Get-ChildItem $pluginsDir -Directory -Filter 'Jellycanvas_*' | Remove-Item -Recurse -Force
$target = Join-Path $pluginsDir "Jellycanvas_$version"
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item $dll.FullName $target
Write-Host "== plugin installed to $target" -ForegroundColor Cyan

# --- 4. start ---------------------------------------------------------------
# --nowebclient is NOT used here: we want the Dashboard and the preview, i.e. the whole web client.
$log = Join-Path $PSScriptRoot 'jellyfin.log'
$errLog = Join-Path $PSScriptRoot 'jellyfin.err.log'
# Keep the previous run's logs - when the server crashed, the reason is in them.
foreach ($f in @($log, $errLog)) {
    if (Test-Path $f) { Move-Item -Force $f ($f + '.prev') }
}
Write-Host "== starting Jellyfin (log: $log)" -ForegroundColor Cyan
$proc = Start-Process -FilePath (Join-Path $server 'jellyfin.exe') `
    -ArgumentList @('--datadir', "`"$data`"", '--cachedir', "`"$(Join-Path $data 'cache')`"") `
    -RedirectStandardOutput $log -RedirectStandardError $errLog `
    -WindowStyle Hidden -PassThru

# Wait until it answers (the first start with an empty database takes longer).
$deadline = (Get-Date).AddSeconds(90)
$ok = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    if ($proc.HasExited) { throw "Jellyfin exited early - see $log" }
    try {
        # /health answers 200 with "Degraded" while the server is still starting;
        # only "Healthy" means the API (and the plugin) will take requests.
        $r = Invoke-WebRequest -Uri 'http://localhost:8096/health' -UseBasicParsing -TimeoutSec 3
        if ($r.StatusCode -eq 200 -and $r.Content -match 'Healthy') { $ok = $true; break }
    } catch { }
}
if (-not $ok) { throw "Jellyfin did not come up in 90 s - see $log" }

$info = Invoke-RestMethod 'http://localhost:8096/System/Info/Public'
Write-Host ("== Jellyfin {0} is up at http://localhost:8096  (wizard done: {1})" -f $info.Version, $info.StartupWizardCompleted) -ForegroundColor Green
if (-not $info.StartupWizardCompleted) {
    Write-Host '   run .\test\Setup-Jellyfin.ps1 to finish the wizard and create test media' -ForegroundColor Yellow
}
