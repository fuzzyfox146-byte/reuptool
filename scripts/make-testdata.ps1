param(
    [switch]$Long
)

$ErrorActionPreference = "Stop"
$root = Join-Path $PSScriptRoot ".." "testdata" "001 AngelsAreCalling-01"
$dirs = @(
    "source\NGUON",
    "text\SUB",
    "background",
    "avatar",
    "soundwave",
    "xuat_render"
)

foreach ($d in $dirs) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root $d) | Out-Null
}

function Invoke-FF([string[]]$Args) {
    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if (-not $ffmpeg) { throw "ffmpeg not found in PATH" }
    & ffmpeg @Args
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed: $($Args -join ' ')" }
}

function Ensure-File([string]$Path, [scriptblock]$Create) {
    if (Test-Path -LiteralPath $Path) {
        Write-Host "skip $Path"
        return
    }
    & $Create
    Write-Host "created $Path"
}

$driver1 = Join-Path $root "source\NGUON\001 🔴 Jesus Saw What Was Coming, Something Unexpected Is About To Transform Your Life.mp4"
Ensure-File $driver1 {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "testsrc=s=320x180:r=30:d=14", "-f", "lavfi", "-i", "sine=frequency=440:duration=14", "-c:v", "libx264", "-c:a", "aac", "-shortest", $driver1)
}

$driver2 = Join-Path $root "source\NGUON\002 ⚠️ Archangel Michael Says You Are Chosen.mp4"
Ensure-File $driver2 {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "testsrc=s=320x180:r=30:d=9", "-f", "lavfi", "-i", "sine=frequency=550:duration=9", "-c:v", "libx264", "-c:a", "aac", "-shortest", $driver2)
}

$driver3 = Join-Path $root "source\NGUON\003 🔥 Third Video With Longer Duration To Chain Backgrounds.mp4"
Ensure-File $driver3 {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "testsrc=s=320x180:r=30:d=21", "-f", "lavfi", "-i", "sine=frequency=330:duration=21", "-c:v", "libx264", "-c:a", "aac", "-shortest", $driver3)
}

Ensure-File (Join-Path $root "background\bg1.mp4") {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "gradients=s=1920x1080:r=30:d=7:speed=0.05", "-c:v", "libx264", "-pix_fmt", "yuv420p", (Join-Path $root "background\bg1.mp4"))
}
Ensure-File (Join-Path $root "background\bg2_has_voice.mp4") {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "testsrc2=s=1920x1080:r=30:d=6", "-f", "lavfi", "-i", "sine=frequency=1000:duration=6", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", (Join-Path $root "background\bg2_has_voice.mp4"))
}
Ensure-File (Join-Path $root "background\bg3.mp4") {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "mandelbrot=s=1280x720:r=30", "-t", "8", "-c:v", "libx264", "-pix_fmt", "yuv420p", (Join-Path $root "background\bg3.mp4"))
}

Ensure-File (Join-Path $root "soundwave\wave1.mov") {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "aevalsrc='sin(2*PI*300*t)*sin(2*PI*2*t)':d=2.5:s=44100", "-filter_complex", "showwaves=s=640x160:mode=cline:colors=0x00E5FF:rate=60,format=yuva444p,colorkey=black:0.1:0.05,format=argb", "-c:v", "qtrle", "-pix_fmt", "argb", (Join-Path $root "soundwave\wave1.mov"))
}

Ensure-File (Join-Path $root "wave_black.mp4") {
    Invoke-FF @("-y", "-f", "lavfi", "-i", "aevalsrc='sin(2*PI*300*t)*sin(2*PI*2*t)':d=2.5:s=44100", "-filter_complex", "showwaves=s=640x160:mode=cline:colors=0xFFAA00:rate=60,format=yuv420p", "-c:v", "libx264", "-pix_fmt", "yuv420p", (Join-Path $root "wave_black.mp4"))
}

foreach ($pair in @(
        @{ Name = "avatar1.png"; Color = "0xFF0000FF"; Label = "A1"; W = 500; H = 700 },
        @{ Name = "avatar2.png"; Color = "0xFF00FF00"; Label = "A2"; W = 800; H = 800 },
        @{ Name = "avatar3.png"; Color = "0xFFFF0000"; Label = "A3"; W = 600; H = 900 }
    )) {
    $path = Join-Path $root "avatar\$($pair.Name)"
    Ensure-File $path {
        Invoke-FF @(
            "-y", "-f", "lavfi", "-i", "color=c=$($pair.Color)@0.0:s=$($pair.W)x$($pair.H)",
            "-vf", "drawbox=x=50:y=50:w=200:h=300:color=$($pair.Color)@0.8:t=fill,drawtext=text='$($pair.Label)':fontcolor=white:fontsize=48:x=120:y=180",
            "-frames:v", "1", "-update", "1", $path
        )
    }
}

$srtTemplate = @'
1
00:00:01,000 --> 00:00:04,000
<i>Hello</i> test [music] line

2
00:00:05,000 --> 00:00:08,000
Second cue with
two lines joined

'@

foreach ($n in 1..3) {
    $names = @(
        "001 🔴 Jesus Saw What Was Coming, Something Unexpected Is About To Transform Your Life.en.srt",
        "002 ⚠️ Archangel Michael Says You Are Chosen.en.srt",
        "003 🔥 Third Video With Longer Duration To Chain Backgrounds.en.srt"
    )
    $path = Join-Path $root "text\SUB\$($names[$n - 1])"
    Ensure-File $path {
        [System.IO.File]::WriteAllText($path, $srtTemplate, [System.Text.UTF8Encoding]::new($true))
    }
}

if ($Long) {
    Write-Host "Creating long test data..."
    
    # Driver dài 37:30 (2250s) - uses veryfast preset for speed
    $longDriver = Join-Path $root "source\NGUON\999 🔥 Long Video 37min30s.mp4"
    Ensure-File $longDriver {
        Invoke-FF @("-y", "-f", "lavfi", "-i", "testsrc=s=320x180:r=30:d=2250", 
                    "-f", "lavfi", "-i", "sine=frequency=400:duration=2250",
                    "-c:v", "libx264", "-preset", "veryfast", "-crf", "28",
                    "-c:a", "aac", "-shortest", $longDriver)
    }
    
    # 40 backgrounds ngắn (3-8s, xoay vòng độ dài) - 1920x1080 resolution
    for ($i = 1; $i -le 40; $i++) {
        $dur = 3 + ($i % 6)
        $bgFile = Join-Path $root "background\long_bg$("{0:D2}" -f $i).mp4"
        Ensure-File $bgFile {
            Invoke-FF @("-y", "-f", "lavfi", "-i", "mandelbrot=s=1920x1080:r=30",
                        "-t", "$dur", "-c:v", "libx264", "-preset", "veryfast",
                        "-crf", "23", "-pix_fmt", "yuv420p", $bgFile)
        }
    }
    
    # SRT cho long video (3-4 cues)
    $longSrt = Join-Path $root "text\SUB\999 🔥 Long Video 37min30s.en.srt"
    $longSrtContent = @'
1
00:00:10,000 --> 00:00:15,000
Long video first cue

2
00:05:00,000 --> 00:05:05,000
Five minutes in

3
00:15:30,000 --> 00:15:35,000
Halfway through [music]

4
00:30:00,000 --> 00:30:05,000
Near the end
'@
    Ensure-File $longSrt {
        [System.IO.File]::WriteAllText($longSrt, $longSrtContent, [System.Text.UTF8Encoding]::new($true))
    }
    
    Write-Host "Long test data (37:30 driver + 40 backgrounds) created"
}

Write-Host "testdata ready at $root"
