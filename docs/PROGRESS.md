# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: bản app gốc `publish-1.0.25`, đã gắn lại GPU 720p và chọn 480/720 trong Cài đặt. Session vẫn `%AppData%\VideoAutoTool`.
- 720p ghép CUDA (`scale_cuda` + `overlay_cuda`). 480p giữ graph CPU. Lỗi CUDA thì job render lại bằng graph CPU.
- FFmpeg in `progress=end` mà không thoát sau 15 giây thì bị dừng và job tính là xong, để hàng đợi báo «Xong» và dừng.
- Chạy thử bản gốc: `publish-1.0.25\VideoAutoTool.App.exe`. Bản GPU đủ tính năng: `publish-2.0.0-gpu\VideoAutoTool.App.exe` (tiêu đề «Video Auto Tool 2.0.0 GPU»). Cùng graph: 720p CUDA, 480p CPU, chọn kích thước, hàng đợi báo Xong. Session GPU: `%AppData%\VideoAutoTool-Gpu` (lần đầu copy từ session gốc, sau đó không ghi đè bản 1.0.25).
- Lỗi video thì dừng video, lỗi phụ đề thì dừng phụ đề. Cookie lỗi thì dừng kênh đó.
- Lỗi video thì dừng video, lỗi phụ đề thì dừng phụ đề. Cookie lỗi thì dừng kênh đó.
- Mỗi kênh tải riêng: Dừng chỉ dừng kênh đó, Tải tiếp tải phần còn thiếu. Đang tải vẫn thêm kênh khác được.
- Tải lại cùng folder: video đã có trong `source` và phụ đề đã có trong `text` thì bỏ qua. Nhập 1–50 khi đã có đến 049 chỉ tải 050, giữ đúng số, không tải đè.
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
- Full CUDA overlay_cuda+ASS is not used on 480p: measured slower than the CPU graph.
- 720p and above uses the verified CUDA graph (frames stay on GPU until `hwdownload` right before ASS). Black-key wave uses `chromakey_cuda`. Alpha wave is uploaded after a CPU scale. If that graph fails, the job retries the CPU graph.
- GPU still does NVENC encode + CUDA decode of cached backgrounds.

## Open issues / risks
- First pass of a new background still spends NVENC on cache; later jobs of the same clip are faster.
- Output on HDD (ổ D) can cap speed regardless of GPU.

## Next step
- Đóng app tự ghi session (template đang chọn, folder từng slot, số video render, lô hàng đợi, song song, phóng nền). Mở lại vẫn là lựa chọn cũ, vẫn sửa được.
- Ctrl+S tải lại danh sách template nhưng giữ folder vừa duyệt (nền, nguồn, …), không trả về path mặc định trong design.
- Sau Hủy: nút Render tiếp (hủy/lỗi về chờ rồi chạy lại, video xong giữ nguyên) và Xóa hàng đợi.
- SRT chỉ khớp khi tên trùng video nguồn (thêm mã ngôn ngữ như `.en` hoặc `.de`). Cùng số nhưng khác tiêu đề là lỗi E035, không đưa vào hàng đợi.
- Tải nguồn nằm trong tab **Tải nguồn**: nhiều kênh, chọn chất lượng 144p–1080p, video (`source`) và phụ đề (`text`) tải cùng lúc, số 001 giống nhau. Log chỉ hiện tên file và tốc độ. Cookie một file dùng chung. Cookie lỗi thì dừng hết. **Xóa lịch sử** xóa `downloaded_videos.txt` và `downloaded_subs.txt`.
- File bat `Tải video - Copy\TaiNguon.bat` vẫn dùng được ngoài app.
- User test multi-source queue: Cài đặt đổi "số video mỗi lượt" khi chưa render; tab Nguồn bấm Render số này / Render hết khi đang chạy nguồn khác.
- Tạm dừng phải dừng ffmpeg và bấm Tiếp tục render lại job đó từ đầu. Đóng cửa sổ khi đang render vẫn hỏi rồi hủy.
- Tool cat N giay dau background: `scripts\CatDauBackground.cmd`.
