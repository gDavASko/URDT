<#
.SYNOPSIS
  Reproduces the interactive polygon honest-input run under Unity CLI control.

  1. Launches the Unity Editor GUI on this project via the `unity` CLI (`unity open`),
     with URDT_AUTOPLAY=1 so UrdtOrchestrator loads the polygon scene and enters Play Mode.
  2. Waits for the polygon's UrdtServerHost to listen on 127.0.0.1:7777.
  3. Runs the randomized honest-input runner (Tools/randomized-ui-stress-runner.js) over the wire.
  4. Shuts the launched Editor down (only the instance this script started).

  uGUI Button.onClick only fires with a real graphics/interactive context, so this uses the
  GUI Editor rather than headless batchmode.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath   = (Split-Path -Parent $PSScriptRoot),
    [string]$EditorVersion = "6000.4.4f1",
    [string]$Report        = "",
    [int]$PortTimeoutSec   = 300
)

$ErrorActionPreference = "Stop"
$port = 7777
$unityCli = Join-Path $env:LOCALAPPDATA "Unity\bin\unity.exe"
if (-not (Test-Path $unityCli)) { throw "unity CLI not found at $unityCli" }
if ([string]::IsNullOrEmpty($Report)) { $Report = Join-Path $ProjectPath ".harness\urdt-orchestrated.jsonl" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Report) | Out-Null

function Test-Port([int]$p) {
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $iar = $c.BeginConnect("127.0.0.1", $p, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(500)
        if ($ok -and $c.Connected) { $c.Close(); return $true }
        $c.Close(); return $false
    } catch { return $false }
}

$editorGlob = "*\Hub\Editor\*\Editor\Unity.exe"
function Get-EditorPids {
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and ($_.Path -like $editorGlob) } |
        Select-Object -ExpandProperty Id
}

if (Test-Port $port) {
    Write-Host "WARN: 127.0.0.1:$port is already listening - an editor/server may already be running. Aborting to avoid driving the wrong instance." -ForegroundColor Yellow
    exit 3
}

$before = @(Get-EditorPids)
# The editor is force-killed at teardown, which leaves scene backups behind. On the next launch
# Unity would pop a blocking "Recovering Scene Backups" modal that stalls startup. Clear the
# recovery sources so the GUI editor boots straight into the auto-play hook.
Remove-Item (Join-Path $ProjectPath "Temp") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ProjectPath "Assets\_Recovery") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ProjectPath "Assets\_Recovery.meta") -Force -ErrorAction SilentlyContinue
Write-Host "Launching Unity Editor GUI via 'unity open' (URDT_AUTOPLAY=1)..." -ForegroundColor Cyan
$env:URDT_AUTOPLAY = "1"
# unity CLI spawns the Editor and returns; the Editor runs as its own Program Files Unity.exe.
Start-Process -FilePath $unityCli -ArgumentList @("open", $ProjectPath, "--editor-version", $EditorVersion, "--no-banner") -WindowStyle Minimized | Out-Null

Write-Host "Waiting up to $PortTimeoutSec s for the polygon server on :$port ..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($PortTimeoutSec)
$ready = $false
while ((Get-Date) -lt $deadline) {
    if (Test-Port $port) { $ready = $true; break }
    Start-Sleep -Seconds 2
}

$launched = @(Get-EditorPids | Where-Object { $before -notcontains $_ })

if (-not $ready) {
    Write-Host "TIMEOUT: server never came up on :$port." -ForegroundColor Red
    foreach ($epid in $launched) { try { Stop-Process -Id $epid -Force } catch {} }
    Remove-Item Env:\URDT_AUTOPLAY -ErrorAction SilentlyContinue
    exit 2
}

Write-Host "Server is up. Running the honest-input runner..." -ForegroundColor Green
$node = (Get-Command node).Source
$runner = Join-Path $ProjectPath "Tools\randomized-ui-stress-runner.js"
$runnerExit = 0
try {
    & $node $runner $Report
    $runnerExit = $LASTEXITCODE
} catch {
    Write-Host "Runner error: $_" -ForegroundColor Red
    $runnerExit = 1
}

Write-Host "Shutting down the launched Editor (pids: $($launched -join ', '))..." -ForegroundColor Cyan
foreach ($epid in $launched) { try { Stop-Process -Id $epid -Force } catch {} }
Remove-Item Env:\URDT_AUTOPLAY -ErrorAction SilentlyContinue

Write-Host "Runner exit code: $runnerExit" -ForegroundColor $(if ($runnerExit -eq 0) { "Green" } else { "Red" })
Write-Host "JSONL trace: $Report"
exit $runnerExit
