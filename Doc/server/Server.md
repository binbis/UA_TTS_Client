# Server (`Server_py/`)

> **Purpose:** the Python TTS server: components, startup, the synthesis and verbalization pipelines, data files, environment and limits.
> **Audience:** developers, and users who run the server.
> **Source of truth:** `Server_py/server.py`, `Server_py/verbalizer.py`, `Server_py/run_server.sh`, `Server_py/requirements.txt`.
> **Verified:** 2026-11-03 (code reading; the endpoints were checked on the reference machine earlier).

## Components

| File | Role |
|---|---|
| `server.py` | Flask application, all endpoints, StyleTTS2 pipeline, document parsing, audio conversion |
| `verbalizer.py` | `Verbalizer.generate_text`: integers become Ukrainian cardinal words (`num2words`, `lang='uk'`) |
| `voices/*.pt` | voice style files. The voice name is the file name without `.pt` |
| `dictionary.json` | pronunciation replacements, read once at startup |
| `run_server.sh` | Linux launcher: changes into `Server_py/` and runs `venv/bin/python server.py` |
| `requirements.txt` | Python dependencies (see Environment) |

## Startup

1. Choose the device: `cuda` if available, otherwise `cpu`.
2. Load the model `StyleTTS2(hf_path='patriotyk/styletts2_ukrainian_multispeaker')`.
3. Create `Verbalizer`.
4. Load every `voices/*.pt` with `torch.load(map_location=device)`. A voice that fails to load is reported and skipped. If the folder has no `.pt` files, the server looks in the current folder.
5. Create the stress model (`Stressifier`).
6. Load `dictionary.json` and compile its rules.
7. Start Flask on port 5000 (`app.run(port=5000, debug=False)`). The default host is `127.0.0.1`.

On the reference machine the start takes about 45 seconds. The first request is served only after that.

## Synthesis pipeline (`POST /api/synthesize`)

1. **Optional translation** (`translate: true`): `GoogleTranslator(source='auto', target='uk')`. This needs internet access. A failure is logged and the original text is used.
2. **Voice:** the requested voice if it is loaded, otherwise the first loaded voice. If no voice is loaded, the error is `No voices loaded`.
3. **Dictionary and transliteration** (`apply_dictionary_and_translit`):
   - dictionary rules are applied, longest keys first, matched case-insensitively. A key that starts or ends with a letter, digit or underscore matches only whole words;
   - each remaining English word (`[a-zA-Z]+`) is replaced by a Ukrainian transliteration (multi-letter rules first, then letter by letter).
4. **Split into parts** (`split_to_parts`): the text is cut after `.`, `?`, `!`, `:`, `;` or a line break, but only when the current part is longer than 30 characters. Empty parts are dropped.
5. **For each part:**
   - `clean_text`: Unicode NFKC normalization; `+` becomes the combining acute accent (stress mark); dash variants become `-`;
   - stress: `ukrainian_word_stress.Stressifier`;
   - phonemes: `ipa_uk.ipa`. A part without phonemes is skipped;
   - tokens: `model.tokenizer.encode`, **cut to 500 tokens without any warning**;
   - audio: `model(tokens, speed=speed, s_prev=voice_style)`.
6. **Assembly:** the parts are concatenated. If `volume != 1.0`, the samples are multiplied by it. The samples are clipped to [-1, 1] and converted to int16.
7. **Output:** WAV, 24 kHz, mono, 16-bit, returned as `audio/wav`.

Errors: `No audio generated` when no part produces audio. Any other exception is returned as `500 {"error": "<text>"}`.

## Verbalization pipeline (`POST /api/verbalize`)

1. `apply_dictionary_and_translit`, as above.
2. The text is split into sentences after `.`, `?`, `!` or `;` followed by whitespace.
3. A sentence that contains digits goes through `Verbalizer.generate_text`: every standalone integer becomes Ukrainian cardinal words; the rest is unchanged; spaces are normalized.
4. A sentence without digits is returned unchanged.

The verbalizer does **not** handle dates, abbreviations or ordinals. For example, `км` stays `км`. Verified by running `verbalizer.py`:

- input: `У 2026 році ми пройшли 5 км.`
- output: `У дві тисячі двадцять шість році ми пройшли п'ять км.`

## Dictionary (`dictionary.json`)

- A flat JSON object of `"source text": "replacement"` pairs. The committed version has 345 entries.
- The file is read **once, at startup**. After any change, restart the server.
- A JSON syntax error is printed as `[!] Помилка словника` and **no** dictionary rules are applied at all.
- The working copy of this file on the reference machine currently has a syntax error: a trailing comma after the entry `"гвт"`. Remove that comma and restart the server. See `user/Troubleshooting.md`.

Examples of entries: `"Google": "Гугл"`, `"Windows": "Віндовс"`, `"cookie policy": "кукі полісі"`.

## Voices (`voices/`)

- Each `*.pt` file is one voice. The name the clients see is the file name without `.pt`.
- The reference set has 31 files, for example `Вероніка Дорош.pt`, `Денис Денисенко.pt`, `Катерина Потапенко.pt`.
- To add a voice, put the file into `voices/` and restart the server. The voice then appears in `GET /api/voices`.
- All voices are loaded into memory at startup, on the chosen device.

## Other endpoints

- `GET /api/voices`, `GET /api/health`: see `server/API.md`.
- `POST /api/convert`: accepts a WAV file and returns it in the requested format (`pydub` with ffmpeg). The default format is `mp3`.
- `POST /api/parse_file`: extracts text from `.pdf` (pypdf), `.docx` (python-docx), `.odt` (odfpy) or `.txt` (UTF-8, invalid bytes ignored). The client method exists; the UI does not use it yet (see `Roadmap.md`).

## Environment

- Tested with Python 3.14 on the reference machine. The documented minimum is 3.10; other versions are not verified.
- Dependencies: `Server_py/requirements.txt`. Some packages are installed from GitHub (`git+https`), so Git must be available.
- `torch` is not listed in `requirements.txt`. It is installed as a dependency of `styletts2-inference`, which requires `torch` and `torchaudio`. The reference machine has `torch 2.14.1+cu130`. CUDA notes are in `user/Installation.md`.
- `numpy<2` is pinned.
- ffmpeg is provided by `static-ffmpeg`. If the package is installed, `static_ffmpeg.add_paths()` runs at startup. `pydub` uses ffmpeg for conversion.

## Limits and behavior to know

- This is the Flask development server. It has no authentication and is not meant for production or for exposure to the internet.
- CORS: every response carries `Access-Control-Allow-Origin: *`. Keep it: a browser extension uses the same server.
- Request size limit: 500 MB.
- A part is cut at 500 tokens **silently**. The end of a very long part is lost.
- `translate` requires internet access and calls the Google translation service.
- The model is loaded at every start. Expect about 45 seconds before the first request is served.
- Verbalization handles integers only.
- Error bodies are JSON `{"error": "<text>"}`. Some older responses were plain text (for example `No file`); the client accepts both formats.

## Running

- Linux: `./Server_py/run_server.sh` from the repository root, or from `Server_py/`.
- Windows: see `user/Installation.md`.
- The server must run from `Server_py/`. Started from the repository root, it reports no error but loads 0 voices and an empty dictionary.
