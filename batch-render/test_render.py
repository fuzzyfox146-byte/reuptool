import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

import render
import ui


class LayoutTests(unittest.TestCase):
    def test_default_design_is_fixed(self):
        placed = render.layout_for(render.Settings())
        self.assertEqual((placed.video_w, placed.video_h, placed.x, placed.y), (1024, 576, 130, 72))
        self.assertEqual(placed.center, (642, 360))

    def test_scale_stays_16_by_9_and_centered_on_hole(self):
        placed = render.layout_for(render.Settings(video_scale=0.5))
        self.assertEqual(placed.video_w / placed.video_h, 16 / 9)
        self.assertEqual(placed.center, (642, 360))


class PlanTests(unittest.TestCase):
    def test_round_robin_cycles_frames_and_backgrounds(self):
        videos = [Path("v1.mp4"), Path("v2.mp4"), Path("v3.mp4"), Path("v4.mp4")]
        frames = [Path("a.png"), Path("b.png")]
        backgrounds = [Path("x.mp4"), Path("y.mp4"), Path("z.mp4")]
        items = render.assign_pairs(videos, frames, backgrounds, "round", 1, None)
        self.assertEqual([Path(item["frame"]).name for item in items], ["a.png", "b.png", "a.png", "b.png"])
        self.assertEqual(
            [Path(item["background"]).name for item in items],
            ["x.mp4", "y.mp4", "z.mp4", "x.mp4"],
        )

    def test_random_uses_each_frame_before_repeating_and_stays_stable(self):
        videos = [Path(f"v{i}.mp4") for i in range(5)]
        frames = [Path("a.png"), Path("b.png"), Path("c.png")]
        backgrounds = [Path("x.mp4"), Path("y.mp4")]
        first = render.assign_pairs(videos, frames, backgrounds, "random", 7, None)
        second = render.assign_pairs(videos, frames, backgrounds, "random", 7, None)
        names = [Path(item["frame"]).name for item in first]
        self.assertEqual(set(names[:3]), {"a.png", "b.png", "c.png"})
        self.assertEqual(names, [Path(item["frame"]).name for item in second])

    def test_existing_plan_keeps_pairs_when_a_video_is_added(self):
        videos = [Path("v1.mp4"), Path("v2.mp4")]
        frames = [Path("a.png"), Path("b.png")]
        backgrounds = [Path("x.mp4")]
        first = render.assign_pairs(videos, frames, backgrounds, "round", 1, None)
        extra = render.assign_pairs(videos + [Path("v3.mp4")], frames, backgrounds, "round", 1, first)
        self.assertEqual(extra[0]["frame"], first[0]["frame"])
        self.assertEqual(extra[1]["frame"], first[1]["frame"])
        self.assertTrue(extra[2]["frame"].endswith("a.png") or extra[2]["frame"].endswith("b.png"))


class CommandTests(unittest.TestCase):
    def test_background_cache_drops_audio_and_covers(self):
        args = render.background_cache_args(Path("bg.mp4"), Path("out.mp4"), render.Settings())
        self.assertIn("-an", args)
        self.assertIn("scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,fps=25,setsar=1", args)
        self.assertNotIn("-c:a", args)

    def test_render_uses_main_audio_only_and_fixed_layers(self):
        settings = render.Settings()
        placed = render.layout_for(settings)
        args = render.render_args(
            Path("main.mp4"),
            Path("bg.mp4"),
            Path("frame.png"),
            Path("noise.mp4"),
            Path("out.mp4"),
            3.5,
            True,
            settings,
            placed,
        )
        graph = args[args.index("-filter_complex") + 1]
        self.assertIn("colorchannelmixer=aa=0.90", graph)
        self.assertIn("overlay=130:72", graph)
        self.assertIn("blend=all_mode=overlay:all_opacity=0.30", graph)
        self.assertNotIn("amix", graph)
        self.assertEqual(args[args.index("-map") + 1], "[out]")
        self.assertIn("0:a:0", args)
        self.assertIn("libx264", args)
        self.assertNotIn("h264_nvenc", args)

    def test_gpu_encoder(self):
        settings = render.Settings(gpu=True)
        args = render.render_args(
            Path("main.mp4"),
            Path("bg.mp4"),
            Path("frame.png"),
            Path("noise.mp4"),
            Path("out.mp4"),
            1,
            False,
            settings,
            render.layout_for(settings),
        )
        self.assertIn("h264_nvenc", args)
        self.assertIn("-an", args)
        self.assertNotIn("0:a:0", args)

    def test_cache_key_changes_when_the_file_changes(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "a.png"
            path.write_bytes(b"one")
            first = render.fingerprint(path, "frame|1280x720")
            path.write_bytes(b"two-bytes")
            second = render.fingerprint(path, "frame|1280x720")
            self.assertNotEqual(first, second)


class QueueTests(unittest.TestCase):
    def test_reopen_puts_running_jobs_back_to_waiting(self):
        jobs = ui.normalize_jobs([{"status": "Đang chạy", "detail": ""}, {"status": "Xong", "detail": ""}])
        self.assertEqual(jobs[0]["status"], "Chờ")
        self.assertEqual(jobs[1]["status"], "Xong")

    def test_enqueue_skips_videos_already_in_the_queue(self):
        existing = [{"video": r"D:\a.mp4", "status": "Xong"}]
        items = [
            {"video": r"D:\a.mp4", "frame": "f.png", "background": "b.mp4", "output": "out.mp4"},
            {"video": r"D:\b.mp4", "frame": "f.png", "background": "b.mp4", "output": "new.mp4"},
        ]
        merged = ui.new_jobs(existing, items)
        self.assertEqual(len(merged), 2)
        self.assertEqual(merged[1]["video"], r"D:\b.mp4")
        self.assertEqual(merged[1]["status"], "Chờ")

    def test_queue_file_roundtrip(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "queue.json"
            ui.save_queue({"jobs": [{"status": "Đang chạy", "video": "a.mp4"}], "videos": "videos"}, path)
            loaded = ui.load_queue(path)
            self.assertEqual(loaded["jobs"][0]["status"], "Chờ")
            self.assertEqual(json.loads(path.read_text(encoding="utf-8"))["videos"], "videos")


class PipelineTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which("ffmpeg") and shutil.which("ffprobe"), "cần ffmpeg")
    def test_short_clip_is_1280x720_with_main_audio(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            videos = root / "videos"
            frames = root / "frames"
            backgrounds = root / "backgrounds"
            out = root / "output"
            for path in (videos, frames, backgrounds):
                path.mkdir()
            self.make(
                videos / "main.mp4",
                "testsrc=size=640x360:rate=25:duration=1,format=yuv420p",
                audio=True,
            )
            self.make(
                backgrounds / "street.mp4",
                "testsrc2=size=320x180:rate=25:duration=1,format=yuv420p",
                audio=False,
            )
            border = (
                "color=c=black:s=1672x941:d=0.1,format=rgba,"
                "geq=r=180:g=20:b=20:a='if(between(X,190,1486)*between(Y,117,823),0,255)'"
            )
            self.make(frames / "lantern.png", border, audio=False, still=True)

            code = render.main(
                [
                    "--videos",
                    str(videos),
                    "--frames",
                    str(frames),
                    "--backgrounds",
                    str(backgrounds),
                    "--out",
                    str(out),
                    "--cache",
                    str(root / "cache"),
                    "--workers",
                    "1",
                    "--preset",
                    "ultrafast",
                    "--crf",
                    "32",
                    "--noise-seconds",
                    "1",
                ]
            )
            self.assertEqual(code, 0)
            rendered = list(out.glob("*.mp4"))
            self.assertEqual(len(rendered), 1)
            probe = subprocess.run(
                [
                    "ffprobe",
                    "-v",
                    "error",
                    "-show_entries",
                    "stream=codec_type,width,height",
                    "-of",
                    "csv=p=0",
                    str(rendered[0]),
                ],
                capture_output=True,
                text=True,
                check=True,
            )
            text = probe.stdout
            self.assertIn("1280,720", text.replace(" ", ""))
            self.assertIn("audio", text)

    def make(self, path: Path, graph: str, audio: bool, still: bool = False) -> None:
        args = ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", graph]
        if audio:
            args += ["-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-shortest", "-c:a", "aac"]
        else:
            args += ["-an"]
        if still:
            args += ["-frames:v", "1"]
        else:
            args += ["-c:v", "libx264", "-preset", "ultrafast", "-crf", "30", "-pix_fmt", "yuv420p"]
        args.append(str(path))
        subprocess.run(args, check=True)


if __name__ == "__main__":
    unittest.main()
