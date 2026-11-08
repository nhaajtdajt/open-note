"""
Builds the pictures of the how-to card (Ctrl+Alt+/) from the source artwork:
cuts each picture out of its background, crops it and sizes it for the card
(2x its size on the card, for sharp edges on high-DPI screens).

    python tools/make-help-art.py <folder with 1.webp ... 9.webp> [output folder]

Output (default src/OpenNote/Assets/help): paper.png, daisies.png, bag.png,
envelope.png, bouquet.png, heart.png, title.png. (The letter in the bag: make-letter-art.py.)
Needs Pillow, numpy and scipy. The title is drawn with Windows' Ink Free font
and baked into a picture, so it looks the same on every PC.
"""
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage

src = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "src", "OpenNote", "Assets", "help")
os.makedirs(out, exist_ok=True)


def load(name):
    return np.array(Image.open(os.path.join(src, name)).convert("RGB")).astype(np.int16)


def border_connected(mask):
    """The parts of a background mask that touch the picture's edge (so look-alike pixels inside stay)."""
    labels, _ = ndimage.label(mask)
    edge = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    return np.isin(labels, edge[edge > 0])


def soft_alpha(background, shrink=1, blur=0.8):
    """Opaque where it isn't background, eaten in by a pixel and softened, so no light halo is left."""
    keep = ~background
    if shrink:
        keep = ndimage.binary_erosion(keep, iterations=shrink)
    alpha = Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur))
    return np.array(alpha)


def save(rgb, alpha, name, width=None, crop=True):
    img = Image.fromarray(np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha]), "RGBA")
    if crop:
        img = img.crop(img.getbbox())
    if width and img.width != width:
        img = img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)
    path = os.path.join(out, name)
    img.save(path, optimize=True)
    print(f"{name}: {img.width}x{img.height}, {os.path.getsize(path) // 1024} KB")
    return img


# 2: the sheet of handmade paper. The scan's background is a cool grey, the paper warm:
# anything not warmer than the grey, touching the edge, goes. Turned a quarter so the card is wide.
a = load("2.webp")
warmth = a[..., 0] - a[..., 2]
alpha = soft_alpha(border_connected(warmth < 4), shrink=1, blur=0.7)
paper = save(a, alpha, "paper.png", crop=True)
Image.open(os.path.join(out, "paper.png")).rotate(-90, expand=True).save(os.path.join(out, "paper.png"), optimize=True)

# 1: the daisy field. Its empty part is pure white; the petals never quite are.
a = load("1.webp")
alpha = soft_alpha(border_connected(a.min(axis=2) >= 245), shrink=2, blur=0.8)
save(a, alpha, "daisies.png", width=440, crop=False)

# 4: the hanging tote bag of flowers, on a flat light grey.
a = load("4.png")
bg = np.array([244, 243, 239])
alpha = soft_alpha(border_connected(np.abs(a - bg).max(axis=2) <= 7), shrink=1, blur=0.8)
save(a, alpha, "bag.png", width=340)

# 6: the kraft envelope with its card, on white.
a = load("6.webp")
alpha = soft_alpha(border_connected(a.min(axis=2) >= 250), shrink=1, blur=0.8)
save(a, alpha, "envelope.png", width=340)

# 8: the painted heart, on cream.
a = load("8.png")
bg = np.array([247, 239, 226])
alpha = soft_alpha(border_connected(np.abs(a - bg).max(axis=2) <= 14), shrink=1, blur=0.8)
save(a, alpha, "heart.png", width=180)

# 9: the daisy bouquet for the bottom-left corner. The picture came already cut out, with a dark
# brown outline about 13 px wide drawn around it: that band is trimmed off, along with dark gaps
# it left between petals near the edge, white holes inside, and crumbs (a stray bud). Mirrored so
# the flowers cut off by the picture's edge sit against the corner of the card.
a = load("9.webp")
lum = 0.3 * a[..., 0] + 0.59 * a[..., 1] + 0.11 * a[..., 2]
white = a.min(axis=2) >= 246
labels, count = ndimage.label(white)
sizes = ndimage.sum(white, labels, range(1, count + 1))
holes = np.isin(labels, 1 + np.nonzero(sizes > 300)[0])  # big white patches inside count as background too
background = border_connected(white) | holes
dist = ndimage.distance_transform_edt(~background)
gone = dist <= 13
labels, _ = ndimage.label(gone | ((lum < 75) & (dist <= 32)))
outside = np.unique(labels[gone])
keep = ~np.isin(labels, outside[outside > 0])
keep = ndimage.binary_opening(keep, structure=np.ones((5, 5)))
labels, count = ndimage.label(keep)
keep = labels == 1 + np.argmax(ndimage.sum(keep, labels, range(1, count + 1)))
alpha = np.array(Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.0)))
img = Image.fromarray(np.dstack([a.astype(np.uint8), alpha]), "RGBA").transpose(Image.FLIP_LEFT_RIGHT)
save(np.array(img)[..., :3], np.array(img)[..., 3], "bouquet.png", width=440)

# The title, written like a quick red marker note (mixed capitals, letters a little off the
# line), baked so it doesn't depend on a font being installed.
random.seed(7)
font_path = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "Inkfree.ttf")
text = "OPeN NoTe"
size = 132
canvas = Image.new("RGBA", (1100, 260), (0, 0, 0, 0))
x = 20
red = (196, 34, 38, 255)
for ch in text:
    if ch == " ":
        x += size * 0.32
        continue
    glyph_size = size if ch.isupper() else int(size * 0.86)
    font = ImageFont.truetype(font_path, glyph_size)
    box = font.getbbox(ch)
    gw, gh = box[2] - box[0] + 24, box[3] + 40
    glyph = Image.new("RGBA", (gw, gh), (0, 0, 0, 0))
    ImageDraw.Draw(glyph).text((12 - box[0], 12), ch, font=font, fill=red, stroke_width=3, stroke_fill=red)
    glyph = glyph.rotate(random.uniform(-6, 6), resample=Image.BICUBIC, expand=True)
    y = 40 + random.uniform(-7, 7) + (size - glyph_size) * 0.9
    canvas.alpha_composite(glyph, (int(x), int(y)))
    x += box[2] - box[0] + size * 0.05
canvas = canvas.crop(canvas.getbbox())
canvas = canvas.resize((520, round(canvas.height * 520 / canvas.width)), Image.LANCZOS)
canvas.save(os.path.join(out, "title.png"), optimize=True)
print(f"title.png: {canvas.width}x{canvas.height}")
