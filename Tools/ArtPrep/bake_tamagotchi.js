// PURPOSE: Bakes the "Tamagotchi" boss's LAYERED character art - the body, belly, ears, paws,
// feet, eyes (iris, catchlight, lids, happy arc, glow), brows, cheeks, nine mouths, the tongue,
// the shadow and the effect sprites - into Assets/Resources/Art/Tamagotchi as PNGs with their
// Unity .meta files. Run with Node:  node Tools/ArtPrep/bake_tamagotchi.js [previewDir]
//
// WHY A BAKER AND NOT A RUNTIME GENERATOR: the brief asks for a layered character ASSET whose
// animator survives the art being replaced. These PNGs are that asset: a painter can overwrite any
// of them (same name, same pivot, same scale) and TamagotchiArt / TamagotchiRig never notice. Until
// then they are drawn here, deterministically, from signed distance fields - soft AA edges, the
// light from the upper left like every tile in the game, berry/plum in the shade, peach in the
// light, a warm raspberry where the light wraps round the edge, and a faint plush grain - so the
// character is a soft matte toy rather than a flat pink disc.
//
// UNITS: one unit is the body's width. Every layer is rendered at PPU pixels per unit around its
// own pivot, and the pivot is written into the .meta (and mirrored in TamagotchiArt.cs), so the
// rig places layers in these same units.
//
// With a preview directory it also composes the rig in a few expressions and writes PNGs there -
// which is how the look is judged before Unity ever sees it.

'use strict';
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const crypto = require('crypto');

const OUT = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'Art', 'Tamagotchi');
const META_TEMPLATE = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'Art', 'Blocks', 'snake_head.png.meta');
const PPU = 400;

// ------------------------------------------------------------------ palette (sRGB 0..255)
const C = {
    plum: [104, 40, 72],
    berry: [150, 58, 96],
    raspberry: [204, 92, 126],
    dusty: [236, 154, 172],
    rose: [246, 186, 196],
    peach: [255, 214, 204],
    shine: [255, 236, 226],
    lineInk: [128, 46, 80],
    irisTop: [34, 14, 30],
    irisBottom: [82, 36, 68],
    irisReflect: [128, 62, 102],
    cream: [255, 246, 230],
    mouthDeep: [74, 20, 44],
    mouthIn: [112, 34, 62],
    tongue: [238, 118, 148],
    tongueDark: [204, 84, 118],
    tongueLight: [252, 168, 186],
    tooth: [255, 248, 240],
    plate: [253, 240, 238],
    plateRim: [138, 48, 86],
    ground: [38, 42, 58],
    groundLit: [74, 82, 108],
    groundDark: [22, 24, 34]
};

// ------------------------------------------------------------------ math
const clamp01 = v => v < 0 ? 0 : v > 1 ? 1 : v;
const lerp = (a, b, t) => a + (b - a) * t;
const mix = (a, b, t) => [lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)];
const smooth = (e0, e1, x) => { const t = clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); };
const len = (x, y) => Math.sqrt(x * x + y * y);
function smin(a, b, k) { const h = clamp01(0.5 + 0.5 * (b - a) / k); return lerp(b, a, h) - k * h * (1 - h); }
function sdEllipse(x, y, cx, cy, rx, ry) { const dx = (x - cx) / rx, dy = (y - cy) / ry; return (len(dx, dy) - 1) * Math.min(rx, ry); }
function sdCircle(x, y, cx, cy, r) { return len(x - cx, y - cy) - r; }
function sdCapsule(x, y, ax, ay, bx, by, r) {
    const pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
    const h = clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
    return len(pax - bax * h, pay - bay * h) - r;
}
function sdRoundCone(x, y, r1, r2, h) {
    // iq: a cone from (0,0) radius r1 to (0,h) radius r2
    const qx = Math.abs(x), qy = y;
    const b = (r1 - r2) / h, a = Math.sqrt(1 - b * b);
    const k = -b * qx + a * qy;   // note: dot(q, vec2(-b, a))
    if (k < 0) return len(qx, qy) - r1;
    if (k > a * h) return len(qx, qy - h) - r2;
    return qx * a + qy * b - r1;
}
// distance to a polyline sampled from a function, as a stroke of radius r
function sdCurve(x, y, f, x0, x1, r, n) {
    let best = 1e9; let px = x0, py = f(x0);
    for (let i = 1; i <= n; i++) {
        const cx = lerp(x0, x1, i / n), cy = f(cx);
        best = Math.min(best, sdCapsule(x, y, px, py, cx, cy, 0) );
        px = cx; py = cy;
    }
    return best - r;
}
function hash(x, y) { let h = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return h - Math.floor(h); }
function noise(x, y) {
    const ix = Math.floor(x), iy = Math.floor(y), fx = x - ix, fy = y - iy;
    const ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
    return lerp(lerp(hash(ix, iy), hash(ix + 1, iy), ux), lerp(hash(ix, iy + 1), hash(ix + 1, iy + 1), ux), uy);
}
const PX = 1 / PPU;
const cover = d => clamp01(0.5 - d / PX);

// ------------------------------------------------------------------ canvas
function canvas(w, h) { return { w, h, d: new Float32Array(w * h * 4) }; }
function over(cv, i, rgb, a) {
    if (a <= 0) return;
    const d = cv.d, k = i * 4, da = d[k + 3], oa = a + da * (1 - a);
    if (oa <= 0) return;
    for (let c = 0; c < 3; c++) d[k + c] = (rgb[c] * a + d[k + c] * da * (1 - a)) / oa;
    d[k + 3] = oa;
}
// Renders a layer: (w,h) pixels, pivot in pixels from the bottom-left, fn(x,y) in units -> [rgb, a]
function layer(name, wPx, hPx, pivotPx, fn) {
    const cv = canvas(wPx, hPx);
    for (let py = 0; py < hPx; py++) {
        for (let px = 0; px < wPx; px++) {
            const x = (px + 0.5 - pivotPx[0]) / PPU, y = (py + 0.5 - pivotPx[1]) / PPU;
            const r = fn(x, y);
            if (r && r[1] > 0) over(cv, py * wPx + px, r[0], r[1]);
        }
    }
    cv.name = name; cv.pivot = pivotPx;
    return cv;
}
function writePng(cv, file) {
    const w = cv.w, h = cv.h, raw = Buffer.alloc((w * 4 + 1) * h);
    for (let y = 0; y < h; y++) {
        const row = (h - 1 - y);
        raw[y * (w * 4 + 1)] = 0;
        for (let x = 0; x < w; x++) {
            const k = (row * w + x) * 4, o = y * (w * 4 + 1) + 1 + x * 4;
            for (let c = 0; c < 3; c++) raw[o + c] = Math.round(Math.max(0, Math.min(255, cv.d[k + c])));
            raw[o + 3] = Math.round(clamp01(cv.d[k + 3]) * 255);
        }
    }
    const crcT = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crcT[n] = c >>> 0; }
    const crc = b => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
    const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
    const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 6;
    fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih),
        chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]));
}
function writeMeta(cv, file) {
    const metaPath = file + '.meta';
    let guid;
    if (fs.existsSync(metaPath)) {
        const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(metaPath, 'utf8'));
        guid = m ? m[1] : null;
    }
    if (!guid) guid = crypto.createHash('md5').update('tamagotchi/' + cv.name).digest('hex');
    let t = fs.readFileSync(META_TEMPLATE, 'utf8');
    t = t.replace(/guid: [0-9a-f]{32}/, 'guid: ' + guid);
    t = t.replace(/spritePivot: \{x: [^,]+, y: [^}]+\}/, 'spritePivot: {x: ' + (cv.pivot[0] / cv.w).toFixed(6) + ', y: ' + (cv.pivot[1] / cv.h).toFixed(6) + '}');
    t = t.replace(/spritePixelsToUnits: \d+/, 'spritePixelsToUnits: ' + PPU);
    t = t.replace(/spriteID: [0-9a-f]+/, 'spriteID: ' + crypto.createHash('md5').update('sprite/' + cv.name).digest('hex'));
    fs.writeFileSync(metaPath, t);
}

// ------------------------------------------------------------------ the soft-toy material
// Lights a shape given its SDF the way the game lights its tiles: from the upper left, a dome of a
// normal from the distance field, a wrapped diffuse ramp plum -> raspberry -> dusty pink -> rose, a
// soft peach specular, a pale rim on the lit edge, a warm raspberry where the light wraps round the
// shadow edge (the gummy part), an inked inner edge, and plush grain.
function toyShade(sdf, x, y, depth, opts) {
    opts = opts || {};
    const d = sdf(x, y);
    const a = cover(d);
    if (a <= 0) return null;
    const e = 0.004;
    let gx = sdf(x + e, y) - sdf(x - e, y), gy = sdf(x, y + e) - sdf(x, y - e);
    const gl = len(gx, gy) || 1; gx /= gl; gy /= gl;
    const s = clamp01(-d / depth);
    const nz = Math.sqrt(1 - (1 - s) * (1 - s));
    let nx = gx * (1 - s), ny = gy * (1 - s);
    const nl = Math.sqrt(nx * nx + ny * ny + nz * nz); nx /= nl; ny /= nl; const nzz = nz / nl;
    const L = [-0.46, 0.58, 0.67], Ll = len3(L);
    const dot = (nx * L[0] + ny * L[1] + nzz * L[2]) / Ll;
    const wrap = clamp01((dot + 0.4) / 1.4);
    let col = wrap < 0.35 ? mix(C.plum, C.raspberry, wrap / 0.35)
        : wrap < 0.68 ? mix(C.raspberry, C.dusty, (wrap - 0.35) / 0.33)
        : mix(C.dusty, C.rose, (wrap - 0.68) / 0.32);
    if (opts.tint) col = mix(col, opts.tint, opts.tintAmount || 0.5);
    // the light wrapping round the shadow edge: warm, a little saturated - the gummy toy
    const rimDirShadow = clamp01(gx * 0.55 + gy * -0.83);
    col = mix(col, C.raspberry, (1 - nzz) * (1 - nzz) * rimDirShadow * 0.55);
    // a pale rim where the light catches the edge
    const rimDirLit = clamp01(gx * -0.62 + gy * 0.78);
    col = mix(col, C.peach, Math.pow(1 - nzz, 3) * rimDirLit * 0.6);
    // soft specular
    const hx = L[0] / Ll, hy = L[1] / Ll, hz = L[2] / Ll + 1, hl = len3([hx, hy, hz]);
    const spec = Math.pow(clamp01((nx * hx + ny * hy + nzz * hz) / hl), opts.specPow || 26) * (opts.spec || 0.42);
    col = mix(col, C.shine, spec);
    // contact occlusion toward the base
    if (opts.aoFloor !== undefined) col = mix(col, C.plum, (1 - smooth(opts.aoFloor, opts.aoFloor + 0.2, y)) * 0.32);
    // plush grain, very faint
    const g = (noise(x * 90, y * 90) - 0.5) * 0.05 + (noise(x * 14 + 7, y * 14) - 0.5) * 0.04;
    col = col.map(v => v * (1 + g));
    // inked inner edge - a soft darker line, not a black outline
    const ink = clamp01(1 - (-d) / (opts.inkWidth || 2.4 * PX));
    col = mix(col, C.lineInk, ink * (opts.ink === undefined ? 0.75 : opts.ink));
    return [col, a];
}
function len3(v) { return Math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]); }

// ------------------------------------------------------------------ the character, in units
// Body: a pear / mochi - a wide low ellipse melted into a narrower head ellipse, the base a little
// flattened where it sits. One unit wide, ~1.04 tall, pivot at the middle of the base.
const bodySdf = (x, y) => {
    const low = sdEllipse(x, y, 0, 0.36, 0.5, 0.36);
    const high = sdEllipse(x, y, 0, 0.7, 0.37, 0.34);
    return Math.max(smin(low, high, 0.16), 0.012 - y);
};
const bellySdf = (x, y) => sdEllipse(x, y, 0, 0, 0.29, 0.22);
const earSdf = (x, y) => sdRoundCone(x, y, 0.048, 0.066, 0.085);
const pawSdf = (x, y) => smin(sdEllipse(x, y, 0.07, 0, 0.088, 0.072), sdCircle(x, y, 0.12, 0.05, 0.03), 0.03);
const footSdf = (x, y) => sdEllipse(x, y, 0, 0, 0.125, 0.06);

function bakeCharacter() {
    const L = [];
    L.push(layer('body', 480, 480, [240, 24], (x, y) => toyShade(bodySdf, x, y, 0.26, { aoFloor: 0.02 })));
    L.push(layer('belly', 280, 220, [140, 110], (x, y) => {
        const d = bellySdf(x, y);
        const a = clamp01(0.5 - d / (0.07)) * 0.55;
        if (a <= 0) return null;
        const top = clamp01((y + 0.22) / 0.44);
        return [mix(C.rose, C.peach, top * 0.7), a];
    }));
    L.push(layer('ear', 96, 112, [48, 26], (x, y) => {
        const r = toyShade(earSdf, x, y, 0.06, { spec: 0.3 });
        if (!r) return null;
        const inner = sdRoundCone(x, y - 0.03, 0.022, 0.04, 0.06);
        const ia = clamp01(0.5 - inner / (0.02)) * 0.55;
        return [mix(r[0], C.rose, ia), r[1]];
    }));
    L.push(layer('paw', 120, 96, [24, 48], (x, y) => toyShade(pawSdf, x, y, 0.06, { spec: 0.35 })));
    L.push(layer('foot', 120, 72, [60, 36], (x, y) => toyShade(footSdf, x, y, 0.05, { spec: 0.2 })));

    // EYES - a deep plum-black iris with a lighter reflection in its lower half, never a flat disc.
    const irisRx = 0.085, irisRy = 0.105;
    L.push(layer('eye_iris', 96, 112, [48, 56], (x, y) => {
        const d = sdEllipse(x, y, 0, 0, irisRx, irisRy);
        const a = cover(d);
        if (a <= 0) return null;
        const t = clamp01((irisRy - y) / (2 * irisRy));
        let col = mix(C.irisTop, C.irisBottom, smooth(0.25, 1, t));
        const refl = sdEllipse(x, y, 0, -0.045, 0.058, 0.04);
        col = mix(col, C.irisReflect, clamp01(0.5 - refl / 0.025) * 0.55 * smooth(0.5, 1, t));
        const edge = clamp01(1 - (-d) / (3 * PX));
        col = mix(col, [24, 8, 20], edge * 0.6);
        return [col, a];
    }));
    L.push(layer('eye_catch', 64, 64, [32, 32], (x, y) => {
        const a1 = cover(sdCircle(x, y, 0, 0, 0.026));
        const a2 = cover(sdCircle(x, y, 0.045, -0.05, 0.011)) * 0.85;
        const a = Math.max(a1, a2);
        return a > 0 ? [C.cream, a] : null;
    }));
    L.push(layer('eye_glow', 160, 176, [80, 88], (x, y) => {
        const d = sdEllipse(x, y, 0, 0, irisRx, irisRy);
        const a = Math.exp(-Math.max(0, d) / 0.03) * smooth(-0.02, 0.0, d) * 0.9;
        return a > 0.002 ? [[255, 255, 255], a] : null;
    }));
    // LIDS are SKIN: each is shaded with the body's own material sampled at the eye it covers
    // (left and right baked apart, because the light comes from the upper left and a mirrored lid
    // would wear the other side's shading), so a closing eye is the face closing over it rather
    // than a pale disc laid on it. A berry lash line runs along the edge that moves.
    const eyeAt = { L: [-0.155, 0.64], R: [0.155, 0.64] };
    const skin = (side, x, y) => {
        const r = toyShade(bodySdf, x + eyeAt[side][0], y + eyeAt[side][1], 0.26, { aoFloor: 0.02 });
        return r ? r[0] : C.dusty;
    };
    const lidLayer = (name, side, edge, below) => L.push(layer(name, 104, 120, [52, 60], (x, y) => {
        const d = sdEllipse(x, y, 0, 0, irisRx + 0.007, irisRy + 0.007);
        if (d > PX * 1.5) return null;
        const e = edge(side === 'R' ? -x : x);
        const cut = below ? e - y : y - e;
        const a = cover(d) * clamp01(0.5 + cut / PX);
        if (a <= 0) return null;
        let col = skin(side, x, y);
        col = mix(col, C.raspberry, clamp01(1 - cut / 0.03) * 0.22);
        col = mix(col, C.lineInk, clamp01(1 - cut / (4.5 * PX)) * 0.95);
        return [col, a];
    }));
    const closures = [0.2, 0.36, 0.52, 0.68, 0.84, 1.0];
    ['L', 'R'].forEach(side => {
        closures.forEach((c, k) => lidLayer('eye_lid_' + side + '_' + (k + 1), side,
            X => irisRy - c * 2 * irisRy - 0.026 * (1 - (X / irisRx) * (X / irisRx)) * (c < 1 ? 1 : -0.5), false));
        // the lower lid of a smug or sleepy look, pushed up from below
        lidLayer('eye_lowlid_' + side, side, X => -irisRy + 0.07 + 0.018 * (1 - (X / irisRx) * (X / irisRx)), true);
        // angry: the lid driven down toward the nose (inner side, +x for the left eye)
        lidLayer('eye_angry_' + side, side, X => 0.03 - (X / irisRx) * 0.05, false);
    });
    // happy crescent: a closed eye as a soft upturned arc
    L.push(layer('eye_happy', 112, 72, [56, 30], (x, y) => {
        const d = sdCurve(x, y, X => -0.045 + 0.075 * (1 - (X / 0.075) * (X / 0.075)), -0.075, 0.075, 0.017, 24);
        const a = cover(d);
        return a > 0 ? [C.irisTop, a] : null;
    }));
    L.push(layer('brow', 80, 40, [40, 20], (x, y) => {
        const d = sdCapsule(x, y, -0.04, 0, 0.04, 0.004, 0.011);
        const a = cover(d);
        return a > 0 ? [C.berry, a] : null;
    }));
    L.push(layer('cheek', 96, 72, [48, 36], (x, y) => {
        const d = sdEllipse(x, y, 0, 0, 0.075, 0.048);
        const a = clamp01(0.5 - d / 0.045) * 0.9;
        return a > 0.002 ? [[255, 255, 255], a] : null;
    }));

    // MOUTHS - each centred on the mouth's anchor. Dark plum inside, a pale raspberry tongue, a
    // berry lip line, tiny rounded teeth where the state calls for them.
    const mouth = (name, fn) => L.push(layer('mouth_' + name, 128, 112, [64, 56], fn));
    const line = (d) => { const a = cover(d); return a > 0 ? [C.lineInk, a] : null; };
    mouth('closed', (x, y) => line(sdCurve(x, y, X => -0.006 + 5 * X * X, -0.045, 0.045, 0.0085, 20)));
    mouth('frown', (x, y) => line(sdCurve(x, y, X => 0.008 - 6 * X * X, -0.04, 0.04, 0.0085, 20)));
    // a lopsided smirk: flat on one side, curled up on the other
    mouth('smug', (x, y) => line(sdCurve(x, y, X => -0.004 + (X > 0 ? 9 : 1.5) * X * X, -0.042, 0.05, 0.0085, 26)));
    mouth('chew_a', (x, y) => line(sdCurve(x, y, X => 0.006 * Math.sin(X * 90) - 0.004 + 2 * X * X, -0.04, 0.04, 0.0085, 30)));
    const openMouth = (rx, ry, flatTop, teethTop, teethBottom, angry) => (x, y) => {
        let d = sdEllipse(x, y, 0, 0, rx, ry);
        if (flatTop) d = Math.max(d, y - ry * flatTop);
        if (angry) d = Math.max(d, -(y + ry * 0.42));  // a flat floor: the open mouth is a frown, never a grin
        const a = cover(d);
        if (a <= 0) return null;
        const depth = clamp01(-d / (ry * 0.9));
        let col = mix(C.mouthIn, C.mouthDeep, depth);
        const tongue = sdEllipse(x, y, 0, angry ? -ry * 0.5 : -ry * 0.95, rx * 0.72, ry * (angry ? 0.4 : 0.62));
        const ta = clamp01(0.5 - tongue / PX);
        let tcol = mix(C.tongue, C.tongueLight, clamp01((y + ry) / (ry * 0.8)) * 0.6);
        tcol = mix(tcol, C.tongueDark, clamp01(1 - Math.abs(x) / (rx * 0.12)) * 0.3);
        col = mix(col, tcol, ta);
        for (let i = 0; i < teethTop; i++) {
            const tx = (i - (teethTop - 1) / 2) * rx * 0.42;
            const td = sdCapsule(x, y, tx, ry * 0.95, tx, ry * 0.62, rx * 0.11);
            col = mix(col, C.tooth, cover(td));
        }
        for (let i = 0; i < teethBottom; i++) {
            const tx = (i - (teethBottom - 1) / 2) * rx * 1.1;
            const fy = angry ? -ry * 0.42 : -ry * 0.95;
            const td = sdCapsule(x, y, tx, fy, tx, fy + ry * 0.28, rx * 0.1);
            col = mix(col, C.tooth, cover(td));
        }
        const lip = clamp01(1 - (-d) / (3.2 * PX));
        col = mix(col, C.lineInk, lip);
        return [col, a];
    };
    mouth('small', openMouth(0.024, 0.029, 0, 0, 0, false));
    mouth('medium', openMouth(0.048, 0.042, 0.7, 0, 0, false));
    mouth('wide', openMouth(0.1, 0.085, 0.82, 2, 0, false));
    mouth('furious', openMouth(0.11, 0.09, 0, 4, 2, true));
    mouth('chew_b', openMouth(0.03, 0.018, 0, 0, 0, false));
    mouth('nom', openMouth(0.036, 0.04, 0, 0, 0, false));

    // the tongue for a lick and the long elastic tongue for a snatch (stretched along x)
    L.push(layer('tongue_tip', 72, 56, [36, 40], (x, y) => {
        const d = Math.max(sdEllipse(x, y, 0, -0.012, 0.032, 0.03), y - 0.004);
        const a = cover(d);
        if (a <= 0) return null;
        let col = mix(C.tongue, C.tongueLight, clamp01((y + 0.04) / 0.04) * 0.4);
        col = mix(col, C.tongueDark, clamp01(1 - Math.abs(x) / 0.004) * 0.4);
        return [col, a];
    }));
    L.push(layer('tongue_strip', 400, 40, [0, 20], (x, y) => {
        const d = Math.max(Math.abs(y) - 0.034, -x);
        const a = cover(Math.max(d, x - 1)) ;
        if (a <= 0) return null;
        let col = mix(C.tongue, C.tongueLight, clamp01(y / 0.034) * 0.6);
        col = mix(col, C.tongueDark, clamp01(1 - Math.abs(y) / 0.006) * 0.35);
        return [col, a];
    }));
    L.push(layer('tongue_end', 64, 64, [16, 32], (x, y) => {
        const d = sdCircle(x, y, 0.02, 0, 0.036);
        const a = cover(d);
        if (a <= 0) return null;
        return [mix(C.tongue, C.tongueLight, clamp01((y + 0.03) / 0.06) * 0.5), a];
    }));
    // the stretchy arm for a paw snatch, toy-shaded across its width
    L.push(layer('arm_strip', 400, 64, [0, 32], (x, y) => {
        const sd = (X, Y) => Math.max(Math.abs(Y) - 0.06, Math.max(-X, X - 1));
        const r = toyShade(sd, x, y, 0.05, { spec: 0.25, ink: 0.5 });
        return r;
    }));
    L.push(layer('shadow', 240, 72, [120, 36], (x, y) => {
        const d = sdEllipse(x, y, 0, 0, 0.42, 0.06);
        const a = clamp01(0.5 - d / 0.07) * 0.8;
        return a > 0.002 ? [[40, 14, 30], a] : null;
    }));
    return L;
}

// ------------------------------------------------------------------ effects
function bakeEffects() {
    const L = [];
    // a heart mote (iq's heart, mirrored): pale raspberry, lighter at the top
    L.push(layer('fx_heart', 64, 64, [32, 32], (x, y) => {
        const k = 0.06, px = Math.abs(x) / k, py = (y + 0.032) / k;
        let d;
        if (py + px > 1) d = len(px - 0.25, py - 0.75) - Math.SQRT2 / 4;
        else {
            const m = 0.5 * Math.max(px + py, 0);
            d = Math.sqrt(Math.min(px * px + (py - 1) * (py - 1), (px - m) * (px - m) + (py - m) * (py - m))) * Math.sign(px - py);
        }
        const a = cover(d * k);
        return a > 0 ? [mix([236, 112, 146], [255, 200, 210], clamp01((y + 0.03) / 0.07)), a] : null;
    }));
    L.push(layer('fx_crumb', 32, 32, [16, 16], (x, y) => {
        const ang = Math.atan2(y, x), r = 0.022 + 0.006 * Math.sin(ang * 3 + 1) + 0.004 * Math.sin(ang * 5);
        const a = cover(len(x, y) - r);
        return a > 0 ? [[255, 255, 255], a] : null;
    }));
    // a four-point sparkle with CONCAVE sides (never a plus sign), a soft glow at its heart
    L.push(layer('fx_sparkle', 64, 64, [32, 32], (x, y) => {
        const r = 0.07, fn = (X, Y) => Math.sqrt(Math.abs(X) / r) + Math.sqrt(Math.abs(Y) / r) - 1;
        const e = 0.002, v = fn(x, y);
        const g = len(fn(x + e, y) - fn(x - e, y), fn(x, y + e) - fn(x, y - e)) / (2 * e) || 1;
        const a = clamp01(cover(v / g) + Math.exp(-len(x, y) / 0.012) * 0.45);
        return a > 0.003 ? [C.cream, a] : null;
    }));
    L.push(layer('fx_soft', 128, 128, [64, 64], (x, y) => {
        const r = len(x, y) / 0.16;
        const a = Math.exp(-r * r * 3) * clamp01(1 - (r - 0.95) / 0.05);
        return a > 0.002 ? [[255, 255, 255], a] : null;
    }));
    // a puff of breath / fury: three soft lobes
    L.push(layer('fx_puff', 128, 96, [64, 48], (x, y) => {
        const d = smin(smin(sdCircle(x, y, -0.05, -0.01, 0.045), sdCircle(x, y, 0.0, 0.025, 0.055), 0.03), sdCircle(x, y, 0.055, -0.005, 0.04), 0.03);
        const a = clamp01(0.5 - d / 0.03) * 0.9;
        return a > 0.002 ? [[255, 255, 255], a] : null;
    }));
    // the request plate: a soft rounded tile, cream inside, deep berry rim, a drop of shadow
    // THE REQUEST PLATE: a small raised tray the requested card sits in - a berry lip lit along
    // its upper edge, a cream well with the lip's shadow inside its top, a drop shadow under it.
    // The bitten variant has a scalloped bite out of its upper right corner with a berry crust
    // along the bite, which is what a FED request collapses into.
    const plateSd = (x, y) => { const bx = Math.abs(x) - 0.17, by = Math.abs(y) - 0.21; return len(Math.max(bx, 0), Math.max(by, 0)) + Math.min(Math.max(bx, by), 0) - 0.08; };
    const biteSd = (x, y) => {
        let d = 1e9;
        for (let i = 0; i < 3; i++) {
            const t = 2.36 + (i - 1) * 0.62;
            d = Math.min(d, sdCircle(x, y, 0.25 + Math.cos(t) * 0.085, 0.29 + Math.sin(t) * 0.085, i === 1 ? 0.07 : 0.06));
        }
        return d;
    };
    const plateFn = bitten => (x, y) => {
        let d = plateSd(x, y), ds = plateSd(x - 0.012, y + 0.024);
        if (bitten) { d = Math.max(d, -biteSd(x, y)); ds = Math.max(ds, -biteSd(x - 0.012, y + 0.024)); }
        const sa = clamp01(0.5 - ds / 0.035) * 0.38;
        const a = cover(d);
        if (a <= 0) return sa > 0.002 ? [[50, 16, 34], sa] : null;
        const lipW = 0.026;
        const lip = clamp01(0.5 - (-d - lipW) / PX);
        let col = C.plate;
        // the well: brighter toward its top, the lip's shadow inside its upper edge
        col = mix(col, [255, 251, 249], clamp01((y + 0.1) / 0.35) * 0.5);
        col = mix(col, [222, 170, 184], clamp01(1 - (-d - lipW) / 0.03) * clamp01((y + 0.05) / 0.2) * 0.55);
        // the lip: berry, lit on its upper-left edge, darker on its lower-right
        let lc = mix(C.plateRim, [196, 92, 128], clamp01((y - x * 0.6 + 0.1) / 0.5) * 0.7);
        lc = mix(lc, [240, 168, 186], clamp01(1 - (-d) / 0.008) * clamp01((y - 0.05) / 0.25) * 0.6);
        col = mix(col, lc, lip);
        return [col, Math.max(a, sa)];
    };
    L.push(layer('plate', 220, 260, [110, 130], plateFn(false)));
    L.push(layer('plate_bitten', 220, 260, [110, 130], plateFn(true)));
    // the patience ring: one scalloped bead of a pet-toy ring (twelve of them make the ring)
    L.push(layer('ring_bead', 64, 48, [32, 24], (x, y) => {
        const d = smin(sdEllipse(x, y, 0, 0, 0.05, 0.026), sdCircle(x, y, 0, 0.01, 0.026), 0.02);
        const a = cover(d);
        return a > 0 ? [mix([255, 255, 255], [255, 240, 244], clamp01(y / 0.03)), a] : null;
    }));
    // a bite-ring left where a fed card used to be in the hand
    L.push(layer('fx_bitering', 160, 160, [80, 80], (x, y) => {
        const r = len(x, y), ang = Math.atan2(y, x);
        const scallop = 0.16 + 0.012 * Math.cos(ang * 9);
        const d = Math.abs(r - scallop) - 0.008;
        const a = cover(d);
        return a > 0 ? [[255, 255, 255], a] : null;
    }));
    // tiny thought mote: a little snack-card silhouette
    L.push(layer('fx_snack', 64, 80, [32, 40], (x, y) => {
        const bx = Math.abs(x) - 0.035, by = Math.abs(y) - 0.05;
        const d = len(Math.max(bx, 0), Math.max(by, 0)) + Math.min(Math.max(bx, by), 0) - 0.012;
        const a = cover(d);
        if (a <= 0) return null;
        const cube = Math.max(Math.abs(x) - 0.016, Math.abs(y + 0.004) - 0.016);
        return [mix([255, 236, 240], [214, 92, 128], cover(cube) * 0.8), a];
    }));
    // ground chunks: torn pieces of the board's own floor, lit on top
    const chunk = (name, pts) => L.push(layer(name, 96, 96, [48, 48], (x, y) => {
        let inside = true, dmin = 1e9;
        for (let i = 0; i < pts.length; i++) {
            const [ax, ay] = pts[i], [bx, by] = pts[(i + 1) % pts.length];
            const ex = bx - ax, ey = by - ay, wx = x - ax, wy = y - ay;
            const h = clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            dmin = Math.min(dmin, len(wx - ex * h, wy - ey * h));
            if (ex * wy - ey * wx < 0) inside = false;
        }
        const d = (inside ? -dmin : dmin) - 0.004;
        const a = cover(d);
        if (a <= 0) return null;
        let col = mix(C.groundDark, C.ground, clamp01((y + 0.08) / 0.12));
        const topEdge = clamp01(1 - (-d) / 0.012) * clamp01((y + 0.02) / 0.06);
        col = mix(col, C.groundLit, topEdge);
        col = col.map(v => v * (1 + (noise(x * 120, y * 120) - 0.5) * 0.12));
        return [col, a];
    }));
    chunk('chunk_a', [[-0.09, -0.07], [0.08, -0.08], [0.1, 0.03], [0.02, 0.09], [-0.08, 0.06]]);
    chunk('chunk_b', [[-0.07, -0.05], [0.06, -0.09], [0.09, 0.06], [-0.05, 0.08]]);
    chunk('chunk_c', [[-0.1, 0.0], [-0.02, -0.08], [0.09, -0.04], [0.06, 0.07], [-0.06, 0.08]]);
    // the bitten edge: a strip of rounded tooth scallops, white for tinting
    L.push(layer('fx_scallop', 160, 40, [80, 20], (x, y) => {
        const period = 0.05;
        const fx = ((x % period) + period) % period - period / 2;
        const d = Math.max(len(fx, y + 0.018) - 0.03, -(y + 0.03));
        const a = cover(Math.max(d, Math.abs(x) - 0.19));
        return a > 0 ? [[255, 255, 255], a] : null;
    }));
    return L;
}

// ------------------------------------------------------------------ preview compositor
// Composes the rig in the same units and layout TamagotchiRig uses, so the look can be judged.
const RIG = {
    belly: [0, 0.34], earL: [-0.18, 0.92], earR: [0.18, 0.92], earTilt: 16,
    pawL: [-0.43, 0.36], pawR: [0.43, 0.36], footL: [-0.2, 0.03], footR: [0.2, 0.03],
    eyeL: [-0.155, 0.64], eyeR: [0.155, 0.64], browL: [-0.16, 0.8], browR: [0.16, 0.8],
    cheekL: [-0.27, 0.52], cheekR: [0.27, 0.52], mouth: [0, 0.5]
};
function composite(layers, pose, size) {
    const by = {}; layers.forEach(l => by[l.name] = l);
    const W = size, H = size, cv = canvas(W, H);
    const bg = [34, 40, 54];
    for (let i = 0; i < W * H; i++) { cv.d[i * 4] = bg[0]; cv.d[i * 4 + 1] = bg[1]; cv.d[i * 4 + 2] = bg[2]; cv.d[i * 4 + 3] = 1; }
    const scale = size / 1.5;                         // px per unit in the preview
    const ox = W / 2, oy = H * 0.12;
    const put = (name, at, opt) => {
        const l = by[name]; if (!l) return;
        opt = opt || {};
        const sx = (opt.sx || 1) * (opt.flip ? -1 : 1), sy = opt.sy || 1, rot = (opt.rot || 0) * Math.PI / 180;
        const tint = opt.tint, alpha = opt.alpha === undefined ? 1 : opt.alpha;
        const k = scale / PPU, cs = Math.cos(rot), sn = Math.sin(rot);
        const ext = Math.max(l.w, l.h) * k * Math.max(Math.abs(sx), sy) * 1.5;
        const cx = ox + at[0] * scale, cy = oy + at[1] * scale;
        for (let py = Math.floor(cy - ext); py < cy + ext; py++) for (let px = Math.floor(cx - ext); px < cx + ext; px++) {
            if (px < 0 || py < 0 || px >= W || py >= H) continue;
            let dx = (px + 0.5 - cx) / k, dy = (py + 0.5 - cy) / k;
            const rx = (dx * cs + dy * sn) / sx, ry = (-dx * sn + dy * cs) / sy;
            const u = rx + l.pivot[0], v = ry + l.pivot[1];
            if (u < 0 || v < 0 || u >= l.w - 1 || v >= l.h - 1) continue;
            const u0 = Math.floor(u), v0 = Math.floor(v), fu = u - u0, fv = v - v0;
            const s = [0, 0, 0, 0];
            for (const [du, dv, w] of [[0, 0, (1 - fu) * (1 - fv)], [1, 0, fu * (1 - fv)], [0, 1, (1 - fu) * fv], [1, 1, fu * fv]]) {
                const kk = ((v0 + dv) * l.w + (u0 + du)) * 4, aa = l.d[kk + 3];
                for (let c = 0; c < 3; c++) s[c] += l.d[kk + c] * aa * w;
                s[3] += aa * w;
            }
            if (s[3] <= 0) continue;
            let rgb = [s[0] / s[3], s[1] / s[3], s[2] / s[3]];
            if (tint) rgb = rgb.map((c, i) => c * tint[i] / 255);
            over(cv, py * W + px, rgb, s[3] * alpha);
        }
    };
    const P = pose;
    put('shadow', [0, 0.01], { sx: 1.05 });
    put('foot', RIG.footL, {}); put('foot', RIG.footR, {});
    put('ear', RIG.earL, { rot: RIG.earTilt + (P.ear || 0) }); put('ear', RIG.earR, { rot: -RIG.earTilt - (P.ear || 0), flip: true });
    put('body', [0, 0], { sx: P.bodySx || 1, sy: P.bodySy || 1 });
    put('belly', RIG.belly, { sx: P.belly || 1, sy: P.belly || 1 });
    put('cheek', RIG.cheekL, { tint: P.cheekTint || [244, 120, 146], alpha: P.cheekA || 0.55 });
    put('cheek', RIG.cheekR, { tint: P.cheekTint || [244, 120, 146], alpha: P.cheekA || 0.55 });
    if (P.eyes === 'happy') { put('eye_happy', RIG.eyeL, {}); put('eye_happy', RIG.eyeR, {}); }
    else {
        const es = P.irisScale || 1;
        if (P.glow) { put('eye_glow', RIG.eyeL, { tint: [214, 60, 120], alpha: 0.85 }); put('eye_glow', RIG.eyeR, { tint: [214, 60, 120], alpha: 0.85 }); }
        put('eye_iris', RIG.eyeL, { sx: es, sy: es }); put('eye_iris', RIG.eyeR, { sx: es, sy: es });
        const cs = P.catch || 1;
        put('eye_catch', [RIG.eyeL[0] - 0.025 + (P.look || [0, 0])[0], RIG.eyeL[1] + 0.035 + (P.look || [0, 0])[1]], { sx: cs, sy: cs });
        put('eye_catch', [RIG.eyeR[0] - 0.025 + (P.look || [0, 0])[0], RIG.eyeR[1] + 0.035 + (P.look || [0, 0])[1]], { sx: cs, sy: cs });
        if (P.lid) { put('eye_lid_L_' + P.lid, RIG.eyeL, {}); put('eye_lid_R_' + P.lid, RIG.eyeR, {}); }
        if (P.angry) { put('eye_angry_L', RIG.eyeL, {}); put('eye_angry_R', RIG.eyeR, {}); }
        if (P.low) { put('eye_lowlid_L', RIG.eyeL, {}); put('eye_lowlid_R', RIG.eyeR, {}); }
    }
    const br = P.brow || 0;
    put('brow', [RIG.browL[0], RIG.browL[1] + (P.browY || 0)], { rot: -br, alpha: P.browA === undefined ? 0.55 : P.browA });
    put('brow', [RIG.browR[0], RIG.browR[1] + (P.browY || 0)], { rot: br, flip: true, alpha: P.browA === undefined ? 0.55 : P.browA });
    put('mouth_' + (P.mouth || 'closed'), RIG.mouth, { sx: P.mouthS || 1, sy: P.mouthS || 1 });
    put('paw', RIG.pawL, { flip: true, rot: P.pawL || 0 }); put('paw', RIG.pawR, { rot: P.pawR || 0 });
    return cv;
}
function writePreview(layers, dir) {
    const poses = [
        { name: 'calm', mouth: 'closed' },
        { name: 'hungry', mouth: 'nom', ear: -8, brow: -6, browY: 0.01 },
        { name: 'excited', mouth: 'wide', irisScale: 1.08, pawL: -40, pawR: 40, ear: -14 },
        { name: 'happy', eyes: 'happy', mouth: 'closed', cheekA: 0.75, belly: 1.04 },
        { name: 'chew', mouth: 'chew_a', lid: 1, cheekA: 0.7 },
        { name: 'impatient', mouth: 'frown', brow: 10, lid: 2, browA: 0.8 },
        { name: 'angry', mouth: 'frown', angry: true, brow: 18, browY: -0.02, browA: 0.95, catch: 0.7, ear: -24, cheekTint: [200, 60, 100] },
        { name: 'furious', mouth: 'furious', angry: true, brow: 24, browY: -0.03, browA: 1, irisScale: 0.86, catch: 0.45, glow: true, ear: -30, cheekTint: [190, 46, 92], cheekA: 0.8 },
        { name: 'smug', mouth: 'smug', lid: 2, low: true, brow: -4 },
        { name: 'sleepy', mouth: 'small', lid: 4 }
    ];
    const tiles = poses.map(p => composite(layers, p, 300));
    const W = 300 * 5, H = 300 * 2, cv = canvas(W, H);
    tiles.forEach((t, i) => {
        const gx = (i % 5) * 300, gy = (1 - Math.floor(i / 5)) * 300;
        for (let y = 0; y < 300; y++) for (let x = 0; x < 300; x++) {
            const s = (y * 300 + x) * 4, d = ((gy + y) * W + gx + x) * 4;
            for (let c = 0; c < 4; c++) cv.d[d + c] = t.d[s + c];
        }
    });
    writePng(cv, path.join(dir, 'tama_preview.png'));
}

// ------------------------------------------------------------------ main
const all = bakeCharacter().concat(bakeEffects());
fs.mkdirSync(OUT, { recursive: true });
for (const l of all) {
    const file = path.join(OUT, l.name + '.png');
    writePng(l, file);
    writeMeta(l, file);
}
console.log('baked ' + all.length + ' layers into ' + OUT);
if (process.argv[2]) {
    writePreview(all, process.argv[2]);
    console.log('preview written to ' + process.argv[2]);
}
