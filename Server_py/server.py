import os
import sys
import builtins
import warnings
import json
import re
from deep_translator import GoogleTranslator

# --- FIX 1: Виправлення кодування для Windows ---
_real_open = builtins.open

def _utf8_open(*args, **kwargs):
    mode = 'r'
    if len(args) > 1:
        mode = args[1]
    elif 'mode' in kwargs:
        mode = kwargs['mode']
    
    if 'b' not in mode and 'encoding' not in kwargs:
        kwargs['encoding'] = 'utf-8'
        
    return _real_open(*args, **kwargs)

builtins.open = _utf8_open
# -------------------------------------------------------------

warnings.filterwarnings("ignore", category=SyntaxWarning)
warnings.filterwarnings("ignore", category=UserWarning)

import io
import glob
import torch
import scipy.io.wavfile
import numpy as np
from flask import Flask, request, send_file, jsonify
from unicodedata import normalize

# --- IMPORT DOC PARSERS ---
try:
    from pypdf import PdfReader
except ImportError:
    PdfReader = None

try:
    import docx
except ImportError:
    docx = None

try:
    from odf.opendocument import load as load_odf
    from odf.text import P
except ImportError:
    load_odf = None
# ---------------------------

try:
    import static_ffmpeg
    static_ffmpeg.add_paths()
    print(" [i] FFmpeg шляхи додано через static_ffmpeg")
except ImportError:
    pass

from styletts2_inference.models import StyleTTS2
from ukrainian_word_stress import Stressifier, StressSymbol
from ipa_uk import ipa
from verbalizer import Verbalizer

try:
    from pydub import AudioSegment
    from pydub.utils import which
    if which("ffmpeg") is None:
        print(" [!] УВАГА: Pydub не бачить ffmpeg.exe")
except ImportError:
    pass

app = Flask(__name__)

# --- FIX 2: ЗБІЛЬШЕНО ЛІМІТ РОЗМІРУ ЗАПИТУ ДО 500 МБ ---
# Раніше було 16MB, через що великі файли відхилялися з помилкою 413 (Payload Too Large),
# що могло сприйматися клієнтом як 401 або збій з'єднання.
app.config['MAX_CONTENT_LENGTH'] = 500 * 1024 * 1024  
# -------------------------------------------------------

# --- FIX 3: ДОДАНО CORS ЗАГОЛОВКИ ДЛЯ БРАУЗЕРНИХ РОЗШИРЕНЬ ---
@app.after_request
def after_request(response):
    response.headers.add('Access-Control-Allow-Origin', '*')
    response.headers.add('Access-Control-Allow-Headers', 'Content-Type,Authorization')
    response.headers.add('Access-Control-Allow-Methods', 'GET,PUT,POST,DELETE,OPTIONS')
    return response
# -------------------------------------------------------------

# --- Налаштування ---
device = 'cuda' if torch.cuda.is_available() else 'cpu'
print(f"Використовується пристрій: {device}")

print("Завантаження StyleTTS2...")
model = StyleTTS2(hf_path='patriotyk/styletts2_ukrainian_multispeaker', device=device)

print("Завантаження Verbalizer...")
verbalizer = Verbalizer(device=device)

voices = {}
print("Сканування голосів...")
voice_files = glob.glob(os.path.join("voices", "*.pt"))
if not voice_files: voice_files = glob.glob("*.pt")

for file_path in voice_files:
    voice_name = os.path.splitext(os.path.basename(file_path))[0]
    try:
        voices[voice_name] = torch.load(file_path, map_location=device)
        print(f" [+] Завантажено голос: {voice_name}")
    except Exception as e:
        print(f" [!] Помилка завантаження {voice_name}: {e}")

stressify = Stressifier()

# --- СЛОВНИК ТА АВТО-ТРАНСЛІТЕРАЦІЯ ---
dict_rules = []

def load_dictionary():
    global dict_rules
    dict_path = "dictionary.json"
    if not os.path.exists(dict_path): return

    try:
        with open(dict_path, "r", encoding="utf-8") as f:
            custom_dict = json.load(f)
        
        sorted_keys = sorted(custom_dict.keys(), key=len, reverse=True)
        rules = []
        for key in sorted_keys:
            val = custom_dict[key]
            pattern_str = re.escape(key)
            prefix = r'\b' if (key[0].isalnum() or key[0] == '_') else ''
            suffix = r'\b' if (key[-1].isalnum() or key[-1] == '_') else ''
            final_pattern = f"{prefix}{pattern_str}{suffix}"
            rules.append((re.compile(final_pattern, re.IGNORECASE), val))
        dict_rules = rules
        print(f" [i] Словник завантажено: {len(rules)} правил.")
    except Exception as e:
        print(f" [!] Помилка словника: {e}")

load_dictionary()

def transliterate_english_to_ukrainian(text):
    mappings_multi = [
        (r"tion\b", "шн"), (r"sion\b", "шн"), (r"ture\b", "чер"),
        (r"sh", "ш"), (r"ch", "ч"), (r"kh", "х"), (r"zh", "ж"), (r"th", "т"), (r"ph", "ф"),
        (r"qu", "кв"), (r"ck", "к"), (r"wh", "в"), (r"wr", "р"),
        (r"ee", "і"), (r"ea", "і"), (r"oo", "у"), (r"ou", "ау"), (r"ai", "ей"), (r"ay", "ей"),
        (r"ie", "і"), (r"ei", "ей"), (r"ju", "джу"), (r"yo", "йо"), (r"ya", "я"),
        (r"kn", "н"),
    ]
    mappings_single = {
        'a': 'а', 'b': 'б', 'c': 'к', 'd': 'д', 'e': 'е', 'f': 'ф', 
        'g': 'г', 'h': 'х', 'i': 'і', 'j': 'дж', 'k': 'к', 'l': 'л', 
        'm': 'м', 'n': 'н', 'o': 'о', 'p': 'п', 'q': 'к', 'r': 'р', 
        's': 'с', 't': 'т', 'u': 'ю', 'v': 'в', 'w': 'в', 'x': 'кс', 
        'y': 'і', 'z': 'з'
    }
    processed = text.lower()
    for en, ua in mappings_multi:
        processed = re.sub(en, ua, processed)
    result = ""
    for char in processed:
        result += mappings_single.get(char, char)
    return result

def apply_dictionary_and_translit(text):
    if not text: return text
    if dict_rules:
        for pattern, replacement in dict_rules:
            text = pattern.sub(replacement, text)
    english_words = re.findall(r'\b[a-zA-Z]+\b', text)
    english_words = set(english_words)
    for word in english_words:
        transliterated = transliterate_english_to_ukrainian(word)
        text = re.sub(r'\b' + re.escape(word) + r'\b', transliterated, text, flags=re.IGNORECASE)
    return text

# --- Допоміжні функції ---

def split_to_parts(text):
    split_symbols = '.?!:;\n'
    parts = ['']
    index = 0
    for s in text:
        parts[index] += s
        if s in split_symbols and len(parts[index]) > 30:
            index += 1
            parts.append('')
    return [p.strip() for p in parts if p.strip()]

def clean_text(text):
    text = normalize('NFKC', text)
    text = text.replace('+', StressSymbol.CombiningAcuteAccent)
    text = re.sub(r'[᠆‐‑‒–—―⁻₋−⸺⸻]', '-', text)
    return text

# --- FILE PARSING HELPERS ---

def extract_text_from_pdf(file_stream):
    if not PdfReader: raise ImportError("pypdf not installed")
    reader = PdfReader(file_stream)
    text = []
    for page in reader.pages:
        txt = page.extract_text()
        if txt: text.append(txt)
    return "\n".join(text)

def extract_text_from_docx(file_stream):
    if not docx: raise ImportError("python-docx not installed")
    doc = docx.Document(file_stream)
    return "\n".join([para.text for para in doc.paragraphs])

def extract_text_from_odt(file_stream):
    if not load_odf: raise ImportError("odfpy not installed")
    doc = load_odf(file_stream)
    return "\n".join([str(p) for p in doc.getElementsByType(P)])

# --- API Endpoints ---

@app.route('/api/voices', methods=['GET'])
def api_get_voices():
    return jsonify(list(voices.keys()))

@app.route('/api/verbalize', methods=['POST'])
def api_verbalize():
    data = request.json
    text = data.get('text', '')
    
    if not text: return jsonify({"error": "No text"}), 400
    
    text = apply_dictionary_and_translit(text)
    
    try:
        sentences = re.split(r'(?<=[.?!;])\s+', text)
        processed_parts = []
        
        for sentence in sentences:
            stripped_sentence = sentence.strip()
            if not stripped_sentence: continue

            if re.search(r'\d', stripped_sentence):
                try:
                    processed = verbalizer.generate_text(stripped_sentence)
                    processed_parts.append(processed)
                except Exception as model_err:
                    print(f" [!] Помилка моделі: {model_err}")
                    processed_parts.append(stripped_sentence) 
            else:
                processed_parts.append(stripped_sentence)
        
        full_result = ' '.join(processed_parts)
        return jsonify({"text": full_result})

    except Exception as e:
        print(f"Verbalization error: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/synthesize', methods=['POST'])
def api_synthesize():
    data = request.json
    text = data.get('text', '')
    voice_name = data.get('voice')
    speed = data.get('speed', 1.0)
    volume = data.get('volume', 1.0) 
    do_translate = data.get('translate', False) # НОВИЙ ПАРАМЕТР: визначає, чи треба перекладати

    # --- БЛОК ПЕРЕКЛАДУ (Спрацює ТІЛЬКИ якщо do_translate == True) ---
    if do_translate and text:
        try:
            # Перекладаємо з автовизначенням мови на українську
            text = GoogleTranslator(source='auto', target='uk').translate(text)
            print(f" [Переклад для розширення]: {text}")
        except Exception as e:
            print(f" [!] Помилка перекладу: {e}")
    # -----------------------------------------------------------------

    style = voices.get(voice_name)
    if style is None:
        if voices: style = voices[list(voices.keys())[0]]
        else: return jsonify({"error": "No voices loaded"}), 500

    try:
        text = apply_dictionary_and_translit(text)
        parts = split_to_parts(text)
        audio_segments = []

        with torch.no_grad():
            for part in parts:
                cleaned = clean_text(part)
                stressed = stressify(cleaned)
                phonemes = ipa(stressed)
                
                if not phonemes: continue

                tokens = model.tokenizer.encode(phonemes)
                if len(tokens) > 500: tokens = tokens[:500]

                wav_tensor = model(tokens, speed=speed, s_prev=style)
                audio_segments.append(wav_tensor)

        if not audio_segments:
            return jsonify({"error": "No audio generated"}), 500

        full_audio = torch.cat(audio_segments, dim=0).cpu().numpy()
        
        if volume != 1.0:
            full_audio = full_audio * volume

        full_audio = np.clip(full_audio, -1.0, 1.0)
        full_audio = (full_audio * 32767).astype(np.int16)
        
        buffer = io.BytesIO()
        scipy.io.wavfile.write(buffer, 24000, full_audio)
        buffer.seek(0)
        return send_file(buffer, mimetype="audio/wav")

    except Exception as e:
        print(f"Synthesis Error: {e}")
        return jsonify({"error": str(e)}), 500

@app.route('/api/convert', methods=['POST'])
def api_convert():
    if 'file' not in request.files: return "No file", 400
    file = request.files['file']
    fmt = request.form.get('format', 'mp3')
    try:
        audio = AudioSegment.from_wav(file)
        out_buf = io.BytesIO()
        audio.export(out_buf, format=fmt)
        out_buf.seek(0)
        return send_file(out_buf, mimetype=f"audio/{fmt}")
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/parse_file', methods=['POST'])
def api_parse_file():
    if 'file' not in request.files: return "No file", 400
    file = request.files['file']
    filename = file.filename.lower()
    
    try:
        text = ""
        if filename.endswith('.pdf'):
            text = extract_text_from_pdf(file)
        elif filename.endswith('.docx'):
            text = extract_text_from_docx(file)
        elif filename.endswith('.odt'):
            text = extract_text_from_odt(file)
        elif filename.endswith('.txt'):
            text = file.read().decode('utf-8', errors='ignore')
        else:
            return jsonify({"error": "Unsupported format"}), 400
            
        return jsonify({"text": text})
    except ImportError as ie:
        return jsonify({"error": f"Missing library: {ie}"}), 500
    except Exception as e:
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    app.run(port=5000, debug=False)