import sys
from PIL import Image
from contact import load
import make_royal as R
from zoom import zoom
items = []
for k in sys.argv[2:]:
    d = load(k); f = d.copy(); f.alpha_composite(R.caparison(k, d)); f.alpha_composite(R.crown(k)); items.append((k, f))
zoom(items, sys.argv[1], S=6)
