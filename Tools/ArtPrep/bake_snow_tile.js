// PURPOSE: Bakes the SNOW block's tile (Assets/Resources/Art/Blocks/block_snow.png) - a
// placeholder a painter can overwrite, drawn to sit beside the other tiles: the same bevelled
// frame lit from the upper left, and a matte white face with soft drifts rather than the water
// tile's swirl or the ice's glassy film. Node, because this machine has no Python.
//
//   node Tools/ArtPrep/bake_snow_tile.js
//
// 364 x 364 like block_water.png (the .meta's pixelsPerUnit is the body's pixel size), drawn at
// 3x and averaged down, so every edge is antialiased without a blur.

const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const SIZE = 364;
const SS = 3;

function clamp01(v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
function mix(a, b, t) { return a + (b - a) * clamp01(t); }
function mix3(a, b, t) { return [mix(a[0], b[0], t), mix(a[1], b[1], t), mix(a[2], b[2], t)]; }
function smooth(e0, e1, x) { const t = clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
function hex(h) { return [(h >> 16 & 255) / 255, (h >> 8 & 255) / 255, (h & 255) / 255]; }

// Signed distance to a rounded box centred on the origin; negative inside.
function roundedBox(px, py, hx, hy, r) {
  const qx = Math.abs(px) - (hx - r);
  const qy = Math.abs(py) - (hy - r);
  const ox = Math.max(qx, 0), oy = Math.max(qy, 0);
  return Math.sqrt(ox * ox + oy * oy) + Math.min(Math.max(qx, qy), 0) - r;
}

function hash(x, y) {
  let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return ((h >>> 0) % 100000) / 100000;
}

// Smooth value noise, for the face's faint powder grain.
function noise(x, y) {
  const xi = Math.floor(x), yi = Math.floor(y);
  const fx = x - xi, fy = y - yi;
  const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  return mix(mix(hash(xi, yi), hash(xi + 1, yi), u), mix(hash(xi, yi + 1), hash(xi + 1, yi + 1), u), v);
}

const FRAME_LIGHT = hex(0xF3F9FF);
const FRAME_LEFT = hex(0xDCEAF6);
const FRAME_RIGHT = hex(0xA9C2D9);
const FRAME_DARK = hex(0x92AFCB);
const FRAME_LINE = hex(0x7F9DBB);
const FACE_TOP = hex(0xFDFEFF);
const FACE_LOW = hex(0xE3EEF8);
const DRIFT_A = hex(0xEFF6FC);
const DRIFT_B = hex(0xD6E6F4);
const DRIFT_C = hex(0xC3D8EC);
const SHADE = hex(0xA9C4DE);

// One sample, in unit space: (0,0) top-left, (1,1) bottom-right.
function shade(u, v) {
  const px = u - 0.5, py = v - 0.5;
  const outer = roundedBox(px, py, 0.5, 0.5, 0.055);
  if (outer > 0) { return [0, 0, 0, 0]; }

  const inner = roundedBox(px, py, 0.378, 0.378, 0.06);
  let col;
  if (inner > 0) {
    // THE FRAME: four bevel faces, split along the diagonals like the other tiles.
    const top = -py > Math.abs(px), bottom = py > Math.abs(px), left = -px > Math.abs(py);
    col = top ? FRAME_LIGHT : bottom ? FRAME_DARK : left ? FRAME_LEFT : FRAME_RIGHT;
    // a thin darker line where the frame meets the face, and at the outer rim
    col = mix3(col, FRAME_LINE, (1 - smooth(0.0, 0.012, inner)) * 0.55);
    col = mix3(col, FRAME_LINE, (1 - smooth(0.0, 0.010, -outer)) * 0.45);
    return [col[0], col[1], col[2], 1];
  }

  // THE FACE: a white field, a breath cooler toward the lower right.
  col = mix3(FACE_TOP, FACE_LOW, (u + v) * 0.5);

  // Three drifts, back to front - each a soft hill line with a shaded foot under its crest.
  const drifts = [
    { base: 0.50, amp: 0.045, freq: 5.2, phase: 0.6, col: DRIFT_A },
    { base: 0.63, amp: 0.055, freq: 4.1, phase: 2.4, col: DRIFT_B },
    { base: 0.77, amp: 0.040, freq: 6.3, phase: 4.3, col: DRIFT_C },
  ];
  for (const d of drifts) {
    const crest = d.base + d.amp * Math.sin(u * d.freq + d.phase)
      + d.amp * 0.45 * Math.sin(u * d.freq * 2.3 + d.phase * 1.7);
    const below = v - crest;
    if (below > -0.004) {
      const body = smooth(-0.004, 0.004, below);
      // lit along the crest, settling into the drift's own tone under it
      const lit = mix3(hex(0xFFFFFF), d.col, smooth(0.0, 0.07, below));
      col = mix3(col, lit, body);
    } else {
      // the shadow the drift in front throws up onto what is behind it
      col = mix3(col, SHADE, (1 - smooth(0.0, 0.035, -below)) * 0.10);
    }
  }

  // Powder grain, barely there, and a few bright flecks.
  col = mix3(col, hex(0xFFFFFF), (noise(u * 46, v * 46) - 0.5) * 0.10 + 0.0);
  const flecks = [[0.30, 0.30, 0.016], [0.62, 0.36, 0.012], [0.44, 0.22, 0.009], [0.72, 0.58, 0.011],
    [0.24, 0.66, 0.010], [0.55, 0.71, 0.008], [0.36, 0.84, 0.010]];
  for (const f of flecks) {
    const dx = u - f[0], dy = v - f[1];
    // a four-point glint: bright along both axes, gone off them
    const star = Math.max(0, 1 - (Math.abs(dx) * Math.abs(dy)) / (f[2] * f[2] * 0.25))
      * Math.max(0, 1 - Math.sqrt(dx * dx + dy * dy) / (f[2] * 2.4));
    col = mix3(col, hex(0xFFFFFF), star * 0.9);
  }

  // The same soft glint the other tiles carry in their upper-left corner.
  const gx = (u - 0.215) / 0.050, gy = (v - 0.235) / 0.095;
  const glint = clamp01(1 - Math.sqrt(gx * gx + gy * gy));
  col = mix3(col, hex(0xFFFFFF), glint * 0.55);

  // A soft inner shadow at the face's edge, so it sits IN the frame.
  col = mix3(col, SHADE, (1 - smooth(0.0, 0.03, -inner)) * 0.30);
  return [col[0], col[1], col[2], 1];
}

function crc32(buf) {
  let c, crc = 0xFFFFFFFF;
  for (let n = 0; n < buf.length; n++) {
    c = (crc ^ buf[n]) & 0xFF;
    for (let k = 0; k < 8; k++) { c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; }
    crc = (crc >>> 8) ^ c;
  }
  return (crc ^ 0xFFFFFFFF) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

const raw = Buffer.alloc(SIZE * (SIZE * 4 + 1));
for (let y = 0; y < SIZE; y++) {
  raw[y * (SIZE * 4 + 1)] = 0;
  for (let x = 0; x < SIZE; x++) {
    let r = 0, g = 0, b = 0, a = 0;
    for (let sy = 0; sy < SS; sy++) {
      for (let sx = 0; sx < SS; sx++) {
        const s = shade((x + (sx + 0.5) / SS) / SIZE, (y + (sy + 0.5) / SS) / SIZE);
        r += s[0] * s[3]; g += s[1] * s[3]; b += s[2] * s[3]; a += s[3];
      }
    }
    const o = y * (SIZE * 4 + 1) + 1 + x * 4;
    const n = SS * SS;
    raw[o] = a > 0 ? Math.round(clamp01(r / a) * 255) : 0;
    raw[o + 1] = a > 0 ? Math.round(clamp01(g / a) * 255) : 0;
    raw[o + 2] = a > 0 ? Math.round(clamp01(b / a) * 255) : 0;
    raw[o + 3] = Math.round(clamp01(a / n) * 255);
  }
}
const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(SIZE, 0); ihdr.writeUInt32BE(SIZE, 4);
ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
  chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0)),
]);
const out = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'Art', 'Blocks', 'block_snow.png');
fs.writeFileSync(out, png);
console.log('wrote ' + out + ' (' + png.length + ' bytes)');
