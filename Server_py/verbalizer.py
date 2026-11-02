import re
from num2words import num2words

class Verbalizer:
    def __init__(self, device=None):
        # Since we do not use neural networks, the device argument is simply ignored,
        # but it is kept for compatibility with your server.py
        pass
    
    def _replace_number(self, match):
        """Replace the matched number with its text form."""
        num_str = match.group()
        try:
            # Convert the number to Ukrainian words
            words = num2words(int(num_str), lang='uk')
            return words
        except Exception as e:
            # If the conversion fails (for example, the number is too large),
            # leave it as is.
            return num_str

    def generate_text(self, text):
        """Find all integer numbers in the text and replace them with words."""
        # The regular expression matches standalone numbers (with no letters adjacent)
        normalized_text = re.sub(r'\b\d+\b', self._replace_number, text)
        
        # Handle minor double-space artifacts (if they occur)
        normalized_text = re.sub(r'\s+', ' ', normalized_text)
        
        return normalized_text.strip()