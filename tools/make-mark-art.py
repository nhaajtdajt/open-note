"""
Builds the flowers a note can be marked with instead of the star (the star's picker in the
note's top-left corner): cuts each out of its background, centres it on a square and sizes it.

    python tools/make-mark-art.py <folder with 11.webp ... 16.webp> [output folder]

Output (default src/OpenNote/Assets/marks): pink.png, yellow.png, blue.png, red.png, daisy.png,
clover.png, 96x96 each (the mark is drawn at about 30 px, so this stays sharp at 300% scaling).
Needs Pillow, numpy and scipy.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

src = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "src", "OpenNote", "Assets", "marks")
os.makedirs(out, exist_ok=True)
SIZE = 96


def border_connected(mask):
    labels, _ = ndimage.label(mask)
    edge = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    return np.isin(labels, edge[edge > 0])


def cut(name, out_name, lightest):
    """Background = grey or white (a white or "transparent" checkerboard backdrop) touching the edge."""
    a = np.array(Image.open(os.path.join(src, name)).convert("RGB")).astype(np.int16)
    neutral = (a.max(axis=2) - a.min(axis=2)) <= 10
    background = border_connected(neutral & (a.min(axis=2) >= lightest))
    keep = ndimage.binary_erosion(~background, iterations=2)
    labels, count = ndimage.label(keep)
    keep = labels == 1 + np.argmax(ndimage.sum(keep, labels, range(1, count + 1)))  # the flower, not crumbs
    alpha = Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    img = Image.fromarray(a.astype(np.uint8)).convert("RGBA")
    img.putalpha(alpha)
    img = img.crop(img.getbbox())
    side = max(img.size)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(img, ((side - img.width) // 2, (side - img.height) // 2))
    square = square.resize((SIZE, SIZE), Image.LANCZOS)
    square.save(os.path.join(out, out_name), optimize=True)
    print(f"{out_name}: {os.path.getsize(os.path.join(out, out_name)) // 1024} KB")


cut("11.webp", "pink.png", 200)    # pink blossom on a grey checkerboard
cut("12.png", "yellow.png", 200)   # frangipani on a grey checkerboard
cut("13.png", "blue.png", 235)
cut("14.png", "red.png", 235)      # watercolour
cut("15.png", "daisy.png", 250)    # white petals on white: only pure white is background
cut("16.webp", "clover.png", 200)  # watercolour four-leaf clover
