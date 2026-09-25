# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: bản gốc `publish-1.0.25`; bản GPU `publish-2.0.0-gpu`; bản tối ưu `publish-2.1.2-opt` (tải nguồn: mỗi số = đúng 1 video playlist, không lệch SRT khi cookie lỗi).
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
- Tải nguồn: mỗi số (002) chỉ lấy đúng 1 mục playlist; lỗi/cookie giữa chừng không đẩy autonumber (trước đây 002 video có thể là clip 003). Lỗi 1 clip vẫn tải clip sau. Log không còn hiện file thumbnail (logo .jpg) như đang tải video.

## Decisions
- Không đổi layout / preset NVENC / màu bt709 / graph 480p `gbrp`.
- Không tự copy nền trong app. Không giới hạn `filter_complex_threads` (chưa đo, giữ ổn định).
- Không so SSIM với file 75 phút của bản 2.0.0-gpu trong phiên này (cần một job ngắn do người dùng render hai bản).

## Open issues / risks
- Lần đầu mỗi nền: cache encode cả clip (lâu hơn trước nếu nền dài hơn video). Các job sau dùng lại thì nhanh và đúng độ dài.
- Cache + `.part` tạm cùng nằm trên C. Giữ ~40 GB trống.
- SSIM ≥ 0.99 so với `publish-2.0.0-gpu` chưa đo.

## Next step
- Chạy `publish-2.1.2-opt\VideoAutoTool.App.exe`. File 002/003 đã lệch từ lần tải cũ thì xóa cặp đó rồi **Tải tiếp** (không xóa cả folder).
- Đừng mở đồng thời hai bản Opt rồi ghi cùng file xuất.
