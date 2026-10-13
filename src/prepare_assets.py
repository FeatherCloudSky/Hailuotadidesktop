"""Split the generated atlas without repainting or changing its alpha channel."""
from pathlib import Path
from PIL import Image
import json
import sys

root = Path(__file__).resolve().parents[1]
(root / 'checks').mkdir(exist_ok=True)
atlas = Image.open(sys.argv[1]).convert('RGBA')
atlas.save(root / 'assets' / 'character-atlas.png')
w, h = atlas.size
assert w % 2 == 0 and h % 2 == 0
report = {}
for i, name in enumerate(('idle', 'happy', 'pressed', 'think')):
    x, y = i % 2 * (w // 2), i // 2 * (h // 2)
    sprite = atlas.crop((x, y, x + w // 2, y + h // 2))
    alpha = sprite.getchannel('A')
    histogram = alpha.histogram()
    assert histogram[0] > sprite.width * sprite.height * .25, name
    assert sum(histogram[200:]) > sprite.width * sprite.height * .15, name
    assert sprite.getpixel((0, 0))[3] == 0, name
    sprite.save(root / 'assets' / (name + '.png'))
    report[name] = {'size': sprite.size, 'alpha_range': alpha.getextrema(), 'transparent_pixels': histogram[0]}
icon = atlas.crop((155, 4, 370, 219))
icon.save(root / 'assets' / 'character.ico', sizes=[(16,16), (32,32), (48,48), (64,64), (128,128), (256,256)])
(root / 'checks' / 'assets.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
