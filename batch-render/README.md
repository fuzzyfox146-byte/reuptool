# Render khung cố định

Tool Python + FFmpeg, tách khỏi Video Auto Tool. Toạ độ lấy từ ô trống trên khung tham chiếu 1672×941, **không** tự dò vùng trong suốt.

Ảnh ví dụ: video nền phủ kín 1280×720, video chính 1024×576 đặt giữa (hơi lớn hơn ô trống nên mép bị khung che), khung PNG ở trên, lớp nhiễu mỏng trên cùng. Tiếng chỉ lấy từ video chính.

## Cài đặt

Cần Python 3.10+ và FFmpeg/ffprobe trong PATH.

```powershell
& "$env:LocalAppData\Programs\Python\Python312\python.exe" --version
ffmpeg -version
```

Không cần cài thư viện ngoài.

## Thư mục

```text
videos/        video chính
frames/        khung PNG (có alpha)
backgrounds/   video nền
output/        file xuất
cache/         tool tự tạo
```

Đặt file vào 3 thư mục nguồn rồi bấm đúp `run.bat` để mở cửa sổ hàng đợi.

Trong cửa sổ: **Thêm vào hàng đợi**, **Bắt đầu**, **Tạm dừng**, **Tiếp tục**, **Hủy**, **Render tiếp** (đưa video lỗi và video đã hủy về chờ), **Xóa hàng đợi**. Đóng cửa sổ lúc đang chạy thì video đó về Chờ. Mở lại vẫn còn hàng đợi (`queue.json`).

Lệnh không cần cửa sổ, trong thư mục `batch-render`:

```powershell
& "$env:LocalAppData\Programs\Python\Python312\python.exe" render.py --videos videos --frames frames --backgrounds backgrounds --out output
```

Hoặc `run.bat` (thêm cờ phía sau nếu cần).

Tên file xuất: `<tên video>_<tên khung>.mp4`. File đã có và khác rỗng thì bỏ qua.

## Lớp (dưới lên trên)

1. Nền: cover-crop 1280×720, 25fps, không tiếng. Ngắn hơn video chính thì lặp.
2. Video chính: 80% chiều rộng canvas, 16:9, crop giữa, tại x=130, y=72, opacity 0.90.
3. Khung PNG đã scale 1280×720, hiện suốt thời lượng.
4. Nhiễu blend overlay, opacity 0.30, đổi hình mỗi 2 frame (tạo ở 12.5fps rồi nhân lên 25fps).

Độ dài output = độ dài video chính (`ffprobe`, `-t`).

## Chỉnh khi render

Mặc định nằm đầu file `render.py`. Có thể đè bằng cờ:

| Cờ | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `--video-scale` | 0.80 | Tỉ lệ chiều rộng video chính |
| `--video-opacity` | 0.90 | Độ đặc video chính |
| `--noise-opacity` | 0.30 | Độ nhiễu |
| `--noise-speed` | 0.5 | 0.5 = nhiễu giữ 2 frame |
| `--crf` | 20 | Chất lượng (CPU CRF, GPU CQ) |
| `--preset` | medium | Preset libx264 |
| `--width` `--height` `--fps` | 1280 720 25 | Canvas |
| `--workers` | số nhân / 2 | Số ffmpeg cùng lúc |
| `--gpu` | tắt | `h264_nvenc`, tối đa 3 phiên |
| `--random` | tắt | Xáo khung và nền, không lặp đến khi hết bộ |
| `--rebuild-cache` | tắt | Xóa `cache/` rồi làm lại |

Đổi kích thước hoặc scale thì video chính vẫn canh **tâm ô trống**, không dò alpha. Đổi opacity không làm lại cache nền.

## Cache

`cache/frames`, `cache/backgrounds`, `cache/effects/noise_loop.mp4`.

Khóa = đường dẫn + mtime + kích thước + tham số. Chỉ mục đổi mới được tạo lại. Cache xong hết rồi mới render. Render đọc file cache, mỗi worker một ffmpeg.

Cặp video / khung / nền được ghi vào `output/plan.json` trước khi chia worker. Chạy lại giữ cặp cũ; video mới nối tiếp vòng.

Một file lỗi không dừng cả mẻ. Log từng file: `output/logs/`.
