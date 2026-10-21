#!/usr/bin/env bash
# Запуск TTS-сервера з правильним робочим каталогом.
# server.py використовує відносні шляхи ("voices/*.pt", "dictionary.json"),
# тому запускати його з кореня репозиторію не можна.
set -euo pipefail
cd "$(dirname "$0")"
exec venv/bin/python server.py
