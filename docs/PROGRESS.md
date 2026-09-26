# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: bản gốc `publish-1.0.25`; bản GPU `publish-2.0.0-gpu`; bản tối ưu `publish-2.1.3-opt`.
- Session: gốc `%AppData%\VideoAutoTool`; GPU `%AppData%\VideoAutoTool-Gpu`; Opt `%AppData%\VideoAutoTool-Opt` (lần đầu copy từ Gpu, rồi gốc; không ghi đè bản kia).
- Nền người dùng đã chuyển tay sang `C:\background` (12 file). Nguồn và file xuất giữ trên D (HDD).
- 720p vẫn CUDA overlay, 480p vẫn CPU graph, NVENC `p1`. Lỗi CUDA thì render lại graph CPU.
- Git: `eab394b` on origin/main is the speed + trim-script commit (Opt chưa commit).

## Done
- Avatar: ảnh tĩnh scale **một lần**, `overlay eof_action=repeat` (không `-loop 1` mỗi khung).
- Wave: cache bake (fps + scale + lumakey nếu nền đen) lossless qtrle. Graph CPU dùng bản bake. 720p wave nền đen vẫn `chromakey_cuda` trên file gốc.
- Cache nền encode **cả DurationFull** (khóa cache vốn ghi vậy). Video dài hơn không còn ăn cache ngắn rồi bị `-shortest` cắt.
- Input nguồn: `-vn -sn -dn` — chỉ lấy tiếng, không demux hình.
- Sau `progress=end`: chỉ kill ffmpeg khi file `.part` **không đổi size 15 giây** (faststart trên HDD không bị cắt giữa chừng).
- File `.part` ghi tạm trên SSD (`%LocalAppData%\VideoAutoTool\out-tmp`) khi C còn trống ≥ 2× ước tính + 5 GB; xong mới copy sang D. Không đủ chỗ thì ghi thẳng D.
- Không `string.Replace` thời lượng vào filter (trước đây duration `4` biến `yuv420p` thành `yuv320p`).
- `dotnet test`: 147 passed. Smoke CLI 3s: `testdata/opt-smoke` ra đúng 3.000s.
- Hàng đợi ghi `%AppData%\<edition>\queue.json` (job + plan + template nguồn). Mở lại khôi phục; job đang chạy lúc đóng về Chờ. Bấm **Render tiếp** để chạy phần còn. **Xóa hàng đợi** vẫn xóa file.
- Tải nguồn: không ghi file lịch sử, không còn nút Xóa lịch sử. Video tải tiếp từ số mp4 lớn nhất + 1 đến ô Đến. Phụ đề quét số còn thiếu; số nào không ra file thì nghỉ 8s rồi thử lại cùng số (tối đa 4 lần), giữa các số nghỉ 3s. `.json3` vẫn tính là đã có.

## Decisions
- Không đổi layout / preset NVENC / màu bt709 / graph 480p `gbrp`.
- Không tự copy nền trong app. Không giới hạn `filter_complex_threads` (chưa đo, giữ ổn định).
- Không so SSIM với file 75 phút của bản 2.0.0-gpu trong phiên này (cần một job ngắn do người dùng render hai bản).

## Open issues / risks
- Tải phụ đề: lỗ SRT vẫn còn nếu 4 lần thử đều không ra file (clip không có sub, hoặc cookie chết). Lần Tải tiếp sẽ xin lại các số trống.
- Lần đầu mỗi nền: cache encode cả clip (lâu hơn trước nếu nền dài hơn video). Các job sau dùng lại thì nhanh và đúng độ dài.
- Cache + `.part` tạm cùng nằm trên C. Giữ ~40 GB trống.
- SSIM ≥ 0.99 so với `publish-2.0.0-gpu` chưa đo.

## Next step
- Publish lại `publish-2.1.3-opt` sau khi có thử lại phụ đề. Tải tiếp: phụ đề lỗi thì thử lại cùng số, không nhảy cóc ngay.
- Đừng mở đồng thời hai bản Opt rồi ghi cùng file xuất.
