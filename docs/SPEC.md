# SPEC - Video Auto Tool (VAT)

> Tài liệu gốc mô tả tool. Agent chỉ đọc PHẦN liên quan tới việc đang làm (mỗi phần có mã §). Khi spec và code lệch nhau: hỏi người dùng, không tự đoán.
> Nhãn độ tin cậy: **[ĐÃ THỬ]** = đã chạy thành công trên dữ liệu giả; **[CHƯA THỬ]** = đề xuất, phải có test chứng minh trước khi dựa vào.

## §0. Mục tiêu

Ứng dụng Windows (C# / .NET 8 / WPF) tạo video hàng loạt, KHÔNG cần Premiere Pro.

Mỗi video ra gồm: **âm thanh của video nguồn** (hình gốc bỏ) + **background** (scale, opacity, nối nhiều clip nếu ngắn) + **avatar** (PNG trong suốt, to, bên phải hoặc trái) + **soundwave** (clip ngắn lặp cho đủ thời lượng) + **sub burn-in** từ file SRT theo style. Bố cục do người dùng **thiết kế 1 lần** trên canvas (template) rồi tool tự làm cho các video sau.

Video có thể dài (ví dụ 37 phút 30 giây) và mỗi batch có thể hàng chục, hàng trăm video.

**Không làm trong bản v1 (non-goals):** keyframe/animation, transition giữa các background, nhiều track audio, xoay layer, hiệu ứng GPU tùy chỉnh, chạy trên macOS/Linux, nhúng vào Premiere/Media Encoder.

## §1. Thuật ngữ

| Từ | Nghĩa |
|---|---|
| Root | Thư mục gốc của 1 batch, ví dụ `D:\CO139\001 AngelsAreCalling-01` |
| Driver | Video nguồn. Mỗi driver sinh ra đúng 1 video xuất. Chỉ lấy âm thanh |
| Layer / track | 1 thành phần trên canvas (Background, Avatar, Soundwave, Sub...) |
| Template | File JSON lưu bố cục + thư mục nguồn + style. Làm mẫu 1 lần, dùng lại nhiều lần |
| Job | Kế hoạch render cho 1 driver (đã chọn avatar/wave/background/sub/style) |
| Plan | Danh sách job của cả batch, mang tính xác định (cùng input => cùng plan) |
| Lock | Khóa chống kéo/sửa nhầm layer (như Premiere) |
| Freeze | Cache kết quả xử lý của layer để tái dùng cho mọi video |

## §2. Cấu trúc thư mục đầu vào

```
<ROOT>\
  source\NGUON\    001 🔴 Tiêu đề dài....mp4        (driver, chỉ lấy âm thanh)
  text\SUB\        001 🔴 Tiêu đề dài....en.srt      (ghép theo số đầu tên)
  background\      bg1.mp4 bg2.mp4 ...               (video nền, có thể có tiếng -> bị bỏ)
  avatar\          *.png (trong suốt)                (xoay vòng)
  soundwave\       *.mov/*.mp4 (ngắn, có alpha hoặc nền đen)  (lặp lại cho đủ thời lượng)
  xuat_render\     <tên nguồn>.mp4                   (đầu ra)
```

- Tên thư mục con **cấu hình được trong template** (mỗi layer có ô "thư mục nguồn"), đường dẫn tương đối tính từ Root; nếu bắt đầu bằng `X:\` hoặc `\\` thì là tuyệt đối.
- Bỏ qua file ẩn, `~$*`, `Thumbs.db`, `*.part`, `*.tmp`.
- Sắp xếp **natural sort** (`2` trước `10`), không phân biệt hoa thường.
- Số thứ tự của file: regex `^\s*(\d+)` trên tên file (`001 🔴 ...` -> 1). Driver sắp theo số tăng dần rồi natural sort tên.
- Ghép sub: `sub.number == driver.number`. Nếu không có số: ghép theo tên file bắt đầu bằng stem của driver. Trùng số: lấy file đầu tiên theo natural sort + cảnh báo.
- Tên có emoji, dấu tiếng Việt, khoảng trắng phải chạy được ở mọi bước.
- Tên file xuất = `{sourceStem}.mp4` (mẫu tên chỉnh được), bỏ ký tự cấm của Windows `<>:"/\|?*`.

## §3. Bố cục tham chiếu (từ 2 ảnh mẫu, hệ 1280x720)

Đây chỉ là **giá trị khởi đầu**; người dùng chỉnh bằng canvas.

- **Background**: phủ kín khung. Mặc định scale 150 % (chế độ "cover"), opacity 60 % trên nền đen.
- **Avatar**: rất to, nửa phải, **bám đáy, tràn ra mép phải và mép dưới (bị cắt)**. Mép trái của avatar ở khoảng x = 640..705, đỉnh ở khoảng y = 50..130. Cao gần bằng cả khung. => avatar KHÔNG bị ép vừa trong nửa khung; layer có scale/vị trí tự do và cho phép tràn.
- **Soundwave**: thanh trắng, nền trong suốt, nửa trái, tâm khoảng (505, 320), rộng khoảng 500, cao khoảng 150. Có video không dùng wave.
- **Sub**: nửa trái, tâm X khoảng 415..430. Ảnh 1: 2 dòng, tâm Y khoảng 575, chữ serif đậm màu vàng cam, viền/bóng đen. Ảnh 2: 3 dòng, tâm Y khoảng 440, chữ slab đậm trắng, viền đen. Cỡ chữ khoảng 55 px. => **vị trí và style sub thuộc về preset style**, không cố định toàn tool.
- Khi avatar sang trái: toàn bộ bố cục lật gương theo trục dọc giữa khung (tùy chọn `mirrorWithSide` trên từng layer).

## §4. Mô hình dữ liệu Template (JSON, `schemaVersion = 1`)

```json
{
  "schemaVersion": 1,
  "name": "CO139-default",
  "canvas": { "width": 1280, "height": 720, "fps": 25 },
  "output": { "folder": "xuat_render", "namePattern": "{sourceStem}", "encoder": "auto",
              "quality": 23, "audioBitrateKbps": 192, "skipExisting": true },
  "driver": { "folder": "source\\NGUON", "extensions": [".mp4", ".mov", ".mkv", ".mp3", ".wav", ".m4a"],
              "numberPattern": "^\\s*(\\d+)" },
  "avatarSide": "right",
  "layers": [
    { "id": "bg", "type": "backgroundChain", "name": "Background", "visible": true, "locked": false, "frozen": true,
      "source": { "folder": "background", "extensions": [".mp4", ".mov", ".mkv"], "pick": "sequentialChain" },
      "scaleMode": "cover", "scale": 1.5, "offsetX": 0, "offsetY": 0, "opacity": 0.6 },
    { "id": "avatar", "type": "image", "name": "Avatar", "visible": true, "locked": false, "frozen": true,
      "source": { "folder": "avatar", "extensions": [".png", ".webp"], "pick": "roundRobin" },
      "transform": { "anchor": "bottomRight", "x": 1280, "y": 720, "scaleMode": "fitHeight", "scale": 1.0 },
      "opacity": 1.0, "mirrorWithSide": true },
    { "id": "wave", "type": "loopVideo", "name": "Soundwave", "visible": true, "locked": false, "frozen": true,
      "source": { "folder": "soundwave", "extensions": [".mov", ".mp4", ".webm"], "pick": "roundRobin" },
      "transform": { "anchor": "center", "x": 505, "y": 320, "scaleMode": "fitWidth", "width": 500 },
      "alpha": "auto", "blackKeyTolerance": 0.15, "opacity": 1.0, "mirrorWithSide": true },
    { "id": "sub", "type": "subtitle", "name": "Sub", "visible": true, "locked": false, "frozen": false,
      "source": { "folder": "text\\SUB", "extensions": [".srt"], "pick": "matchByNumber" },
      "wrap": "pixelWidth", "stripEmoji": true, "upper": false,
      "styleAssignment": { "mode": "fixed", "presets": ["gold-serif"] },
      "mirrorWithSide": true }
  ],
  "stylePresets": [
    { "id": "gold-serif", "font": "Georgia", "fontSource": "bundled", "size": 55, "bold": true,
      "color": "#F5A623", "outlineColor": "#000000", "outline": 2.5, "shadow": 1.0, "lineSpacing": 0,
      "box": { "x": 175, "y": 465, "width": 480, "height": 220, "anchor": "topLeft" },
      "textAlign": "center", "verticalAlign": "middle" },
    { "id": "white-slab", "font": "Rockwell Condensed", "fontSource": "bundled", "size": 55, "bold": true,
      "color": "#FFFFFF", "outlineColor": "#000000", "outline": 2.5, "shadow": 0.0, "lineSpacing": 0,
      "box": { "x": 175, "y": 330, "width": 480, "height": 220, "anchor": "topLeft" },
      "textAlign": "center", "verticalAlign": "middle" }
  ]
}
```

Quy ước:
- Thứ tự trong mảng `layers` = từ **dưới lên trên** (phần tử đầu ở dưới cùng).
- `type`: `backgroundChain` | `image` | `loopVideo` | `subtitle` | `fixedText` (tùy chọn) | `staticImage` (1 file cố định, logo).
- `pick`: `sequentialChain` (chỉ background) | `roundRobin` | `fixed` (kèm `file`) | `random` (kèm `seed`) | `matchByNumber` (sub).
- `anchor`: `topLeft|top|topRight|left|center|right|bottomLeft|bottom|bottomRight` - điểm neo của layer đặt tại (x, y) tính bằng pixel canvas.
- `scaleMode` ảnh/video: `fitWidth` (rộng = `width`), `fitHeight` (cao = `scale * canvasHeight`), `native` (`scale` x kích thước gốc), `stretch`.
- `alpha` (loopVideo): `auto` (dò), `alpha` (file có kênh alpha), `blackKey` (nền đen -> xóa đen).
- `styleAssignment.mode`: `fixed` | `roundRobin` | `byRange` (danh sách `{from, to, preset}` theo số video).
- **Layout sub nằm trong từng `stylePreset`**: `box` (x, y, width, height, anchor), `textAlign`, `verticalAlign`. Layer `sub` không còn `box` (template cũ: fallback preset đầu tiên).
- **Font**: `font` = tên family; `fontSource`: `bundled` (trong `assets/fonts/` của app) | `imported` (trong `%APPDATA%\VideoAutoTool\fonts\`) | `system` (font cài Windows). Render copy font bundled/imported vào `temp/fonts/` cho libass.
- Khi thiếu trường: dùng mặc định trong code, không crash. Trường lạ: bỏ qua. `schemaVersion` cao hơn: báo lỗi rõ ràng.

## §5. Lập kế hoạch (Planner) - phải xác định (deterministic)

1. Quét driver, sub, background, avatar, wave (§2). Đọc thời lượng bằng ffprobe (có cache theo đường dẫn + mtime + size).
2. Với driver thứ `i` (0-based, tính trên TOÀN BỘ danh sách driver, kể cả video đã render hay bị bỏ qua để kết quả không đổi khi chạy lại):
   - `D` = thời lượng audio stream đầu tiên (không có audio => driver lỗi, bỏ qua + báo).
   - `avatar = avatars[i % n]`, `wave = waves[i % m]` (nếu layer bật và thư mục có file).
   - `side`: `right` | `left` | `alternate` (i chẵn = `right`, lẻ = `left`).
   - Style preset theo `styleAssignment`.
   - **Chuỗi background**: có con trỏ `p` toàn cục bắt đầu từ 0. Lặp: lấy `bg = backgrounds[p % nb]`, `p++`; bỏ qua background hỏng/ngắn hơn 0.5 s (vẫn tăng `p`); cộng dồn thời lượng; dừng khi `tổng >= D - 0.02`. Giới hạn an toàn 500 đoạn/video (vượt = lỗi cấu hình). Video kế tiếp tiếp tục từ `p` hiện tại.
3. Job giữ: driver, sub (hoặc null), D, chuỗi background `[(file, durationFull)]`, avatar, wave, side, preset, đường dẫn xuất.

**Ví dụ chuẩn (golden test)** với dữ liệu giả ở Phụ lục A:
background lần lượt dài 7 s, 6 s, 8 s (bg1, bg2, bg3); driver dài 14 s, 9 s, 21 s.
- Video 1 (14 s): bg1 + bg2 + bg3 (7+6 = 13 < 13.98 nên cần thêm bg3)
- Video 2 (9 s): bg1 + bg2 (con trỏ p=3 -> bg1; 7 < 8.98 nên thêm bg2)
- Video 3 (21 s): bg3 + bg1 + bg2 (8+7+6 = 21 >= 20.98)
- Avatar: avatar1, avatar2, avatar3. Wave: wave1 cho cả 3 (chỉ có 1 file).

## §6. Render (ffmpeg) - xem thêm `.cursor/rules/ffmpeg.mdc`

Pipeline đã thử: **[ĐÃ THỬ]** - đồ thị filter, công thức, và các lỗi đã gặp nằm trong `ffmpeg.mdc`. Tóm tắt:

1. Nền đen -> background (từng đoạn: `trim`, `fps`, `scale` phủ kín x hệ số, `crop`, `pad`) -> `concat` -> opacity -> overlay.
2. Avatar: `format=rgba`, `scale ... force_original_aspect_ratio=decrease`, overlay tại vị trí tính từ `anchor,x,y` (cho phép tràn).
3. Wave: lặp bằng `-stream_loop -1` + `trim=duration=D`, `scale`, alpha hoặc `lumakey`, overlay.
4. Sub + text: file ASS -> filter `ass`.
5. Chuyển màu về bt709, `format=yuv420p`, mã hóa (x264 hoặc NVENC), âm thanh `-map 0:a:0`.

Công thức vị trí overlay từ `anchor` (x, y) và kích thước layer (w, h) sau khi scale:
`left = x - w * ax`, `top = y - h * ay` với `(ax, ay)` = (0|0.5|1, 0|0.5|1) theo anchor. Khi `side = left` và `mirrorWithSide = true`: thay `x` bằng `canvasWidth - x` và đảo anchor ngang (left <-> right).

Scale layer: `fitHeight`: `h = scale * canvasHeight`, `w` theo tỉ lệ ảnh; `fitWidth`: `w = width`; `native`: `w = scale * imageWidth`; kích thước làm tròn về số chẵn.

Video xuất: mp4, H.264 + AAC 48 kHz, kích thước/fps theo `canvas`, độ dài = D. Ghi vào `*.mp4.part` rồi đổi tên khi thành công.

## §7. Sub và text (ASS)

- Đọc SRT: thử mã hóa UTF-8 (có/không BOM) -> UTF-16 -> CP1258 -> Latin-1. Chuẩn hóa xuống dòng. Bỏ thẻ `<i>`, `{...}`. **Giữ nguyên** thẻ dạng `[music]` và mọi `[..]` khác trên video. Gộp các dòng của 1 cue thành 1 câu rồi **tự ngắt dòng**.
- **Ngắt dòng theo bề rộng pixel của hộp preset** (`preset.box.width`): đo bằng font thật (SkiaSharp/WPF) với hệ số an toàn 0,97, chèn `\N`. `WrapStyle: 2` trong ASS để libass không tự ngắt thêm. Không đếm ký tự.
- Vị trí: `{\an<N>\pos(x,y)}` với N từ `preset.textAlign` + `preset.verticalAlign`, (x, y) là điểm neo tương ứng trong `preset.box`.
- Style lấy từ preset: font, cỡ, đậm, màu chữ, màu viền, độ dày viền, bóng. Màu ASS = `&H00BBGGRR`.
- Cue bắt đầu sau D: bỏ. Cue kết thúc sau D: cắt về D. Cue chồng nhau: giữ nguyên (ASS tự xếp).
- Text từ tên file (nếu layer `fixedText` dùng): bỏ số đầu, bỏ đuôi `.en`, bỏ emoji, gộp khoảng trắng.
- ASS chỉ phục vụ render. Trên canvas thiết kế dùng hộp chữ WPF để kéo thả; bản xem thử chính xác luôn lấy từ ffmpeg.

## §8. Kiểm tra hợp lệ (Validation) - nút "Kiểm tra"

Chạy trước khi render, trả về danh sách `{code, level, scope, message, fixHint}`. `level`: `Error` (chặn video/track đó), `Warning` (vẫn chạy), `Info`.
Giao diện hiển thị bảng xanh/vàng/đỏ theo từng track và từng video, lọc được, xuất ra text.

| Mã | Mức | Điều kiện |
|---|---|---|
| E001 | Error | Không tìm thấy ffmpeg/ffprobe (hướng dẫn chỉ đường dẫn) |
| E010 | Error | Thư mục của layer không tồn tại hoặc không đọc được |
| E011 | Error | Thư mục driver rỗng |
| W012 | Warning | Thư mục avatar/wave/background rỗng (layer sẽ bị bỏ qua) |
| E020 | Error | Driver không đọc được (ffprobe lỗi) |
| E021 | Error | Driver không có audio |
| E030 | Error | Driver không có sub tương ứng (luôn chặn render) |
| E031 | Error | SRT không đọc được hoặc 0 cue |
| W032 | Warning | Trùng số SRT (dùng file đầu tiên) |
| W033 | Warning | Có cue bắt đầu sau độ dài audio |
| W034 | Warning | Cue cuối kết thúc sớm hơn audio quá 20 % (sub thiếu?) |
| E040 | Error | Background hỏng |
| W041 | Warning | Background < 2 s (nhiều đoạn nối, dễ giật) |
| W042 | Warning | Độ phân giải background nhỏ hơn khung x scale (sẽ bị mờ) |
| I043 | Info | Background có audio (sẽ bị bỏ) |
| E050 | Error | Avatar hỏng / không phải ảnh |
| W051 | Warning | Avatar không có kênh alpha (sẽ hiện thành hình chữ nhật) |
| E060 | Error | Wave hỏng |
| W061 | Warning | Wave chế độ `alpha` nhưng file không có alpha |
| W062 | Warning | Wave < 1 s (lặp giật) |
| I063 | Info | Wave khác fps với canvas (sẽ được chuẩn hóa) |
| W070 | Warning | Font trong preset không tìm thấy (bundled / imported / system) |
| W071f | Warning | Font imported được tham chiếu nhưng file không còn trong `%APPDATA%\VideoAutoTool\fonts\` |
| E071 | Error | Thư mục xuất không ghi được |
| W072 | Warning | Ổ đĩa xuất có ít dung lượng hơn ước tính (ước tính = tổng thời lượng x bitrate ước lượng x 1,3) |
| W073 | Warning | Tên file xuất trùng nhau |
| W074 | Warning | Video xuất đã tồn tại (sẽ bỏ qua nếu `skipExisting`) |
| W090 | Warning | Tổng thời lượng background không đủ cho các video kế tiếp (sẽ lặp lại từ đầu) |

Quy tắc: 1 lỗi Error của 1 driver không làm dừng các driver khác. Kết quả kiểm tra là dữ liệu có cấu trúc (test được), giao diện chỉ hiển thị.

## §9. Cache / Freeze

Hai nút khác nhau trên mỗi layer:
- **Lock**: chỉ chặn kéo/sửa nhầm.
- **Freeze**: layer được xử lý một lần và cache lại để tái dùng cho mọi video.

Thứ nào cache được:
- Background: từng clip đã chuẩn bị (scale/crop/pad, opacity, đúng kích thước/fps canvas). **[CHƯA THỬ]** cách dùng: nối bằng concat demuxer (`-f concat`) thành **một** input duy nhất cho lệnh render (xem `ffmpeg.mdc`).
- Avatar: ảnh đã scale sẵn về kích thước cuối.
- Wave: clip lặp đã dựng ở đúng kích thước, có alpha (định dạng trung gian nhẹ để giải mã nhanh).
- Sub: KHÔNG cache (mỗi video khác nhau).

Khóa cache = SHA-256 của (đường dẫn + size + mtime của file nguồn + mọi tham số ảnh hưởng kết quả: kích thước canvas, fps, scale, mode, opacity, alpha mode, tolerance). Đổi tham số => tự tạo cache mới, cache cũ bị dọn theo LRU.
Vị trí: `%LOCALAPPDATA%\VideoAutoTool\cache\`. Giới hạn dung lượng mặc định 20 GB, chỉnh được. Có nút "Xóa cache" và hiển thị dung lượng.
Tiêu chí chấp nhận: kết quả có cache và không cache **giống nhau** (độ sáng vùng nền chênh dưới 3 %) và render **nhanh hơn** trên bộ dữ liệu dài (§14).

## §10. Hàng đợi render

- Trạng thái job: `Pending`, `Running`, `Paused`, `Done`, `Failed`, `Cancelled`, `Skipped`.
- Lưu bền vào `%LOCALAPPDATA%\VideoAutoTool\queue.json` (tắt tool bật lại vẫn còn; job `Running` dở dang quay về `Pending`).
- Số job chạy song song chỉnh được 1..3, mặc định 1.
- Có: tạm dừng/tiếp tục cả hàng đợi, hủy từng job, thử lại job lỗi, đổi thứ tự, xem log ffmpeg của từng job, tiến độ % và thời gian còn lại.
- Job lỗi không làm dừng hàng đợi. Xóa file `.part` khi lỗi/hủy.
- Tùy chọn khi xong: mở thư mục xuất, phát âm thanh thông báo, tắt máy.
- Chế độ tự động (v1.1): theo dõi thư mục driver; khi có video mới và có SRT tương ứng (ổn định kích thước 10 s) thì tự thêm vào hàng đợi.

## §11. Giao diện (WPF)

Cửa sổ chính chia tab: **Thiết kế** | **Nguồn & kiểm tra** | **Hàng đợi** | **Cài đặt**.

```
+---------------------------------------------------------------------------------+
| [Template v] [Lưu] [Root: D:\CO139\001 ...] [Quét]        Video: [001 Jesus... v] |
+-----------------+----------------------------------------------+----------------+
| LAYERS          |  CANVAS 1280x720 (zoom: Vừa | 50% | 100%)     | THUỘC TÍNH     |
| 👁 🔒 ❄ Sub     |  +----------------------------------------+  | Vị trí X/Y     |
| 👁 🔒 ❄ Wave    |  |  (khung thật, avatar tràn ra ngoài     |  | Neo (9 điểm)   |
| 👁 🔒 ❄ Avatar  |  |   được tô mờ)                          |  | Scale % / W    |
| 👁 🔒 ❄ Backgr. |  +----------------------------------------+  | Opacity        |
| [+ layer]       |  Giây: [====o=====] 00:12   [Xem chính xác]  | Thư mục nguồn  |
|                 |                                              | Cách chọn file |
+-----------------+----------------------------------------------+----------------+
| Log / kết quả kiểm tra                                                            |
+---------------------------------------------------------------------------------+
```

**Canvas thiết kế** (giống Premiere ở mức cơ bản):
- Chọn layer bằng click (canvas hoặc danh sách). Hộp chọn có 8 tay cầm: kéo góc = scale giữ tỉ lệ, giữ Shift = scale tự do; kéo thân = di chuyển; phím mũi tên = 1 px, Shift + mũi tên = 10 px.
- Ô số bên phải liên kết 2 chiều với canvas. Dán số thì canvas cập nhật ngay.
- Snap vào giữa/1/3/mép khung (bật/tắt), đường gióng hiện khi kéo. Vùng ngoài khung hiển thị mờ để thấy phần avatar tràn ra.
- Sắp xếp layer bằng kéo thả trong danh sách. Mỗi hàng có: hiện/ẩn, Lock, Freeze, tên. Layer Lock không kéo/sửa được.
- Undo/Redo (Ctrl+Z / Ctrl+Y) cho mọi thao tác thiết kế. Tự lưu nháp template; hiển thị dấu "chưa lưu".
- Layer Sub: chọn **style preset** ở bảng thuộc tính → canvas hiển thị **hộp sub của preset đó** (kéo/co giãn riêng từng preset, lưu trong template như Premiere). Đổi preset = đổi hộp + font/màu/viền/bóng. Chiều rộng hộp preset quyết định ngắt dòng (§7).
- **Font manager** (tab Cài đặt hoặc panel Thiết kế): liệt kê font bundled / imported / system; nút **Import font** (`.ttf`/`.otf` → `%APPDATA%\VideoAutoTool\fonts\`); xóa font imported.
- Hai tầng xem thử: (1) **tức thì** khi kéo thả bằng ảnh proxy đã cache (1 frame của background/driver tại giây đang chọn, avatar, 1 frame của wave, chữ mẫu) ghép bằng WPF; (2) **"Xem chính xác"** gọi ffmpeg dựng khung hình thật (debounce 500 ms sau thay đổi cuối) - đây là hình đúng với video xuất, đặc biệt cho sub.
- Thanh giây chọn thời điểm xem thử. Mặc định nhảy tới giây của cue đầu tiên + 0,4 s. Nút "Render thử 10 giây" và tự mở file.

**Tab Nguồn & kiểm tra**: bảng theo layer: đường dẫn (sửa được + Browse), số file, loại file, trạng thái; nút "Kiểm tra" chạy §8; bảng kết quả theo video. Không cho vào hàng đợi video có lỗi Error (có nút "Bỏ qua các video lỗi").

**Tab Hàng đợi**: §10. **Tab Cài đặt**: đường dẫn ffmpeg/ffprobe (tự dò PATH + kiểm tra phiên bản), encoder (Auto/NVENC/x264), chất lượng, thư mục cache, số job song song, **quản lý font (Import / xóa imported)**, ngôn ngữ.

Giao diện: chủ đề tối, chữ tiếng Việt, cửa sổ co giãn, tối thiểu 1280x800; các thao tác dài không được làm treo giao diện.

## §12. Lưu trữ

- Cài đặt chung: `%APPDATA%\VideoAutoTool\settings.json`. Template: `%APPDATA%\VideoAutoTool\templates\*.json` (có Import/Export).
- Font imported: `%APPDATA%\VideoAutoTool\fonts\*.ttf|*.otf`. Font bundled: `assets/fonts/` trong thư mục cài app (ship khi publish).
- Hàng đợi, cache, log: `%LOCALAPPDATA%\VideoAutoTool\{queue.json, cache\, logs\yyyy-MM-dd.log}`. Log xoay vòng, giữ 14 ngày.
- Không ghi gì vào thư mục Root ngoài `xuat_render`.

## §13. Công cụ dòng lệnh `vat` (để test Core không cần UI)

```
vat scan      --root <path> --template <file>          # in danh sách driver/sub/bg/avatar/wave
vat validate  --root <path> --template <file> [--json] # in kết quả §8
vat plan      --root <path> --template <file> [--json] # in kế hoạch §5
vat preview   --root <path> --template <file> --index N --time T --out frame.png
vat render    --root <path> --template <file> --index N [--seconds 10] [--out file.mp4]
vat run       --root <path> --template <file> [--from N --limit M] [--parallel K]
```
Mã thoát: 0 = ok, 1 = có job lỗi, 2 = cấu hình/kiểm tra không hợp lệ, 3 = không tìm thấy ffmpeg.

## §14. Hiệu năng và video dài

- Video dài tới ~40 phút, batch hàng trăm video. Mục tiêu: không treo, không tăng RAM không giới hạn, hủy/tiếp tục được.
- Một lệnh ffmpeg KHÔNG được mở hàng chục input: dùng cache background + concat demuxer (§9) cho video dài. Bản baseline (mỗi đoạn 1 input) chỉ dùng cho video ngắn hoặc làm đối chứng.
- Wave lặp bằng `-stream_loop`. Avatar là ảnh tĩnh (`-loop 1`).
- File ASS có thể hàng trăm/nghìn cue: phải chạy bình thường.
- Đo và ghi lại thời gian render (giây render / giây video) vào log để so sánh x264 và NVENC. Không hứa tốc độ cụ thể.
- NVENC: tự dò; nếu lỗi thì rơi về x264 và báo trong log. **[CHƯA THỬ]** trên máy có GPU (máy người dùng có NVIDIA RTX 5060).

## §15. Xử lý lỗi và log

- Mỗi job lưu lệnh ffmpeg đã chạy (đã ẩn đường dẫn nhạy cảm nếu cần) và 25 dòng cuối stderr khi lỗi.
- Thông báo lỗi cho người dùng bằng tiếng Việt, nói rõ **file nào, bước nào, cách sửa**.
- Không bao giờ để lại file `.part` hoặc thư mục tạm sau khi kết thúc/hủy.

## §16. Tiêu chí nghiệm thu tổng thể (test tự động trên dữ liệu giả)

| Kiểm tra | Yêu cầu |
|---|---|
| Độ dài | \|độ dài video - độ dài audio nguồn\| <= 0,1 s |
| Luồng | đúng 1 video + 1 audio; 1280x720; 25 fps; H.264 + AAC |
| Âm thanh | không dùng âm thanh của background (chỉ map `0:a:0`) |
| Chuỗi background | khớp ví dụ chuẩn §5 |
| Opacity | tỉ lệ độ sáng vùng chỉ có background giữa opacity 0,6 và 1,0 nằm trong 0,60 +- 0,03 (bản thử đo được 0,604) |
| Lật gương | `side = left`: tâm avatar ở nửa trái, tâm wave ở nửa phải, chữ sub ở nửa phải |
| Tràn khung | avatar đặt tràn ra mép phải/dưới vẫn render đúng, không lỗi |
| Sub | frame giữa 1 cue có điểm ảnh màu chữ trong vùng hộp; frame ở khoảng trống giữa 2 cue thì không |
| Unicode | tên file có emoji/khoảng trắng/dấu tiếng Việt chạy đủ các bước |
| Resume | chạy lại lần 2 => bỏ qua video đã có, không để lại `.part` |
| Hủy | hủy giữa chừng => ffmpeg dừng, `.part` bị xóa |
| Lỗi 1 video | 1 driver lỗi không làm dừng cả batch |
| Cache | kết quả có/không cache giống nhau (§9) |
| Video dài | bộ dữ liệu 37:30 phút tổng hợp render xong, không mở quá 8 input trong 1 lệnh, RAM ổn định |

## §17. Rủi ro và điều chưa chắc

1. NVENC trên RTX 5060 chưa thử (cần ffmpeg build mới và driver mới).
2. Cache background + concat demuxer chưa thử; nếu không nhanh hơn thì đề xuất phương án khác (ví dụ ghép clip nền đã chuẩn bị thành 1 file dài cho từng batch).
3. Đo chữ bằng Skia/WPF có thể lệch nhẹ so với libass: cần hệ số an toàn và test bằng khung hình thật.
4. Kéo-thả canvas kiểu Premiere tốn công nhất; làm sau khi Core render ổn định.
5. Tên font tiếng Việt/font cài riêng có thể không khớp tên libass thấy: cần bước kiểm tra font (W070).
6. Font preset: placeholder Georgia / Rockwell Condensed; có thể thay bằng Import font hoặc bundled. Avatar side theo template. Wave tùy chọn (layer có thể tắt).

## Phụ lục A - Dữ liệu giả để test (script `scripts/make-testdata.ps1` phải tạo lại được)

Thư mục: `testdata\001 AngelsAreCalling-01\{source\NGUON, text\SUB, background, avatar, soundwave, xuat_render}`. Các lệnh dưới đây **[ĐÃ THỬ]** (bash), cần chuyển sang PowerShell.

```
# Driver (chỉ cần âm thanh): 14 s, 9 s, 21 s; tên có emoji + số đầu
ffmpeg -y -f lavfi -i "testsrc=s=320x180:r=30:d=14" -f lavfi -i "sine=frequency=440:duration=14" -c:v libx264 -c:a aac -shortest "source\NGUON\001 🔴 Jesus Saw What Was Coming, Something Unexpected Is About To Transform Your Life.mp4"
#   002 ⚠️ Archangel Michael Says You Are Chosen.mp4      -> 9 s,  sine 550 Hz
#   003 🔥 Third Video With Longer Duration To Chain Backgrounds.mp4 -> 21 s, sine 330 Hz

# Background: 7 s (không tiếng), 6 s (CÓ tiếng), 8 s (720p)
ffmpeg -y -f lavfi -i "gradients=s=1920x1080:r=30:d=7:speed=0.05" -c:v libx264 -pix_fmt yuv420p background\bg1.mp4
ffmpeg -y -f lavfi -i "testsrc2=s=1920x1080:r=30:d=6" -f lavfi -i "sine=frequency=1000:duration=6" -c:v libx264 -pix_fmt yuv420p -c:a aac background\bg2_has_voice.mp4
ffmpeg -y -f lavfi -i "mandelbrot=s=1280x720:r=30" -t 8 -c:v libx264 -pix_fmt yuv420p background\bg3.mp4

# Soundwave có alpha (2,5 s, 60 fps, thanh xanh)
ffmpeg -y -f lavfi -i "aevalsrc='sin(2*PI*300*t)*sin(2*PI*2*t)':d=2.5:s=44100" -filter_complex "showwaves=s=640x160:mode=cline:colors=0x00E5FF:rate=60,format=yuva444p,colorkey=black:0.1:0.05,format=argb" -c:v qtrle -pix_fmt argb soundwave\wave1.mov

# Wave nền đen (để test lumakey), đặt ngoài thư mục chuẩn
ffmpeg -y -f lavfi -i "aevalsrc='sin(2*PI*300*t)*sin(2*PI*2*t)':d=2.5:s=44100" -filter_complex "showwaves=s=640x160:mode=cline:colors=0xFFAA00:rate=60,format=yuv420p" -c:v libx264 -pix_fmt yuv420p wave_black.mp4
```
- Avatar: 3 file PNG trong suốt với kích thước khác nhau (500x700, 800x800, 600x900), mỗi file một hình đơn giản có màu khác nhau (đầu = hình elip, thân = hình chữ nhật bo góc) và nhãn `A1`, `A2`, `A3`. Tạo bằng công cụ nào cũng được (ví dụ SkiaSharp trong 1 project nhỏ, hoặc ffmpeg `drawbox` trên canvas rgba trong suốt).
- SRT: 3 file cùng tên với driver + hậu tố `.en.srt`, mã hóa UTF-8 có BOM, mỗi file 2-3 cue nằm trong độ dài audio; 1 cue có 2 dòng và thẻ `<i>`; nội dung tiếng Anh.
- Ngoài ra script tạo thêm bộ **video dài**: 1 driver 37:30 (audio sine, hình 320x180 tối thiểu) + 40 background ngắn (3-8 s) để test §14.
