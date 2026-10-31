"""A Steam Workshop preview picture for an item, from a picture in media/: media/workshop/<Item>/Thumbnail.jpg.

    python tools/workshop-thumbnail.py <picture> <Item> [--width W] [--nearest]

<Item> is the item's folder name as tools/make-release-zip.py stages it (GK2SaddleUp, GK2SaddleUpDragonsMountPack,
GK2SaddleUpDonkeySkinPack). The picture is scaled to --width (default 1024; never upscaled above 2x) and written as a
JPEG under 1 MB (Steam's limit for the preview); --nearest keeps pixel art crisp when it is scaled up by a whole
number. Give it a square picture cut from a full-size screenshot (`tools/crop-shot.py <shot> <box> -o x.png --width 0`
in GK2QOLMods): Steam shows the preview in square tiles and letterboxes anything else (our first, 16:9 previews had
bars, 2026-10-31), and a 560-wide page picture scaled up comes out blurred. The tool keeps the aspect it is given and
says so when it is not square.
make-release-zip.py copies the file into workshop/<Item>/ as Thumbnail.jpg, which the game's uploader sends as the preview.
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main():
    argv = sys.argv[1:]
    args = [a for k, a in enumerate(argv) if not a.startswith("--") and (k == 0 or argv[k - 1] != "--width")]
    if len(args) != 2:
        sys.exit(__doc__)
    width = 1024
    if "--width" in sys.argv:
        width = int(sys.argv[sys.argv.index("--width") + 1])
    nearest = "--nearest" in sys.argv
    src, item = args
    im = Image.open(src).convert("RGB")
    w, h = im.size
    if w != h:
        print(f"note: {w}x{h} is not square; Steam's tiles will show bars")
    scale = width / w
    if scale > 2:
        scale = 2
    if nearest:
        scale = max(1, int(scale)) if scale >= 1 else scale
    size = (round(w * scale), round(h * scale))
    im = im.resize(size, Image.NEAREST if nearest and scale >= 1 else Image.LANCZOS)
    out_dir = os.path.join(ROOT, "media", "workshop", item)
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "Thumbnail.jpg")
    for quality in (92, 88, 84, 80, 75, 70):
        im.save(out, "JPEG", quality=quality, optimize=True)
        if os.path.getsize(out) < 1_000_000:
            break
    print(f"{os.path.relpath(out, ROOT)}: {size[0]}x{size[1]}, {os.path.getsize(out):,} bytes (from {os.path.relpath(src, ROOT) if os.path.isabs(src) else src} {w}x{h})")


if __name__ == "__main__":
    main()
