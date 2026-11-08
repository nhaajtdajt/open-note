"""
Builds the letter that comes out of the bag on the how-to card (Ctrl+Alt+/): a white instant
photo with a picture of flowers on top and a thank-you note written under it (the card tilts it
and lays it on its shadow). The note is drawn with Windows' Sitka font and baked into the
picture, so no font is shipped and it looks the same on every PC. Made at 2x its size on the
card, for high-DPI screens; a JPEG, because most of it is a photo.

    python tools/make-letter-art.py <flowers photo> [output folder]

Output (default src/OpenNote/Assets/help): letter-card.jpg, letter-shadow.png. Needs Pillow.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

photo_path = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "src", "OpenNote", "Assets", "help")

NOTE = [
    "Thank you for everything, I'm so happy because this app has over 150 users. I'm too poor "
    "to buy and send flowers to every user, so this version is the flower version. I hope you "
    "will love it. Have a lovely day!",
    "Don't forget to share this app with others and give me a 5-star rating on the Microsoft "
    "Store! Love you so much, so much!",
]
SIGNATURE = "xx, Gem"

S = 2                      # drawn at 2x
W = 360 * S                # the card's width on screen: 360
SIDE = 22 * S              # white border at the sides and top
PHOTO_H = 206 * S
INK = (29, 62, 134)        # the blue of the sample lettering
PAPER = (253, 252, 248)
SHADOW_PAD = 24            # on screen, around the card, for its shadow

font_file = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "SitkaVF.ttf")


def sitka(size, style):
    font = ImageFont.truetype(font_file, size)
    font.set_variation_by_name(style)
    return font


body = sitka(int(14.5 * S), "Text")
sign = sitka(int(16 * S), "Subheading Semibold")
line_h = int(21 * S)
gap = int(8 * S)
text_w = W - 2 * SIDE - 4 * S


def wrap(text, font, width):
    lines, line = [], ""
    for word in text.split():
        trial = f"{line} {word}".strip()
        if font.getlength(trial) <= width:
            line = trial
        else:
            lines.append(line)
            line = word
    lines.append(line)
    return lines


paragraphs = [wrap(p, body, text_w) for p in NOTE]
text_h = sum(len(p) for p in paragraphs) * line_h + (len(paragraphs) - 1) * gap
H = SIDE + PHOTO_H + 18 * S + text_h + 8 * S + line_h + 16 * S
card = Image.new("RGB", (W, H), PAPER)

# The photo: a wide slice of the flower stall, cropped to fill its window.
photo = Image.open(photo_path).convert("RGB")
pw, ph = W - 2 * SIDE, PHOTO_H
scale = max(pw / photo.width, ph / photo.height)
photo = photo.resize((round(photo.width * scale), round(photo.height * scale)), Image.LANCZOS)
top = round((photo.height - ph) * 0.42)
left = (photo.width - pw) // 2
photo = photo.crop((left, top, left + pw, top + ph))
round_mask = Image.new("L", (pw * 4, ph * 4), 0)
ImageDraw.Draw(round_mask).rounded_rectangle([0, 0, pw * 4 - 1, ph * 4 - 1], radius=4 * S * 4, fill=255)
card.paste(photo, (SIDE, SIDE), round_mask.resize((pw, ph), Image.LANCZOS))

# The note, then the signature on the right.
draw = ImageDraw.Draw(card)
y = SIDE + PHOTO_H + 18 * S
for i, p in enumerate(paragraphs):
    if i:
        y += gap
    for line in p:
        draw.text((SIDE + 2 * S, y), line, font=body, fill=INK)
        y += line_h
y += 8 * S
draw.text((W - SIDE - sign.getlength(SIGNATURE) - 4 * S, y), SIGNATURE, font=sign, fill=INK)

os.makedirs(out, exist_ok=True)
path = os.path.join(out, "letter-card.jpg")
card.save(path, quality=90, optimize=True, progressive=False, subsampling=0)
print(f"letter-card.jpg: {W}x{H} ({W // S}x{H // S} on the card), {os.path.getsize(path) // 1024} KB")

# Its shadow, at screen size (it is only a blur): the card's shape with SHADOW_PAD around it.
w, h = W // S, H // S
shape = Image.new("L", (w + 2 * SHADOW_PAD, h + 2 * SHADOW_PAD), 0)
ImageDraw.Draw(shape).rectangle([SHADOW_PAD, SHADOW_PAD, SHADOW_PAD + w - 1, SHADOW_PAD + h - 1], fill=255)
alpha = shape.filter(ImageFilter.GaussianBlur(9)).point(lambda v: v * 36 // 100)
shadow = Image.new("RGBA", shape.size, (60, 45, 30, 0))
shadow.putalpha(alpha)
path = os.path.join(out, "letter-shadow.png")
shadow.save(path, optimize=True)
print(f"letter-shadow.png: {shadow.width}x{shadow.height}, {os.path.getsize(path) // 1024} KB")
