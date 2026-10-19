import sys
from PIL import Image, ImageDraw
from contact import load
def zoom(keys, out, S=8, box=(18, 14, 86, 68)):
    x0, y0, x1, y1 = box; W, H = (x1 - x0) * S, (y1 - y0) * S
    sheet = Image.new("RGBA", (W * len(keys), H + 20), (40, 40, 44, 255)); d = ImageDraw.Draw(sheet)
    for n, k in enumerate(keys):
        im = load(k) if isinstance(k, str) else k[1]
        name = k if isinstance(k, str) else k[0]
        c = im.crop(box).resize((W, H), Image.NEAREST); ox = n * W
        sheet.alpha_composite(c, (ox, 20))
        for x in range(x0, x1):
            if x % 5 == 0: d.line([(ox + (x - x0) * S, 20), (ox + (x - x0) * S, 20 + H)], fill=(90, 90, 100, 255) if x % 10 else (150, 150, 60, 255))
        for y in range(y0, y1):
            if y % 5 == 0: d.line([(ox, 20 + (y - y0) * S), (ox + W, 20 + (y - y0) * S)], fill=(90, 90, 100, 255) if y % 10 else (150, 150, 60, 255))
        d.text((ox + 4, 4), f"{name}  (yellow lines every 10: x from {x0}, y from {y0})", fill=(255, 255, 0, 255))
    sheet.save(out)
if __name__ == "__main__":
    zoom(sys.argv[2:], sys.argv[1])
