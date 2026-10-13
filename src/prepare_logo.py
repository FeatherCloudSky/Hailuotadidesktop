"""Package a rendering of the hand-drawn SVG logo as Windows icon sizes."""
from pathlib import Path
from PIL import Image
import json
import sys

root = Path(__file__).resolve().parents[1]
(root / 'checks').mkdir(exist_ok=True)
logo = Image.open(sys.argv[1]).convert('RGBA')
assert logo.width == logo.height, 'App logo must be square'
assert logo.getpixel((0, 0))[3] < 20, 'Logo outer corners must be transparent'
assert logo.getpixel((logo.width // 2, logo.height // 2))[3] > 200, 'Logo center must be solid'
logo.save(root / 'assets' / 'logo.png')
logo.save(root / 'assets' / 'pet.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
icon = Image.open(root / 'assets' / 'pet.ico')
report = {'logo_size': logo.size, 'alpha_range': logo.getchannel('A').getextrema(), 'icon_sizes': sorted(icon.ico.sizes())}
assert (16,16) in icon.ico.sizes() and (256,256) in icon.ico.sizes()
(root / 'checks' / 'logo.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
