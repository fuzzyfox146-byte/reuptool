param(
    [string]$Folder = "",
    [double]$Seconds = -1,
    [switch]$Recurse
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path $scriptRoot -Parent

function Find-Tool([string]$name) {
    $bundled = Join-Path $repoRoot "tools\ffmpeg\$name"
    if (Test-Path -LiteralPath $bundled) {
        return $bundled
    }

    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    foreach ($dir in @("C:\ffmpeg\bin", "C:\tools\ffmpeg\bin")) {
        $path = Join-Path $dir $name
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    throw "Khong tim thay $name. Dat ffmpeg.exe/ffprobe.exe vao tools\ffmpeg hoac PATH."
}

function Invoke-Args([string]$Exe, [string[]]$Arguments) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Exe failed (exit $LASTEXITCODE)"
    }
}

function Get-DurationSeconds([string]$ffprobe, [string]$path) {
    $raw = & $ffprobe -v "error" -show_entries "format=duration" -of "default=noprint_wrappers=1:nokey=1" -i $path
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    $value = 0.0
    if ([double]::TryParse($raw.Trim(), [ref]$value)) {
        return $value
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($Folder)) {
    $Folder = Read-Host "Duong dan folder video background"
}

$Folder = $Folder.Trim('"')
if (-not (Test-Path -LiteralPath $Folder -PathType Container)) {
    throw "Folder khong ton tai: $Folder"
}

if ($Seconds -lt 0) {
    $rawSeconds = Read-Host "So giay cat o dau video (vi du 3)"
    $Seconds = [double]::Parse($rawSeconds.Trim(), [System.Globalization.CultureInfo]::InvariantCulture)
}

if ($Seconds -le 0) {
    throw "Seconds phai > 0"
}

$ffmpeg = Find-Tool "ffmpeg.exe"
$ffprobe = Find-Tool "ffprobe.exe"
$allowed = @(".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v")
$search = Get-ChildItem -LiteralPath $Folder -File -Recurse:$Recurse | Where-Object {
    $allowed -contains $_.Extension.ToLowerInvariant() -and $_.Name -notlike "*.vat-trim*"
}
$files = @($search | Sort-Object FullName)
if ($files.Count -eq 0) {
    Write-Host "Khong co video trong folder."
    exit 0
}

Write-Host "Folder : $Folder"
Write-Host "Cat    : $Seconds giay dau"
Write-Host "FFmpeg : $ffmpeg"
Write-Host "So file: $($files.Count)"
Write-Host ""

$ok = 0
$skip = 0
$fail = 0

foreach ($file in $files) {
    $path = $file.FullName
    $duration = Get-DurationSeconds $ffprobe $path
    if ($null -eq $duration) {
        Write-Host "[SKIP] Khong doc duoc duration: $($file.Name)"
        $skip++
        continue
    }

    if ($duration -le ($Seconds + 0.05)) {
        Write-Host ("[SKIP] Ngan hon {0:0.###}s: {1} (D={2:0.##}s)" -f $Seconds, $file.Name, $duration)
        $skip++
        continue
    }

    $temp = Join-Path $file.DirectoryName ($file.BaseName + ".vat-trim" + $file.Extension)
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Force
    }

    Write-Host ("[RUN ] {0}  ({1:0.##}s -> {2:0.##}s)" -f $file.Name, $duration, ($duration - $Seconds))
    try {
        Invoke-Args $ffmpeg @(
            "-y", "-hide_banner", "-loglevel", "error", "-nostdin",
            "-ss", $Seconds.ToString("0.###", [System.Globalization.CultureInfo]::InvariantCulture),
            "-i", $path,
            "-c", "copy",
            "-map", "0",
            "-avoid_negative_ts", "make_zero",
            $temp
        )

        if (-not (Test-Path -LiteralPath $temp) -or ((Get-Item -LiteralPath $temp).Length -lt 1024)) {
            throw "Temp file missing or too small"
        }

        Remove-Item -LiteralPath $path -Force
        Move-Item -LiteralPath $temp -Destination $path
        Write-Host "[OK  ] $($file.Name)"
        $ok++
    }
    catch {
        Write-Host "[FAIL] $($file.Name): $($_.Exception.Message)"
        if (Test-Path -LiteralPath $temp) {
            Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
        }

        $fail++
    }
}

Write-Host ""
Write-Host "Xong: $ok ok, $skip skip, $fail loi."
if ($fail -gt 0) {
    exit 1
}
