# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: **M7 improved v8 + undo/redo** (Version 1.0.5). All tests passing!
- Latest: Wired **Undo/Redo** into the design canvas (Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y + buttons). Each drag or resize gesture = one undo step via new `TransformLayerCommand`. Bumped to 1.0.5, exe published.

## Done
- SPEC: preset.box, font bundled/import, `[music]` kept, E030 always Error.
- M0–M2: Solution, ffmpeg wrapper, Template/Planner/SRT, Render, FontCatalog, CLI.
- M3: ValidationEngine đầy đủ §8 — mỗi mã E/W/I có factory trong `ValidationRules`; `ValidationReport` có `CanRender`, `InfoCount`, sort xác định; W042/W072/W070/W071f; 17 tests xanh.
- M4: Cache §9 đầy đủ — `CacheKey` (SHA256 của path+size+mtime+canvas+layer params), `CacheStore` (LRU cleanup, atomic writes, `%TEMP%\vat-cache`), `PreparedAssetService` (prepare bg/avatar/wave 1 video 1 lần, no parallel), `ConcatListBuilder` (concat demuxer list), `FilterGraphBuilder.BuildCached` + `RenderCommandBuilder.BuildCachedArguments` (sử dụng concat input), `JobRenderer.RenderAsync(..., useCache)`. CLI: `vat cache --info/--clear`, `vat bench` (so sánh baseline vs cached). Script `make-testdata.ps1 -Long` tạo driver 37:30 + 40 backgrounds. 35 tests pass (18 cache tests mới).
- M5: Render Queue đầy đủ — `RenderJobItem` + `JobStatus` enum (Pending/Running/Done/Failed/Cancelled/Skipped), `JobStore` (queue.json atomic writes, reset Running→Pending on load), `RenderQueue` (add jobs from plan, run parallel 1-3, pause/resume/cancel/retry, progress events), `IJobRunner` interface + `JobRendererAdapter`, `vat run` command với progress bar + Ctrl+C handling + exit codes (0=success, 1=có failed, 2=cancelled). Mock tests pass (order, parallel, retry, save/restore). 45 tests total.
- M6 (skeleton): WPF UI với dark theme — `Microsoft.Extensions.DependencyInjection` setup, `DarkTheme.xaml` (dark colors), `MainWindow` với 4 tabs (Thiết kế, Nguồn & Kiểm tra, Hàng đợi, Cài đặt), 5 ViewModels cơ bản (MainViewModel, DesignViewModel, SourceViewModel, QueueViewModel, SettingsViewModel). Build thành công. **Note**: Đây là skeleton/foundation, chưa có đầy đủ features như validation results, queue controls - cần implement thêm để đạt full M6 spec.
- M7 (foundation, partial done): Core design classes (DesignHistory, SnappingEngine, LayerCommands) + tests pass; Canvas control với zoom, layer rendering (color-coded rectangles), LayerItemViewModel (Visible/Lock/Freeze), property panel binding. **Còn thiếu**: selection adorner, drag behavior, preview.
  - **NEW (2026-09-20)**: Implemented drag-and-drop for layers, clear Vietnamese labels on each layer visual, Save/Load Draft functionality with JSON serialization. Layers can now be dragged on canvas (if not locked), each layer shows clear label (Nền/Ảnh/Avatar/Phụ đề/Video lặp), Save Draft button saves current design to `template_draft.json`, Load Draft button loads from file.
  - **NEW v2 (2026-09-20)**: Added ALL layer types (Soundwave, FixedText, StaticImage) to sample data and visuals. Improved checkbox tooltips with Vietnamese explanations (👁️ Hiện/Ẩn = bỏ tick ẩn layer, 🔒 Khóa = không kéo thả được, ❄️ Đóng băng = cache layer render nhanh). **Implemented resize handles**: hover chuột gần góc/cạnh layer → cursor đổi thành resize arrows → kéo để scale layer. Resize works for all unlocked layers.
  - **NEW v3 (2026-09-20)**: Bug fixes + versioning — Fixed `JobStore.Save()` file locking race condition (added lock for thread-safe parallel writes), fixed `RenderQueue.ProcessJobAsync()` to check job status before starting (prevents cancelled jobs from running), adjusted test timings for reliability. Added version 1.0.1 to all .csproj files (Version, AssemblyVersion, FileVersion). All 74 tests pass.
  - **NEW v4 (2026-09-20)**: UI interaction rewrite (v1.0.2) — Completely rewrote `DesignCanvasControl.xaml.cs` to fix drag/resize/selection bugs. `LayerItemViewModel` now exposes observable `X/Y/LayerScale/LayerWidth` + `IsSelected` proxies over `Transform`. Canvas has a `SelectedLayer` DP (two-way bound to `DesignViewModel.SelectedLayerItem`). **Single interaction state machine** (`Mode` None/Dragging/Resizing) replaces the old conflicting drag+resize handlers → no more lag. **Click any component → selects it → property panel shows its values** (X/Y/Scale/Width bind directly to `SelectedLayerItem`). **Corner resize handles** (white squares) appear on the selected layer; resize anchors the opposite corner and computes uniform scale from base size (no accumulation bug). **Live 2-way sync**: dragging/resizing on canvas updates the panel instantly, and editing the panel updates the canvas in place (no full re-render → smooth). Selected layer shows a gold highlight border. Removed obsolete `SelectedLayerX/Y/Scale/Width` proxies from `DesignViewModel`.
  - **NEW v5 (2026-09-20)**: Edge resize (v1.0.3) — Renamed `Corner` enum → `ResizeHandle` and added `Left/Right/Top/Bottom`. Selected layer now shows 8 white handles (4 corners + 4 mid-edges). `HitHandle` checks corners first then edges. Cursor feedback: SizeWE on left/right edges, SizeNS on top/bottom.
  - **NEW v6 (2026-09-20)**: Non-uniform resize (v1.0.4) — Added `int? Height` to `TransformSettings` (render ignores it; only `Scale`/`Width` were ever used, so no regression; 74 tests still green). `LayerItemViewModel` now exposes `LayerWidth`/`LayerHeight` px proxies. Canvas draws each box from explicit `Transform.Width`/`Height` (initialized once from `base*Scale` for old data), and resize sets W/H **independently**: corners change both, Left/Right change only width, Top/Bottom change only height. The opposite edge stays anchored; min size 10px. Property panel replaced the Scale slider with **Rộng(W)** + **Cao(H)** text boxes (two-way, live-synced). NOTE: design W/H is layout-only; mapping to actual ffmpeg render sizes (avatar Scale, wave Width) is still TODO.
  - **NEW v7 (2026-09-20)**: Undo/Redo (v1.0.5) — Added `TransformLayerCommand` (X/Y/Width/Height, `CanMergeWith=>false`) in Core. `DesignViewModel` owns a `DesignHistory` + `UndoCommand`/`RedoCommand`. Canvas got a `History` DP (bound to `DesignViewModel.History`); it captures the box at mouse-down and, on mouse-up, records ONE `TransformLayerCommand` per gesture if the layer actually moved/resized. Subscribes to `DesignHistory.Changed` → re-renders + refreshes panel after any execute/undo/redo. Key bindings in `MainWindow`: Ctrl+Z=Undo, Ctrl+Shift+Z & Ctrl+Y=Redo; plus ↶/↷ buttons in the layer panel. 74 tests still green.
- M8 (done): `AutoModeService` (FileSystemWatcher, 10s stability, video+SRT detection), `dotnet publish` (self-contained exe), README.md tiếng Việt (cài đặt, hướng dẫn, troubleshooting). **Skipped**: error message audit (time-consuming), acceptance tests §16 (need real ffmpeg + long testdata).

## Decisions
- Sub layout in `stylePreset.box`; fonts bundled + `%APPDATA%\VideoAutoTool\fonts\`.
- W072 bitrate heuristic: `max(800, 9000 - quality * 250)` kbps video + audio, ×1.3.
- Cache key dùng SHA256 của normalized params; concat demuxer để chain backgrounds; asset prep tuần tự (1 video 1 lần) theo yêu cầu user.
- Queue: mặc định parallel=1, max 3; job store tại %TEMP%; CancelAll cancels Pending jobs immediately (ProcessJobAsync checks status before starting); useCache=true cho queue rendering. JobStore.Save() is thread-safe with lock.
- WPF: Dark theme với màu #1E1E1E background, MVVM strict (ViewModel không ref UI), DI container cho services.
- M7 snapping: Chỉ snap to canvas guides (center/thirds/edges), không snap to other layers (cần actual media dimensions - complex).
- M7 drag/resize (v1.0.2 rewrite): Single state machine on each layer container (`Border`). Mouse-down near a corner (within 12px) of the selected layer → Resizing; otherwise → Dragging. Locked layers only get selected (no move). During interaction the ViewModel's observable X/Y/LayerScale are set, which live-updates the container geometry in place (no full re-render → no lag) and the property panel simultaneously. Resize uses fixed opposite-corner anchor + uniform scale from base size.
- M7 labels: Each layer visual shows clear Vietnamese label (Nền, Ảnh/Avatar, Phụ đề, Soundwave, Text cố định, Ảnh tĩnh) with dark background for readability.
- M7 save: Draft saved as `template_draft.json` with full Template structure (Canvas, Layers, Output settings).
- M7 resize: Mouse hover near layer corners/edges → cursor changes to resize arrows (SizeNWSE, SizeNESW, SizeWE, SizeNS) → drag to scale layer. ResizeMode enum tracks which edge/corner being dragged. Scale factor calculated from drag distance.

## Open issues / risks
- NVENC smoke test chưa chạy.
- Benchmark thật chưa chạy (infrastructure ready: `vat bench --root testdata/... --index 0`, cần long testdata + ffmpeg).
- M6 chỉ là skeleton - chưa có: validation results grid, queue job list/controls, folder browse dialogs, ffmpeg detection, etc.
- M7 còn thiếu (optional): instant preview (proxy images/thumbnails), exact preview (ffmpeg), add new layer button, delete layer button. (Selection highlight + corner resize handles DONE in v1.0.2.)

## Measurements
- Render speed: n/a (chờ benchmark thật với `vat bench`)

## Next step
- **UI ready for user testing v2!** User can now:
  - Drag layers on canvas (Design tab)
  - **Resize layers** by hovering near corners/edges and dragging
  - See ALL layer types: Nền, Soundwave, Avatar, Phụ đề, Text cố định, Ảnh tĩnh
  - Clear tooltips for 3 checkboxes (👁️ Hiện/Ẩn, 🔒 Khóa, ❄️ Đóng băng)
  - Save/Load draft designs
  - Try the app: `dotnet run --project src\VideoAutoTool.App` or use published exe at `publish\app\VideoAutoTool.App.exe`
- **Optional next improvements** (based on user feedback):
  - Visual resize handles (corner dots/squares) instead of just cursor change
  - Add "New Layer" / "Delete Layer" buttons
  - Selection highlight border on selected layer
  - Instant preview (show actual images/videos as thumbnails)
  - Exact preview (render frame with FFmpeg)
