# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: **render speed + per-job timing** (Version **1.0.18**).
- Git: `a5e660f` on origin/main is 1.0.17. This speed work is **uncommitted**.

## Done
- Per-file cache lock so two jobs with different backgrounds prepare in parallel (global lock was making the queue look 1-by-1).
- Cached graph uses prepared 720p clip directly (no second cover/pad/overlay of the background).
- NVENC `p1` + skip extra `-s` scaler; probe avatar/wave once; no CUDA decode of the audio driver.
- Queue log + status + chi tiết job: thời gian render từng video; đồng hồ tổng vẫn còn.

## Decisions
- Do not change design / avatar / wave layout.
- Full CUDA overlay_cuda+ASS is not used: libass and PNG alpha stay on CPU; ffmpeg 7 CUDA overlay graphs can stall.
- GPU still does NVENC encode + CUDA decode of cached backgrounds.

## Open issues / risks
- First pass of a new background still spends NVENC on cache; later jobs of the same clip are faster.
- Output on HDD (ổ D) can cap speed regardless of GPU.

## Next step
- Tool cat N giay dau background: `scripts\CatDauBackground.cmd` (keo tha folder) hoac `pwsh scripts\trim-background-start.ps1 -Folder "D:\bg" -Seconds 3`.
- App 1.0.18 van o `publish-1.0.18\VideoAutoTool.App.exe` neu hang doi cu dang khoa `publish\`.
