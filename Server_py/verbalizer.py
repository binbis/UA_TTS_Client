import re
from num2words import num2words

class Verbalizer:
    def __init__(self, device=None):
        # Оскільки ми не використовуємо нейромережі, аргумент device просто ігнорується,
        # але залишається для сумісності з вашим server.py
        pass
    
    def _replace_number(self, match):
        """Функція для заміни знайденого числа на текст"""
        num_str = match.group()
        try:
            # Конвертуємо число у слова українською мовою
            words = num2words(int(num_str), lang='uk')
            return words
        except Exception as e:
            # Якщо конвертація не вдалася (наприклад, занадто велике число),
            # залишаємо як є.
            return num_str

    def generate_text(self, text):
        """Шукає всі цілі числа у тексті і замінює їх на слова."""
        # Регулярний вираз шукає окремі числа (без літер поруч)
        normalized_text = re.sub(r'\b\d+\b', self._replace_number, text)
        
        # Вирішення дрібних артефактів подвійних пробілів (якщо виникнуть)
        normalized_text = re.sub(r'\s+', ' ', normalized_text)
        
        return normalized_text.strip()