#!/usr/bin/env bash
# Launch the TTS server with the correct working directory.
# server.py uses relative paths ("voices/*.pt", "dictionary.json"),
# so it cannot be launched from the repository root.
set -euo pipefail
cd "$(dirname "$0")"
exec venv/bin/python server.py
