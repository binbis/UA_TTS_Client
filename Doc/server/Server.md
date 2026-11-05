# Server (`Server_py/`)

> **Purpose:** the Python TTS server: components, startup, the synthesis and verbalization pipelines, data files, environment and limits.
> **Audience:** developers, and users who run the server.
> **Source of truth:** `Server_py/server.py`, `Server_py/verbalizer.py`, `Server_py/run_server.sh`, `Server_py/requirements.txt`.
> **Verified:** 2026-11-05 (code reading; the endpoints and the synthesis were run on the development machine, CUDA, 31 voices).

## Components

| File | Role |
|---|---|
| `server.py` | Flask application, all endpoints, StyleTTS2 pipeline, document parsing, audio conversion |
| `verbalizer.py` | `verbalize`: numbers, dates, times, units and currency become Ukrainian words (`ukrainian-tn`, preset `tts_friendly`). CPU only |
| `voices/*.pt` | voice style files. The voice name is the file name without `.pt` |
| `dictionary.json` | pronunciation replacements, read once at startup |
| `run_server.sh` | Linux launcher: changes into `Server_py/` and runs `venv/bin/python server.py` |
| `requirements.txt` | Python dependencies (see Environment) |

## Startup

1. Choose the device: `cuda` if available, otherwise `cpu`.
2. Load the model `StyleTTS2(hf_path='patriotyk/styletts2_ukrainian_multispeaker')`.
3. Load every `voices/*.pt` with `torch.load(map_location=device)`. A voice that fails to load is reported and skipped. If the folder has no `.pt` files, the server looks in the current folder.
4. Create the stress model (`Stressifier`).
5. Load `dictionary.json` and compile its rules. The rules whose key contains an English letter are kept in a separate list (see the synthesis pipeline).
6. Start Flask on port 5000 (`app.run(port=5000, debug=False)`). The default host is `127.0.0.1`.

On the reference machine the start takes about 45 seconds. The first request is served only after that.

## Synthesis pipeline (`POST /api/synthesize`)

1. **Optional translation** (`translate: true`): `GoogleTranslator(source='auto', target='uk')`. This needs internet access. A failure is logged and the original text is used.
2. **Voice:** the requested voice if it is loaded, otherwise the first loaded voice. If no voice is loaded, the error is `No voices loaded`.
3. **Text preparation** (`prepare_text`), in this order:
   - Latin dictionary rules: the rules whose key contains an English letter are applied first. The user's reading of an English term (for example `"tracking number"`) is kept, so the library does not read it on its own;
   - verbalization (`verbalize`, see below), for lines that contain a digit;
   - dictionary rules, longest keys first, matched case-insensitively. A key that starts or ends with a letter, digit or underscore matches only whole words;
   - each remaining English word (`[a-zA-Z]+`) is replaced by a Ukrainian transliteration (multi-letter rules first, then letter by letter).
4. **Split into parts** (`split_to_parts`): the text is cut after `.`, `?`, `!`, `:`, `;` or a line break, but only when the current part is longer than 30 characters. Empty parts are dropped.
5. **For each part:**
   - `clean_text`: Unicode NFKC normalization; `+` becomes the combining acute accent (stress mark); dash variants become `-`;
   - stress: `ukrainian_word_stress.Stressifier`;
   - phonemes: `ipa_uk.ipa`. A part without phonemes is skipped;
   - tokens: `model.tokenizer.encode`. The tokenizer drops every character that is not in the model vocabulary; the console lists such characters (`[!] Модель пропускає символи …`). Tokens are cut to 500, with a console warning (`[!] Фрагмент обрізано до 500 токенів`);
   - audio: `model(tokens, speed=speed, s_prev=voice_style)`.
6. **Assembly:** the parts are concatenated. If `volume != 1.0`, the samples are multiplied by it. The samples are clipped to [-1, 1] and converted to int16.
7. **Output:** WAV, 24 kHz, mono, 16-bit, returned as `audio/wav`.

Errors: `No audio generated` when no part produces audio. Any other exception is returned as `500 {"error": "<text>"}`.

## Verbalization (`verbalize`, `POST /api/verbalize`)

`verbalize` (in `verbalizer.py`) wraps the Python package `ukrainian-tn`, preset `tts_friendly`. The package is rule-based and runs on the CPU: no model and no GPU are used. A line without a digit is not changed by `verbalize`. A line with a digit is normalized. Line breaks are kept, because `split_to_parts` uses them as split points. The library does not know the unit `ГВт`, so `verbalize` writes it as `гвт` first; the dictionary entry `гвт` then expands it (`2 ГВт` → `два гіга ват`).

`POST /api/verbalize` returns the text that the synthesis uses after step 3 above, without audio. Examples (outputs of the server):

| Input | Output |
|---|---|
| `У 2026 році ми пройшли 5 км.` | `У дві тисячі двадцять шостому році ми пройшли п'ять кілометрів.` |
| `Курс становить 40,50 грн за долар.` | `Курс становить сорок гривень п'ятдесят копійок за долар.` |
| `Температура тіла — 36,6 °C.` | `Температура тіла — тридцять шість цілих і шість десятих градуса Цельсія.` |
| `Вага вантажу становить 5–7 кг.` | `Вага вантажу становить від п'яти до семи кілограмів.` |
| `Дедлайн — 15.03.2025, не пізніше.` | `Дедлайн — п'ятнадцятого березня дві тисячі двадцять п'ятого року, не пізніше.` |
| `Зустріч о 21:30 у кафе.` | `Зустріч о двадцять першій годині тридцять хвилин у кафе.` |
| `Ціна 150грн.` | `Ціна сто п'ятдесят гривень.` |

Covered: cardinal numbers with nouns, decimals and fractions, percent, ordinals (`3-й` → `третій`), dates, times, units, currency, `№`, `€`, `$`, and phone numbers, which are read digit by digit.

Known reading errors, found on 74 test phrases:

- A range with «з» gets the instrumental case: `з 9 до 18 години` → `з дев'ятьма до вісімнадцяти годин` (the correct form is `з дев'яти`); `з 8:00 до 17:00` → `з вісім годин до сімнадцять годин`. Date ranges with «по» are wrong too: `з 1 по 5 вересня` → `з одним по п'ятого вересня`.
- `за 5 хвилин до 9` → `за п'ять хвилин до дев'ять` (the correct form is `до дев'ятої`).
- Compound ordinals: `21-шу` → `двадцятиодношу` (the correct form is `двадцять першу`).
- `4а` → `чотириа`.
- `Гб` is not expanded: `16 Гб` → `шістнадцять Гб`.
- Uppercase `МВт` is read by the library (`500 МВт` → `п'ятсот мегават`), so the dictionary entry `мвт` does not apply to it. Lowercase `мВт` takes the dictionary value (`500 мВт` → `п'ятсот мега ват`).
- A phone number with a leading zero gets a country prefix: `0979456822` → `плюс три вісім нуль …`.
- The guillemets `«»` are removed in lines with digits.
- English words next to numbers are read by the library, not by the transliteration. For some words the reading is worse (`Photoshop 2024` → `пготошоп`). A dictionary entry with that key takes precedence.

Console warnings from the verbalization:

- `[!] Неоднозначне читання: «…» (unit)` or `(foreign_word)`: the library is not sure about a span, for example `16 Гб`. Single letters are not logged. Some warnings are false, for example `4 о` in `4 о 17:45`.
- `[!] Помилка вербалізації, рядок лишено без змін: …`: the line is kept as it is.

## Dictionary (`dictionary.json`)

- A flat JSON object of `"source text": "replacement"` pairs. In the copy checked on 2026-11-05 the file has 342 entries and loads without errors.
- The file is read **once, at startup**. After any change, restart the server.
- A JSON syntax error is printed as `[!] Помилка словника` and **no** dictionary rules are applied at all. A trailing comma is a common cause; see `user/Troubleshooting.md`.
- Keys with an English letter are applied before verbalization, so `"Google": "Гугл"` or `"cookie policy": "кукі полісі"` also work in lines with digits. Keys without English letters are applied after verbalization. The entries `грн`, `млн` and `млрд` are applied after verbalization, so the verbalizer reads them with the number. Moving them before verbalization gives a wrong reading (`40,50 грн` would be read as a decimal).

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
- `ukrainian-tn==0.7.0` (verbalization) is a Rust library with `abi3` wheels: one wheel per platform covers Python 3.9 and newer. A Windows wheel exists, but the library has not been run on Windows yet. It runs on the CPU.
- `torch` is not listed in `requirements.txt`. It is installed as a dependency of `styletts2-inference`, which requires `torch` and `torchaudio`. The reference machine has `torch 2.14.1+cu130`. CUDA notes are in `user/Installation.md`.
- `numpy<2` is pinned.
- ffmpeg is provided by `static-ffmpeg`. If the package is installed, `static_ffmpeg.add_paths()` runs at startup. `pydub` uses ffmpeg for conversion.

## Limits and behavior to know

- This is the Flask development server. It has no authentication and is not meant for production or for exposure to the internet.
- CORS: every response carries `Access-Control-Allow-Origin: *`. Keep it: a browser extension uses the same server.
- Request size limit: 500 MB.
- A part is cut at 500 tokens. The console warns about it, but the end of the part is still lost.
- Characters that are not in the model vocabulary are dropped by the tokenizer, and the console lists them (`[!] Модель пропускає символи`). For example, `№` in a line without digits. A line with digits is converted by the verbalizer first, so such characters are rare there.
- `translate` requires internet access and calls the Google translation service.
- The model is loaded at every start. Expect about 45 seconds before the first request is served.
- Verbalization is rule-based. Its known reading errors are listed in the verbalization section.
- Error bodies are JSON `{"error": "<text>"}`. Some older responses were plain text (for example `No file`); the client accepts both formats.

## Running

- Linux: `./Server_py/run_server.sh` from the repository root, or from `Server_py/`.
- Windows: see `user/Installation.md`.
- The server must run from `Server_py/`. Started from the repository root, it reports no error but loads 0 voices and an empty dictionary.
