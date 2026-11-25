# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec for UA TTS Server (onedir).

Build (from Server_py/, with the build venv active):

    pyinstaller packaging/UA_TTS_Server.spec

Result: dist/UA_TTS_Server/ with the executable, _internal/ and — after the
extra copy step (see packaging/README) — voices/ and dictionary.json next to
the executable. The user never needs Python: the interpreter and every
dependency (including the Torch CUDA libraries) are inside _internal/.

Notes:
- onedir, not onefile: a onefile build unpacks gigabytes into a temp folder
  on every start.
- console=True: a double-click shows the console with logs. The desktop
  client starts the same exe with CreateNoWindow, so no window appears then.
- voices/*.pt and dictionary.json are intentionally NOT inside the exe, so
  the user can add voices and edit the dictionary without rebuilding.
- ffmpeg: bundled via imageio-ffmpeg (build-time dependency only). The
  server prefers a bundled ffmpeg next to the executable and does not
  download anything at runtime.
"""

import sys
from pathlib import Path

from PyInstaller.utils.hooks import collect_all

# Server_py/ (this file lives in Server_py/packaging/, so one level up).
PROJECT_ROOT = Path(SPECPATH).resolve().parent
ENTRY_POINT = PROJECT_ROOT / "server.py"

hiddenimports = [
    "flask",
    "pydub",
    "pypdf",
    "docx",
    "odf",
    "odf.opendocument",
    "odf.text",
    "ukrainian_word_stress",
    "ipa_uk",
    "deep_translator",
    "scipy",
    "static_ffmpeg",
    "verbalizer",
    "styletts2_inference.models",
    # Imported at runtime from inside accentor-lite.pt (word_tokenizer.py)
    # via torch.package unpickling; static analysis cannot see it. Locally
    # six used to arrive via spaces -> gradio -> pandas -> dateutil, which
    # is not guaranteed (Python 3.12 resolved differently in CI).
    "six",
]

datas = []
binaries = []

# Packages with data files / native extensions that PyInstaller must copy
# explicitly. torch brings the CUDA libraries; transformers brings tokenizer
# data. These are the usual suspects for "module not found" at runtime.
for package in (
    "transformers",
    "torch",
    "scipy",
    "librosa",
    "sentencepiece",
    "styletts2_inference",
    # ukrainian_word_stress -> ukrainian_accentor loads accentor-lite.pt
    # from the package directory via torch.package at import time.
    "ukrainian_word_stress",
    "ukrainian_accentor",
    "ipa_uk",
    "ukrainian_tn",
):
    try:
        pkg_datas, pkg_binaries, pkg_hidden = collect_all(package)
    except Exception as exc:  # a package may be absent on the build machine
        print(f" [!] collect_all({package}) failed: {exc}")
        continue
    datas += pkg_datas
    binaries += pkg_binaries
    hiddenimports += pkg_hidden

# ffmpeg binary inside the bundle (looked up by server.py at startup as
# "ffmpeg"/"ffmpeg.exe" in the ffmpeg folder).
try:
    import imageio_ffmpeg

    _ffmpeg_exe = Path(imageio_ffmpeg.get_ffmpeg_exe())
    _ffmpeg_name = "ffmpeg.exe" if sys.platform == "win32" else "ffmpeg"
    if _ffmpeg_exe.is_file():
        binaries.append((str(_ffmpeg_exe), "ffmpeg"))
        print(f" [i] ffmpeg bundled from imageio-ffmpeg: {_ffmpeg_exe} -> ffmpeg/{_ffmpeg_name}")
    else:
        print(f" [!] imageio-ffmpeg has no binary: {_ffmpeg_exe}")
except ImportError:
    print(" [!] imageio-ffmpeg is not installed; the bundle will have no ffmpeg.")

a = Analysis(
    [str(ENTRY_POINT)],
    pathex=[str(PROJECT_ROOT)],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
)

pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="UA_TTS_Server",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name="UA_TTS_Server",
)
