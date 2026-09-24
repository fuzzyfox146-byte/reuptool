# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: **multi-source queue** (Version **1.0.18**, uncommitted).
- Chạy thử: `publish-1.0.19\VideoAutoTool.App.exe` (bản 1.0.18 đang bị app cũ khóa).
- Git: `eab394b` on origin/main is the speed + trim-script commit.

## Done
- Multi-source queue: each "Render số này" / "Render hết" adds intake 01, 02, … Main queue takes N videos per source (setting, default 9, locked while rendering). Remainder stays in the right-hand list. When main has no pending job, the next N of every source is promoted. Output file names unchanged. Per-job timing kept.
- Pause cancels the running ffmpeg and holds the job; Resume renders it again from the start. Queue UI updates are deferred so pause/close does not deadlock the window.
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
- Đóng app tự ghi session (template đang chọn, folder từng slot, số video render, lô hàng đợi, song song, phóng nền). Mở lại vẫn là lựa chọn cũ, vẫn sửa được.
- Ctrl+S tải lại danh sách template nhưng giữ folder vừa duyệt (nền, nguồn, …), không trả về path mặc định trong design.
- Sau Hủy: nút Render tiếp (hủy/lỗi về chờ rồi chạy lại, video xong giữ nguyên) và Xóa hàng đợi.
- User test multi-source queue: Cài đặt đổi "số video mỗi lượt" khi chưa render; tab Nguồn bấm Render số này / Render hết khi đang chạy nguồn khác.
- Tạm dừng phải dừng ffmpeg và bấm Tiếp tục render lại job đó từ đầu. Đóng cửa sổ khi đang render vẫn hỏi rồi hủy.
- Tool cat N giay dau background: `scripts\CatDauBackground.cmd`.
