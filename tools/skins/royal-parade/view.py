"""Six frames of Royal Parade over the donkey, zoomed: python view.py out\\look.png bdy_static_left bdy_walk_left_03 ..."""
import os, sys
from PIL import Image
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "donkey"))
from contact import load
import make_royal as R
from zoom import zoom
items = []
for k in sys.argv[2:]:
    d = load(k); f = d.copy(); f.alpha_composite(R.caparison(k, d)); f.alpha_composite(R.crown(k)); items.append((k, f))
zoom(items, sys.argv[1], S=6)
