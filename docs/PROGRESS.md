# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: **settings scale + parallel 2 + larger ASS** (Version **1.0.17**).
- Git: visual-fix 1.0.16 still **uncommitted**, this continues on that tree.

## Done
- Settings: background scale 100–250% (default 150), applied at render (cache key includes scale).
- Settings: parallel combo actually binds (was ComboBoxItem vs int). Default **2**. Cache prep serialized so 2 jobs don't race.
- Subtitles: ASS `\fs` + color override; font size = max(Cỡ, 36% chiều cao hộp Sub). Default Cỡ 72. Preview canvas follows that size.
- CPU: dropped extra `scale=W:H` after overlays (encode still NVENC). Overlay/ASS remain CPU by design.

## Decisions
- Do not change avatar/soundwave layout.
- Two concurrent NVENC encodes on RTX 5060; cache prepare is one-at-a-time then both encode.

## Open issues / risks
- Saved session ParallelCount=1 still restores to 1 until user picks 2 in Cài đặt.
- NVENC dual-session not integration-tested here.

## Next step
- Đã publish lại 1.0.17: `publish\VideoAutoTool.App.exe`. Chạy file này (đóng bản cũ nếu còn mở).
