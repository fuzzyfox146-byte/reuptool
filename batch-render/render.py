"""Batch-render a fixed frame design with FFmpeg.

Coordinates come from the reference hole below. The tool does not scan alpha
to find an empty region.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import shutil
import subprocess
import sys
import threading
import time
from concurrent.futures import ProcessPoolExecutor, ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from pathlib import Path

# Canvas and the reference frame the hole was measured on.
W = 1280
H = 720
FPS = 25
REF_W = 1672
REF_H = 941
# x, y, width, height on the 1672x941 reference. Not detected from pixels.
HOLE = (190, 117, 1296, 706)

VIDEO_SCALE = 0.80
VIDEO_OPACITY = 0.90
NOISE_OPACITY = 0.30
NOISE_SPEED = 0.5
NOISE_SECONDS = 10

CRF = 20
PRESET = "medium"
BG_CRF = 14
BG_PRESET = "fast"
NOISE_CRF = 8
AUDIO_BITRATE = "192k"
NVENC_MAX_WORKERS = 3

VIDEO_EXT = {".mp4", ".mkv", ".mov", ".webm", ".m4v"}
FRAME_EXT = {".png"}
CREATE_NO_WINDOW = 0x08000000 if os.name == "nt" else 0
ROOT = Path(__file__).resolve().parent


@dataclass(frozen=True)
class Settings:
    width: int = W
    height: int = H
    fps: int = FPS
    video_scale: float = VIDEO_SCALE
    video_opacity: float = VIDEO_OPACITY
    noise_opacity: float = NOISE_OPACITY
    noise_speed: float = NOISE_SPEED
    noise_seconds: float = NOISE_SECONDS
    crf: int = CRF
    preset: str = PRESET
    gpu: bool = False
    workers: int = 1
    threads: int = 1


@dataclass(frozen=True)
class Layout:
    video_w: int
    video_h: int
    x: int
    y: int

    @property
    def center(self) -> tuple[int, int]:
        return (self.x + self.video_w // 2, self.y + self.video_h // 2)


def even(value: float) -> int:
    rounded = int(round(value))
    if rounded % 2:
        rounded += 1 if value >= rounded else -1
    return max(rounded, 2)


def layout_for(settings: Settings) -> Layout:
    video_w = even(settings.width * settings.video_scale)
    video_h = even(video_w * 9 / 16)
    hole_x, hole_y, hole_w, hole_h = HOLE
    center_x = (hole_x + hole_w / 2) * settings.width / REF_W
    center_y = (hole_y + hole_h / 2) * settings.height / REF_H
    x = int(round(center_x - video_w / 2))
    y = int(round(center_y - video_h / 2))
    return Layout(video_w, video_h, x, y)


def list_media(folder: Path, extensions: set[str]) -> list[Path]:
    if not folder.is_dir():
        raise SystemExit(f"Không thấy thư mục: {folder}")
    files = [path for path in folder.iterdir() if path.is_file() and path.suffix.lower() in extensions]
    return sorted(files, key=lambda path: path.name.lower())


def fingerprint(path: Path, params: str) -> str:
    stat = path.stat()
    raw = f"{path.resolve()}|{stat.st_mtime_ns}|{stat.st_size}|{params}"
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def stable_seed(frames: list[Path], backgrounds: list[Path]) -> int:
    raw = "\n".join(str(path.resolve()) for path in frames + backgrounds)
    return int(hashlib.sha256(raw.encode("utf-8")).hexdigest()[:8], 16)


def output_name(video: Path, frame: Path) -> str:
    def clean(stem: str) -> str:
        return "".join("_" if char in '<>:"/\\|?*' else char for char in stem).strip() or "clip"

    return f"{clean(video.stem)}_{clean(frame.stem)}.mp4"


def assign_pairs(
    videos: list[Path],
    frames: list[Path],
    backgrounds: list[Path],
    mode: str,
    seed: int,
    previous: list[dict] | None,
) -> list[dict]:
    frame_order = list(frames)
    background_order = list(backgrounds)
    if mode == "random":
        rng = random.Random(seed)
        rng.shuffle(frame_order)
        rng.shuffle(background_order)

    saved = {}
    if previous:
        for item in previous:
            saved[str(Path(item["video"]).resolve())] = item

    frame_set = {str(path.resolve()) for path in frames}
    background_set = {str(path.resolve()) for path in backgrounds}
    items: list[dict | None] = []
    kept = 0
    for video in videos:
        old = saved.get(str(video.resolve()))
        if old and old.get("frame") in frame_set and old.get("background") in background_set:
            items.append(old)
            kept += 1
        else:
            items.append(None)

    cursor = kept
    used_names: dict[str, int] = {}
    planned: list[dict] = []
    for video, existing in zip(videos, items):
        if existing is None:
            frame = frame_order[cursor % len(frame_order)]
            background = background_order[cursor % len(background_order)]
            cursor += 1
            existing = {
                "video": str(video.resolve()),
                "frame": str(frame.resolve()),
                "background": str(background.resolve()),
            }
        name = output_name(Path(existing["video"]), Path(existing["frame"]))
        used_names[name] = used_names.get(name, 0) + 1
        if used_names[name] > 1:
            stem = Path(name).stem
            name = f"{stem}_{used_names[name]}.mp4"
        row = dict(existing)
        row["output"] = name
        planned.append(row)
    return planned


def load_plan(path: Path) -> dict | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def build_plan(videos, frames, backgrounds, mode: str, plan_path: Path) -> dict:
    previous = load_plan(plan_path)
    same_pool = False
    seed = stable_seed(frames, backgrounds)
    if previous and previous.get("mode") == mode:
        old_frames = previous.get("frames") or []
        old_backgrounds = previous.get("backgrounds") or []
        same_pool = old_frames == [str(path.resolve()) for path in frames] and old_backgrounds == [
            str(path.resolve()) for path in backgrounds
        ]
        if same_pool and isinstance(previous.get("seed"), int):
            seed = previous["seed"]
    items = assign_pairs(
        videos,
        frames,
        backgrounds,
        mode,
        seed,
        previous.get("items") if same_pool and previous else None,
    )
    plan = {
        "mode": mode,
        "seed": seed,
        "frames": [str(path.resolve()) for path in frames],
        "backgrounds": [str(path.resolve()) for path in backgrounds],
        "items": items,
    }
    plan_path.parent.mkdir(parents=True, exist_ok=True)
    plan_path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
    return plan


def run_command(args: list[str], log_path: Path | None = None, cancel: threading.Event | None = None) -> None:
    process = subprocess.Popen(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=CREATE_NO_WINDOW,
    )
    chunks: list[str] = []

    def pump(stream) -> None:
        if stream is None:
            return
        for line in stream:
            chunks.append(line)

    pumps = [
        threading.Thread(target=pump, args=(process.stdout,), daemon=True),
        threading.Thread(target=pump, args=(process.stderr,), daemon=True),
    ]
    for thread in pumps:
        thread.start()
    try:
        while process.poll() is None:
            if cancel is not None and cancel.is_set():
                process.kill()
                process.wait(timeout=5)
                raise RuntimeError("Đã hủy.")
            time.sleep(0.15)
    finally:
        for thread in pumps:
            thread.join(timeout=1)
        if process.stdout is not None:
            process.stdout.close()
        if process.stderr is not None:
            process.stderr.close()
    text = "".join(chunks)
    if log_path is not None:
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_path.write_text(text, encoding="utf-8")
    if cancel is not None and cancel.is_set():
        raise RuntimeError("Đã hủy.")
    if process.returncode != 0:
        tail = text.strip()[-2000:]
        raise RuntimeError(tail or f"Lệnh thoát mã {process.returncode}")


def probe_duration(path: str) -> float:
    completed = subprocess.run(
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            path,
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=CREATE_NO_WINDOW,
    )
    if completed.returncode != 0:
        raise RuntimeError((completed.stderr or "ffprobe lỗi").strip())
    return float(completed.stdout.strip())


def has_audio(path: str) -> bool:
    completed = subprocess.run(
        [
            "ffprobe",
            "-v",
            "error",
            "-select_streams",
            "a",
            "-show_entries",
            "stream=index",
            "-of",
            "csv=p=0",
            path,
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=CREATE_NO_WINDOW,
    )
    return completed.returncode == 0 and bool(completed.stdout.strip())


def nvenc_available() -> bool:
    completed = subprocess.run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-f",
            "lavfi",
            "-i",
            "color=c=black:s=256x256:d=0.2",
            "-c:v",
            "h264_nvenc",
            "-f",
            "null",
            "-",
        ],
        capture_output=True,
        creationflags=CREATE_NO_WINDOW,
    )
    return completed.returncode == 0


def cache_file(folder: Path, source: Path, suffix: str) -> Path:
    digest = hashlib.sha256(str(source.resolve()).encode("utf-8")).hexdigest()[:8]
    return folder / f"{source.stem}_{digest}{suffix}"


def frame_cache_args(src: Path, dst: Path, settings: Settings) -> list[str]:
    return [
        "ffmpeg",
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-nostdin",
        "-i",
        str(src),
        "-vf",
        f"scale={settings.width}:{settings.height}:flags=lanczos,format=rgba",
        "-frames:v",
        "1",
        str(dst),
    ]


def background_cache_args(src: Path, dst: Path, settings: Settings) -> list[str]:
    return [
        "ffmpeg",
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-nostdin",
        "-i",
        str(src),
        "-an",
        "-vf",
        (
            f"scale={settings.width}:{settings.height}:force_original_aspect_ratio=increase,"
            f"crop={settings.width}:{settings.height},fps={settings.fps},setsar=1"
        ),
        "-c:v",
        "libx264",
        "-crf",
        str(BG_CRF),
        "-preset",
        BG_PRESET,
        "-g",
        str(settings.fps),
        "-pix_fmt",
        "yuv420p",
        str(dst),
    ]


def noise_cache_args(dst: Path, settings: Settings) -> list[str]:
    source_rate = settings.fps * settings.noise_speed
    graph = (
        f"color=c=gray:s={settings.width}x{settings.height}:r={source_rate}:d={settings.noise_seconds},"
        f"noise=alls=25:allf=t,fps={settings.fps},setsar=1,format=yuv420p"
    )
    return [
        "ffmpeg",
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-nostdin",
        "-f",
        "lavfi",
        "-i",
        graph,
        "-an",
        "-c:v",
        "libx264",
        "-crf",
        str(NOISE_CRF),
        "-preset",
        "fast",
        "-g",
        str(settings.fps),
        "-pix_fmt",
        "yuv420p",
        str(dst),
    ]


def render_filter(settings: Settings, placed: Layout) -> str:
    opacity = f"{settings.video_opacity:.2f}"
    noise = f"{settings.noise_opacity:.2f}"
    return (
        f"[0:v]fps={settings.fps},setpts=PTS-STARTPTS,"
        f"scale={placed.video_w}:{placed.video_h}:force_original_aspect_ratio=increase,"
        f"crop={placed.video_w}:{placed.video_h},setsar=1,format=rgba,colorchannelmixer=aa={opacity}[fg];"
        f"[1:v]fps={settings.fps},setpts=PTS-STARTPTS,setsar=1[bg];"
        f"[bg][fg]overlay={placed.x}:{placed.y}:format=auto[m1];"
        f"[2:v]format=rgba,fps={settings.fps},setpts=PTS-STARTPTS[fr];"
        f"[m1][fr]overlay=0:0:format=auto[m2];"
        f"[m2]format=gbrp[base];"
        f"[3:v]fps={settings.fps},setpts=PTS-STARTPTS,format=gbrp[ns];"
        f"[base][ns]blend=all_mode=overlay:all_opacity={noise},format=yuv420p[out]"
    )


def render_args(
    main_video: Path,
    background: Path,
    frame: Path,
    noise: Path,
    destination: Path,
    duration: float,
    with_audio: bool,
    settings: Settings,
    placed: Layout,
) -> list[str]:
    args = [
        "ffmpeg",
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-nostdin",
        "-i",
        str(main_video),
        "-stream_loop",
        "-1",
        "-i",
        str(background),
        "-loop",
        "1",
        "-framerate",
        str(settings.fps),
        "-i",
        str(frame),
        "-stream_loop",
        "-1",
        "-i",
        str(noise),
        "-filter_complex",
        render_filter(settings, placed),
        "-map",
        "[out]",
    ]
    if with_audio:
        args += ["-map", "0:a:0", "-c:a", "aac", "-b:a", AUDIO_BITRATE]
    else:
        args.append("-an")
    args += ["-t", f"{duration:.3f}", "-r", str(settings.fps), "-threads", str(settings.threads)]
    if settings.gpu:
        args += ["-c:v", "h264_nvenc", "-preset", "p4", "-rc", "vbr", "-cq", str(settings.crf), "-b:v", "0"]
    else:
        args += ["-c:v", "libx264", "-preset", settings.preset, "-crf", str(settings.crf), "-profile:v", "high"]
    args += [
        "-pix_fmt",
        "yuv420p",
        "-colorspace",
        "bt709",
        "-color_primaries",
        "bt709",
        "-color_trc",
        "bt709",
        "-color_range",
        "tv",
        "-movflags",
        "+faststart",
        str(destination),
    ]
    return args


def cache_one(
    kind: str,
    src: str,
    dst: str,
    settings: Settings,
    cancel: threading.Event | None = None,
) -> None:
    destination = Path(dst)
    destination.parent.mkdir(parents=True, exist_ok=True)
    partial = destination.with_name(destination.stem + ".part" + destination.suffix)
    if kind == "frame":
        args = frame_cache_args(Path(src), partial, settings)
    elif kind == "background":
        args = background_cache_args(Path(src), partial, settings)
    else:
        args = noise_cache_args(partial, settings)
    try:
        run_command(args, cancel=cancel)
        partial.replace(destination)
    except Exception:
        partial.unlink(missing_ok=True)
        raise


def prepare_cache(
    frames: list[Path],
    backgrounds: list[Path],
    cache: Path,
    settings: Settings,
    workers: int,
    cancel: threading.Event | None = None,
) -> dict[str, str]:
    manifest_path = cache / "manifest.json"
    manifest = {}
    if manifest_path.is_file():
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            manifest = {}
    frame_keys = manifest.get("frames") or {}
    background_keys = manifest.get("backgrounds") or {}
    jobs = []
    mapping: dict[str, str] = {}

    for frame in frames:
        destination = cache_file(cache / "frames", frame, ".png")
        key = fingerprint(frame, f"frame|{settings.width}x{settings.height}")
        mapping[str(frame.resolve())] = str(destination.resolve())
        if frame_keys.get(str(frame.resolve())) == key and destination.is_file():
            continue
        jobs.append(("frame", str(frame.resolve()), str(destination), key))

    for background in backgrounds:
        destination = cache_file(cache / "backgrounds", background, ".mp4")
        key = fingerprint(background, f"bg|{settings.width}x{settings.height}|{settings.fps}|crf{BG_CRF}")
        mapping[str(background.resolve())] = str(destination.resolve())
        if background_keys.get(str(background.resolve())) == key and destination.is_file():
            continue
        jobs.append(("background", str(background.resolve()), str(destination), key))

    noise_path = cache / "effects" / "noise_loop.mp4"
    noise_key = f"noise|{settings.width}x{settings.height}|{settings.fps}|{settings.noise_speed}|{settings.noise_seconds}|crf{NOISE_CRF}"
    noise_ready = manifest.get("noise") == noise_key and noise_path.is_file()
    if not noise_ready:
        jobs.append(("noise", "", str(noise_path), noise_key))

    if jobs:
        print(f"Tiền xử lý cache: {len(jobs)} mục")
        with ThreadPoolExecutor(max_workers=max(1, workers)) as pool:
            futures = [pool.submit(cache_one, kind, src, dst, settings, cancel) for kind, src, dst, _key in jobs]
            for future in as_completed(futures):
                if cancel is not None and cancel.is_set():
                    raise RuntimeError("Đã hủy.")
                future.result()

    new_frames = dict(frame_keys)
    new_backgrounds = dict(background_keys)
    for kind, src, _dst, key in jobs:
        if kind == "frame":
            new_frames[src] = key
        elif kind == "background":
            new_backgrounds[src] = key
    for frame in frames:
        source = str(frame.resolve())
        new_frames[source] = fingerprint(frame, f"frame|{settings.width}x{settings.height}")
    for background in backgrounds:
        source = str(background.resolve())
        new_backgrounds[source] = fingerprint(
            background, f"bg|{settings.width}x{settings.height}|{settings.fps}|crf{BG_CRF}"
        )
    payload = {"frames": new_frames, "backgrounds": new_backgrounds, "noise": noise_key}
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    mapping["noise"] = str(noise_path.resolve())
    return mapping


def render_job(payload: dict) -> dict:
    output = Path(payload["output"])
    log_path = Path(payload["log"])
    cancel = payload.get("cancel")
    partial = output.with_name(output.stem + ".part.mp4")
    try:
        if cancel is not None and cancel.is_set():
            raise RuntimeError("Đã hủy.")
        if output.is_file() and output.stat().st_size > 0:
            return {"ok": True, "output": str(output), "skipped": True}
        duration = probe_duration(payload["video"])
        audio = has_audio(payload["video"])
        settings = Settings(**payload["settings"])
        placed = Layout(**payload["layout"])
        output.parent.mkdir(parents=True, exist_ok=True)
        args = render_args(
            Path(payload["video"]),
            Path(payload["background"]),
            Path(payload["frame"]),
            Path(payload["noise"]),
            partial,
            duration,
            audio,
            settings,
            placed,
        )
        run_command(args, log_path, cancel)
        partial.replace(output)
        return {"ok": True, "output": str(output), "skipped": False, "cancelled": False}
    except Exception as exc:
        partial.unlink(missing_ok=True)
        log_path.parent.mkdir(parents=True, exist_ok=True)
        with log_path.open("a", encoding="utf-8") as handle:
            handle.write("\n" + str(exc) + "\n")
        cancelled = (cancel is not None and cancel.is_set()) or str(exc) == "Đã hủy."
        return {
            "ok": False,
            "output": str(output),
            "error": str(exc),
            "skipped": False,
            "cancelled": cancelled,
        }


def default_workers() -> int:
    cpus = os.cpu_count() or 2
    return max(1, cpus // 2)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Render hàng loạt video trong khung cố định.")
    parser.add_argument("--videos", required=True, type=Path)
    parser.add_argument("--frames", required=True, type=Path)
    parser.add_argument("--backgrounds", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--random", action="store_true")
    parser.add_argument("--gpu", action="store_true")
    parser.add_argument("--workers", type=int, default=default_workers())
    parser.add_argument("--rebuild-cache", action="store_true")
    parser.add_argument("--video-scale", type=float, default=VIDEO_SCALE)
    parser.add_argument("--video-opacity", type=float, default=VIDEO_OPACITY)
    parser.add_argument("--noise-opacity", type=float, default=NOISE_OPACITY)
    parser.add_argument("--noise-speed", type=float, default=NOISE_SPEED)
    parser.add_argument("--noise-seconds", type=float, default=NOISE_SECONDS)
    parser.add_argument("--crf", type=int, default=CRF)
    parser.add_argument("--preset", default=PRESET)
    parser.add_argument("--width", type=int, default=W)
    parser.add_argument("--height", type=int, default=H)
    parser.add_argument("--fps", type=int, default=FPS)
    parser.add_argument("--cache", type=Path, default=ROOT / "cache")
    return parser.parse_args(argv)


def settings_from(args: argparse.Namespace, workers: int) -> Settings:
    cpus = os.cpu_count() or 2
    threads = max(1, cpus // workers)
    return Settings(
        width=args.width,
        height=args.height,
        fps=args.fps,
        video_scale=args.video_scale,
        video_opacity=args.video_opacity,
        noise_opacity=args.noise_opacity,
        noise_speed=args.noise_speed,
        noise_seconds=args.noise_seconds,
        crf=args.crf,
        preset=args.preset,
        gpu=args.gpu,
        workers=workers,
        threads=threads,
    )


def draw_progress(done: int, total: int, ok: int, failed: int) -> None:
    width = 28
    filled = 0 if total == 0 else int(width * done / total)
    bar = "#" * filled + "-" * (width - filled)
    print(f"\r[{bar}] {done}/{total}  xong={ok} lỗi={failed}", end="", flush=True)


def configure_stdio() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is None:
            continue
        try:
            reconfigure(encoding="utf-8", errors="replace")
        except (OSError, ValueError):
            pass


def main(argv: list[str] | None = None) -> int:
    configure_stdio()
    args = parse_args(sys.argv[1:] if argv is None else argv)
    if args.workers < 1:
        raise SystemExit("--workers phải từ 1.")
    if not 0 < args.video_scale <= 1:
        raise SystemExit("--video-scale phải trong khoảng (0, 1].")
    if not 0 <= args.video_opacity <= 1 or not 0 <= args.noise_opacity <= 1:
        raise SystemExit("Opacity phải từ 0 đến 1.")
    if not 0 < args.noise_speed <= 1:
        raise SystemExit("--noise-speed phải trong khoảng (0, 1].")

    workers = args.workers
    use_gpu = args.gpu
    if use_gpu and not nvenc_available():
        print("Không dùng được h264_nvenc. Render bằng libx264.")
        use_gpu = False
    if use_gpu and workers > NVENC_MAX_WORKERS:
        print(f"NVENC giới hạn {NVENC_MAX_WORKERS} phiên. Hạ workers {workers} -> {NVENC_MAX_WORKERS}.")
        workers = NVENC_MAX_WORKERS
    args.gpu = use_gpu

    videos = list_media(args.videos, VIDEO_EXT)
    frames = list_media(args.frames, FRAME_EXT)
    backgrounds = list_media(args.backgrounds, VIDEO_EXT)
    if not videos or not frames or not backgrounds:
        raise SystemExit("Cần ít nhất 1 video, 1 PNG khung và 1 video nền.")

    settings = settings_from(args, workers)
    placed = layout_for(settings)
    center_x, center_y = placed.center
    print(
        f"Video chính {placed.video_w}x{placed.video_h} tại x={placed.x}, y={placed.y} "
        f"(tâm {center_x},{center_y}). Opacity {settings.video_opacity:.2f}. Không dò vùng trống."
    )

    cache = args.cache
    if args.rebuild_cache and cache.exists():
        shutil.rmtree(cache)

    plan = build_plan(videos, frames, backgrounds, "random" if args.random else "round", args.out / "plan.json")
    print(f"Đã chốt {len(plan['items'])} cặp trong {args.out / 'plan.json'}")

    mapping = prepare_cache(frames, backgrounds, cache, settings, workers)
    print("Cache xong. Bắt đầu render.")

    jobs = []
    for item in plan["items"]:
        output = args.out / item["output"]
        jobs.append(
            {
                "video": item["video"],
                "background": mapping[item["background"]],
                "frame": mapping[item["frame"]],
                "noise": mapping["noise"],
                "output": str(output),
                "log": str(args.out / "logs" / (Path(item["output"]).stem + ".log")),
                "settings": settings.__dict__,
                "layout": placed.__dict__,
            }
        )

    ok = 0
    failed = 0
    args.out.mkdir(parents=True, exist_ok=True)
    with ProcessPoolExecutor(max_workers=workers) as pool:
        futures = [pool.submit(render_job, job) for job in jobs]
        done = 0
        for future in as_completed(futures):
            result = future.result()
            done += 1
            if result["ok"]:
                ok += 1
            else:
                failed += 1
                print(f"\nLỗi {result['output']}: {result.get('error', '')[:300]}")
            draw_progress(done, len(jobs), ok, failed)
    print()
    print(f"Xong {ok}, lỗi {failed}. File lỗi nằm trong {args.out / 'logs'}.")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
