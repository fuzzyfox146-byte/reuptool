# Video Auto Tool (VAT)

Công cụ Windows tự động hóa sản xuất video hàng loạt từ template thiết kế, sử dụng FFmpeg.

## Tính năng chính

- **Template thiết kế**: Canvas 1280x720 với drag/drop layers (background, avatar, wave, subtitle)
- **Batch processing**: Xử lý nhiều video cùng lúc từ folder driver
- **Queue rendering**: Hàng đợi render với pause/resume/cancel/retry
- **Cache/Freeze**: Cache prepared assets để render nhanh hơn (dành cho video dài)
- **Validation**: Kiểm tra tự động file, duration, codec, font trước khi render
- **Auto mode**: Theo dõi folder và tự động thêm video mới vào queue (khi có cả MP4 + SRT)
- **NVENC support**: Tự động dùng NVIDIA GPU encoding nếu có

## Yêu cầu hệ thống

- **Windows 10/11** (64-bit)
- **.NET 8 Runtime** (app tự động yêu cầu cài nếu thiếu)
- **FFmpeg**: Download từ [ffmpeg.org](https://ffmpeg.org/download.html#build-windows), giải nén và thêm vào PATH, hoặc đặt trong folder app

### Cài đặt FFmpeg (nếu chưa có)

1. Download FFmpeg build cho Windows: https://www.gyan.dev/ffmpeg/builds/ (chọn "ffmpeg-release-essentials.zip")
2. Giải nén file ZIP
3. Copy folder `ffmpeg-xxx\bin` vào `C:\ffmpeg`
4. Thêm `C:\ffmpeg\bin` vào biến môi trường PATH:
   - Mở **System Properties** > **Environment Variables**
   - Tìm biến **Path** trong **System variables**, click **Edit**
   - Click **New**, nhập `C:\ffmpeg\bin`, click **OK**
5. Mở Command Prompt mới và gõ `ffmpeg -version` để kiểm tra

## Cài đặt

1. Download file `VideoAutoTool-v1.0.0-win-x64.zip` từ Releases
2. Giải nén vào folder bất kỳ (ví dụ: `C:\VideoAutoTool`)
3. Chạy `VideoAutoTool.App.exe` để mở giao diện, hoặc `vat.exe` để dùng CLI

## Hướng dẫn sử dụng

### Tab "Thiết kế"
- **Canvas 1280x720**: Vùng thiết kế video output
- **Zoom**: 50% / Vừa khung / 100%
- **Layers**: Danh sách các lớp (Background, Avatar, Subtitle, Wave...)
  - **Visible** ☑: Hiện/ẩn layer
  - **Lock** 🔒: Khóa layer (không chỉnh sửa được)
  - **Freeze** ❄: Freeze layer (cache sẵn, render nhanh hơn cho video dài)
- **Thuộc tính**: Chỉnh X, Y, Scale của layer đang chọn
- **Lưu template**: File > Save As > chọn tên file .json

### Tab "Nguồn & Kiểm tra"
- **Thư mục gốc**: Chọn folder chứa video driver + SRT
- **Scan**: Tìm tất cả file MP4 trong folder con
- **Validation**: Kiểm tra file thiếu, codec không hỗ trợ, font thiếu, etc.
- **Filter**: Chọn chỉ render các job không có lỗi Error

### Tab "Hàng đợi"
- **Add to queue**: Thêm các job đã validate vào hàng đợi
- **Start**: Bắt đầu render (parallel 1-3 jobs)
- **Pause/Resume**: Tạm dừng/tiếp tục
- **Cancel**: Hủy các job Pending (job đang Running sẽ chạy đến khi xong)
- **Retry**: Thử lại các job Failed
- **Progress**: Theo dõi tiến trình từng job

### Tab "Cài đặt"
- **FFmpeg path**: Đường dẫn đến ffmpeg.exe (auto-detect nếu có trong PATH)
- **Parallel jobs**: Số job render cùng lúc (1-3)
- **Auto mode**: Bật/tắt chế độ tự động
  - Khi bật: theo dõi folder driver, khi có video mới + SRT → tự thêm vào queue
  - Video được coi là "stable" sau 10 giây không thay đổi kích thước

### CLI (Command Line)

```bash
# Scan và plan
vat plan --template my-template.json --root "D:\videos\drivers"

# Validate
vat validate --template my-template.json --root "D:\videos\drivers"

# Render single job
vat render --template my-template.json --root "D:\videos\drivers" --index 0

# Render from queue
vat run --template my-template.json --root "D:\videos\drivers"

# Cache info
vat cache --info
vat cache --clear

# Benchmark (so sánh baseline vs cached)
vat bench --template my-template.json --root "D:\testdata" --index 0
```

## Xử lý sự cố thường gặp

### Lỗi "FFmpeg not found"
- Kiểm tra FFmpeg đã cài và trong PATH: mở CMD gõ `ffmpeg -version`
- Hoặc trong tab Cài đặt, chỉ định đường dẫn đến ffmpeg.exe

### Lỗi "Font not found"
- Template yêu cầu font chưa cài trên Windows
- Cài font bị thiếu, hoặc chỉnh template dùng font khác (Arial, Tahoma...)

### Render chậm / video dài
- Bật **Freeze** cho background layers (cache sẵn, render nhanh hơn)
- Dùng NVENC (GPU encoding) nếu có card NVIDIA: template > output > encoder = nvenc_h264

### Video output bị lỗi / không chạy được
- Kiểm tra validation trước khi render (tab "Nguồn & Kiểm tra")
- Đảm bảo SRT file matching với driver (cùng tên, ví dụ: video001.mp4 + video001.srt)
- Kiểm tra log file trong folder output

## Giải thích Lock vs Freeze

- **Lock 🔒**: Chặn chỉnh sửa layer trong UI (không ảnh hưởng render)
- **Freeze ❄**: Cache prepared asset của layer (scale, opacity, crop...) → dùng lại cho tất cả jobs, render nhanh hơn cho video dài. Chỉ áp dụng cho background/avatar/wave layers.

## Hỗ trợ

- Issues: https://github.com/your-repo/issues
- Docs: `docs/SPEC.md` (đặc tả đầy đủ)

## Phiên bản

v1.0.0 - Release đầu tiên (2026-09-20)
