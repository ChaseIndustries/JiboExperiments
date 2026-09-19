#!/usr/bin/env python3
"""Keep Chatterbox loaded and serve wavs for the OpenJibo cloud clone path."""

from __future__ import annotations

import argparse
import io
import json
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

import numpy as np
import soundfile as sf
from mlx_audio.tts.utils import load_model

HERE = Path(__file__).resolve().parent
DEFAULT_REF = HERE / "compare" / "ref-melissa.wav"
DEFAULT_REF_TEXT = (
    "Melissa just sent a reminder that she's picking you up in half an hour "
    "to go grocery shopping."
)
STATE: dict = {}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Serve the isolated Jibo clone over HTTP.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8091)
    parser.add_argument("--ref", default=str(DEFAULT_REF))
    parser.add_argument("--exaggeration", type=float, default=0.55)
    parser.add_argument("--cfg-weight", type=float, default=0.5)
    parser.add_argument("--temperature", type=float, default=0.8)
    parser.add_argument("--model", default="mlx-community/chatterbox-fp16")
    return parser.parse_args()


def resample(audio: np.ndarray, src_sr: int, dst_sr: int) -> np.ndarray:
    if src_sr == dst_sr:
        return audio
    n = int(round(len(audio) * dst_sr / src_sr))
    t_old = np.linspace(0, 1, len(audio), endpoint=False)
    t_new = np.linspace(0, 1, n, endpoint=False)
    return np.interp(t_new, t_old, audio).astype(np.float32)


def synthesize(text: str) -> bytes:
    kwargs = {
        "text": text,
        "ref_audio": STATE["ref"],
        "ref_text": STATE["ref_text"],
        "exaggeration": STATE["exaggeration"],
        "cfg_weight": STATE["cfg_weight"],
        "temperature": STATE["temperature"],
        "verbose": False,
    }
    chunks: list[np.ndarray] = []
    sample_rate = 24000
    for result in STATE["model"].generate(**kwargs):
        chunks.append(np.array(result.audio, dtype=np.float32).reshape(-1))
        sample_rate = int(result.sample_rate)
    if not chunks:
        raise RuntimeError("Clone model returned no audio.")
    audio = resample(np.concatenate(chunks), sample_rate, 48000)
    peak = float(np.max(np.abs(audio)) or 1)
    audio = (0.89 * audio / peak).astype(np.float32)
    buf = io.BytesIO()
    sf.write(buf, audio, 48000, format="WAV")
    return buf.getvalue()


class Handler(BaseHTTPRequestHandler):
    def do_GET(self) -> None:  # noqa: N802
        if self.path.rstrip("/") == "/health":
            self.respond(200, b'{"ok":true}', "application/json")
            return
        self.respond(404, b"not found", "text/plain")

    def do_POST(self) -> None:  # noqa: N802
        if self.path.rstrip("/") != "/speak":
            self.respond(404, b"not found", "text/plain")
            return
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length) if length else b"{}"
        try:
            payload = json.loads(raw.decode("utf-8") or "{}")
            text = str(payload.get("text") or "").strip()
        except (json.JSONDecodeError, UnicodeDecodeError):
            self.respond(400, b"invalid json", "text/plain")
            return
        if not text:
            self.respond(400, b"text is required", "text/plain")
            return
        try:
            wav = synthesize(text)
        except Exception as exc:  # noqa: BLE001
            self.respond(500, str(exc).encode("utf-8"), "text/plain")
            return
        self.respond(200, wav, "audio/wav")

    def log_message(self, fmt: str, *args: object) -> None:
        print("%s - %s" % (self.address_string(), fmt % args))

    def respond(self, status: int, body: bytes, content_type: str) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main() -> None:
    args = parse_args()
    ref = Path(args.ref).expanduser().resolve()
    if not ref.exists():
        raise SystemExit(f"Missing reference wav: {ref}")
    print(f"Loading {args.model}...")
    STATE["model"] = load_model(args.model)
    STATE["ref"] = str(ref)
    STATE["ref_text"] = DEFAULT_REF_TEXT
    STATE["exaggeration"] = args.exaggeration
    STATE["cfg_weight"] = args.cfg_weight
    STATE["temperature"] = args.temperature
    print(f"Clone TTS listening on http://{args.host}:{args.port}")
    # HTTPServer keeps generate() on the thread that loaded MLX.
    # ThreadingHTTPServer hands requests to workers with no Metal stream.
    server = HTTPServer((args.host, args.port), Handler)
    server.serve_forever()


if __name__ == "__main__":
    main()
