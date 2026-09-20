# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: **GPU-first encode** (Version **1.0.14**).
- Latest: Auto/NVENC uses **h264_nvenc** (preset p4) for final render **and** cache prepare-background. Video inputs get `-hwaccel cuda` (or d3d11va). Queue label shows NVENC/CPU. Default parallel jobs = 2.

## Done
- v1.0.13: cache `.tmp.mp4` muxer fix.
- **v1.0.14**: EncoderSelector returns `EncodeSettings`; cache no longer hardcodes libx264.

## Decisions
- Filters stay CPU (verified overlay/ass graph). GPU = hardware **decode** + NVENC **encode**.
- Wave cache stays qtrle (alpha). Avatar PNG stays CPU (tiny).
- Forced X264 in template still CPU encode, but may still hw-decode.

## Open issues / risks
- NVENC/hwaccel not integration-tested in this session (needs NVIDIA on the run machine).
- If `-hwaccel cuda` breaks a filter job, fallback is still missing (retry without hwaccel).

## Next step
- User: publish exe, Render thử, check queue label “NVENC GPU”.
