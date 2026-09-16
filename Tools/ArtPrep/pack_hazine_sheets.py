"""Repack the two generated 8-frame bursts ("Hazine" treasure + dynamite) into regular 4x2 sheets.

Usage: python pack_hazine_sheets.py <out dir>   (writes hazine_treasure_sheet.png,
hazine_dynamite_sheet.png and a preview on the board's slate; copy the two sheets to
Assets/Resources/Art/Fx). SRC below points at the generated originals.

The source frames are scattered on one canvas at different sizes, with a fringe of saturated
red/yellow at alpha < 32 and loose specks. Steps:
 1. core mask = alpha >= 48, labelled; the 8 largest components are the frames
 2. every other alpha>0 pixel joins the nearest frame (within reach) or is dropped
 3. fringe: alpha is remapped by a smoothstep (16..56) so the garbage band goes away, and the
    RGB of what is left is pulled toward the frame's own mean core colour where alpha is low
 4. each frame is centred on its BRIGHT CORE (luminance-weighted centroid of the top pixels),
    and ONE scale is used for the whole sheet so the growth is kept
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

CELL = 256
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
SRC = {
    "dynamite": r"C:/Users/yigit/Downloads/ChatGPT Image 16 Eyl 2026 17_28_49.png",
    "treasure": r"C:/Users/yigit/Downloads/ChatGPT Image 16 Eyl 2026 17_32_56.png",
}


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def frames_of(path):
    img = np.array(Image.open(path).convert("RGBA")).astype(np.float64)
    al = img[..., 3]
    core = al >= 48
    core = ndi.binary_opening(core, iterations=2)
    lab, n = ndi.label(core)
    sizes = ndi.sum(core, lab, range(1, n + 1))
    order = np.argsort(sizes)[::-1][:8]
    ids = [int(i) + 1 for i in order]
    # nearest frame for every pixel, limited reach
    dist = np.full(al.shape, np.inf)
    owner = np.zeros(al.shape, int)
    for k, i in enumerate(ids):
        d = ndi.distance_transform_edt(lab != i)
        closer = d < dist
        dist[closer] = d[closer]
        owner[closer] = k
    keep = (al > 0) & (dist <= 24)
    out = []
    for k, i in enumerate(ids):
        m = keep & (owner == k)
        ys, xs = np.nonzero(m)
        cm = lab == i
        ly, lx = np.nonzero(cm)
        # reading order key: row by centre y band, then x
        out.append(dict(mask=m, core=cm, cy=ly.mean(), cx=lx.mean(),
                        box=(ys.min(), ys.max() + 1, xs.min(), xs.max() + 1), area=cm.sum()))
    # rows: split by median centre y
    ymid = np.median([f["cy"] for f in out])
    out.sort(key=lambda f: (f["cy"] > ymid, f["cx"]))
    return img, out


def clean(img, f, warm_only=False):
    y0, y1, x0, x1 = f["box"]
    pad = 8
    y0, x0 = max(0, y0 - pad), max(0, x0 - pad)
    y1, x1 = min(img.shape[0], y1 + pad), min(img.shape[1], x1 + pad)
    px = img[y0:y1, x0:x1].copy()
    m = f["mask"][y0:y1, x0:x1]
    a = px[..., 3] * m
    a = a * smooth(16, 56, a)
    # drop specks: components of the kept alpha smaller than 30 px far from the core
    solid = a > 8
    lab, n = ndi.label(solid)
    if n > 1:
        sz = ndi.sum(solid, lab, range(1, n + 1))
        big = np.argmax(sz) + 1
        small = np.isin(lab, [j + 1 for j in range(n) if sz[j] < 60 and j + 1 != big])
        a[small] = 0
    rgb = px[..., :3]
    if warm_only:
        # treasure is gold through and through: a cold/grey texel is generator dirt
        lum0 = 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]
        cold = (rgb[..., 0] - rgb[..., 2] < 70) & (lum0 < 225)
        a = np.where(cold, a * 0.0, a)
    hi = a >= 150
    mean = rgb[hi].mean(0) if hi.any() else rgb[a > 0].mean(0)
    # low-alpha colour -> nearest high-alpha colour (dilated), blended by alpha
    idx = ndi.distance_transform_edt(~hi, return_distances=False, return_indices=True)
    near = rgb[idx[0], idx[1]]
    w = smooth(40, 150, a)[..., None]
    rgb = rgb * w + near * (1 - w)
    if warm_only:
        # faint ivory over a dark board reads as cold grey haze: faint texels wear the frame's
        # own gold, and white survives only where it is opaque
        warm = (rgb[..., 0] - rgb[..., 2] > 90) & (a > 120)
        gold = rgb[warm].mean(0) if warm.any() else np.array([250.0, 185.0, 70.0])
        k = smooth(60, 170, a)[..., None]
        rgb = rgb * k + gold * (1 - k)
    # bright core centre: luminance-weighted centroid of the brightest 3%
    lum = (0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]) * (a / 255)
    thr = np.percentile(lum[a > 0], 97)
    sel = lum >= thr
    yy, xx = np.nonzero(sel)
    wts = lum[sel]
    cy = (yy * wts).sum() / wts.sum()
    cx = (xx * wts).sum() / wts.sum()
    # radius needed around that centre
    ys, xs = np.nonzero(a > 4)
    r = np.sqrt(((ys - cy) ** 2 + (xs - cx) ** 2)).max()
    return np.dstack([rgb, a]), (cy, cx), r


def pack(name):
    img, fs = frames_of(SRC[name])
    parts = [clean(img, f, name == 'treasure') for f in fs]
    rmax = max(p[2] for p in parts)
    scale = (CELL / 2 - 3) / rmax
    sheet = np.zeros((CELL * 2, CELL * 4, 4))
    report = []
    for k, (px, (cy, cx), r) in enumerate(parts):
        h, w = px.shape[:2]
        # premultiply before resampling so the edge does not drag in dark rgb
        pm = px.copy()
        pm[..., :3] *= pm[..., 3:4] / 255
        nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
        im = Image.fromarray(np.clip(pm, 0, 255).astype(np.uint8), "RGBA")
        chans = [np.array(Image.fromarray(np.clip(pm[..., c], 0, 255).astype(np.uint8)).resize((nw, nh), Image.LANCZOS)).astype(float) for c in range(4)]
        sm = np.dstack(chans)
        ox = round(CELL / 2 - cx * scale)
        oy = round(CELL / 2 - cy * scale)
        col, row = k % 4, k // 4
        cell = np.zeros((CELL, CELL, 4))
        ys0, xs0 = max(0, oy), max(0, ox)
        ys1, xs1 = min(CELL, oy + nh), min(CELL, ox + nw)
        cell[ys0:ys1, xs0:xs1] = sm[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox]
        a = cell[..., 3:4]
        rgb = np.where(a > 0, cell[..., :3] * 255 / np.maximum(a, 1e-6), 0)
        cell = np.dstack([np.clip(rgb, 0, 255), a])
        # hard-zero the outermost 2px so bilinear never bleeds into the next frame
        cell[:2], cell[-2:], cell[:, :2], cell[:, -2:] = 0, 0, 0, 0
        sheet[row * CELL:(row + 1) * CELL, col * CELL:(col + 1) * CELL] = cell
        cov = (cell[..., 3] > 24).mean()
        report.append((k, round(r * scale / (CELL / 2), 3), round(cov, 3)))
    out = Image.fromarray(np.clip(sheet, 0, 255).astype(np.uint8), "RGBA")
    path = f"{OUT}/hazine_{name}_sheet.png"
    out.save(path)
    print(name, "frames", len(parts), "scale", round(scale, 3), "(frame, reach/half-cell, coverage)", report)
    return sheet


def preview(sheets):
    bg = np.array([0.105, 0.115, 0.14]) * 255
    rows = []
    for s in sheets:
        a = s[..., 3:4] / 255
        rows.append(s[..., :3] * a + bg * (1 - a))
    Image.fromarray(np.vstack(rows).astype(np.uint8)).save(f"{OUT}/hazine_sheets_preview.png")


if __name__ == "__main__":
    preview([pack("treasure"), pack("dynamite")])
