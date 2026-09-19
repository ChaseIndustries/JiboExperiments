#!/usr/bin/env python3
"""Copy a wav to the kitchen Jibo and play it through TTSOut."""

from __future__ import annotations

import argparse
import os
import shutil
import socket
import subprocess
import tempfile
import uuid
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Play a wav on Jibo through aplay TTSOut.")
    parser.add_argument("wav", help="Local wav or mp3 path.")
    parser.add_argument("--jibo-ip", required=True, help="Robot LAN IPv4 address.")
    parser.add_argument("--ssh-user", default="root")
    return parser.parse_args()


def require_jibo_ip(value: str) -> str:
    host = value.strip()
    placeholders = {"jibo_ip", "<jibo_ip>", "your_jibo_ip", "robot_ip"}
    if host.lower().replace("-", "_") in placeholders or "<" in host or ">" in host:
        raise SystemExit("Pass the robot LAN address, like --jibo-ip 192.168.4.24.")
    try:
        socket.inet_aton(host)
    except OSError as exc:
        raise SystemExit(f"{host!r} is not an IPv4 address.") from exc
    return host


def resample_for_robot(wav_path: Path) -> Path:
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise SystemExit("ffmpeg is required to resample the clone for Jibo.")
    tmp = Path(tempfile.mkstemp(prefix="jibo-clone-", suffix=".wav")[1])
    result = subprocess.run(
        [
            ffmpeg,
            "-y",
            "-i",
            str(wav_path),
            "-ar",
            "48000",
            "-ac",
            "2",
            "-c:a",
            "pcm_s16le",
            str(tmp),
        ],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        raise SystemExit(f"ffmpeg resample failed:\n{result.stderr}")
    return tmp


def ssh_with_password(args: list[str], password: str, timeout: int) -> None:
    expect = shutil.which("expect")
    if expect is None:
        raise SystemExit("expect is required for robot SSH. It ships with macOS.")
    quoted = " ".join(subprocess.list2cmdline([arg]) for arg in args)
    script = f"""
set timeout {timeout}
spawn {quoted}
expect {{
  "password:" {{ send -- "{password}\\r" }}
  timeout {{ puts "SSH timed out"; exit 1 }}
  eof {{ exit 0 }}
}}
expect eof
catch wait result
exit [lindex $result 3]
"""
    result = subprocess.run([expect, "-c", script], check=False)
    if result.returncode != 0:
        raise SystemExit(f"Robot SSH failed: {' '.join(args[:2])}")


def play_on_jibo(wav_path: Path, jibo_ip: str, ssh_user: str) -> None:
    password = os.environ.get("JIBO_SSH_PASSWORD", "jibo")
    target = f"{ssh_user}@{jibo_ip}"
    remote = f"/tmp/jibo-clone-{uuid.uuid4().hex}.wav"
    ssh_opts = [
        "-o",
        "StrictHostKeyChecking=accept-new",
        "-o",
        "PreferredAuthentications=password",
        "-o",
        "PubkeyAuthentication=no",
    ]
    print(f"Copying clone to {target}:{remote}")
    prepared = resample_for_robot(wav_path)
    try:
        ssh_with_password(
            ["scp", *ssh_opts, str(prepared), f"{target}:{remote}"],
            password,
            timeout=40,
        )
        print("Playing through TTSOut. That is the mixer Griffin uses.")
        ssh_with_password(
            ["ssh", *ssh_opts, target, f"aplay -D TTSOut {remote}"],
            password,
            timeout=60,
        )
    finally:
        prepared.unlink(missing_ok=True)


def main() -> None:
    args = parse_args()
    wav = Path(args.wav).expanduser().resolve()
    if not wav.exists():
        raise SystemExit(f"Missing wav: {wav}")
    play_on_jibo(wav, require_jibo_ip(args.jibo_ip), args.ssh_user)


if __name__ == "__main__":
    main()
