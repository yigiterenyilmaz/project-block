"""Fredoka'ya eksik Turkce harfleri ekler: Isdot, gbreve, scedilla ve buyukleri.

Hicbir sey CIZILMIYOR. Fontun kendi uni0306 (breve), uni0327 (cedilla) ve uni0307 (dot
above) glifleri var; eksik olan yalnizca bunlari s/S/g/G/I ile birlestiren composite
glifler ve cmap girdileri. Yerlesim kurallari fontun MEVCUT composite'lerinden olculdu:
  - kucuk harf ustu aksan: dy=0   (butun aksanlar zaten y=728'de tepe hizali cizilmis)
  - buyuk harf ustu aksan: dy=200 (Acircumflex/Odieresis/... hepsi bunu kullaniyor)
  - cedilla: dy fontun kendi Ccedilla'sindan (buyukte +10, kucukte 0), yatayda ise
    TABAN BBOX ORTASI. Once s/S'in alt ink'ine gore hizalamayi denedim - Turkce'nin klasik
    yerlesimi ortalamak ve yan yana bakinca o kazandi, S'te digeri 20 birim sola kayiyordu.
"""
import sys
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

CAP_DY = 200          # olculdu: buyuk harf aksan yuksekligi
CEDILLA_CAP_DY = 10   # Ccedilla'nin kendi degeri

def bbox(glyf, name):
    g = glyf[name]
    return (g.xMin, g.yMin, g.xMax, g.yMax)

def bottom_center(glyf, name, band=0.20):
    """Tabana degen ink'in yatay ortasi - cedilla oraya asilir."""
    g = glyf[name]
    coords, ends, flags = g.getCoordinates(glyf)
    ys = [p[1] for p in coords]
    lo, hi = min(ys), max(ys)
    cut = lo + (hi - lo) * band
    xs = [p[0] for p in coords if p[1] <= cut]
    return (min(xs) + max(xs)) / 2.0

def make(font, cp, name, base, accent, dx, dy):
    glyf, hmtx = font['glyf'], font['hmtx']
    g = Glyph()
    g.numberOfContours = -1
    g.components = []
    for gname, x, y in ((base, 0, 0), (accent, int(round(dx)), int(round(dy)))):
        c = GlyphComponent()
        c.glyphName, c.x, c.y = gname, x, y
        c.flags = 0x0004 | 0x0002      # ROUND_XY_TO_GRID | ARGS_ARE_XY_VALUES
        g.components.append(c)
    glyf.glyphs[name] = g
    font.glyphOrder.append(name)
    glyf.glyphOrder = font.glyphOrder
    g.recalcBounds(glyf)
    hmtx.metrics[name] = (hmtx.metrics[base][0], g.xMin)
    for table in font['cmap'].tables:
        if table.isUnicode():
            table.cmap[cp] = name
    return g

def patch(src, dst):
    f = TTFont(src)
    glyf = f['glyf']
    cm = f.getBestCmap()
    n = {c: cm[ord(c)] for c in "sSgGIi"}

    acc_breve, acc_ced, acc_dot = 'uni0306', 'uni0327', 'uni0307'
    bc = {a: (bbox(glyf, a)[0] + bbox(glyf, a)[2]) / 2.0 for a in (acc_breve, acc_ced, acc_dot)}

    # --- ustteki aksanlar: taban bbox ortasina hizala (acircumflex/Ocircumflex kurali)
    def center_dx(base, acc):
        b = bbox(glyf, base)
        return (b[0] + b[2]) / 2.0 - bc[acc]

    made = []
    made.append(('gbreve',    0x011F, make(f, 0x011F, 'gbreve',    n['g'], acc_breve,
                                           center_dx(n['g'], acc_breve), 0)))
    made.append(('Gbreve',    0x011E, make(f, 0x011E, 'Gbreve',    n['G'], acc_breve,
                                           center_dx(n['G'], acc_breve), CAP_DY)))
    # I'nin noktasi: Idieresis ile ayni yukseklige otursun (aksan bolgesi 744'te basliyor)
    dot_dy = 744 - bbox(glyf, acc_dot)[1]
    made.append(('Idotaccent', 0x0130, make(f, 0x0130, 'Idotaccent', n['I'], acc_dot,
                                            center_dx(n['I'], acc_dot), dot_dy)))
    # --- cedilla: tabanin alt ink ortasina
    made.append(('scedilla', 0x015F, make(f, 0x015F, 'scedilla', n['s'], acc_ced,
                                          center_dx(n['s'], acc_ced), 0)))
    made.append(('Scedilla', 0x015E, make(f, 0x015E, 'Scedilla', n['S'], acc_ced,
                                          center_dx(n['S'], acc_ced), CEDILLA_CAP_DY)))

    f['maxp'].numGlyphs = len(f.getGlyphOrder())
    f.save(dst)
    print(f"{dst}")
    for name, cp, g in made:
        comp = g.components[1]
        print(f"   U+{cp:04X} {name:11} dx={comp.x:5} dy={comp.y:4}  bbox={g.xMin},{g.yMin},{g.xMax},{g.yMax}")

if __name__ == '__main__':
    patch(sys.argv[1], sys.argv[2])
