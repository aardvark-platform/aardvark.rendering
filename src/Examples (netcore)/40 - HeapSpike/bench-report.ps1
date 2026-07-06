# renderbench report: heap vs baked-multidraw baseline on THIS machine (Windows).
#
#   .\bench-report.ps1 [-Ns "100000 300000 700000"] [-Frames 60] [-Out report.md]
#                      [-Icd C:\path\to\driver_icd.json] [-NoQuery]
#
#   -Ns       object counts to sweep (space-separated, default: 100k 300k 700k)
#   -Frames   timed frames per round (default 60; use ~20 on slow iGPUs)
#   -Out      output markdown report (default: bench-report-<host>.md here)
#   -Gpu      pick the Vulkan device by NAME substring (e.g. -Gpu radeon);
#             preferred over -Icd — no ICD paths needed
#   -Icd      Vulkan ICD json to pin a specific GPU/driver (sets VK_DRIVER_FILES)
#   -NoQuery  fence-blocked CPU timing instead of GPU time queries
#
# Run from anywhere; builds HeapSpike Release and sweeps `renderbench`.
param(
    [string]$Ns = "100000 300000 700000",
    [int]$Frames = 60,
    [string]$Out = "",
    [string]$Gpu = "",
    [string]$Icd = "",
    [switch]$NoQuery
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if ($Out -eq "") { $Out = "bench-report-$env:COMPUTERNAME.md" }
if ($Icd -ne "") { $env:VK_DRIVER_FILES = $Icd; $env:VK_ICD_FILENAMES = $Icd }
if ($NoQuery)    { $env:HEAPSPIKE_NO_GPU_QUERY = "1" }

Write-Host "building HeapSpike (Release)..."
dotnet build -c Release -v q | Out-Null
$bin = Join-Path $PSScriptRoot "..\..\..\bin\Release\net8.0\40 - HeapSpike.exe"

$gpu = "unknown"; $driver = "unknown"
try {
    $vi = vulkaninfo 2>$null
    $gpu    = ($vi | Select-String -Pattern "deviceName\s*=\s*(.+)" | Select-Object -First 1).Matches[0].Groups[1].Value.Trim()
    $driver = ($vi | Select-String -Pattern "driverName\s*=\s*(.+)" | Select-Object -First 1).Matches[0].Groups[1].Value.Trim()
} catch {
    try { $gpu = (Get-CimInstance Win32_VideoController | Select-Object -First 1).Name } catch {}
}
$commit = try { git rev-parse --short HEAD } catch { "?" }

$lines = @(
    "# renderbench — $env:COMPUTERNAME",
    "",
    "| | |",
    "|---|---|",
    "| date | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm')) UTC |",
    "| GPU | $gpu |",
    "| driver | $driver |",
    "| OS | $([System.Environment]::OSVersion.VersionString) |",
    "| commit | $commit |",
    "| frames/round | $Frames (median of 3 rounds, 30-frame warmup) |"
)
if ($Gpu -ne "") { $lines += "| GPU filter | -Gpu $Gpu |" }
if ($Icd -ne "") { $lines += "| ICD override | $Icd |" }
if ($NoQuery)    { $lines += "| timing | fence-blocked CPU (no GPU queries) |" }
$lines += @(
    "",
    "| objects | heap GPU ms | baseline GPU ms | ratio | heap CPU ms | baseline CPU ms | ingest s | upload s |",
    "|---:|---:|---:|---:|---:|---:|---:|---:|"
)

function Get-Last([string[]]$log, [string]$pattern) {
    $m = $log | Select-String -Pattern $pattern | Select-Object -Last 1
    if ($m) { return $m.Matches[0].Groups[1].Value } else { return $null }
}

foreach ($n in ($Ns -split "\s+")) {
    Write-Host "=== n=$n ==="
    $gpuArgs = @(); if ($Gpu -ne "") { $gpuArgs = @("--gpu", $Gpu) }
    $log = & $bin renderbench --n $n --frames $Frames @gpuArgs 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) {
        $lines += "| $n | run FAILED | | | | | | |"
        $log | Select-Object -Last 3 | Write-Host
        continue
    }
    $heapG  = Get-Last $log 'renderbench\[heap\]: GPU ([0-9.]+)'
    $heapC  = Get-Last $log 'renderbench\[heap\]:.*task\.Run CPU ([0-9.]+)'
    $baseG  = Get-Last $log 'renderbench\[baked-baseline\]: GPU ([0-9.]+)'
    $baseC  = Get-Last $log 'renderbench\[baked-baseline\]:.*task\.Run CPU ([0-9.]+)'
    $ratio  = Get-Last $log 'heap/baseline = ([0-9.]+x)'
    $dev    = Get-Last $log 'renderbench: device = (.+)'
    if ($dev) { $lines = $lines | ForEach-Object { $_ -replace '^\| GPU \| .*', "| GPU | $dev |" } }
    $ingest = Get-Last $log 'ingest [0-9]+ parts: ([0-9]+)'
    $upload = Get-Last $log 'GPU upload \(cum\) [0-9.]+ MB: ([0-9]+)'
    if ($NoQuery) {
        $ratio = "{0:N2}x*" -f ([double]$heapC / [double]$baseC)
        $heapG = "—"; $baseG = "—"
    }
    $ingestS = "{0:N1}" -f ([double]($ingest ?? 0) / 1000)
    $uploadS = "{0:N1}" -f ([double]($upload ?? 0) / 1000)
    $lines += "| $n | $heapG | $baseG | $ratio | $heapC | $baseC | $ingestS | $uploadS |"
}

$lines += @(
    "",
    "*heap = storage-decoded indirect multidraw (clustered); baseline = pre-baked soup, one multidraw, fixed-function vertex input. \*ratio from fence-blocked CPU times.*"
)
$lines | Set-Content -Encoding UTF8 $Out
Write-Host "report: $Out"
Get-Content $Out
