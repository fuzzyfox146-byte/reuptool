# PROMPTS - dán lần lượt vào Cursor

Mỗi mục bên dưới là **một prompt hoàn chỉnh cho một chat mới**. Làm đúng thứ tự M0 -> M8. Mỗi lát cắt phải chạy được và có test trước khi sang lát kế tiếp.

## 0. Chuẩn bị máy (làm 1 lần, KHÔNG dán vào Cursor)

Mở PowerShell:

```powershell
winget install Git.Git
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.PowerShell          # lệnh pwsh (script test dùng)
winget install Gyan.FFmpeg                   # ffmpeg + ffprobe (hoặc tải bản "release full" ở gyan.dev rồi thêm vào PATH)
```

Mở PowerShell MỚI rồi kiểm tra:

```powershell
git --version
dotnet --version                # phải là 8.x
pwsh --version
ffmpeg -version
ffmpeg -hide_banner -encoders | findstr nvenc     # có h264_nvenc là card NVIDIA dùng được; cập nhật driver NVIDIA mới nhất
```

Tạo project:

```powershell
mkdir D:\dev\VideoAutoTool
cd D:\dev\VideoAutoTool
git init
```

Giải nén `cursor-pack.zip` vào đúng thư mục `D:\dev\VideoAutoTool` (phải có `.cursor\rules\project.mdc`, `docs\SPEC.md`, `docs\PROMPTS.md`, `docs\PROGRESS.md`, `.cursorignore`, `.gitignore`).
Mở thư mục đó bằng Cursor (File > Open Folder). Vào Cursor Settings > Rules và kiểm tra thấy `project` (luôn bật) và `ffmpeg` (theo đường dẫn). Nếu không thấy: kiểm tra đúng thư mục `.cursor\rules\`.

```powershell
git add -A ; git commit -m "chore: add cursor rules and spec"
```

## Cách dùng mỗi prompt

1. Mở **chat mới** (Ctrl+L, chọn Agent). Bấm **Shift+Tab để vào Plan Mode**. Chọn model: Auto cho M0, M1, M3, M5, M6, M8; chọn model mạnh cho M2, M4, M7 (phần ffmpeg và canvas khó nhất).
2. Dán nguyên khối prompt. Agent sẽ hỏi lại và đưa ra plan. **Đọc plan**, sửa nếu thừa/thiếu (plan là file markdown sửa trực tiếp được), rồi mới bấm chạy.
3. Khi agent xong: **tự chạy** `dotnet build` và `dotnet test` trong terminal, và chạy lệnh thử agent nêu ra. Không tin lời "đã xong" nếu chưa thấy test chạy.
4. Nếu đúng: `git add -A ; git commit -m "M1: ..."`. Nếu sai: `git restore .` (hoặc `git reset --hard`), **sửa lại plan cho cụ thể hơn rồi chạy lại**, đừng vá bằng nhiều câu nhắc.
5. Chat dài làm agent loãng: mỗi lát cắt dùng 1 chat mới (agent tự đọc `docs/PROGRESS.md` để biết đang ở đâu).
6. Muốn hỏi phụ mà không làm bẩn chat chính: dùng `/side`.

Câu nhắc hay dùng khi cần: "Dừng. Chưa code. Liệt kê file sẽ sửa và cách kiểm chứng." / "Chạy lại dotnet test và dán kết quả." / "Đừng sửa file ngoài danh sách plan."

---

## M0 - Khung solution, ffmpeg wrapper, dữ liệu test

```text
MỤC TIÊU
Dựng khung dự án "Video Auto Tool" và nền tảng gọi ffmpeg. Chưa làm logic nghiệp vụ.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (chỉ §1, §2, §12, §13, Phụ lục A)  @.cursor/rules/project.mdc  @.cursor/rules/ffmpeg.mdc

VIỆC CẦN LÀM
1. Dùng dotnet CLI tạo solution `VideoAutoTool.sln` với đúng cấu trúc trong project.mdc:
   - src/VideoAutoTool.Core (classlib, net8.0, Nullable enable, TreatWarningsAsErrors true)
   - src/VideoAutoTool.Cli (console, assembly name `vat`, tham chiếu Core)
   - src/VideoAutoTool.App (WPF net8.0-windows, chỉ là cửa sổ trống, tham chiếu Core; CHƯA thêm gì khác)
   - tests/VideoAutoTool.Core.Tests (xUnit, tham chiếu Core)
   Thêm `Directory.Build.props` (LangVersion 12, Nullable, ImplicitUsings) và `.editorconfig` (utf-8, LF/CRLF nhất quán, 4 spaces).
2. Trong Core tạo:
   - `Logging/ILog` (Info/Warn/Error) + `ConsoleLog` + `FileLog` (ghi %LOCALAPPDATA%\VideoAutoTool\logs\yyyy-MM-dd.log).
   - `Ffmpeg/FfmpegLocator`: tìm ffmpeg.exe/ffprobe.exe theo thứ tự: đường dẫn người dùng cấu hình -> PATH -> C:\ffmpeg\bin -> C:\tools\ffmpeg\bin. Trả về kết quả có phiên bản; không tìm thấy thì ném `FfmpegNotFoundException` với hướng dẫn tiếng Việt.
   - `Ffmpeg/MediaInfo` (record: Path, DurationSeconds, HasAudio, AudioDurationSeconds, HasVideo, Width, Height, Fps, PixelFormat, HasAlpha, Codec) và `Ffmpeg/FfmpegProbe` (chạy ffprobe -show_entries ... -of json, parse). HasAlpha đúng khi pix_fmt thuộc nhóm có alpha (rgba, bgra, argb, abgr, yuva420p, yuva422p, yuva444p, yuva444p10le, gbrap, ...). Định nghĩa `IMediaProbe` để mock được, và cache theo (path, size, mtime).
   - `Ffmpeg/FfmpegRunner`: chạy 1 lệnh ffmpeg theo đúng quy tắc trong ffmpeg.mdc (ArgumentList, WorkingDirectory tùy chọn, đọc stdout+stderr song song, CancellationToken kill cả process tree, parse `-progress pipe:1` ra IProgress<double> theo tổng thời lượng truyền vào, giữ 25 dòng cuối không phải progress). Trả về `FfmpegResult(ExitCode, Tail, Elapsed)`.
3. Trong Cli: lệnh `vat probe <file>` in MediaInfo dạng JSON đẹp; `vat --version`.
4. Viết `scripts/make-testdata.ps1` (PowerShell 7) tạo lại toàn bộ dữ liệu ở Phụ lục A trong `testdata/` (có tham số `-Long` để tạo thêm bộ video dài 37:30 + 40 background ngắn; mặc định KHÔNG tạo bộ dài). Script phải chạy lại được nhiều lần (bỏ qua file đã có) và in rõ ràng những gì đã tạo. Avatar PNG trong suốt tạo bằng cách bạn thấy chắc chắn nhất (đề xuất trước khi làm).
5. Test (xUnit): parse MediaInfo từ chuỗi JSON mẫu của ffprobe (có audio, không audio, ảnh có alpha, mov argb); test FfmpegLocator với thư mục giả; test tích hợp `FfmpegProbe` trên testdata (tự bỏ qua nếu chưa có ffmpeg hoặc chưa có testdata).

RÀNG BUỘC
- Không thêm NuGet package nào ngoài các package mặc định của template và xUnit.
- Không viết logic planner/render ở lát cắt này.
- Đường dẫn có emoji, khoảng trắng, dấu tiếng Việt phải chạy được (có test).

TIÊU CHÍ NGHIỆM THU
- `dotnet build` 0 warning, `dotnet test` xanh.
- `pwsh scripts/make-testdata.ps1` tạo đủ cây thư mục và file như Phụ lục A.
- `dotnet run --project src/VideoAutoTool.Cli -- probe "testdata/001 AngelsAreCalling-01/source/NGUON/001 🔴 ....mp4"` in duration khoảng 14.0 (sai số 0.1), hasAudio true.
- `.gitignore` bỏ qua bin/obj/.vs/testdata/cache/*.part.

KHI XONG
Cập nhật docs/PROGRESS.md; báo cách chạy từng lệnh; nêu rõ phần nào chưa kiểm chứng.
Trước khi viết code: dùng Plan Mode, liệt kê file sẽ tạo và cách kiểm chứng, chờ tôi duyệt.
```

---

## M1 - Template, quét thư mục, SRT, Planner (logic thuần, chưa render)

```text
MỤC TIÊU
Cài đặt mô hình template, quét thư mục, đọc SRT và Planner xác định đúng SPEC. Không gọi ffmpeg render.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (§2, §4, §5, §7 phần đọc SRT)  @.cursor/rules/project.mdc

VIỆC CẦN LÀM
1. `Templates/`: các record/class cho Template (canvas, output, driver, layers, stylePresets) đúng JSON ở SPEC §4. Dùng System.Text.Json với enum dạng chuỗi camelCase; trường thiếu dùng mặc định; trường lạ bỏ qua; `schemaVersion` cao hơn hiện tại thì ném lỗi rõ ràng. Có `TemplateStore.Load/Save(path)` và `TemplateDefaults.CreateCo139()` trả về template mặc định theo SPEC §3/§4. Có test round-trip (save rồi load bằng nhau).
2. `Scanning/`: `NaturalSort`, `FileScanner` (lọc đuôi file, bỏ file ẩn/~$/Thumbs.db/*.part/*.tmp, sắp natural sort, đường dẫn tương đối tính từ Root, đường dẫn tuyệt đối nếu bắt đầu bằng `X:\` hoặc `\\`), `NumberExtractor` (regex `^\s*(\d+)` từ template.driver.numberPattern).
3. `Subtitles/SrtParser`: đọc SRT theo SPEC §7 (thử UTF-8/BOM -> UTF-16 -> CP1258 -> Latin-1; xử lý `\r\n`; thời gian có dấu phẩy hoặc chấm, mili-giây 1-3 chữ số; bỏ thẻ <i> và {..}; cue nhiều dòng gộp thành 1 chuỗi). Trả về `List<Cue(Start, End, Text)>`. Cue lỗi bị bỏ + ghi cảnh báo, không ném lỗi cả file.
4. `Planning/JobPlanner`: đúng SPEC §5 (con trỏ background toàn cục, round-robin avatar/wave, side right/left/alternate, style preset fixed/roundRobin/byRange, ghép sub theo số rồi theo stem). Nhận `IMediaProbe` để test bằng dữ liệu giả. Kết quả `Plan(List<Job>, List<PlanWarning>)`. Job có đủ trường theo SPEC §5 mục 3.
5. Cli: `vat scan --root --template` và `vat plan --root --template [--json]`. Nếu không có `--template` thì dùng `TemplateDefaults.CreateCo139()`.
6. Test (xUnit) BẮT BUỘC:
   - Golden test §5: background 7/6/8 s, driver 14/9/21 s => chuỗi background video 1 = bg1+bg2+bg3; video 2 = bg1+bg2; video 3 = bg3+bg1+bg2; avatar1..3.
   - Kết quả plan không đổi khi chạy 2 lần và không đổi khi đánh dấu 1 số video là "đã render".
   - Natural sort: 2 trước 10; số đầu tên `001` = 1; tên không có số.
   - Ghép sub theo số; trùng số => lấy file đầu + cảnh báo; thiếu sub => job.Sub = null + cảnh báo.
   - SRT: file UTF-8 BOM, UTF-16, có thẻ <i>, cue 2 dòng, thời gian dấu chấm.
   - Thư mục avatar rỗng => job.Avatar = null, không lỗi. Không có background => chuỗi rỗng + cảnh báo.
   - background hỏng (thời lượng null) bị bỏ qua nhưng vẫn tăng con trỏ.

RÀNG BUỘC
- Core không được tham chiếu WPF. Không gọi ffmpeg thật trong test của Planner (dùng fake probe).
- Không thêm package.
- Không làm phần render/ASS ở lát cắt này.

TIÊU CHÍ NGHIỆM THU
- dotnet build/test xanh, 0 warning.
- `vat plan --root "testdata/001 AngelsAreCalling-01"` in đúng bảng như golden test (dữ liệu thật do make-testdata tạo).

KHI XONG
Cập nhật docs/PROGRESS.md. Trước khi viết code: Plan Mode, liệt kê file và test, chờ tôi duyệt.
```

---

## M2 - Render baseline: 1 video + khung hình xem thử

```text
MỤC TIÊU
Render đúng 1 video và 1 khung hình xem thử bằng ffmpeg theo pipeline ĐÃ THỬ trong ffmpeg.mdc.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @.cursor/rules/ffmpeg.mdc  @docs/SPEC.md (§3, §4, §6, §7, §16)  và các file Planner/Template của M1.

VIỆC CẦN LÀM
1. `Render/LayerGeometry`: tính vị trí/kích thước layer từ anchor/x/y/scaleMode/scale/width (công thức SPEC §6), lật gương theo side + mirrorWithSide, làm tròn kích thước về số chẵn. Test bằng bảng số (ví dụ avatar bottomRight tại (1280,720) fitHeight scale 1.0; wave center (505,320) fitWidth 500; side=left thì x -> 1280-x và đảo anchor ngang).
2. `Subtitles/TextWrapper` với `ITextMeasurer` (bản dùng SkiaSharp trong Core hoặc trong project riêng nếu cần tránh phụ thuộc; hỏi tôi nếu cần thêm package). Ngắt dòng theo bề rộng pixel của hộp sub với hệ số an toàn 0.97. `Subtitles/AssBuilder` tạo file ASS theo SPEC §7 (\an, \pos, WrapStyle 2, màu &H00BBGGRR, thời gian H:MM:SS.cc, bỏ emoji, cue vượt D bị cắt/bỏ).
3. `Render/FilterGraphBuilder`: dựng đồ thị filter đúng ffmpeg.mdc (nền đen, chuỗi background từng đoạn, opacity, avatar, wave alpha hoặc lumakey, ass, chuyển bt709). Trả về đối tượng có danh sách input + chuỗi filter_complex (test được mà không cần chạy ffmpeg).
4. `Render/RenderCommandBuilder`: 3 chế độ Full / Clip(seconds) / Frame(time) -> danh sách đối số ffmpeg (ArgumentList). Chế độ Frame theo ffmpeg.mdc (D rút ngắn t+0.5, -ss ở phía output, PNG rộng 800). `Render/EncoderSelector` (auto dò NVENC bằng bản mã hóa thử 256x256; rơi về libx264; cache kết quả).
5. `Render/JobRenderer`: tạo thư mục tạm cho job, ghi sub.ass, chạy FfmpegRunner (cwd = thư mục tạm; mọi đường dẫn tuyệt đối), ghi ra `.mp4.part` rồi đổi tên khi thành công, dọn thư mục tạm trong finally, hủy được bằng CancellationToken.
6. Cli: `vat preview --root --template --index N --time T --out frame.png` và `vat render --root --template --index N [--seconds 10] [--out file.mp4]`. Nếu thiếu `--time` thì lấy giây của cue đầu tiên + 0.4.
7. Test tích hợp (tự bỏ qua nếu thiếu ffmpeg/testdata), dùng helper `FrameSampler` (SkiaSharp hoặc cách bạn đề xuất) để đọc điểm ảnh:
   - Độ dài video 1 = độ dài audio nguồn (sai số <= 0.1 s); đúng 1 video + 1 audio; 1280x720; 25 fps; h264 + aac.
   - Opacity: template bgOpacity 0.6 và 1.0 -> tỉ lệ độ sáng vùng chỉ có background trong 0.60 +- 0.03.
   - side=left: tâm avatar ở nửa trái, tâm wave ở nửa phải.
   - Avatar đặt tràn ra ngoài mép phải/dưới vẫn render đúng.
   - Sub: có màu chữ trong vùng hộp ở giữa 1 cue, không có ở khoảng trống.
   - Tên file có emoji/khoảng trắng/dấu tiếng Việt chạy hết (sẽ có sẵn trong testdata).
   - Hủy giữa chừng: ffmpeg dừng, .part bị xóa, thư mục tạm bị xóa.
   - Wave nền đen dùng lumakey chạy được (dùng file wave_black.mp4 từ script).

RÀNG BUỘC
- Đây là bản baseline: mỗi đoạn background là 1 input. CHƯA làm cache/concat demuxer (M4).
- Không sửa lại quy ước trong ffmpeg.mdc; nếu cần khác thì DỪNG và hỏi tôi kèm bằng chứng.
- NVENC: chỉ cần dò và có nhánh code; không được khẳng định chạy tốt nếu chưa chạy thật.

TIÊU CHÍ NGHIỆM THU
- Xem `vat preview` cho khung hình giống bố cục SPEC §3 (sub bên trái, wave bên trái phía trên, avatar bên phải bám đáy).
- `vat render --index 1 --seconds 10 --out test.mp4` chạy được và mở được bằng trình phát.
- Toàn bộ test xanh.

KHI XONG
Cập nhật docs/PROGRESS.md, kể cả thời gian render đo được (giây render / giây video) trên máy này và encoder đã dùng.
Trước khi viết code: Plan Mode, liệt kê file/test, chờ tôi duyệt.
```

---

## M3 - Kiểm tra hợp lệ (Validation)

```text
MỤC TIÊU
Cài đặt nút "Kiểm tra": engine trả về danh sách kết quả có cấu trúc theo bảng mã SPEC §8.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (§8, §2, §4)  và code Planner/Probe/Template.

VIỆC CẦN LÀM
1. `Validation/`: `ValidationIssue(Code, Level, Scope, Message, FixHint)`, `ValidationReport` (nhóm theo layer và theo video; đếm Error/Warning/Info; `CanRender(videoIndex)`), `ValidationEngine.RunAsync(template, root, IProgress, CancellationToken)`.
2. Mỗi mã E/W/I trong bảng §8 là một rule riêng, một class nhỏ hoặc một hàm có tên rõ ràng, có test riêng (thư mục tạm + fake probe). Thông báo tiếng Việt: nói rõ file nào, vấn đề gì, cách sửa.
3. W042: so sánh độ phân giải background với (canvas x scale) theo chế độ cover. W072: ước tính dung lượng = tổng thời lượng x bitrate ước lượng x 1.3 (bitrate ước lượng từ quality, ghi rõ công thức trong code) rồi so với dung lượng trống của ổ đích. W070: kiểm tra font đã cài (đề xuất interface `IFontCatalog`, bản Windows dùng API hệ thống; bản test dùng fake).
4. Cli: `vat validate --root --template [--json]` in bảng dễ đọc và JSON; mã thoát 0/2 theo SPEC §13.
5. Test: mỗi mã có ít nhất 1 test dương và 1 test âm; kịch bản nhiều lỗi cùng lúc; 1 video lỗi không ảnh hưởng video khác; kết quả JSON ổn định (sắp xếp xác định).

RÀNG BUỘC
- Không gọi ffmpeg render. Probe thật chỉ dùng trong 1-2 test tích hợp.
- Không làm UI.

TIÊU CHÍ NGHIỆM THU
- Trên testdata mặc định: chỉ có I043 (bg2 có tiếng) và các mã Info/Warning hợp lý; không có Error.
- Xóa 1 file SRT trong testdata => E030 cho đúng video đó, các video khác vẫn hợp lệ.

KHI XONG
Cập nhật docs/PROGRESS.md. Trước khi viết code: Plan Mode, liệt kê rule và test, chờ tôi duyệt.
```

---

## M4 - Cache / Freeze và video dài

```text
MỤC TIÊU
Làm cache cho layer freeze và chứng minh render video dài không cần mở hàng chục input. Đây là phần CHƯA THỬ: phải có bằng chứng đo được.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @.cursor/rules/ffmpeg.mdc (mục Cache)  @docs/SPEC.md (§9, §14, §16)  và code Render của M2.

VIỆC CẦN LÀM
1. Chạy `pwsh scripts/make-testdata.ps1 -Long` để có driver 37:30 và 40 background ngắn.
2. `Cache/CacheKey` (SHA-256 của đường dẫn + size + mtime + mọi tham số ảnh hưởng kết quả), `Cache/CacheStore` (thư mục %LOCALAPPDATA%\VideoAutoTool\cache, ghi nguyên tử qua file tạm, LRU dọn khi vượt giới hạn dung lượng, thống kê, xóa tất cả).
3. `Cache/PreparedAssetService`: chuẩn bị (a) từng background ở đúng kích thước/fps canvas, opacity nướng sẵn (nhân RGB với opacity), mã hóa H.264 tham số giống hệt nhau, (b) avatar đã scale sẵn, (c) wave đã dựng sẵn. Chuẩn bị song song có giới hạn, có progress, hủy được.
4. Đường render "có cache": các background đã chuẩn bị nối bằng concat demuxer thành MỘT input duy nhất, dùng trực tiếp làm lớp nền (trim=duration=D); các layer còn lại dùng bản cache. Giữ đường baseline để so sánh.
5. Cli: `vat bench --root --template --index N` chạy cả hai đường (baseline vs cache) và in: thời gian chuẩn bị cache, thời gian render, số input trong lệnh, độ chênh độ sáng vùng nền. `vat cache --info | --clear`.
6. Test: khóa cache đổi khi đổi bất kỳ tham số nào (scale, opacity, canvas, fps, mtime file); dọn LRU; ghi nguyên tử (kill giữa chừng không để cache hỏng).

TIÊU CHÍ NGHIỆM THU (phải đo và báo số liệu thật)
- Kết quả có cache và baseline giống nhau (độ sáng vùng nền chênh dưới 3 %), độ dài bằng nhau (<= 0.1 s).
- Lệnh render có cache mở không quá 8 input.
- Bộ dữ liệu dài render xong, RAM ổn định (báo mức đỉnh đo được), lần render thứ 2 (cache nóng) nhanh hơn baseline. Nếu KHÔNG nhanh hơn: DỪNG, báo số liệu và đề xuất phương án khác. Không tự đổi kiến trúc.

RÀNG BUỘC
- Không đổi hành vi baseline của M2. Không thêm package. Chưa làm UI.

KHI XONG
Ghi toàn bộ số liệu đo vào docs/PROGRESS.md. Trước khi viết code: Plan Mode, chờ tôi duyệt.
```

---

## M5 - Hàng đợi render

```text
MỤC TIÊU
Hàng đợi render bền vững, chạy hàng loạt bằng dòng lệnh.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (§10, §13, §15)  và JobRenderer/Cache của M2, M4.

VIỆC CẦN LÀM
1. `Queue/RenderJobItem` (id, driver, trạng thái Pending/Running/Paused/Done/Failed/Cancelled/Skipped, tiến độ, thời gian bắt đầu/kết thúc, log tail, đường dẫn xuất), `Queue/JobStore` lưu queue.json ghi nguyên tử; khởi động lại thì job Running quay về Pending.
2. `Queue/RenderQueue`: thêm job từ Plan (bỏ video có Error của validation; tùy chọn skipExisting), chạy song song 1..3 (mặc định 1), tạm dừng/tiếp tục, hủy từng job/tất cả, thử lại job lỗi, đổi thứ tự, sự kiện tiến độ tổng (% và thời gian còn lại ước tính), job lỗi không dừng hàng đợi, luôn dọn .part và thư mục tạm.
3. Cli: `vat run --root --template [--from N --limit M --parallel K]` với thanh tiến độ đơn giản; Ctrl+C hủy an toàn và lưu trạng thái; mã thoát theo SPEC §13.
4. Test: dùng `IJobRunner` giả để test logic (thứ tự, song song, tạm dừng, hủy, thử lại, lưu/khôi phục, skipExisting); 2 test tích hợp với ffmpeg thật trên video ngắn (chạy lại lần 2 => bỏ qua hết, không .part; hủy giữa chừng => sạch).

RÀNG BUỘC
- Không UI. Không thêm package. Không chạy song song quá 3.

TIÊU CHÍ NGHIỆM THU
- `vat run` trên testdata tạo đủ 3 video đúng độ dài; chạy lần 2 bỏ qua; kill process giữa chừng rồi chạy lại vẫn ổn.

KHI XONG
Cập nhật docs/PROGRESS.md. Trước khi viết code: Plan Mode, chờ tôi duyệt.
```

---

## M6 - Vỏ WPF: nguồn, kiểm tra, hàng đợi (chưa canvas)

```text
MỤC TIÊU
Ứng dụng WPF dùng được đầy đủ luồng làm việc, nhưng bố cục layer chỉnh bằng ô số (canvas kéo thả để M7).

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (§11 tab Nguồn & kiểm tra / Hàng đợi / Cài đặt, §12)  @.cursor/rules/project.mdc

VIỆC CẦN LÀM
1. Cho phép thêm package: Microsoft.Extensions.DependencyInjection (chỉ package này ngoài CommunityToolkit.Mvvm). Cấu trúc MVVM: View chỉ binding; ViewModel không tham chiếu UI; dịch vụ Core được inject.
2. Cửa sổ chính, chủ đề tối, chuỗi tiếng Việt trong Resources/Strings.vi.resx, 4 tab: Thiết kế (tạm thời: danh sách layer + bảng thuộc tính bằng ô số, kèm nút "Xem chính xác" gọi Preview của M2 và hiện ảnh), Nguồn & kiểm tra, Hàng đợi, Cài đặt.
3. Tab Nguồn & kiểm tra: bảng theo layer (thư mục sửa được + nút Browse, số file, trạng thái); nút "Kiểm tra" chạy ValidationEngine; bảng kết quả xanh/vàng/đỏ theo layer và theo video, lọc được; nút "Thêm vào hàng đợi" (tùy chọn bỏ qua video lỗi).
4. Tab Hàng đợi: danh sách job, tiến độ %, thời gian còn lại, tạm dừng/tiếp tục/hủy/thử lại, xem log của job, số job song song, tùy chọn khi xong (mở thư mục xuất).
5. Tab Cài đặt: đường dẫn ffmpeg/ffprobe (nút "Dò" + hiển thị phiên bản), encoder, chất lượng, thư mục cache + dung lượng + nút xóa, số job song song.
6. Template: Mở/Lưu/Lưu thành/Import/Export; dấu "chưa lưu"; nhớ Root và template gần nhất trong settings.json.
7. Mọi thao tác dài chạy nền, giao diện không treo; hiển thị lỗi bằng thông báo tiếng Việt rõ ràng.

RÀNG BUỘC
- Không viết logic nghiệp vụ trong code-behind. Không canvas kéo thả ở lát cắt này.
- Test ViewModel bằng xUnit khi có logic (lệnh, trạng thái).

TIÊU CHÍ NGHIỆM THU (tôi kiểm tra bằng tay; agent liệt kê từng bước để tôi làm)
- Mở app, chọn Root testdata, Quét, Kiểm tra, Thêm vào hàng đợi, chạy, thấy 3 video ra trong xuat_render; app không treo.
- Tắt app giữa chừng rồi mở lại: hàng đợi còn nguyên, job dở dang trở về Pending.

KHI XONG
Cập nhật docs/PROGRESS.md; nêu rõ phần nào chỉ kiểm chứng được bằng tay. Trước khi viết code: Plan Mode, chờ tôi duyệt.
```

---

## M7 - Canvas thiết kế kiểu Premiere

```text
MỤC TIÊU
Canvas 1280x720 để thiết kế template bằng kéo thả, có Lock/Freeze, xem tức thì và xem chính xác.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (§3, §4, §7, §9, §11 phần Canvas thiết kế)  và code App của M6.

VIỆC CẦN LÀM
1. Core: `Design/DesignHistory` (Undo/Redo theo lệnh cho mọi thay đổi template, gộp các thay đổi liên tiếp khi đang kéo thành 1 bước), có test đầy đủ. `Design/Snapping` (giữa, 1/3, mép khung, layer khác; bật/tắt) có test.
2. App - Canvas: vùng vẽ 1280x720 co giãn (Vừa/50 %/100 %), vùng ngoài khung hiển thị mờ để thấy phần tràn. Mỗi layer là 1 phần tử trên canvas; chọn bằng click; hộp chọn có 8 tay cầm (kéo góc = scale giữ tỉ lệ, Shift = tự do), kéo thân = di chuyển, phím mũi tên 1 px / Shift 10 px, đường gióng khi snap.
3. Danh sách layer: hiện/ẩn, Lock (không kéo/sửa được), Freeze (nối với cache M4), đổi tên, kéo thả đổi thứ tự lớp, thêm/xóa layer (loại: backgroundChain, image, loopVideo, subtitle, fixedText, staticImage).
4. Bảng thuộc tính bên phải liên kết 2 chiều với canvas: X/Y, neo (chọn 1 trong 9 điểm), scale %/rộng, opacity, thư mục nguồn (+Browse), cách chọn file, preset style, cạnh avatar right/left/alternate.
5. Layer Sub: hộp chữ kéo/co giãn; chiều rộng hộp quyết định ngắt dòng; chọn preset (font, cỡ, đậm, màu chữ, màu viền, độ dày viền, bóng) và xem ngay bằng chữ mẫu; danh sách font lấy từ font đã cài.
6. Xem thử 2 tầng: (a) tức thì: dùng ảnh proxy đã cache (frame background/driver tại giây đang chọn, avatar, 1 frame wave) ghép bằng WPF, kéo thả phải mượt; (b) "Xem chính xác": gọi Preview của M2 (debounce 500 ms sau thay đổi cuối, hủy request cũ khi có request mới) và hiện đúng hình video xuất. Thanh giây chọn thời điểm; mặc định nhảy tới cue đầu + 0.4 s. Nút "Render thử 10 giây" tự mở file.
7. Tự lưu nháp template; Ctrl+Z / Ctrl+Y; Ctrl+S; dấu "chưa lưu".

RÀNG BUỘC
- Không keyframe/animation. Không xoay layer. Hình xem tức thì chỉ là xấp xỉ; hình chính xác luôn đến từ ffmpeg.
- Kéo thả phải không giật với ảnh 1280x720; nếu cần tối ưu, đề xuất trước khi làm.

TIÊU CHÍ NGHIỆM THU (tôi kiểm tra bằng tay; agent phải liệt kê từng bước)
- Dựng lại bố cục 2 ảnh mẫu bằng chuột: avatar to bám đáy tràn mép phải, wave ở nửa trái, sub ở nửa trái; "Xem chính xác" khớp; Lưu template; đóng/mở lại vẫn đúng.
- Lock chặn kéo; Undo/Redo đúng; đổi scale background thì xem chính xác cập nhật.

KHI XONG
Cập nhật docs/PROGRESS.md kèm danh sách hạn chế đã biết. Trước khi viết code: Plan Mode; vì phần này lớn, hãy chia thành các bước nhỏ có thể kiểm chứng từng bước, chờ tôi duyệt.
```

---

## M8 - Chế độ tự động, hoàn thiện, đóng gói

```text
MỤC TIÊU
Chế độ tự động, hoàn thiện trải nghiệm, đóng gói thành file chạy.

ĐỌC TRƯỚC
@docs/PROGRESS.md  @docs/SPEC.md (§10 chế độ tự động, §14, §15, §16)

VIỆC CẦN LÀM
1. Chế độ tự động: theo dõi thư mục driver bằng FileSystemWatcher; video mới được coi là sẵn sàng khi kích thước ổn định 10 giây và đã có SRT tương ứng; tự kiểm tra rồi thêm vào hàng đợi; bật/tắt trong Cài đặt; log rõ mọi quyết định.
2. Rà soát thông báo lỗi tiếng Việt (nói rõ file nào, bước nào, cách sửa) và trạng thái trống của từng màn hình.
3. Chạy lại TOÀN BỘ bảng nghiệm thu SPEC §16 (test tự động + danh sách kiểm tra bằng tay), đo hiệu năng trên bộ dữ liệu dài và ghi số liệu (x264 và NVENC trên máy này).
4. Đóng gói: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` cho App; hướng dẫn cài ffmpeg trong app khi thiếu; file README.md tiếng Việt (cài đặt, dùng từng tab, xử lý sự cố thường gặp, ý nghĩa Lock/Freeze).
5. Tag phiên bản v1.0.0 trong git (chỉ đề xuất lệnh, tôi tự chạy).

TIÊU CHÍ NGHIỆM THU
- Bảng §16 đạt hết hoặc liệt kê rõ mục chưa đạt và lý do.
- Chạy được file .exe đã publish trên máy sạch không cài .NET (tôi kiểm tra bằng tay).

KHI XONG
Cập nhật docs/PROGRESS.md với danh sách việc còn lại cho v1.1. Trước khi viết code: Plan Mode, chờ tôi duyệt.
```
