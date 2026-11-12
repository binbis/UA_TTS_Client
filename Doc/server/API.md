# TTS server API

> **Purpose:** the HTTP contract of the TTS server, with request and response examples.
> **Audience:** developers and AI agents who change the client or the server.
> **Source of truth:** `Server_py/server.py`. If this document and the code disagree, the code is right; update this document.
> **Client wrapper:** `Infrastructure/Api/TtsApiClient.cs` (`ITtsApiClient`).
> **Verified:** 2026-11-12 (code reading; the live-server runs behind the `curl` examples are from 2026-11-05).

## General

| Property | Value |
|---|---|
| Base URL | `http://127.0.0.1:5000` (Flask development server, `app.run(port=5000)`) |
| CORS | `Access-Control-Allow-Origin: *`; allowed methods `GET,PUT,POST,DELETE,OPTIONS`; headers `Content-Type,Authorization`. Do not change it: a browser extension uses the same server. |
| Request size limit | 500 MB (`MAX_CONTENT_LENGTH`) |
| Error format | JSON `{"error": "<text>"}` with the matching HTTP status code |
| Client timeout | 5 minutes (`App.axaml.cs` sets it; `TtsApiClient` raises a shorter timeout to 5 minutes) |
| Not present | `/api/ping` and `/health` return 404. Use `/api/health`. |

The client accepts a plain-text error body as well as JSON (old responses were plain text, for example `No file`). It rejects HTML error pages, so the user sees the message text and not markup.

---

## GET `/api/voices`

The list of available voices: the names of the files in `Server_py/voices/` without the `.pt` extension.

**Success (200, `application/json`):**

```json
["Вероніка Дорош", "Денис Денисенко", "Катерина Потапенко"]
```

The response is an array of strings, not an object. The reference set has **31 voices**. Flask escapes non-ASCII characters as `\uXXXX`; a JSON parser returns the same text.

```bash
curl -sS http://127.0.0.1:5000/api/voices
```

---

## GET `/api/health`

Health check.

**Success (200):**

```json
{
  "status": "ok",
  "device": "cuda",
  "voices": 31,
  "model": "patriotyk/styletts2_ukrainian_multispeaker"
}
```

| Field | Value |
|---|---|
| `status` | always `ok` when the server responds |
| `device` | `cuda` or `cpu` |
| `voices` | number of loaded voices |
| `model` | model identifier on Hugging Face |

```bash
curl -sS http://127.0.0.1:5000/api/health
```

**Client behavior:** `TtsApiClient.GetHealthAsync` treats a 404 as an older server and calls `GET /api/voices` instead, returning `{"status":"ok","voices":N}`.

---

## POST `/api/verbalize`

Returns the text that the synthesis uses before it is split into parts: the Latin dictionary rules, the verbalization of numbers, dates, times, units and currency, the dictionary and the English transliteration. No audio is produced, and line breaks are kept. The steps and the known reading errors are described in `server/Server.md`.

**Body (JSON):**

```json
{ "text": "У 2026 році ми пройшли 5 км." }
```

**Success (200):**

```json
{ "text": "У дві тисячі двадцять шостому році ми пройшли п'ять кілометрів." }
```

**Errors:** `400 {"error": "No text"}`; `500 {"error": "<exception text>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/verbalize \
  -H "Content-Type: application/json" \
  -d '{"text":"У 2026 році ми пройшли 5 км."}'
```

---

## POST `/api/synthesize`

The main endpoint: text to WAV. Before splitting, the server prepares the text the same way as `POST /api/verbalize` does. The server then splits the text into parts internally (see `server/Server.md`). The client sends one fragment per request.

**Body (JSON):**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `text` | string | yes | — | text to speak |
| `voice` | string | no | the first loaded voice | a name from `GET /api/voices`. An unknown name is replaced by the first voice without an error. |
| `speed` | number | no | `1.0` | passed to the model |
| `volume` | number | no | `1.0` | a multiplier applied to the samples. The result is clipped to [-1, 1]. Values outside 0–1 are not rejected. |
| `translate` | bool | no | `false` | translate the text to Ukrainian first (Google Translate, needs internet) |

```json
{
  "text": "Привіт, світе!",
  "voice": "Денис Денисенко",
  "speed": 1.0,
  "volume": 1.0,
  "translate": false
}
```

**Success (200):** `Content-Type: audio/wav`, 24 kHz, mono, 16-bit PCM.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/synthesize \
  -H "Content-Type: application/json" \
  -d '{"text":"Привіт.","voice":"Денис Денисенко","speed":1.0,"volume":1.0}' \
  -o t.wav
file t.wav   # RIFF WAVE PCM mono 24000 Hz
```

**Errors:**

| Code | Body |
|---|---|
| `500` | `{"error":"No voices loaded"}` — no voice is loaded |
| `500` | `{"error":"No audio generated"}` — the text produced no audio |
| `500` | `{"error":"<exception text>"}` |

The client throws `TtsApiException` (with `StatusCode`, or with `IsConnectionError` when there is no response). It never exposes `HttpRequestException` to the ViewModels.

---

## POST `/api/convert`

Converts audio. The input must be **WAV**. The output is the format given in the `format` field.

**Body:** `multipart/form-data`

| Field | Type | Default |
|---|---|---|
| `file` | file (WAV) | — (without it the response is 400) |
| `format` | string | `mp3` |

**Success (200):** `Content-Type: audio/<format>`.

**Errors:** `400 {"error":"No file"}`; `500 {"error":"<exception text>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/convert \
  -F "file=@t.wav" -F "format=mp3" -o t.mp3
```

**Verified formats on the reference machine:** `mp3` (`audio/mp3`), `wav` and `mp4` (`audio/mp4`) work. `m4a` does **not**: the server returns `500` with an ffmpeg encoding error. `mp4` is audio only (AAC in an MP4 container).

The client calls this endpoint for MP3 export (`ITtsApiClient.ConvertAsync`): it sends the concatenated WAV with `format=mp3`. WAV export does not call the server; the client concatenates the fragments itself (`WavAudioConcatenator`).

---

## POST `/api/parse_file`

Extracts text from a document.

**Body:** `multipart/form-data`, field `file`. The extension is taken from the file name, in lower case:

| Extension | Parser |
|---|---|
| `.pdf` | `pypdf` |
| `.docx` | `python-docx` |
| `.odt` | `odfpy` |
| `.txt` | UTF-8, invalid bytes ignored |
| other | `400 {"error":"Unsupported format"}` |

**Success (200):**

```json
{ "text": "Розпізнаний текст документа…" }
```

**Errors:** `400 {"error":"No file"}`; `400 {"error":"Unsupported format"}`; `500 {"error":"Missing library: …"}`; `500 {"error":"<exception text>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/parse_file -F "file=@book.pdf"
```

The client method is `ITtsApiClient.ParseFileAsync(Stream, string fileName, CancellationToken)`. The UI does not call it yet (the Reader tab is a placeholder).

---

## Client compatibility

| Client rule | Where it is implemented |
|---|---|
| Health falls back to `/api/voices` on 404 | `TtsApiClient.GetHealthAsync` |
| An error body is read as `{"error"}` or as plain text; HTML is rejected | `TtsApiClient.ReadErrorMessageAsync` |
| Every transport failure becomes `TtsApiException` (`StatusCode`, `IsConnectionError`) | `Core/Exceptions/TtsApiException.cs` |
| Timeout: 5 minutes | `App.axaml.cs` and the `TtsApiClient` constructor |
| DTO property names match the server fields | `Core/Models/TtsRequestDto.cs` and the other DTOs |
| `/api/synthesize` requests are sequential: one per fragment, for both the queue and the export | `FragmentPlaybackService`, `AudioExportService` |
| `/api/convert` is called once per MP3 export, with the concatenated WAV; never for WAV | `AudioExportService`, `TtsApiClient.ConvertAsync` |

## Quick server check

```bash
curl -sS http://127.0.0.1:5000/api/health
curl -sS http://127.0.0.1:5000/api/voices | head -c 200
curl -sS -X POST http://127.0.0.1:5000/api/verbalize \
  -H "Content-Type: application/json" -d '{"text":"Тест 5 км."}'
```
