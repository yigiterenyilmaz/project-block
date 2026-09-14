# PURPOSE: Turns the prepared Matruşka sheet into the sprites and material masks the game loads.
#
# The art is NOT redrawn here. The sheet holds four finished dolls side by side; this cuts each one
# out along the space between them, finds the seam each one is painted with, and saves:
#   matryoshka_g<N>.png          the whole doll, generation N (1 = largest, as MatruskaBoss counts)
#   matryoshka_g<N>_top.png      the same canvas, only what is ABOVE the seam
#   matryoshka_g<N>_bottom.png   the same canvas, only what is BELOW it
#   matryoshka_g<N>_mask.png     the same canvas: R lacquer, G gold, B face, A seam - what the idle
#                                shader (Resources/Shaders/MatryoshkaIdle) reads to light each part
# All four share one canvas, so the game lines them up just by standing them where the doll stands.
#
# THE SEAM IS TRACED, NOT GUESSED. It is painted as a slightly curved groove, so it is found column by
# column (the darkest thin line across the lower body) and fitted to a smooth curve. The shells are
# cut along that curve with a few pixels of soft alpha, so an opening shell has an anti-aliased edge.
#
# THE MASKS COME FROM THE ART'S OWN COLOURS. Gold is the ornament's hue; the face is everything the
# gold face-frame encloses (skin, hair, eyes, lips, cheeks) so no light ever plays across it; the
# lacquer is the rest of the doll. Each is softened by a pixel so nothing lights up in steps.
#
# It also picks three points ON each doll's gold ornament for the idle glints to land on, and prints
# them - with the seam heights - in the form MatryoshkaView.Style expects.
#
# The dolls are resized together, by one factor, so the sheet's own size differences survive.
#
# Re-run whenever the sheet changes:   python Tools/ArtPrep/matryoshka_sprites.py [sheet.png]
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Matryoshka')
TEMPLATE = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Blocks', 'block_fire.png.meta')
DEFAULT_SHEET = os.path.join(os.path.expanduser('~'), 'Downloads',
                             'ChatGPT Image 10 Eyl 2026 13_50_09.png')

TALLEST = 384      # the largest doll's height in the shipped sprite, px
FEATHER = 3.0      # px of soft alpha across the seam cut, at the shipped size
PAD = 6            # transparent margin kept round each doll in the source, px
SEAM_REACH = 12.0  # px either side of the seam the mask's A channel fades over, at the shipped size


def column_runs(alpha):
    """Runs of columns holding solid pixels - one per doll, split where neighbours touch."""
    solid = (alpha > 128).sum(axis=0) > 0
    runs, x = [], 0
    while x < len(solid):
        if solid[x]:
            start = x
            while x < len(solid) and solid[x]:
                x += 1
            runs.append((start, x))
        x += 1
    return [r for r in runs if r[1] - r[0] > 40]


def vertical_blur(a, size):
    half = size // 2
    padded = np.pad(a, ((half, half), (0, 0)), mode='edge')
    sums = np.vstack([np.zeros((1, a.shape[1])), np.cumsum(padded, axis=0)])
    return (sums[size:] - sums[:-size]) / size


def trace_seam(doll):
    """The painted seam as y = poly(x), in the doll's own pixel coordinates."""
    h, w = doll.shape[:2]
    lum = 0.299 * doll[..., 0] + 0.587 * doll[..., 1] + 0.114 * doll[..., 2]
    ridge = lum - vertical_blur(lum, 15)          # a thin dark line stands out; shading does not
    lo, hi = int(h * 0.50), int(h * 0.80)
    body = np.where((doll[lo:hi, :, 3] > 230).all(axis=0))[0]
    left, right = body[0], body[-1]
    span = right - left
    xs, ys = [], []
    for x in range(int(left + span * 0.08), int(right - span * 0.08)):
        xs.append(x)
        ys.append(lo + int(np.argmin(ridge[lo:hi, x])))
    xs, ys = np.array(xs, float), np.array(ys, float)
    keep = np.ones(len(xs), bool)
    for _ in range(4):
        poly = np.polyfit(xs[keep], ys[keep], 2)
        miss = ys - np.polyval(poly, xs)
        keep = np.abs(miss) < max(2.0, 2.0 * miss[keep].std())
    return np.polyfit(xs[keep], ys[keep], 2), int(keep.sum()), len(xs)


def resize_premultiplied(rgba, width, height):
    """Resized without dark fringes: colour is weighted by alpha while it is filtered."""
    a = rgba[..., 3:4] / 255.0
    stack = np.concatenate([rgba[..., :3] / 255.0 * a, a], axis=2).astype(np.float32)
    channels = [np.asarray(Image.fromarray(stack[..., i], mode='F').resize(
        (width, height), Image.LANCZOS)) for i in range(4)]
    out = np.stack(channels, axis=2)
    alpha = np.clip(out[..., 3], 0, 1)
    colour = np.where(alpha[..., None] > 1e-4, out[..., :3] / np.maximum(alpha[..., None], 1e-4), 0)
    return np.concatenate([np.clip(colour, 0, 1), alpha[..., None]], axis=2) * 255.0


# ------------------------------------------------------------------ masks

def hsv(rgb):
    top, low = rgb.max(axis=2), rgb.min(axis=2)
    span = top - low
    safe = np.maximum(span, 1e-6)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(top == r, ((g - b) / safe) % 6, np.where(top == g, (b - r) / safe + 2, (r - g) / safe + 4))
    hue = np.where(span < 1e-6, 0.0, hue * 60.0)
    sat = np.where(top > 1e-6, span / np.maximum(top, 1e-6), 0.0)
    return hue, sat, top


def as_image(mask):
    return Image.fromarray((mask * 255).astype(np.uint8), 'L')


def dilate(mask, px):
    return np.asarray(as_image(mask).filter(ImageFilter.MaxFilter(2 * px + 1))) > 127


def fill_holes(mask):
    """Everything the mask encloses: what a flood from outside cannot reach."""
    outside = Image.new('L', (mask.shape[1] + 2, mask.shape[0] + 2), 255)
    outside.paste(as_image(~mask), (1, 1))
    ImageDraw.floodfill(outside, (0, 0), 128)
    return mask | (np.asarray(outside)[1:-1, 1:-1] == 255)


def soften(mask, radius=1.0):
    return np.asarray(as_image(mask).filter(ImageFilter.GaussianBlur(radius))) / 255.0


def seeded_region(region, pale):
    """The connected part of region holding the densest pale area - the face."""
    ys, xs = np.where(pale & region)
    if len(xs) == 0:
        return np.zeros_like(region)
    k = int(np.argmax(soften(pale, 4.0)[ys, xs]))
    # A COPY: an image made from an array is read-only, and a flood into it is silently dropped.
    image = as_image(region).copy()
    ImageDraw.floodfill(image, (int(xs[k]), int(ys[k])), 128)
    return np.asarray(image) == 128


def material_mask(whole, seam_rows):
    rgb = whole[..., :3] / 255.0
    solid = whole[..., 3] > 127
    hue, sat, val = hsv(rgb)
    gold = solid & (hue >= 26) & (hue <= 55) & (sat >= 0.35) & (sat <= 0.92) & (val > 0.55)
    pale = solid & (hue <= 45) & (sat < 0.52) & (val > 0.72) & ~gold
    # The face is the ONE region the gold frame encloses round the skin - skin, hair, brows, eyes,
    # cheeks, lips. It is flooded from inside the face itself, so pale pixels elsewhere (the cream
    # light along the collar, a highlight on the hood) are never taken for it.
    enclosed = fill_holes(dilate(gold | pale, 1)) & ~gold
    face = dilate(seeded_region(enclosed, pale), 1) & ~gold & solid
    # The cream light running along the gold lines belongs to the ornament, not to the lacquer.
    gold = gold | (pale & ~face & dilate(gold, 2))
    lacquer = solid & ~gold & ~face
    rows = np.arange(whole.shape[0])[:, None] + 0.5
    seam = np.clip(1.0 - np.abs(rows - seam_rows) / SEAM_REACH, 0.0, 1.0) * lacquer
    out = np.stack([soften(lacquer), soften(gold), soften(face), seam], axis=2)
    return np.clip(out * 255.0, 0, 255), gold


def glint_points(gold, count=3):
    """Points ON the gold ornament, spread across it, favouring where there is enough gold round a
    point for a glint to have something to catch."""
    ys, xs = np.where(gold)
    if len(xs) == 0:
        return [(0.5, 0.5)] * count
    density = soften(gold, 3.0)[ys, xs]
    points = np.stack([xs, ys], axis=1).astype(float)
    chosen = [int(np.argmax(density))]
    while len(chosen) < count:
        nearest = np.min(np.stack([np.hypot(*(points - points[c]).T) for c in chosen]), axis=0)
        chosen.append(int(np.argmax(nearest * np.sqrt(density))))
    h, w = gold.shape
    return [((points[c, 0] + 0.5) / w, 1.0 - (points[c, 1] + 0.5) / h) for c in chosen]


# ------------------------------------------------------------------ files

def save_png(rgba, path):
    Image.fromarray(np.clip(rgba + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(path, optimize=True)


def write_meta(png, guid, sprite_id, pixels_per_unit, data=False):
    text = open(TEMPLATE, encoding='utf-8').read()
    text = re.sub(r'^guid: \w+', 'guid: ' + guid, text, count=1, flags=re.M)
    text = re.sub(r'spritePixelsToUnits: [\d.]+', 'spritePixelsToUnits: %d' % pixels_per_unit, text)
    text = re.sub(r'spriteID: \w+', 'spriteID: ' + sprite_id, text)
    if data:
        # A mask is data, not a picture: linear, not a sprite, and its alpha is a channel of its own
        # (the seam) that must not be treated as transparency.
        text = re.sub(r'sRGBTexture: \d', 'sRGBTexture: 0', text)
        text = re.sub(r'alphaIsTransparency: \d', 'alphaIsTransparency: 0', text)
        text = re.sub(r'textureType: \d+', 'textureType: 0', text)
        text = re.sub(r'spriteMode: \d', 'spriteMode: 0', text)
    open(png + '.meta', 'w', encoding='utf-8', newline='\n').write(text)


def main():
    sheet = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SHEET
    if not os.path.exists(sheet):
        sys.exit('sheet not found: ' + sheet)
    source = np.asarray(Image.open(sheet).convert('RGBA')).astype(np.float32)
    alpha = source[..., 3]
    runs = column_runs(alpha)
    if len(runs) != 4:
        sys.exit('expected 4 dolls on the sheet, found %d' % len(runs))
    bounds = [(runs[i][1] + runs[i + 1][0]) / 2.0 for i in range(len(runs) - 1)]
    centres = np.arange(source.shape[1]) + 0.5

    dolls = []
    for k in range(len(runs)):
        left = bounds[k - 1] if k > 0 else 0.0
        right = bounds[k] if k < len(runs) - 1 else float(source.shape[1])
        own = source.copy()
        own[:, (centres < left) | (centres >= right), 3] = 0     # nothing of a neighbour
        ys, xs = np.where(own[..., 3] > 8)
        x0, x1 = max(0, xs.min() - PAD), min(source.shape[1], xs.max() + 1 + PAD)
        y0, y1 = max(0, ys.min() - PAD), min(source.shape[0], ys.max() + 1 + PAD)
        dolls.append(own[y0:y1, x0:x1])

    scale = TALLEST / float(max(d.shape[0] for d in dolls))
    os.makedirs(OUT, exist_ok=True)
    print('scale %.4f   (largest doll -> %d px tall)' % (scale, TALLEST))
    seams, anchors = [], []
    for k, doll in enumerate(dolls):
        generation = k + 1
        poly, inliers, samples = trace_seam(doll)
        h, w = doll.shape[:2]
        H, W = max(1, int(round(h * scale))), max(1, int(round(w * scale)))
        whole = resize_premultiplied(doll, W, H)
        rows, cols = np.mgrid[0:H, 0:W]
        seam_rows = scale * np.polyval(poly, (cols + 0.5) / scale)
        below = rows + 0.5 - seam_rows
        top = whole.copy()
        top[..., 3] *= np.clip((FEATHER / 2 - below) / FEATHER, 0, 1)
        bottom = whole.copy()
        bottom[..., 3] *= np.clip((below + FEATHER / 2) / FEATHER, 0, 1)
        mask, gold = material_mask(whole, seam_rows[0:1, :].repeat(H, axis=0))
        for variant, (suffix, image, data) in enumerate([('', whole, False), ('_top', top, False),
                                                         ('_bottom', bottom, False), ('_mask', mask, True)]):
            name = 'matryoshka_g%d%s.png' % (generation, suffix)
            path = os.path.join(OUT, name)
            save_png(image, path)
            ident = '%02x%02x' % (generation, variant)
            write_meta(path, '3a7f' + '0' * 24 + ident, '6d21' + '0' * 24 + ident, H, data)
        mid = scale * np.polyval(poly, (w / 2.0))
        seams.append(1.0 - mid / H)
        anchors.append(glint_points(gold))
        coverage = mask[..., :3].reshape(-1, 3)[whole[..., 3].reshape(-1) > 127].mean(axis=0) / 255.0
        print('g%d  %dx%d   seam %.3f from the bottom   fit %d/%d   lacquer %.0f%%  gold %.1f%%  face %.0f%%'
              % (generation, W, H, seams[-1], inliers, samples,
                 100 * coverage[0], 100 * coverage[1], 100 * coverage[2]))

    print()
    print('SeamHeight   = { %s };' % ', '.join('%.3ff' % s for s in seams))
    print('GlintAnchors =')
    for points in anchors:
        print('    ' + ', '.join('new Vector2(%.3ff, %.3ff)' % p for p in points) + ',')


if __name__ == '__main__':
    main()
