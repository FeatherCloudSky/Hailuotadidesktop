"""Clean the existing atlas masks locally; leave the source atlas unchanged.

Remove low-alpha background and disconnected fragments from neighbouring cells.
The dominant connected component is the character; no image model is used.
"""
from pathlib import Path
from PIL import Image
import json

root = Path(__file__).resolve().parents[1]
(root / 'checks').mkdir(exist_ok=True)
atlas = Image.open(root / 'assets' / 'character-atlas.png').convert('RGBA')
width, height = atlas.width // 2, atlas.height // 2
report = {}

def components(mask, w, h):
    seen = bytearray(w * h)
    found = []
    for seed in range(w * h):
        if not mask[seed] or seen[seed]:
            continue
        seen[seed] = 1
        pending = [seed]
        cursor = 0
        while cursor < len(pending):
            current = pending[cursor]
            cursor += 1
            y, x = divmod(current, w)
            for ny in range(max(0, y - 1), min(h, y + 2)):
                for nx in range(max(0, x - 1), min(w, x + 2)):
                    neighbour = ny * w + nx
                    if mask[neighbour] and not seen[neighbour]:
                        seen[neighbour] = 1
                        pending.append(neighbour)
        found.append(pending)
    return sorted(found, key=len, reverse=True)

for index, name in enumerate(('idle', 'happy', 'pressed', 'think')):
    x, y = index % 2 * width, index // 2 * height
    original = atlas.crop((x, y, x + width, y + height))
    alpha = original.getchannel('A').tobytes()
    mask = bytearray(value >= 32 for value in alpha)
    groups = components(mask, width, height)
    assert groups and len(groups[0]) > sum(mask) * .95, (name, [len(c) for c in groups[:5]])
    keep = bytearray(width * height)
    for pixel in groups[0]:
        keep[pixel] = 1
    # Retain antialiasing at the contour, make the interior fully opaque.
    cleaned_alpha = bytes(min(255, round((value - 32) * 255 / 204)) if keep[i] else 0
                          for i, value in enumerate(alpha))
    cleaned = original.copy()
    cleaned.putalpha(Image.frombytes('L', (width, height), cleaned_alpha))
    cleaned.save(root / 'assets' / (name + '.png'))
    for label, image in (('before', original), ('after', cleaned)):
        for background in ('white', '#1d1f28'):
            proof = Image.new('RGBA', image.size, background)
            proof.alpha_composite(image)
            suffix = 'white' if background == 'white' else 'dark'
            proof.convert('RGB').save(root / 'checks' / (name + '-' + label + '-' + suffix + '.png'))
    # Recheck the final silhouette rather than assuming the mask was applied.
    final_mask = bytearray(v > 0 for v in cleaned_alpha)
    final_groups = components(final_mask, width, height)
    assert len(final_groups) == 1, (name, len(final_groups))
    report[name] = {'kept_character_pixels': len(groups[0]),
                    'removed_components': len(groups) - 1,
                    'removed_fragment_pixels': sum(len(c) for c in groups[1:]),
                    'largest_removed_fragment': max([len(c) for c in groups[1:]] or [0]),
                    'alpha_range': cleaned.getchannel('A').getextrema(),
                    'final_connected_components': len(final_groups)}
(root / 'checks' / 'mask-cleanup.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
