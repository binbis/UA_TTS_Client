# API TTS-сервера (UA_TTS_Client)

> **Статус:** перевірено `curl`-ом 2026-10-21 на живому сервері; формати `/api/convert`
> додатково перевірено 2026-10-22.
> Джерело істини — `Server_py/server.py`. Якщо код і цей документ розходяться,
> правий код; допиши сюди.
> Клієнтська обгортка — `Infrastructure/Api/TtsApiClient.cs` (`ITtsApiClient`).

## Загальне

| Властивість | Значення |
|---|---|
| Base URL | `http://127.0.0.1:5000` (Flask dev server, `app.run(port=5000)`) |
| CORS | `Access-Control-Allow-Origin: *`, дозволені `GET,PUT,POST,DELETE,OPTIONS`, заголовки `Content-Type,Authorization` — **не чіпати**: цим самим сервером користується браузерне розширення |
| Ліміт розміру запиту | 500 МБ (`MAX_CONTENT_LENGTH`) |
| Формат помилок | **JSON** `{"error": "<текст>"}` з відповідним HTTP-кодом |
| Таймаут клієнта | 5 хв (`TtsApiClient` піднімає `HttpClient.Timeout` до 5 хв, якщо він менший за 2 хв) |
| **Немає** | `/api/ping`, `/health` → **404** (використовуйте `/api/health`) |

Клієнт терпить **і JSON, і plain text** у тілі помилки (сервер історично відповідав
`No file` plain text'ом) і відкидає HTML-сторінки помилок — користувачу показується
текст помилки, а не розмітка.

---

## GET `/api/voices`

Список доступних голосів (імена без розширення із `Server_py/voices/*.pt`).

**Успіх (200, `application/json`):**

```json
["Вероніка Дорош", "Денис Денисенко", "Катерина Понамаренко"]
```

Фактично на цій машині — **31 голос**. Відповідь — масив рядків, **не** об'єкт.

```bash
curl -sS http://127.0.0.1:5000/api/voices
```

---

## GET `/api/health`

Health-check. **Додано в `server.py` (Крок 2)** — у старому сервері його немає (404).

**Успіх (200):**

```json
{
  "status": "ok",
  "device": "cuda",
  "voices": 31,
  "model": "patriotyk/styletts2_ukrainian_multispeaker"
}
```

| Поле | Значення |
|---|---|
| `status` | `ok` |
| `device` | `cuda` або `cpu` |
| `voices` | кількість завантажених голосів |
| `model` | ідентифікатор моделі на HuggingFace |

```bash
curl -sS http://127.0.0.1:5000/api/health
```

**Поведінка клієнта:** `TtsApiClient.GetHealthAsync` на **404** робить fallback на
`GET /api/voices` і повертає синтетичну відповідь `{"status":"ok","voices":N}`
(сумісність зі старим сервером). Fallback не видаляти.

---

## POST `/api/verbalize`

Вербалізація: перетворення чисел, дат і скорочень у слова (напр. `2026 р.` →
«дві тисячі двадцять шостий рік»). Обробляється по реченнях; речення без цифр
повертаються без змін.

**Тіло (JSON):**

```json
{ "text": "У 2026 році ми пройшли 5 км." }
```

**Успіх (200):**

```json
{ "text": "У дві тисячі двадцять шостому році ми пройшли п'ять кілометрів." }
```

**Помилка:** `400` → `{"error":"No text"}`; `500` → `{"error":"<текст винятку>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/verbalize \
  -H "Content-Type: application/json" \
  -d '{"text":"У 2026 році ми пройшли 5 км."}'
```

---

## POST `/api/synthesize`

Основний ендпоінт: текст → WAV.

**Тіло (JSON):**

| Поле | Тип | Обов'язкове | За замовчуванням | Опис |
|---|---|---|---|---|
| `text` | string | ✅ | — | текст для озвучення |
| `voice` | string | ❌ | перший завантажений голос | ім'я з `GET /api/voices`; невідоме ім'я мовчки замінюється першим голосом |
| `speed` | number | ❌ | `1.0` | швидкість мовлення (клавіша передається в модель) |
| `volume` | number | ❌ | `1.0` | підсилення амплітуди **на боці сервера** (0…1, потім `np.clip`) |
| `translate` | bool | ❌ | `false` | перекласти текст українською (`GoogleTranslator`, `source='auto'`) перед синтезом |

```json
{
  "text": "Привіт, світе!",
  "voice": "Денис Денисенко",
  "speed": 1.0,
  "volume": 1.0,
  "translate": false
}
```

**Успіх (200):** `Content-Type: audio/wav` — **24 кГц, моно, 16-bit PCM**.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/synthesize \
  -H "Content-Type: application/json" \
  -d '{"text":"Привіт.","voice":"Денис Денисенко","speed":1.0,"volume":1.0}' \
  -o t.wav
file t.wav   # RIFF WAVE PCM mono 24000 Hz
```

**Помилки:**

| Код | Тіло |
|---|---|
| `500` | `{"error":"No voices loaded"}` (жоден голос не завантажився) |
| `500` | `{"error":"No audio generated"}` (порожній/нечитабельний текст) |
| `500` | `{"error":"<текст винятку>"}` |

Клієнт кидає доменний `TtsApiException` (із `StatusCode`), **не** `HttpRequestException`.

---

## POST `/api/convert`

Конвертація аудіо. **Приймає лише WAV** (`AudioSegment.from_wav`), віддає в форматі
з поля `format`.

**Тіло:** `multipart/form-data`

| Поле | Тип | За замовчуванням |
|---|---|---|
| `file` | файл (WAV) | — (без нього → 400) |
| `format` | string | `mp3` |

**Успіх (200):** `Content-Type: audio/<format>` (напр. `audio/mp3`).

**Помилки:** `400` `{"error":"No file"}`; `500` `{"error":"<текст винятку>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/convert \
  -F "file=@t.wav" -F "format=mp3" -o t.mp3
```

**Перевірені формати на цій машині:** `mp3`, `wav`, `mp4` — працюють; `m4a` — **ні**
(ffmpeg повертає помилку кодування). `mp4` — це аудіо без відео (AAC у контейнері MP4).

> Клієнт поки що цей ендпоінт не викликає; `ITtsApiClient.ConvertAsync` додається для
> експорту аудіо в MP4 (Крок 16 плану `Doc/Curent_Work_Plan.md`).

---

## POST `/api/parse_file`

Витяг тексту з документа.

**Тіло:** `multipart/form-data`, поле `file`. Підтримувані розширення
(визначаються за `file.filename`, малими літерами):

| Розширення | Парсер |
|---|---|
| `.pdf` | `pypdf` |
| `.docx` | `python-docx` |
| `.odt` | `odfpy` |
| `.txt` | `utf-8` (`errors='ignore'`) |
| інше | `400` `{"error":"Unsupported format"}` |

**Успіх (200):**

```json
{ "text": "Розпізнаний текст документа…" }
```

**Помилки:** `400` `{"error":"No file"}`; `400` `{"error":"Unsupported format"}`;
`500` `{"error":"Missing library: …"}` або `{"error":"<текст винятку>"}`.

```bash
curl -sS -X POST http://127.0.0.1:5000/api/parse_file \
  -F "file=@book.pdf"
```

Клієнт: `ITtsApiClient.ParseFileAsync(Stream, string fileName, CancellationToken)`.

---

## Сумісність із клієнтом

| Правило клієнта | Де |
|---|---|
| Health → fallback на `/api/voices` при 404 | `TtsApiClient.GetHealthAsync` |
| Помилка читається як `{"error"}` **або** plain text; HTML відкидається | `TtsApiClient.ReadErrorMessageAsync` |
| Усі збої транспорту → `TtsApiException` (`StatusCode`, `IsConnectionError`) | `Core/Exceptions/TtsApiException.cs` |
| Таймаут ≥ 2 хв (фактично 5 хв) | конструктор `TtsApiClient` + `App.axaml.cs` |
| JSON-властивості DTO збігаються з іменами полів сервера | `Core/Models/TtsRequestDto.cs` та ін. |

## Швидка перевірка сервера

```bash
curl -sS http://127.0.0.1:5000/api/health                    # стан
curl -sS http://127.0.0.1:5000/api/voices | head -c 200       # голоси
curl -sS -X POST http://127.0.0.1:5000/api/verbalize \
  -H "Content-Type: application/json" -d '{"text":"Тест 5 км."}'
```
