// PURPOSE: Turns the painted snow block (the "Karlı ve Buzlu Oyun Taşı" drawing) into the board's
// SNOW tile, Assets/Resources/Art/Blocks/block_snow.png. It replaced the placeholder this file's
// predecessor baked (bake_snow_tile.js, deleted with it).
//
//   node Tools/ArtPrep/prep_snow_tile.js "<path to the drawing>"
//
// A tile's BODY is its whole canvas (the .meta's pixelsPerUnit is the body's pixel size), so the
// drawing is cropped to the snow block itself - the loose flakes floating outside it are dropped,
// they would read as specks on the neighbouring cells - and area-averaged down to 364 px, the size
// of the other tiles. The drawing's body is alpha ~253, not 255: anything that opaque is made
// fully opaque, or the dark slot under the cube shows through as a grey cast.

const path = require('path');
const { decode, encode } = require('./png_io.js');

const SIZE = 364;
const src = process.argv[2];
if (!src) {
  console.error('usage: node prep_snow_tile.js <drawing.png>');
  process.exit(1);
}
const im = decode(src);
const d = im.data;

// The body: the rows and columns more than half covered by near-opaque pixels.
const solid = (x, y) => d[(y * im.w + x) * 4 + 3] > 200;
function span(count, length, at) {
  let first = -1, last = -1;
  for (let i = 0; i < count; i++) {
    let n = 0;
    for (let j = 0; j < length; j++) { if (at(i, j)) n++; }
    if (n / length > 0.5) { if (first < 0) first = i; last = i; }
  }
  return [first, last];
}
const [bx0, bx1] = span(im.w, im.h, (x, y) => solid(x, y));
const [by0, by1] = span(im.h, im.w, (y, x) => solid(x, y));
// A square round the body's centre, a hair past it so the ragged frosted rim is kept whole.
const side = Math.round(Math.max(bx1 - bx0, by1 - by0) * 1.012);
const cx = (bx0 + bx1) / 2, cy = (by0 + by1) / 2;
const ox = cx - side / 2, oy = cy - side / 2;

const out = Buffer.alloc(SIZE * SIZE * 4);
const scale = side / SIZE;
for (let y = 0; y < SIZE; y++) {
  for (let x = 0; x < SIZE; x++) {
    // Area average, premultiplied, over the source pixels this one covers.
    const sx0 = ox + x * scale, sx1 = sx0 + scale, sy0 = oy + y * scale, sy1 = sy0 + scale;
    let r = 0, g = 0, b = 0, a = 0, wsum = 0;
    for (let sy = Math.floor(sy0); sy < Math.ceil(sy1); sy++) {
      const wy = Math.min(sy + 1, sy1) - Math.max(sy, sy0);
      for (let sx = Math.floor(sx0); sx < Math.ceil(sx1); sx++) {
        const wx = Math.min(sx + 1, sx1) - Math.max(sx, sx0);
        const w = wx * wy;
        if (w <= 0) continue;
        wsum += w;
        if (sx < 0 || sy < 0 || sx >= im.w || sy >= im.h) continue;
        const o = (sy * im.w + sx) * 4;
        const al = d[o + 3] / 255;
        r += d[o] * al * w; g += d[o + 1] * al * w; b += d[o + 2] * al * w; a += al * w;
      }
    }
    const o = (y * SIZE + x) * 4;
    const alpha = wsum > 0 ? a / wsum : 0;
    out[o] = a > 0 ? Math.round(r / a) : 0;
    out[o + 1] = a > 0 ? Math.round(g / a) : 0;
    out[o + 2] = a > 0 ? Math.round(b / a) : 0;
    out[o + 3] = alpha >= 0.94 ? 255 : Math.round(alpha * 255);
  }
}
const dest = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'Art', 'Blocks', 'block_snow.png');
encode(dest, SIZE, SIZE, out);
console.log('body ' + bx0 + ',' + by0 + ' - ' + bx1 + ',' + by1 + '; cropped ' + side + ' px; wrote ' + dest);
