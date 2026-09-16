# -*- coding: utf-8 -*-
# JOKER VE GUC KARTI - statik kontrol.
#
# Bu dosyanin korudugu iki sey var.
#
# 1. HeldItemCard'daki bolge kesirleri, kart PNG'lerinin GERCEK bolgeleriyle ayni mi? Sanat
#    yeniden cizilirse ikon oyugun disina, isim de plakanin disina kayar ve bunu kimse fark etmez -
#    cunku ikisi de "biraz kaymis" gibi durur, bozuk gibi degil.
# 2. Plakaya yazilan her renk, plakada GERCEKTEN okunuyor mu? Ilk surumde her renk murekkebe
#    sabit oranda karistiriliyordu; olculdugunde soluk nadirlik renkleri 1.9-3.2 kontrastta
#    kaliyordu ve nadir bir jokerin adi rafta kayboluyordu.
from __future__ import print_function
import colorsys
import os
import re
import sys

try:
    import numpy as np
    from PIL import Image
except ImportError:
    print('kart kontrolu: numpy/PIL yok, olcum atlandi')
    sys.exit(0)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CARDS = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Cards')
SRC = os.path.join(ROOT, 'Assets', 'Scripts', 'View', 'HeldItemCard.cs')

fail = []


def check(label, ok, why):
    print('   %-70s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


def read(path):
    return open(path, encoding='utf-8').read()


def method(src, signature):
    """The body of a method, so a check cannot match something in a neighbour."""
    i = src.index(signature)
    depth = 0
    started = False
    for j in range(i, len(src)):
        if src[j] == '{':
            depth += 1
            started = True
        elif src[j] == '}':
            depth -= 1
            if started and depth == 0:
                return src[i:j + 1]
    return src[i:]


def widest(mask, h, w):
    share = mask.mean(axis=1)
    ys = np.where(share > 0.5)[0]
    if not len(ys):
        return None
    runs, start = [], ys[0]
    for i in range(1, len(ys)):
        if ys[i] != ys[i - 1] + 1:
            runs.append((start, ys[i - 1]))
            start = ys[i]
    runs.append((start, ys[-1]))
    lo, hi = max(runs, key=lambda r: r[1] - r[0])
    xs = np.where(mask[lo:hi + 1].mean(axis=0) > 0.5)[0]
    return lo / h, (hi + 1) / h, xs[0] / w, (xs[-1] + 1) / w


def regions(path):
    """The card's own regions, measured: the pale icon panel and (if any) the name plate."""
    a = np.array(Image.open(path).convert('RGBA')).astype(int)
    h, w = a.shape[:2]
    lum = a[..., :3].mean(axis=2)
    alpha = a[..., 3]
    panel = widest((lum > 150) & (alpha > 200), h, w)
    plate = widest((lum > 150) & (lum < 195) & (alpha > 200), h, w)
    return w / float(h), panel, plate, a


def declared(src, name):
    """The floats of a `new CardArt(...)` initialiser."""
    m = re.search(r'%s = new CardArt\(([^)]*)\)' % name, src, re.S)
    if not m:
        return None
    return [float(x.strip().rstrip('f')) for x in m.group(1).split(',')]


src = read(SRC)
TOL = 0.02

print('=== 1. KART SANATLARI YERINDE ===')
joker_png = os.path.join(CARDS, 'card_joker.png')
power_png = os.path.join(CARDS, 'card_power.png')
check('Art/Cards/card_joker.png var', os.path.exists(joker_png),
      'boyali joker karti yok - kartlar sessizce duz dikdortgene doner')
check('Art/Cards/card_power.png var', os.path.exists(power_png),
      'boyali guc karti yok - guc kartlari duz dikdortgen kalir')
if not (os.path.exists(joker_png) and os.path.exists(power_png)):
    print('\nHATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)

j_aspect, j_panel, j_plate, j_px = regions(joker_png)
p_aspect, p_panel, _, _ = regions(power_png)

print('=== 2. KESIRLER SANATIN GERCEK BOLGELERIYLE AYNI ===')
jc = declared(src, 'JokerCard')
pc = declared(src, 'PowerCard')
check('JokerCard sekiz sayiyla tanimli (plakali)', jc is not None and len(jc) == 8,
      'joker kartinin anatomisi okunamiyor')
check('PowerCard DORT sayiyla tanimli (plakasiz - uzerine yazi yazilmaz)',
      pc is not None and len(pc) == 4,
      'guc kartina plaka uydurulmus - o kart yalnizca ikonun cercevesi')
if jc and len(jc) == 8:
    for i, (name, value) in enumerate((('IconTop', j_panel[0]), ('IconBottom', j_panel[1]),
                                       ('IconLeft', j_panel[2]), ('IconRight', j_panel[3]),
                                       ('PlateTop', j_plate[0]), ('PlateBottom', j_plate[1]),
                                       ('PlateLeft', j_plate[2]), ('PlateRight', j_plate[3]))):
        check('joker %s = %.3f  (sanatta %.3f)' % (name, jc[i], value),
              abs(jc[i] - value) <= TOL,
              'joker karti %s sanatla uyusmuyor - ikon ya da isim bolgesinin disina tasiyor'
              % name)
if pc and len(pc) == 4:
    for i, (name, value) in enumerate((('IconTop', p_panel[0]), ('IconBottom', p_panel[1]),
                                       ('IconLeft', p_panel[2]), ('IconRight', p_panel[3]))):
        check('guc %s = %.3f  (sanatta %.3f)' % (name, pc[i], value),
              abs(pc[i] - value) <= TOL,
              'guc karti %s sanatla uyusmuyor - ikon cercevenin disina tasiyor' % name)

print('=== 3. HER KART KENDI YUVASININ SEKLINDE ===')
layout = read(os.path.join(ROOT, 'Assets', 'Scripts', 'View', 'UiLayout.cs'))


def slots(name):
    return [(float(a), float(b)) for a, b in
            re.findall(r'%s = new Vector2\(([\d.]+)f, ([\d.]+)f\)' % name, layout)]


for label, art, name in (('joker', j_aspect, 'JokerPanel'), ('guc', p_aspect, 'PowerPanel')):
    for w, h in slots(name):
        slot = w / h
        check('%s yuvasi %.0fx%.0f = %.3f, sanat %.3f (fark %%%.1f)'
              % (label, w, h, slot, art, abs(slot - art) / slot * 100),
              abs(slot - art) / slot < 0.03,
              '%s yuvasi ile kart orani tutmuyor - sanat ya eziliyor ya bos bant birakiyor'
              % label)

print('=== 4. SANAT OLDUGU GIBI CIZILIYOR ===')
check('boyali kart Simple ciziliyor, 9-slice DEGIL',
      'card.Body.type = Image.Type.Simple;' in src,
      '9-slice boyali karti her boyutta baska bir karta cevirir')
check('boyali kartin uzerine ikinci bir cerceve cizilmiyor',
      'card.Frame.enabled = !card.Painted;' in src,
      'sanatin kendi kenari ile nadirlik halkasi cakisiyor')
check('boyali kartta kendi oyugumuz kapali',
      'card.Well.enabled = !card.Painted;' in src,
      'sanatin oyugunun uzerine ikinci bir oyuk ciziliyor')
check('durum rengi sanati YENIDEN BOYAMIYOR, uzerine dusuyor',
      'ArtStateCast' in src and 'A STATE CANNOT REPAINT A PAINTING' in src,
      'koyu panel rengi boyali karti karartiyor')
check('PLAKASIZ kartta hicbir yazi cizilmiyor',
      'if (!anatomy.HasPlate)' in method(src, 'private void LayoutPainted('),
      'guc kartinin kenarina isim yaziliyor')
check('murekkep SABIT ORANLA degil, KONTRAST HEDEFIYLE koyulasiyor',
      'ArtInkContrast' in src and 'private static float Contrast(' in src,
      'sabit karisim: soluk nadirlik renkleri plakada 1.9-3.2 kontrastta kaliyor, okunmuyor')
check('koyulasirken HUE korunuyor (doygunluk artiyor)',
      'ArtInkSaturate' in src and 'Color.RGBToHSV(' in src,
      'her renk ayni kahverengiye iniyor - tier okunmuyor')
check('ikon 3:4 oranini koruyor',
      'card.Icon.preserveAspect = true;' in src, 'ikon oyuga gore eziliyor')
check('boyali kartta ikon oyugun ICINDE kaliyor (negatif tasma yok)',
      'card.Painted ? 0f : -12f' in src, 'ikon boyali oyugun disina tasiyor')
check('sanat yoksa duz karta donuyor',
      'string.IsNullOrEmpty(artName) ? null : ViewUtil.CardSprite(artName)' in src,
      'sanat silinirse kart yok olur')

print('=== 5. PLAKA RENGI VE OKUNURLUK OLCULDU ===')
h, w = j_px.shape[:2]
pl = j_px[int(h * (j_plate[0] + j_plate[1]) * 0.5), int(w * (j_plate[2] + j_plate[3]) * 0.5)]
pl = tuple(v / 255.0 for v in pl[:3])
m = re.search(r'ArtPlate = new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)', src)
got = tuple(float(x) for x in m.groups()) if m else (0, 0, 0)
check('ArtPlate = %s  (sanatta %s)' % (tuple(round(v, 3) for v in got),
                                       tuple(round(v, 3) for v in pl)),
      m is not None and all(abs(got[i] - pl[i]) <= 0.04 for i in range(3)),
      'plaka rengi sanatla uyusmuyor - kontrast hedefi yanlis zemine gore hesaplaniyor')


def channel(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def relative(c):
    return 0.2126 * channel(c[0]) + 0.7152 * channel(c[1]) + 0.0722 * channel(c[2])


def contrast(c1, c2):
    l1, l2 = relative(c1) + 0.05, relative(c2) + 0.05
    return max(l1, l2) / min(l1, l2)


def ink(c, target=4.5, boost=1.55, steps=20):
    hh, ss, vv = colorsys.rgb_to_hsv(*c)
    ss = min(1.0, ss * boost)
    for i in range(steps + 1):
        out = colorsys.hsv_to_rgb(hh, ss, vv * (1 - i / steps))
        if contrast(out, got) >= target:
            return out
    return (0.17, 0.12, 0.09)


PALETTE = [('Common', (0.78, 0.82, 0.90)), ('Rare', (0.38, 0.72, 1.0)),
           ('Legendary', (1.0, 0.78, 0.30)), ('JokerTag', (0.82, 0.68, 1.0)),
           ('JokerName', (1.0, 0.93, 0.72)), ('PowerTag', (0.55, 0.92, 0.95)),
           ('Ucuz', (1.0, 0.92, 0.45)), ('Pahali', (1.0, 0.45, 0.4))]
worst = min(contrast(ink(c), got) for _, c in PALETTE)
check('paletteki HER renk plakada >= 4.5 kontrast (en kotusu %.2f)' % worst, worst >= 4.49,
      'bir renk plakada hala okunmuyor')

print('=== 6. MARKET AYNI KARTI, AYNI OLCULERLE CIZIYOR ===')
market = read(os.path.join(ROOT, 'Assets', 'Scripts', 'View', 'MarketView.cs'))
check('market kendi kesirlerini tutmuyor, HeldItemCard.ArtFor okuyor',
      'HeldItemCard.ArtFor(' in market and 'new CardArt(' not in market,
      'olcum iki yere kopyalanmis - biri sanat yeniden cizildigi gun bayatlar')
check('market YALNIZ plakali karti kullaniyor (isimsiz raf karosu olmaz)',
      '.HasPlate' in method(market, 'private static Sprite PaintedCardFor('),
      'rafta ismi ve fiyati yazacak yeri olmayan bir kart kullaniliyor')
check('boyali kart bolmeye ORANI KORUNARAK sigiyor',
      'private static Vector2 FitPainted(' in market,
      'kart bolmenin sekline esnetiliyor - altin cizgi kayiyor, koseler ovalleniyor')
check('fiyat plakanin uzerinde ve MUREKKEP',
      'paintedPriceAt' in market and 'HeldItemCard.InkOn(priceColor)' in market,
      'fiyat kartin altinda bosta ya da acik plakada acik renkte')
check('sanat yoksa market eski duz karosuna donuyor',
      'Sprite painted = PaintedCardFor(key);' in market,
      'sanat silinirse market karosu yok olur')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('kart kontrolu: hepsi tamam')
