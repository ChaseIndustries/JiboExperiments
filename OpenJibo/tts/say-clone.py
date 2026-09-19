#!/usr/bin/env python3
"""Generate a local Jibo clone line, then optionally try it on the robot."""

from __future__ import annotations

import argparse
import os
import shutil
import socket
import subprocess
import tempfile
from pathlib import Path

from mlx_audio.tts.generate import generate_audio
from mlx_audio.tts.utils import load_model

HERE = Path(__file__).resolve().parent
COMPARE = HERE / "compare"
DEFAULT_REFS = {
    "melissa": COMPARE / "ref-melissa.wav",
    "mix": COMPARE / "ref-isolated.wav",
    "peekaboo": COMPARE / "jibo-peekaboo.wav",
    "pigs": COMPARE / "jibo-pigs.wav",
    "ann": COMPARE / "jibo-ann.wav",
    "chinese": COMPARE / "jibo-chinese.wav",
    "welcome": COMPARE / "jibo-welcome.wav",
}
REF_TEXT = {
    "melissa": "Melissa just sent a reminder that she's picking you up in half an hour to go grocery shopping.",
    "mix": "Hi Jibo. Excuse me, Ann? Hey, where'd you go? There you are. Welcome home, Eric. Sure thing. Chinese as usual?",
    "peekaboo": "Hey, where'd you go? There you are.",
    "pigs": "Let me in or else I'll. And I'll. And I'll blow your house in.",
    "ann": "Excuse me, Ann?",
    "chinese": "Sure thing. Chinese as usual?",
    "welcome": "Welcome home, Eric.",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Clone Jibo locally with Chatterbox. Melissa is the default enrollment."
    )
    parser.add_argument("text", nargs="?", help="Words to speak. Or pipe them on stdin.")
    parser.add_argument(
        "--ref",
        default="melissa",
        help="Named clip (melissa, mix, peekaboo, pigs, ann, chinese, welcome) or a wav path.",
    )
    parser.add_argument(
        "--exaggeration",
        type=float,
        default=0.55,
        help="Emotion punch. 0.3 calm. 0.55 default. 0.75 shouty. Range 0 to 1.",
    )
    parser.add_argument(
        "--cfg-weight",
        type=float,
        default=0.5,
        help="Stick-to-the-sample. Higher is closer to Melissa. Lower wanders. Try 0.3 to 0.7.",
    )
    parser.add_argument(
        "--temperature",
        type=float,
        default=0.8,
        help="Reroll luck. Lower is steadier. Higher is weirder. Try 0.6 to 1.0.",
    )
    parser.add_argument(
        "--repetition-penalty",
        type=float,
        default=1.2,
        help="Stops him looping a syllable. Raise it if a take stutters.",
    )
    parser.add_argument(
        "--prefix",
        default="say",
        help="Output file prefix inside --out.",
    )
    parser.add_argument(
        "--out",
        default=str(COMPARE),
        help="Directory for wavs.",
    )
    parser.add_argument(
        "--model",
        default="mlx-community/chatterbox-fp16",
        help="MLX Chatterbox repo. Turbo is faster and flatter.",
    )
    parser.add_argument(
        "--jibo-ip",
        help="Kitchen test. Real robot LAN address, like 192.168.4.24. Copies the wav over SSH and plays it.",
    )
    parser.add_argument(
        "--ssh-user",
        default="root",
        help="SSH user on the robot. Default root.",
    )
    return parser.parse_args()


def resolve_ref(value: str) -> Path:
    named = DEFAULT_REFS.get(value.lower())
    path = named if named is not None else Path(value).expanduser()
    if not path.is_absolute():
        path = (HERE / path).resolve() if not path.exists() else path.resolve()
    if not path.exists():
        raise SystemExit(f"Missing reference wav: {path}")
    return path


def require_jibo_ip(value: str) -> str:
    host = value.strip()
    placeholders = {"jibo_ip", "<jibo_ip>", "your_jibo_ip", "robot_ip"}
    if host.lower().replace("-", "_") in placeholders or "<" in host or ">" in host:
        raise SystemExit(
            "Pass the robot LAN address, like --jibo-ip 192.168.4.24. "
            "JIBO_IP was a placeholder."
        )
    try:
        socket.inet_aton(host)
    except OSError as exc:
        raise SystemExit(
            f"{host!r} is not an IPv4 address. Use the robot LAN IP."
        ) from exc
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
    remote = "/tmp/jibo-clone.wav"
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
            timeout=30,
        )
    finally:
        prepared.unlink(missing_ok=True)


def main() -> None:
    args = parse_args()
    text = args.text
    if not text:
        import sys

        if sys.stdin.isatty():
            raise SystemExit('Pass words: ./scripts/cloud/say-jibo-clone.sh "Hi, I\'m Jibo!"')
        text = sys.stdin.read().strip()
    if not text:
        raise SystemExit("No text to speak.")

    ref = resolve_ref(args.ref)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    print(f"ref            {ref}")
    print(f"exaggeration   {args.exaggeration}")
    print(f"cfg_weight     {args.cfg_weight}")
    print(f"temperature    {args.temperature}")
    print(f"text           {text}")

    model = load_model(args.model)
    generate_kwargs = dict(
        model=model,
        text=text,
        ref_audio=str(ref),
        exaggeration=args.exaggeration,
        cfg_weight=args.cfg_weight,
        temperature=args.temperature,
        repetition_penalty=args.repetition_penalty,
        output_path=str(out),
        file_prefix=args.prefix,
        verbose=True,
    )
    named = args.ref.lower()
    if named in REF_TEXT:
        generate_kwargs["ref_text"] = REF_TEXT[named]
    generate_audio(**generate_kwargs)
    wav = out / f"{args.prefix}_000.wav"
    print(f"wrote {wav}")
    if args.jibo_ip:
        if not wav.exists():
            raise SystemExit(f"Expected {wav} after generate.")
        play_on_jibo(wav, require_jibo_ip(args.jibo_ip), args.ssh_user)


if __name__ == "__main__":
    main()
