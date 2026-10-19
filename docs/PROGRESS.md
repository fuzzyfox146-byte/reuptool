# PROGRESS - handoff log (agent: read first, update at the end of every task)

## Current state
- Milestone: bản gốc `publish-1.0.25`; bản GPU `publish-2.0.0-gpu`; bản tối ưu `publish-2.1.3-opt`.
- Session: gốc `%AppData%\VideoAutoTool`; GPU `%AppData%\VideoAutoTool-Gpu`; Opt `%AppData%\VideoAutoTool-Opt` (lần đầu copy từ Gpu, rồi gốc; không ghi đè bản kia).
- Nền người dùng đã chuyển tay sang `C:\background` (12 file). Nguồn và file xuất giữ trên D (HDD).
- 720p vẫn CUDA overlay, 480p vẫn CPU graph, NVENC `p1`. Lỗi CUDA thì render lại graph CPU.
- Git: branch `gpu-render`. Fix lệch số thumb/sub/video + render hợp lệ đã publish `publish-2.1.3-opt`.

## Done
- YouTube Folder Uploader: nút **Đăng hết ngày mai** bỏ qua kênh đã có lịch ngày mai và ghi log, không đăng thêm. Kênh thiếu video cũng bỏ qua. Exe: `Upload video\app-daily-18\YtFolderUploader.exe`.
- YouTube Folder Uploader: nút **Đăng hết ngày mai** đăng lần lượt từ kênh trên xuống dưới, không đăng cùng lúc. Nút **Xóa video đã đăng mọi kênh** xóa file local đã đăng, video trên YouTube vẫn còn. Exe: `Upload video\app-daily-17\YtFolderUploader.exe`.
- Tải video theo view: một số chỉ còn một file phụ đề. `en` và `en-orig` không được giữ cả hai. Ô ngôn ngữ hiện tên (Anh, Nhật, Bồ), không hiện tên class. Bản mang máy: `C:\HdDownloaderView-5`.
- Tải video theo view: phụ đề thiếu thì lấy phụ đề gốc của video, cùng số với video. Video tải H.264 + AAC để Premiere mở được. File AV1 đã tải thì bấm Tải lại, app tải lại hoặc chuyển sang H.264.
- Tải video theo view: danh sách thả xuống (chất lượng, số video, ngôn ngữ phụ đề) nền tối, chữ trắng.
- Tải video theo view: **Ngôn ngữ phụ đề** là danh sách thả xuống. Mặc định Anh, Nhật, Bồ. Có từng ngôn ngữ, vài bộ kết hợp, và Tất cả. Mỗi file vẫn cùng số với video.
- Tải video theo view có ô **Tải phụ đề vào folder sub**, bật sẵn. Phụ đề ghi cùng số với video: video `001` thì file trong `sub` cũng `001`.
- Tải video theo view, bản mang máy: `C:\HdDownloaderView`. Một exe tự chứa .NET, kèm `yt-dlp.exe`, `ffmpeg.exe`, `ffprobe.exe`, `cookies.txt`. Zip cả folder rồi mở exe trên máy khác. Ổ D hết chỗ nên folder nằm trên C.
- YouTube Folder Uploader: hết hạn mức thumbnail thì hàng đợi đổi ngày công khai ngay, không giữ ngày cũ. Exe: `Upload video\app-daily-16\YtFolderUploader.exe`.
- YouTube Folder Uploader: trước khi đăng thì thử gắn thumbnail. Không gắn được thì dừng cả lượt, không đăng thêm, và đẩy ngày công khai sang ngày sau. Exe: `Upload video\app-daily-15\YtFolderUploader.exe`.
- Tải video theo view: app riêng `publish-hd-view\HdDownloaderView.exe`, cấu hình `%AppData%\HdDownloaderView`. Ô **Theo view, cao xuống thấp** bật sẵn. Trong khoảng Từ–Đến, video nhiều view nhất ghi số ở ô Số (1 thành 001), rồi giảm dần. Bản `HdDownloader` hiện tại không có ô này và vẫn theo thứ tự kênh.
- Frame Render 3 giống Frame Render 2 (GPU, 480p, hàng đợi). Exe `publish-frame-3\FrameRender3.exe`, dữ liệu `%AppData%\FrameRender-3`, file tạm `%LocalAppData%\FrameRender-3\out-tmp`. Không đọc hàng của `%AppData%\FrameRender-2`.
- YouTube Folder Uploader: hàng đợi xếp giờ từ ngày mai, không lấy nốt giờ còn lại của hôm nay. Exe: `Upload video\app-daily-14\YtFolderUploader.exe`.
- YouTube Folder Uploader: **Dùng mẫu này** đổ số video, giờ và các mục bên dưới theo mẫu. Mẫu đã đặt tên không bị ghi đè khi sửa kênh. Mẫu tên 4 video lúc 08:30 được khôi phục đúng giờ đó. Exe: `Upload video\app-daily-13\YtFolderUploader.exe`. Lịch tự động chỉ tiếp các kênh đã bấm **Đăng**. Exe: `Upload video\app-daily-12\YtFolderUploader.exe`. Kênh đang upload thì khóa ô sửa. Hàng của kênh đang đăng không bị quét đè. Exe: `Upload video\app-daily-11\YtFolderUploader.exe`. Enter hoặc nút đó lưu tên kênh đang chọn. Exe: `Upload video\app-daily-10\YtFolderUploader.exe`. Không xóa file. Quét lại thì danh sách hiện lại. Exe: `Upload video\app-daily-9\YtFolderUploader.exe`. Đổi giờ rồi **Lưu** thì mẫu đang chọn và mọi kênh dùng mẫu đó đổi theo. **Thêm mẫu** tạo bộ mới, mẫu cũ giữ nguyên. Có ô tên mẫu và **Xóa mẫu**. Danh sách kênh không còn ô tick. Exe: `Upload video\app-daily-8\YtFolderUploader.exe`.
- YouTube Folder Uploader: tab **Tùy chọn đăng** có **mẫu cấu hình** dùng chung. Lưu hoặc Ctrl+S: cấu hình khác các mẫu đã có thì tạo mẫu mới. Kênh khác chọn mẫu rồi bấm **Dùng mẫu này**. Không copy token hay nguồn. Exe: `Upload video\app-daily-7\YtFolderUploader.exe`.
- YouTube Folder Uploader: nút **Ủy quyền lại**. Lần đăng nhập Google chờ 10 phút. Hết giờ hoặc đóng trình duyệt thì bấm lại, không kẹt lần cũ. Exe: `Upload video\app-daily-6\YtFolderUploader.exe`.
- YouTube Folder Uploader: chọn kênh nào thì hàng đợi chỉ hiện kênh đó. Xóa file đã đăng thì xóa cả video và thumbnail, không vào thùng rác. Exe: `Upload video\app-daily-5\YtFolderUploader.exe`.
- YouTube Folder Uploader: đổi số video mỗi ngày không còn tick hết hàng đợi. Chỉ tick đúng số video của lượt kế tiếp, video còn lại để **Chờ ngày sau**. Ô lịch 1 ngày không bị tắt khi sửa số lượng.
- YouTube Folder Uploader: Ctrl+S lưu cấu hình đăng. Đổi **Số video công khai mỗi ngày** hoặc **Từ số** thì hàng đợi quét lại. Video **Bỏ qua** không còn hiện trong hàng. Số video một ngày lấy theo ô số lượng, nhưng không vượt số giờ đã nhập.
- YouTube Folder Uploader: ô **Lên lịch trước 1 ngày** (bật sẵn). Chỉ xếp đúng các giờ đã nhập cho ngày mai. Bấm **Đăng video đã chọn** rồi để app mở: qua ngày mới tự đăng ngày kế tiếp. Thiếu video cho đủ một ngày thì tên kênh đỏ chữ **thiếu video** và dừng, không đăng nửa ngày. **Dừng** tắt lịch tự động.
- YouTube Folder Uploader: tab **Nguồn video** có danh sách video có ảnh và nút **Đưa video vào hàng đợi**. Nút đó bật nguồn, chỉ đưa video đã có thumbnail vào **Hàng đợi**, rồi chuyển sang tab đó. Video thiếu ảnh không được đăng. Exe mới: `Upload video\app-daily-3\YtFolderUploader.exe`.
- Frame Render 2 có ô **Chất lượng**: 720p (1280×720) hoặc 480p (854×480). Chọn 480p rồi **Bắt đầu**. File xuất đã có ở 720p thì vẫn Bỏ qua, không render lại 480p.
- Frame Render 2: chọn nguồn `D:\HD044\video` thì ô xuất thành `D:\HD044\xuat`. Folder chưa có thì tạo, có rồi thì dùng. Bản hàng đợi thường không tự đổi ô xuất.
- Bản đang mở `FrameRender.exe` (tiêu đề **Frame Render — hàng đợi**, hàng `%AppData%\FrameRender`) khác bản 2. Ctrl+S trước đây không gắn. GPU bản cũ khóa 3 video. Bản mới xóa hẳn dòng **Bỏ qua** khỏi hàng, Ctrl+S lưu cấu hình và áp dụng **Số video cùng lúc** (1–16). Tiến độ `x/tất cả` vẫn tính video đã có.
- Frame Render 2 lấy đúng ô **Số video cùng lúc**. Trước đây bật GPU thì khóa cứng 3 dù ô ghi 5. Giờ số trong ô là số ffmpeg chạy (1–16). Đổi số lúc đang render: bấm Enter hoặc click ra ngoài ô, không cần hủy. Hết chỗ đĩa thì máy vẫn tự giảm và ô cập nhật theo.
- Frame Render 2 giữ hàng đợi khi đóng cửa sổ: video đang chạy về Chờ, tạm dừng và đã hủy vẫn nằm trong hàng. Chỉ **Xóa hàng đợi** mới xóa. Mỗi folder hiện `x/tất cả` (xong hoặc bỏ qua / tổng video), cột **Nguồn (x/tất cả)** và ô **Từng folder**. File `%AppData%\FrameRender-2\queue.json` ghi qua file tạm rồi mới thay.
- Frame Render 2 bật GPU sẵn (NVENC + CUDA). Công thức giữ độ mờ 0.90. Nguồn `D:\HD037\video` đặt tên HD037, không còn `video` / `video-2`. File mp4 ghi thẳng vào folder xuất; lần render sau bỏ qua file đã có và không tạo folder con.
- Bỏ hẳn lớp nhiễu và cache nhiễu. Graph còn nền CUDA, video chính, khung PNG, rồi NVENC.
- Đường GPU của FrameRender giữ frame trên card giống bản Opt: `scale_cuda` nền đúng 1280×720, `overlay_cuda` video, khung PNG, NVENC `p1`. Không còn khung đen dài bằng cả video và không kéo video chính về CPU để làm mờ 90% (`overlay_cuda` không có opacity). Đo clip 20 giây trên file thật, cùng lúc 3 job đang chạy: graph cũ ~3,5× thời gian thực, graph mới ~9×. Graph CPU vẫn làm mờ 0.90. Lỗi CUDA thì render lại graph CPU.
- Tool WinForms `FrameRender` xếp hàng đợi theo folder giống bản Opt: mỗi folder đưa 3 video vào hàng chính (ô **Mỗi folder**), folder sau cũng 3, hết lượt đó mới lấy 3 tiếp. Cửa sổ có **Hàng chính** và **Nguồn đang chờ**. File xuất nằm trong `thư mục xuất\<tên folder>`. Tạm dừng thì dừng ffmpeg đang chạy, Tiếp tục render lại video đó. Hết chỗ đĩa hoặc file bị khóa thì giảm số video cùng lúc rồi render lại. File `.part.mp4` ghi tạm trên SSD (`%LocalAppData%\FrameRender\out-tmp`) khi thư mục xuất ở ổ khác và ổ C còn đủ chỗ. Exe bản đang dùng: `src\FrameRender.App\bin\Debug\net8.0-windows\FrameRender.exe` (hàng đợi `%AppData%\FrameRender`). Bản mới riêng: `publish-frame-2\FrameRender2.exe`, hàng đợi `%AppData%\FrameRender-2`, file tạm `%LocalAppData%\FrameRender-2\out-tmp`. Hai bản không đọc chung queue.
- Tool Python `batch-render` vẫn là bản dòng lệnh (`render.py`). `ui.py` không phải cửa sổ để dùng.
- Tool riêng `HdDownloader`: nhập link kênh, chất lượng mặc định 720, nhiều video cùng lúc (1–4). Tên folder do người dùng đặt (ví dụ `HD036`) chứa `video` và `thumb`, file `001 …`. Không tải phụ đề. Số đã có thì bỏ qua. App VAT không đổi. Nền cửa sổ gắn trực tiếp vì style `Window` không ăn, chữ trắng trên nền trắng.

- Avatar: ảnh tĩnh scale **một lần**, `overlay eof_action=repeat` (không `-loop 1` mỗi khung).
- Wave: cache bake (fps + scale + lumakey nếu nền đen) lossless qtrle. Graph CPU dùng bản bake. 720p wave nền đen vẫn `chromakey_cuda` trên file gốc.
- Cache nền encode **cả DurationFull** (khóa cache vốn ghi vậy). Video dài hơn không còn ăn cache ngắn rồi bị `-shortest` cắt.
- Input nguồn: `-vn -sn -dn` — chỉ lấy tiếng, không demux hình.
- Sau `progress=end`: chỉ kill ffmpeg khi file `.part` **không đổi size 15 giây** (faststart trên HDD không bị cắt giữa chừng).
- File `.part` ghi tạm trên SSD (`%LocalAppData%\VideoAutoTool\out-tmp`) khi C còn trống ≥ 2× ước tính + 5 GB; xong mới copy sang D. Không đủ chỗ thì ghi thẳng D.
- Không `string.Replace` thời lượng vào filter (trước đây duration `4` biến `yuv420p` thành `yuv320p`).
- Hàng đợi ghi `%AppData%\<edition>\queue.json` (job + plan + template nguồn). Mở lại khôi phục; job đang chạy lúc đóng về Chờ. Bấm **Render tiếp** để chạy phần còn. **Xóa hàng đợi** vẫn xóa file.
- Tải nguồn: không ghi file lịch sử. Video tải tiếp từ số mp4 lớn nhất + 1. Phụ đề/thumb theo **video cùng số** (ghép title → watch URL), không theo vị trí playlist hôm nay. File lệch tên bị xóa rồi tải lại. Lỗ không có video thì bỏ. Sau số video lớn nhất vẫn bù đến số nhập. Retry 4 lần / 8s. Video mới vẫn ghi thumb khi tải mp4.
- Render: **Render hết** chỉ đưa vào hàng đợi khi mọi video đều khớp SRT. **Render video hợp lệ** đưa các cặp khớp, bỏ qua video lệch. Hàng đợi xong thì chuyển video Done sang `rendered` (folder cha của video đầu tiên) và báo số cặp còn hợp lệ.
- `dotnet build` FrameRender: 0 warning. `FrameBatchTests`: 15 passed (thêm khung xuất 480p 854×480).

## Decisions
- Không đổi layout / preset NVENC / màu bt709 / graph 480p `gbrp`.
- Không tự copy nền trong app. Không giới hạn `filter_complex_threads` (chưa đo, giữ ổn định).
- Không so SSIM với file 75 phút của bản 2.0.0-gpu trong phiên này (cần một job ngắn do người dùng render hai bản).
- Catalog `--flat-playlist` cả kênh (không `--playlist-end`) để video cũ vẫn tìm được id.

## Open issues / risks
- `FrameRender` chưa render khung PNG thật của người dùng. Test chỉ kiểm tra tọa độ, lệnh ffmpeg và ghép cặp. Clip 1 giây đã thử ở bản Python `batch-render`, chưa thử lại trên exe WinForms.
- `HdDownloader` chưa chạy tải thật một kênh YouTube trong phiên này (test dùng yt-dlp giả). Cần `yt-dlp.exe`, `cookies.txt` và `bin\ffmpeg.exe` (tool tự tìm ở `D:\Tải video`).
- Tải phụ đề: lỗ SRT vẫn còn nếu 4 lần thử đều không ra file (clip không có sub, hoặc cookie chết). Lần Tải tiếp sẽ xin lại các số trống.
- Video `AfterHighest` vẫn có thể trùng title khi playlist dịch (CO139 404 vs 408). Chưa gắn id vào file video.
- Title trùng trên kênh: ưu tiên `playlist_index == số file`, có thể vẫn lệch.
- Lần đầu mỗi nền: cache encode cả clip (lâu hơn trước nếu nền dài hơn video). Các job sau dùng lại thì nhanh và đúng độ dài.
- Cache + `.part` tạm cùng nằm trên C. Giữ ~40 GB trống.
- SSIM ≥ 0.99 so với `publish-2.0.0-gpu` chưa đo.

## Next step
- Bản 2: mở `D:\Coding\publish-frame-2\FrameRender2.exe`. Ô **Chất lượng** chọn `480p` rồi **Bắt đầu**. Nhật ký phải có `Xuất 854x480`. Đóng cửa sổ vẫn còn hàng đợi (kể cả tạm dừng / đã hủy). **Xóa hàng đợi** mới xóa. Mỗi folder hiện `x/tất cả`. GPU bật sẵn. Nguồn `D:\HD037\video` hiện là HD037. File xuất nằm thẳng trong folder xuất, video đã có thì Bỏ qua, không tạo folder con.
- Chạy tool riêng: `dotnet run --project src/HdDownloader`. Đặt tên folder `HD036`, dán link kênh, chất lượng 720. Ra `HD036\video` và `HD036\thumb`. Không tải phụ đề.
- Chạy `D:\Coding\publish-2.1.3-opt\VideoAutoTool.App.exe` rồi **Tải tiếp** `D:\CO139` để xóa thumb/SRT lệch và tải lại theo title video.
- Đừng mở đồng thời hai bản Opt rồi ghi cùng file xuất.
