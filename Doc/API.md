# TTS server API (UA_TTS_Client)

> **Status:** verified with `curl` against a live server on 2026-10-21; `/api/convert` formats were additionally verified on 2026-10-22.
> The source of truth is `Server_py/server.py`. If the code and this document disagree, the code is right; update this document.
> Client wrapper: `Infrastructure/Api/TtsApiClient.cs` (`ITtsApiClient`).

## General

| Property | Value |
|---|---|
| Base URL | `http://127.0.0.1:5000` (Flask dev server, `app.run(port=5000)`) |
| CORS | `Access-Control-Allow-Origin: *`, allowed `GET,PUT,POST,DELETE,OPTIONS`, headers `Content-Type,Authorization` — **do not change**: a browser extension uses this same server |
| Request size limit | 500 MB (`MAX_CONTENT_LENGTH`) |
| Error format | **JSON** `{"error": "<text>"}` with the matching HTTP status code |
| Client timeout | 5 min (`TtsApiClient` raises `HttpClient.Timeout` to 5 min if it is less than 2 min) |
| **Not present** | `/api/ping`, `/health` → **404** (use `/api/health`) |

The client accepts **both JSON and plain text** in an error body (the server historically replied `No file` as plain text) and rejects HTML error pages, so the user sees the error text, not markup.

---

## GET `/api/voices`

List of available voices (names without extension, from `Server_py/voices/*.pt`).

**Success (200, `application/json`):**

```json
["Вероніка Дорош", "Денис Денисенко", "Катерина Понамаренко"]
```

On this machine there are **31 voices**. The response is an array of strings, **not** an object.

```bash
curl -sS http://127.0.0.1:5000/api/voices
```

---

## GET `/api/health`

Health check. **Added to `server.py` in Step 2** — the old server does not have it (404).

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
| `status` | `ok` |
| `device` | `cuda` or `cpu` |
| `voices` | number of loaded voices |
| `model` | model identifier on HuggingFace |

```bash
curl -sS http://127.0.0.1:5000/api/health
```

**Client behavior:** `TtsApiClient.GetHealthAsync` on **404** falls back to `GET /api/voices` and returns a synthetic response `{"status":"ok","voices":N}` (compatibility with the old server). Do not remove the fallback.

---

## POST `/api/verbalize`

Verbalization: converting numbers, dates and abbreviations into words (e.g. `2026 р.` → «дві тисячі двадцять шостий рік» — "two thousand twenty-sixth year"). Processed sentence by sentence; sentences without digits are returned unchanged.

**Body (JSON):**

```json
{ "text": "У 2026 році ми пройшли 5 км." }
```

**Success (200):**

```json
{ "text": "У дві тисячі двадцять шостому році ми пройшли п'ять кілометрів." }
```

**Error:** `400` → `{"error":"No text"}`; `500` → `{"error":"<exception text>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/verbalize \
  -H "Content-Type: application/json" \
  -d '{"text":"У 2026 році ми пройшли 5 км."}'
```

---

## POST `/api/synthesize`

Main endpoint: text → WAV.

**Body (JSON):**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `text` | string | ✅ | — | text to speak |
| `voice` | string | ❌ | first loaded voice | name from `GET /api/voices`; an unknown name is silently replaced with the first voice |
| `speed` | number | ❌ | `1.0` | speech speed (the value is passed to the model) |
| `volume` | number | ❌ | `1.0` | amplitude gain **on the server side** (0…1, then `np.clip`) |
| `translate` | bool | ❌ | `false` | translate the text into Ukrainian (`GoogleTranslator`, `source='auto'`) before synthesis |

```json
{
  "text": "Привіт, світе!",
  "voice": "Денис Денисенко",
  "speed": 1.0,
  "volume": 1.0,
  "translate": false
}
```

**Success (200):** `Content-Type: audio/wav` — **24 kHz, mono, 16-bit PCM**.

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
| `500` | `{"error":"No voices loaded"}` (no voice was loaded) |
| `500` | `{"error":"No audio generated"}` (empty or unreadable text) |
| `500` | `{"error":"<exception text>"}` |

The client throws the domain exception `TtsApiException` (with `StatusCode`), **not** `HttpRequestException`.

---

## POST `/api/convert`

Audio conversion. **Accepts only WAV** (`AudioSegment.from_wav`) and returns it in the format given in the `format` field.

**Body:** `multipart/form-data`

| Field | Type | Default |
|---|---|---|
| `file` | file (WAV) | — (without it → 400) |
| `format` | string | `mp3` |

**Success (200):** `Content-Type: audio/<format>` (e.g. `audio/mp3`).

**Errors:** `400` `{"error":"No file"}`; `500` `{"error":"<exception text>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/convert \
  -F "file=@t.wav" -F "format=mp3" -o t.mp3
```

**Verified formats on this machine:** `mp3`, `wav`, `mp4` work; `m4a` does **not** (ffmpeg returns an encoding error). `mp4` is audio without video (AAC in an MP4 container).

> The client calls this endpoint during MP4 export (`ITtsApiClient.ConvertAsync`): it sends the concatenated WAV and `format=mp4`. For WAV export no conversion is needed — the client concatenates the fragments itself (`WavAudioConcatenator`).

---

## POST `/api/parse_file`

Extracts text from a document.

**Body:** `multipart/form-data`, field `file`. Supported extensions (determined from `file.filename`, lowercase):

| Extension | Parser |
|---|---|
| `.pdf` | `pypdf` |
| `.docx` | `python-docx` |
| `.odt` | `odfpy` |
| `.txt` | `utf-8` (`errors='ignore'`) |
| other | `400` `{"error":"Unsupported format"}` |

**Success (200):**

```json
{ "text": "Розпізнаний текст документа…" }
```

**Errors:** `400` `{"error":"No file"}`; `400` `{"error":"Unsupported format"}`; `500` `{"error":"Missing library: …"}` or `{"error":"<exception text>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/parse_file \
  -F "file=@book.pdf"
```

Client: `ITtsApiClient.ParseFileAsync(Stream, string fileName, CancellationToken)`.

---

## Client compatibility

| Client rule | Where |
|---|---|
| Health → fallback to `/api/voices` on 404 | `TtsApiClient.GetHealthAsync` |
| Error is read as `{"error"}` **or** plain text; HTML is rejected | `TtsApiClient.ReadErrorMessageAsync` |
| All transport failures → `TtsApiException` (`StatusCode`, `IsConnectionError`) | `Core/Exceptions/TtsApiException.cs` |
| Timeout ≥ 2 min (actually 5 min) | `TtsApiClient` constructor + `App.axaml.cs` |
| DTO JSON properties match the server's field names | `Core/Models/TtsRequestDto.cs` and others |
| `/api/synthesize` — **sequential** requests: one per fragment (queue and export), no parallel requests to the GPU | `FragmentPlaybackService`, `AudioExportService` |
| `/api/convert` — one request per MP4 export (concatenated WAV → `mp4`); not called for WAV | `AudioExportService` + `TtsApiClient.ConvertAsync` |

## Quick server check

```bash
curl -sS http://127.0.0.1:5000/api/health                    # status
curl -sS http://127.0.0.1:5000/api/voices | head -c 200       # voices
curl -sS -X POST http://127.0.0.1:5000/api/verbalize \
  -H "Content-Type: application/json" -d '{"text":"Тест 5 км."}'
```
