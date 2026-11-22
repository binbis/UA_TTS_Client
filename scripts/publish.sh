#!/usr/bin/env bash
# Publishes the desktop client (self-contained) for Linux and Windows and
# assembles the final distribution layout:
#
#   publish/linux/                 publish/win/
#   ├── UA_TTS_Client              ├── UA_TTS_Client.exe
#   └── Server/ (optional)         ├── bass.dll (optional, see below)
#                                  └── Server/ (optional)
#
# The Server/ folder is the PyInstaller package (Server_py/dist/UA_TTS_Server
# or the CI zip, unpacked). Pass it via the environment:
#   SERVER_DIR_LINUX=/path/to/UA_TTS_Server SERVER_DIR_WIN=/path/to/UA_TTS_Server scripts/publish.sh
#
# bass.dll (BASS 2.4 x64, from https://www.un4seen.com/files/bass24.zip,
# the x64/bass.dll entry) is not committed to git. Place it at tools/bass.dll
# or pass BASS_DLL=/path/to/bass.dll.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet publish UA_TTS_Client.csproj -c Release -r linux-x64 --self-contained -o publish/linux
dotnet publish UA_TTS_Client.csproj -c Release -r win-x64 --self-contained -o publish/win

BASS_DLL="${BASS_DLL:-tools/bass.dll}"
if [ -f "$BASS_DLL" ]; then
    cp "$BASS_DLL" publish/win/bass.dll
    echo "bass.dll copied from $BASS_DLL"
else
    echo "WARNING: bass.dll not found ($BASS_DLL); publish/win has no audio library."
    echo "Download bass24.zip from un4seen.com and take x64/bass.dll into tools/bass.dll."
fi

if [ -n "${SERVER_DIR_LINUX:-}" ] && [ -d "$SERVER_DIR_LINUX" ]; then
    cp -r "$SERVER_DIR_LINUX" publish/linux/Server
    echo "Server (Linux) copied from $SERVER_DIR_LINUX"
fi
if [ -n "${SERVER_DIR_WIN:-}" ] && [ -d "$SERVER_DIR_WIN" ]; then
    cp -r "$SERVER_DIR_WIN" publish/win/Server
    echo "Server (Windows) copied from $SERVER_DIR_WIN"
fi

echo
echo "Done. Layout:"
echo "  publish/linux/  (client + optional Server/)"
echo "  publish/win/    (client + bass.dll + optional Server/)"
echo "Server zips come from GitHub Actions (workflow build-server.yml)."
