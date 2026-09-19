#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
venv="$repo_root/tts/.venv/bin/python"
script="$repo_root/tts/serve-clone.py"

if [[ ! -x "$venv" ]]; then
  echo "Missing $venv. The clone venv lives under OpenJibo/tts/.venv." >&2
  exit 1
fi

exec "$venv" "$script" "$@"
