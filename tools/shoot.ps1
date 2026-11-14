# Renders a local HTML page in headless Edge and saves a PNG screenshot.
# Prints any browser console messages (errors show up as CONSOLE lines).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File shoot.ps1 -Html page.html -Png out.png
#     [-W 1440] [-H 2400] [-Theme light|dark] [-Y 0] [-Crop "x,y,w,h"] [-Query "a=b&c=d"]
#
# -H is the viewport height in CSS px: make it at least as tall as the page to capture all of it.
# -Y scrolls the page down that many px before the shot (the kit reads ?y=).
# -Crop cuts a region out of the finished screenshot (pixels), for a close look at native size.
param(
  [Parameter(Mandatory = $true)][string]$Html,
  [Parameter(Mandatory = $true)][string]$Png,
  [int]$W = 1440,
  [int]$H = 2400,
  [string]$Theme = 'light',
  [int]$Y = 0,
  [string]$Crop = '',
  [string]$Query = ''
)

$ErrorActionPreference = 'Stop'
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (-not (Test-Path -LiteralPath $edge)) { throw "Edge not found at $edge" }

$full = (Resolve-Path -LiteralPath $Html).Path
$url = ([System.Uri]$full).AbsoluteUri + "?theme=$Theme"
if ($Y -gt 0) { $url += "&y=$Y" }
if ($Query) { $url += "&$Query" }

$pngFull = [System.IO.Path]::GetFullPath($Png)
$pngDir = [System.IO.Path]::GetDirectoryName($pngFull)
if (-not (Test-Path -LiteralPath $pngDir)) { New-Item -ItemType Directory -Force -Path $pngDir | Out-Null }
if (Test-Path -LiteralPath $pngFull) { Remove-Item -LiteralPath $pngFull -Force }

$ud = Join-Path $env:TEMP ('rdshot_' + [guid]::NewGuid().ToString('N'))
$edgeArgs = @(
  '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
  '--no-default-browser-check', '--disable-extensions', '--disable-sync',
  "--user-data-dir=`"$ud`"", "--window-size=$W,$H", '--force-device-scale-factor=1',
  '--virtual-time-budget=6000', '--enable-logging', '--v=0',
  "--screenshot=`"$pngFull`"", "`"$url`""
)
$proc = Start-Process -FilePath $edge -ArgumentList $edgeArgs -PassThru -WindowStyle Hidden
if (-not $proc.WaitForExit(90000)) { try { $proc.Kill() } catch {} ; Write-Output 'WARN: Edge timed out after 90s' }

$deadline = (Get-Date).AddSeconds(10)
while (-not (Test-Path -LiteralPath $pngFull) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }

$log = Join-Path $ud 'chrome_debug.log'
$consoleLines = @()
if (Test-Path -LiteralPath $log) {
  $consoleLines = @(Select-String -LiteralPath $log -Pattern 'CONSOLE' | ForEach-Object { $_.Line })
}
if ($consoleLines.Count -gt 0) {
  Write-Output "CONSOLE MESSAGES ($($consoleLines.Count)):"
  $consoleLines | Select-Object -First 25 | ForEach-Object { Write-Output "  $_" }
} else {
  Write-Output 'CONSOLE: clean (no messages)'
}

try { Remove-Item -LiteralPath $ud -Recurse -Force -ErrorAction Stop } catch {}

if (-not (Test-Path -LiteralPath $pngFull)) { Write-Output "FAILED: no screenshot written for $full"; exit 1 }

if ($Crop) {
  Add-Type -AssemblyName System.Drawing
  $parts = $Crop.Split(',') | ForEach-Object { [int]$_.Trim() }
  $src = [System.Drawing.Image]::FromFile($pngFull)
  try {
    $cx = [Math]::Max(0, $parts[0]); $cy = [Math]::Max(0, $parts[1])
    $cw = [Math]::Min($parts[2], $src.Width - $cx); $ch = [Math]::Min($parts[3], $src.Height - $cy)
    $rect = New-Object System.Drawing.Rectangle $cx, $cy, $cw, $ch
    $bmp = New-Object System.Drawing.Bitmap $cw, $ch
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $cw, $ch), $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
  } finally { $src.Dispose() }
  $bmp.Save($pngFull, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

$img = $null
try {
  Add-Type -AssemblyName System.Drawing
  $img = [System.Drawing.Image]::FromFile($pngFull)
  Write-Output "OK: $pngFull ($($img.Width)x$($img.Height))"
} finally { if ($img) { $img.Dispose() } }
