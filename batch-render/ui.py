"""Queue window for the fixed-frame batch renderer."""

from __future__ import annotations

import hashlib
import json
import queue
import threading
import time
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

import render

WAIT = "Chờ"
RUNNING = "Đang chạy"
DONE = "Xong"
FAILED = "Lỗi"
SKIPPED = "Bỏ qua"
CANCELLED = "Đã hủy"
QUEUE_FILE = render.ROOT / "queue.json"

BG = "#1e1e1e"
FG = "#f2f2f2"
SURFACE = "#2d2d30"
ACCENT = "#007acc"
DANGER = "#c42b1c"


def normalize_jobs(jobs: list[dict]) -> list[dict]:
    for job in jobs:
        if job.get("status") in (RUNNING, "Tạm dừng"):
            job["status"] = WAIT
            job["detail"] = "Mở lại nên đưa về chờ."
    return jobs


def load_queue(path: Path = QUEUE_FILE) -> dict:
    if not path.is_file():
        return {"jobs": []}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {"jobs": []}
    data["jobs"] = normalize_jobs(list(data.get("jobs") or []))
    return data


def save_queue(data: dict, path: Path = QUEUE_FILE) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(".json.tmp")
    temp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    temp.replace(path)


def job_id(video: str) -> str:
    return hashlib.sha256(video.encode("utf-8")).hexdigest()[:16]


def new_jobs(existing: list[dict], items: list[dict]) -> list[dict]:
    known = {job["video"] for job in existing}
    added = []
    for item in items:
        if item["video"] in known:
            continue
        output = Path(item["output"])
        status = SKIPPED if output.is_file() and output.stat().st_size > 0 else WAIT
        added.append(
            {
                "id": job_id(item["video"]),
                "video": item["video"],
                "frame": item["frame"],
                "background": item["background"],
                "output": str(output),
                "status": status,
                "detail": "File đã có." if status == SKIPPED else "",
            }
        )
        known.add(item["video"])
    return existing + added


class App:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Render khung — hàng đợi")
        self.root.geometry("1180x760")
        self.root.minsize(980, 640)
        self.root.configure(bg=BG)
        self.lock = threading.Lock()
        self.events: queue.Queue = queue.Queue()
        self.cancel = threading.Event()
        self.mode = "idle"
        self.generation = 0
        self.active = 0
        self.worker_limit = 1
        self.run_settings: render.Settings | None = None
        self.run_out = ""
        self.jobs: list[dict] = []
        self.started_at = 0.0
        self.mapping: dict[str, str] = {}
        self._build()
        self._load()
        threading.Thread(target=self._loop, daemon=True).start()
        self.root.after(200, self._poll)
        self.root.protocol("WM_DELETE_WINDOW", self._close)

    def _build(self) -> None:
        style = ttk.Style()
        style.theme_use("clam")
        style.configure(".", background=BG, foreground=FG, fieldbackground=SURFACE)
        style.configure("TLabel", background=BG, foreground=FG)
        style.configure("TButton", background=ACCENT, foreground=FG, padding=6)
        style.configure("Danger.TButton", background=DANGER, foreground=FG)
        style.configure("TEntry", fieldbackground=SURFACE, foreground=FG)
        style.configure("TCheckbutton", background=BG, foreground=FG)
        style.configure("Treeview", background=SURFACE, foreground=FG, fieldbackground=SURFACE, rowheight=24)
        style.configure("Treeview.Heading", background="#3e3e42", foreground=FG)
        style.configure("Horizontal.TProgressbar", background=ACCENT, troughcolor="#3e3e42")

        pad = {"padx": 8, "pady": 4}
        form = tk.Frame(self.root, bg=BG)
        form.pack(fill="x", padx=12, pady=8)
        self.videos = self._folder_row(form, 0, "Video chính", "videos")
        self.frames = self._folder_row(form, 1, "Khung PNG", "frames")
        self.backgrounds = self._folder_row(form, 2, "Video nền", "backgrounds")
        self.out = self._folder_row(form, 3, "Thư mục xuất", "output")

        options = tk.Frame(self.root, bg=BG)
        options.pack(fill="x", padx=12)
        self.scale = self._number(options, "Scale", "0.80")
        self.opacity = self._number(options, "Opacity video", "0.90")
        self.noise = self._number(options, "Opacity nhiễu", "0.30")
        self.crf = self._number(options, "CRF", "20")
        self.workers = self._number(options, "Số video cùng lúc", str(render.default_workers()))
        self.gpu = tk.BooleanVar(value=False)
        self.random_pair = tk.BooleanVar(value=False)
        ttk.Checkbutton(options, text="GPU (NVENC)", variable=self.gpu).pack(side="left", padx=8)
        ttk.Checkbutton(options, text="Chọn ngẫu nhiên", variable=self.random_pair).pack(side="left", padx=8)

        buttons = tk.Frame(self.root, bg=BG)
        buttons.pack(fill="x", padx=12, pady=8)
        ttk.Button(buttons, text="Thêm vào hàng đợi", command=self.enqueue).pack(side="left", padx=4)
        ttk.Button(buttons, text="Bắt đầu", command=self.start).pack(side="left", padx=4)
        ttk.Button(buttons, text="Tạm dừng", command=self.pause).pack(side="left", padx=4)
        ttk.Button(buttons, text="Tiếp tục", command=self.resume).pack(side="left", padx=4)
        ttk.Button(buttons, text="Hủy", command=self.cancel_run, style="Danger.TButton").pack(side="left", padx=4)
        ttk.Button(buttons, text="Render tiếp", command=self.render_again).pack(side="left", padx=4)
        ttk.Button(buttons, text="Xóa hàng đợi", command=self.clear_queue).pack(side="left", padx=4)
        ttk.Button(buttons, text="Copy lỗi", command=self.copy_detail).pack(side="left", padx=4)

        self.progress = ttk.Progressbar(self.root, mode="determinate", maximum=100)
        self.progress.pack(fill="x", padx=16, pady=4)
        self.status = tk.StringVar(value="Thêm video vào hàng đợi rồi bấm Bắt đầu.")
        ttk.Label(self.root, textvariable=self.status).pack(anchor="w", padx=16)

        table = tk.Frame(self.root, bg=BG)
        table.pack(fill="both", expand=True, padx=12, pady=8)
        columns = ("video", "frame", "background", "output", "status")
        self.tree = ttk.Treeview(table, columns=columns, show="headings", selectmode="browse")
        headers = {
            "video": "Video",
            "frame": "Khung",
            "background": "Nền",
            "output": "File xuất",
            "status": "Trạng thái",
        }
        widths = {"video": 180, "frame": 140, "background": 140, "output": 280, "status": 110}
        for key in columns:
            self.tree.heading(key, text=headers[key])
            self.tree.column(key, width=widths[key], anchor="w")
        scroll = ttk.Scrollbar(table, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        self.tree.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")
        self.tree.bind("<<TreeviewSelect>>", lambda _event: self._show_selected())

        bottom = tk.Frame(self.root, bg=BG)
        bottom.pack(fill="both", expand=False, padx=12, pady=8)
        left = tk.Frame(bottom, bg=BG)
        right = tk.Frame(bottom, bg=BG)
        left.pack(side="left", fill="both", expand=True, padx=(0, 6))
        right.pack(side="left", fill="both", expand=True, padx=(6, 0))
        ttk.Label(left, text="Chi tiết / lỗi").pack(anchor="w")
        self.detail = tk.Text(left, height=8, bg=SURFACE, fg=FG, insertbackground=FG, wrap="word")
        self.detail.pack(fill="both", expand=True)
        ttk.Label(right, text="Nhật ký").pack(anchor="w")
        self.logbox = tk.Listbox(right, height=8, bg=SURFACE, fg=FG, highlightthickness=0)
        self.logbox.pack(fill="both", expand=True)

    def _folder_row(self, parent: tk.Frame, row: int, label: str, default: str) -> tk.StringVar:
        ttk.Label(parent, text=label, width=16).grid(row=row, column=0, sticky="w", pady=3)
        var = tk.StringVar(value=str(render.ROOT / default))
        entry = ttk.Entry(parent, textvariable=var, width=90)
        entry.grid(row=row, column=1, sticky="ew", padx=6)
        ttk.Button(parent, text="Duyệt", command=lambda: self._browse(var)).grid(row=row, column=2)
        parent.columnconfigure(1, weight=1)
        return var

    def _number(self, parent: tk.Frame, label: str, value: str) -> tk.StringVar:
        ttk.Label(parent, text=label).pack(side="left", padx=(0, 4))
        var = tk.StringVar(value=value)
        ttk.Entry(parent, textvariable=var, width=8).pack(side="left", padx=(0, 12))
        return var

    def _browse(self, var: tk.StringVar) -> None:
        picked = filedialog.askdirectory(initialdir=var.get() or str(render.ROOT))
        if picked:
            var.set(picked)

    def _load(self) -> None:
        data = load_queue()
        for key, var in (
            ("videos", self.videos),
            ("frames", self.frames),
            ("backgrounds", self.backgrounds),
            ("out", self.out),
        ):
            if data.get(key):
                var.set(data[key])
        for key, var in (
            ("scale", self.scale),
            ("opacity", self.opacity),
            ("noise", self.noise),
            ("crf", self.crf),
            ("workers", self.workers),
        ):
            if data.get(key):
                var.set(str(data[key]))
        self.gpu.set(bool(data.get("gpu")))
        self.random_pair.set(bool(data.get("random")))
        self.jobs = data["jobs"]
        self._redraw()
        self._log(f"Hàng đợi: {len(self.jobs)} video.")

    def _snapshot(self) -> dict:
        return {
            "videos": self.videos.get(),
            "frames": self.frames.get(),
            "backgrounds": self.backgrounds.get(),
            "out": self.out.get(),
            "scale": self.scale.get(),
            "opacity": self.opacity.get(),
            "noise": self.noise.get(),
            "crf": self.crf.get(),
            "workers": self.workers.get(),
            "gpu": self.gpu.get(),
            "random": self.random_pair.get(),
            "jobs": self.jobs,
        }

    def _persist(self) -> None:
        try:
            save_queue(self._snapshot())
        except OSError as exc:
            self._log(str(exc))

    def _log(self, text: str) -> None:
        self.logbox.insert(0, text)
        if self.logbox.size() > 40:
            self.logbox.delete(40, "end")

    def _redraw(self) -> None:
        self.tree.delete(*self.tree.get_children())
        done = 0
        for job in self.jobs:
            self.tree.insert("", "end", iid=job["id"], values=self._values(job))
            if job["status"] in (DONE, SKIPPED):
                done += 1
        total = len(self.jobs)
        self.progress["value"] = 0 if total == 0 else 100 * done / total
        running = sum(1 for job in self.jobs if job["status"] == RUNNING)
        failed = sum(1 for job in self.jobs if job["status"] == FAILED)
        elapsed = ""
        if self.started_at and self.mode == "run":
            seconds = int(time.time() - self.started_at)
            elapsed = f"  Thời gian: {seconds // 60:02d}:{seconds % 60:02d}"
        encoder = "NVENC" if self.gpu.get() else "libx264"
        self.status.set(f"{encoder}  Xong {done}/{total}  Đang chạy {running}  Lỗi {failed}{elapsed}")

    def _values(self, job: dict) -> tuple[str, str, str, str, str]:
        return (
            Path(job["video"]).name,
            Path(job["frame"]).name,
            Path(job["background"]).name,
            Path(job["output"]).name,
            job["status"],
        )

    def _settings(self) -> render.Settings:
        workers = max(1, int(float(self.workers.get())))
        cpus = render.os.cpu_count() or 2
        use_gpu = self.gpu.get()
        if use_gpu and workers > render.NVENC_MAX_WORKERS:
            workers = render.NVENC_MAX_WORKERS
            self.workers.set(str(workers))
            self._log(f"NVENC tối đa {render.NVENC_MAX_WORKERS} video cùng lúc.")
        return render.Settings(
            video_scale=float(self.scale.get()),
            video_opacity=float(self.opacity.get()),
            noise_opacity=float(self.noise.get()),
            crf=int(float(self.crf.get())),
            gpu=use_gpu,
            workers=workers,
            threads=max(1, cpus // workers),
        )

    def enqueue(self) -> None:
        try:
            videos = render.list_media(Path(self.videos.get()), render.VIDEO_EXT)
            frames = render.list_media(Path(self.frames.get()), render.FRAME_EXT)
            backgrounds = render.list_media(Path(self.backgrounds.get()), render.VIDEO_EXT)
        except SystemExit as exc:
            messagebox.showerror("Hàng đợi", str(exc))
            return
        if not videos or not frames or not backgrounds:
            messagebox.showerror("Hàng đợi", "Cần ít nhất 1 video, 1 PNG khung và 1 video nền.")
            return
        out = Path(self.out.get())
        out.mkdir(parents=True, exist_ok=True)
        plan = render.build_plan(
            videos,
            frames,
            backgrounds,
            "random" if self.random_pair.get() else "round",
            out / "plan.json",
        )
        before = len(self.jobs)
        items = []
        for item in plan["items"]:
            row = dict(item)
            row["output"] = str(out / item["output"])
            items.append(row)
        self.jobs = new_jobs(self.jobs, items)
        self._redraw()
        self._persist()
        self._log(f"Thêm {len(self.jobs) - before} video. Đã chốt cặp trong plan.json.")

    def start(self) -> None:
        if not any(job["status"] == WAIT for job in self.jobs):
            self.enqueue()
        if not any(job["status"] == WAIT for job in self.jobs):
            self._log("Không còn video đang chờ.")
            return
        try:
            settings = self._settings()
        except ValueError:
            messagebox.showerror("Hàng đợi", "Scale, opacity, CRF hoặc số video không hợp lệ.")
            return
        if settings.gpu and not render.nvenc_available():
            self.gpu.set(False)
            self._log("Không dùng được NVENC. Chuyển sang libx264.")
            settings = self._settings()
        self.cancel.clear()
        self.generation += 1
        self.started_at = time.time()
        self.mode = "cache"
        self.run_settings = settings
        self.run_out = self.out.get()
        self.worker_limit = settings.workers
        self._log("Đang làm cache, xong mới render.")
        threading.Thread(
            target=self._cache_then_run,
            args=(settings, self.generation, self.frames.get(), self.backgrounds.get()),
            daemon=True,
        ).start()

    def _cache_then_run(self, settings: render.Settings, generation: int, frames_dir: str, backgrounds_dir: str) -> None:
        try:
            frames = render.list_media(Path(frames_dir), render.FRAME_EXT)
            backgrounds = render.list_media(Path(backgrounds_dir), render.VIDEO_EXT)
            mapping = render.prepare_cache(
                frames,
                backgrounds,
                render.ROOT / "cache",
                settings,
                settings.workers,
                self.cancel,
            )
        except Exception as exc:
            self.events.put(("log", str(exc)[:500]))
            self.events.put(("mode", "idle"))
            return
        if generation != self.generation or self.cancel.is_set():
            self.events.put(("mode", "idle"))
            return
        self.mapping = mapping
        self.events.put(("log", "Cache xong. Bắt đầu hàng đợi."))
        self.events.put(("mode", "run"))

    def pause(self) -> None:
        if self.mode == "run":
            self.mode = "pause"
            self._log("Tạm dừng. Video đang chạy sẽ xong, không lấy video mới.")

    def resume(self) -> None:
        if self.mode == "pause":
            self.mode = "run"
            self._log("Tiếp tục hàng đợi.")

    def cancel_run(self) -> None:
        self.cancel.set()
        self.mode = "idle"
        self.generation += 1
        with self.lock:
            for job in self.jobs:
                if job["status"] == RUNNING:
                    job["status"] = CANCELLED
                    job["detail"] = "Đã hủy."
        self._redraw()
        self._persist()
        self._log("Đã hủy các video đang chạy.")

    def render_again(self) -> None:
        changed = 0
        for job in self.jobs:
            if job["status"] in (FAILED, CANCELLED):
                job["status"] = WAIT
                job["detail"] = ""
                changed += 1
        self._redraw()
        self._persist()
        self._log(f"Đưa {changed} video lỗi hoặc đã hủy về chờ.")
        if changed:
            self.start()

    def clear_queue(self) -> None:
        if not messagebox.askyesno("Xóa hàng đợi", "Xóa hết video trên hàng đợi?"):
            return
        self.cancel_run()
        self.jobs = []
        self._redraw()
        self._persist()
        self._log("Đã xóa hàng đợi.")

    def copy_detail(self) -> None:
        text = self.detail.get("1.0", "end").strip()
        if not text:
            return
        self.root.clipboard_clear()
        self.root.clipboard_append(text)

    def _loop(self) -> None:
        while True:
            time.sleep(0.2)
            if self.mode != "run":
                continue
            with self.lock:
                if self.active >= self.worker_limit:
                    continue
                job = next((item for item in self.jobs if item["status"] == WAIT), None)
                if job is None:
                    if self.active == 0:
                        self.mode = "idle"
                        self.events.put(("log", "Hàng đợi đã chạy xong."))
                        self.events.put(("refresh", None))
                    continue
                job["status"] = RUNNING
                self.active += 1
                generation = self.generation
                self.events.put(("refresh", None))
            threading.Thread(target=self._work, args=(job, generation), daemon=True).start()

    def _work(self, job: dict, generation: int) -> None:
        try:
            settings = self.run_settings or render.Settings()
            placed = render.layout_for(settings)
            payload = {
                "video": job["video"],
                "background": self.mapping.get(job["background"], job["background"]),
                "frame": self.mapping.get(job["frame"], job["frame"]),
                "noise": self.mapping.get("noise", ""),
                "output": job["output"],
                "log": str(Path(self.run_out) / "logs" / (Path(job["output"]).stem + ".log")),
                "settings": settings.__dict__,
                "layout": {"video_w": placed.video_w, "video_h": placed.video_h, "x": placed.x, "y": placed.y},
                "cancel": self.cancel,
            }
            result = render.render_job(payload)
        except Exception as exc:
            result = {"ok": False, "skipped": False, "cancelled": False, "error": str(exc)}
        with self.lock:
            self.active = max(0, self.active - 1)
            if generation == self.generation:
                current = next((item for item in self.jobs if item["id"] == job["id"]), None)
                if current is not None and current["status"] == RUNNING:
                    if result.get("skipped"):
                        current["status"] = SKIPPED
                        current["detail"] = "File đã có."
                    elif result.get("ok"):
                        current["status"] = DONE
                        current["detail"] = ""
                    elif result.get("cancelled"):
                        current["status"] = CANCELLED
                        current["detail"] = "Đã hủy."
                    else:
                        current["status"] = FAILED
                        current["detail"] = str(result.get("error", ""))[:2000]
        self.events.put(("refresh", None))
        self.events.put(("save", None))

    def _show_selected(self) -> None:
        selected = self.tree.selection()
        if not selected:
            return
        job = next((item for item in self.jobs if item["id"] == selected[0]), None)
        if job is None:
            return
        log_path = Path(self.out.get()) / "logs" / (Path(job["output"]).stem + ".log")
        log_text = ""
        if log_path.is_file():
            log_text = log_path.read_text(encoding="utf-8", errors="replace")[-4000:]
        body = "\n".join(
            part
            for part in (
                job["status"],
                job["video"],
                job["output"],
                job.get("detail") or "",
                log_text,
            )
            if part
        )
        self.detail.delete("1.0", "end")
        self.detail.insert("1.0", body)

    def _poll(self) -> None:
        try:
            while True:
                kind, payload = self.events.get_nowait()
                if kind == "log":
                    self._log(payload)
                elif kind == "mode":
                    self.mode = payload
                elif kind == "refresh":
                    self._redraw()
                elif kind == "save":
                    self._persist()
        except queue.Empty:
            pass
        if self.mode == "run":
            self._redraw()
        self.root.after(200, self._poll)

    def _close(self) -> None:
        self.cancel.set()
        self.mode = "idle"
        self.generation += 1
        for job in self.jobs:
            if job["status"] == RUNNING:
                job["status"] = WAIT
                job["detail"] = "Đóng cửa sổ nên đưa về chờ."
        self._persist()
        self.root.destroy()


def main() -> None:
    root = tk.Tk()
    App(root)
    root.mainloop()


if __name__ == "__main__":
    main()
