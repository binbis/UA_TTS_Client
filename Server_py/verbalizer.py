import re

from ukrainian_tn import NormalizeOptions, flag_uncertain, normalize_with

_OPTIONS = NormalizeOptions("tts_friendly")
_DIGIT = re.compile(r"\d")
_LOGGED_CATEGORIES = {"unit", "foreign_word"}


def verbalize(text):
    """Spell out numbers, dates, times, units and currency. Lines without digits are kept as they are."""
    lines = []
    for line in text.split("\n"):
        if not _DIGIT.search(line):
            lines.append(line)
            continue
        try:
            for span in flag_uncertain(line):
                if span.category in _LOGGED_CATEGORIES and len(span.text) > 1:
                    print(f" [!] Неоднозначне читання: «{span.text}» ({span.category})")
            lines.append(normalize_with(line, _OPTIONS))
        except Exception as err:
            print(f" [!] Помилка вербалізації, рядок лишено без змін: {err}")
            lines.append(line)
    return "\n".join(lines)
