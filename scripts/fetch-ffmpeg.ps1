# Downloads Gyan FFmpeg essentials (ffmpeg.exe + ffprobe.exe) into tools/ffmpeg/.
# Used so the app ships ffmpeg and does not depend on PATH.

param(
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $repoRoot "tools\ffmpeg"
}

$ffmpegExe = Join-Path $Destination "ffmpeg.exe"
$ffprobeExe = Join-Path $Destination "ffprobe.exe"
if ((Test-Path $ffmpegExe) -and (Test-Path $ffprobeExe)) {
    Write-Host "Bundled ffmpeg already present: $Destination"
    exit 0
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

function Copy-FromDir([string]$dir) {
    $srcFfmpeg = Join-Path $dir "ffmpeg.exe"
    $srcProbe = Join-Path $dir "ffprobe.exe"
    if ((Test-Path $srcFfmpeg) -and (Test-Path $srcProbe)) {
        Copy-Item $srcFfmpeg $ffmpegExe -Force
        Copy-Item $srcProbe $ffprobeExe -Force
        Write-Host "Copied ffmpeg from $dir"
        return $true
    }
    return $false
}

$localCandidates = @(
    "C:\ffmpeg",
    "C:\ffmpeg\bin",
    "C:\tools\ffmpeg\bin"
)
foreach ($dir in $localCandidates) {
    if (Copy-FromDir $dir) {
        & $ffmpegExe -hide_banner -version | Select-Object -First 1
        exit 0
    }
}

$cacheDir = Join-Path $repoRoot "tools\cache"
New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
$zipPath = Join-Path $cacheDir "ffmpeg-win64.zip"
$urls = @(
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
)

$downloaded = $false
foreach ($url in $urls) {
    try {
        Write-Host "Downloading FFmpeg from $url ..."
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
        $downloaded = $true
        break
    }
    catch {
        Write-Host "Download failed: $($_.Exception.Message)"
    }
}

if (-not $downloaded) {
    throw "Could not download FFmpeg. Place ffmpeg.exe and ffprobe.exe in $Destination"
}

$extractDir = Join-Path $cacheDir "extract"
if (Test-Path $extractDir) {
    Remove-Item $extractDir -Recurse -Force
}

Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

$foundFfmpeg = Get-ChildItem -Path $extractDir -Filter "ffmpeg.exe" -Recurse -File | Select-Object -First 1
$foundProbe = Get-ChildItem -Path $extractDir -Filter "ffprobe.exe" -Recurse -File | Select-Object -First 1
if (-not $foundFfmpeg -or -not $foundProbe) {
    throw "ffmpeg.exe/ffprobe.exe not found inside downloaded zip."
}

Copy-Item $foundFfmpeg.FullName $ffmpegExe -Force
Copy-Item $foundProbe.FullName $ffprobeExe -Force

Write-Host "Installed bundled ffmpeg:"
& $ffmpegExe -hide_banner -version | Select-Object -First 1
