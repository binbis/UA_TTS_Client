#!/usr/bin/env bash
# Launch the TTS server from the source tree (venv).
# server.py resolves voices/ and dictionary.json next to itself, so the
# working directory no longer matters; cd is kept for historical reasons.
set -euo pipefail
cd "$(dirname "$0")"
exec venv/bin/python server.py
