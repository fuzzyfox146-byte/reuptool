# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: **concat list UTF-8 no BOM** (Version **1.0.15**).
- Git: `621fc11` on `main` (queue/session/ffmpeg/NVENC/cache) **before** this concat fix.
- Latest: ffmpeg concat failed with `unknown keyword '﻿file'` because `Encoding.UTF8` writes a BOM. Concat list is now UTF-8 **without** BOM.

## Done
- Commit `621fc11` Ship queue, session save, bundled FFmpeg, NVENC, and cache pipeline.
- Stopped ignoring `src/.../Cache` (gitignore was `cache/` matching any folder named cache).

## Decisions
- Concat demuxer list: `UTF8Encoding(encoderShouldEmitUTF8Identifier: false)`.

## Open issues / risks
- NVENC/hwaccel still not integration-tested beyond encode probe.

## Next step
- User: publish 1.0.15, Render thử lại.
