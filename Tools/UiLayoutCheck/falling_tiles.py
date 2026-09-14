# Dusen kupler (defolu blok): her blok turu HANGI dokuyla dusuyor?
#
# Neden var: dusen kupler uzun sure duz renkli kare olarak cizildi. Labda "tum turler" sirasi
# ayni kareyi farkli renklerde gosterdi ve bu "derleniyor" ile gorunmuyordu. Bu betik:
#   1. ViewUtil'in doku tablolarini kaynaktan okur ve CardCubeTile kuralini birebir uygulayarak
#      her turun (duz + her element) L blogunun her kupu icin hangi PNG'nin kullanilacagini listeler,
#   2. o PNG'lerin Assets/Resources/Art/Blocks altinda gercekten var oldugunu kontrol eder,
#   3. dusen kuplerin, elin ve labin ayni kurali (CardCubeTile) kullandigini yapisal olarak dogrular.
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
V = os.path.join(ROOT, 'Assets', 'Scripts', 'View')
ART = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Blocks')
fail = []


def read(name):
    return open(os.path.join(V, name), encoding='utf-8').read()


def body(src, signature):
    i = src.index(signature)
    j = src.index('\n        }', i)
    return src[i:j]


vu = read('ViewUtil.cs')
by_element = dict(re.findall(r'case BlockElement\.(\w+): return Tile\("(\w+)"\);',
                             body(vu, 'private static Sprite OwnTile(BlockElement element)')))
by_kind = dict(re.findall(r'case CubeKind\.(\w+): return Tile\("(\w+)"\);',
                          body(vu, 'private static Sprite OwnTile(CubeKind kind)')))
default_tile = re.search(r'DefaultTile\s*\{\s*get \{ return Tile\("(\w+)"\)', vu).group(1)
body_tile = re.search(r'Tile\("(block_target_body)"\)', vu).group(1)
elements = re.findall(r'^\s+([A-Z]\w+) = \d+,?\s*$',
                      open(os.path.join(ROOT, 'Assets', 'Scripts', 'Core', 'Blocks', 'BlockElement.cs'),
                           encoding='utf-8').read(), flags=re.M)


def card_tile_for_normal(card_elements):
    """ViewUtil.CubeTile(CubeKind.Normal, card)."""
    if 'Targeted' in card_elements:
        return body_tile
    for e in card_elements:
        if e in by_element:
            return by_element[e]
    return default_tile


def cube_tile(card_elements, per_cube, index):
    """ViewUtil.CardCubeTile - no TargetCellIndex is ever set on a lab card, so the target shows
    only through a designed (per-cube) card, exactly as the lab builds it."""
    element = None
    for e in card_elements:
        if e != 'Targeted':
            element = e
            break
    if per_cube is not None:
        element = per_cube[index]
        if element is not None:
            return by_element.get(element, default_tile)
    return card_tile_for_normal(card_elements)


print('=== HER TUR HANGI DOKUYLA DUSUYOR (L blok, 3 kup) ===')
rows = [('DUZ', [], None)]
for e in elements:
    if e == 'Void':
        continue    # kara delik: jokerin tek rauntluk tuzagi, markette satilmaz -> defolu olamaz
    if e == 'Targeted':
        rows.append((e, ['Targeted'], ['Targeted', None, None]))
    else:
        rows.append((e, [e], None))
faces = {}
for name, els, per_cube in rows:
    tiles = [cube_tile(els, per_cube, i) for i in range(3)]
    missing = [t for t in set(tiles) if not os.path.exists(os.path.join(ART, t + '.png'))]
    faces[name] = tuple(tiles)
    print('   %-12s %s%s' % (name, '  '.join(tiles), ('   EKSIK: ' + ', '.join(missing)) if missing else ''))
    if missing:
        fail.append('%s icin dosya yok: %s' % (name, missing))

print()
print('=== DUZ BLOKLA AYNI YUZE DUSEN TURLER ===')
same = [n for n, f in faces.items() if n != 'DUZ' and f == faces['DUZ']]
print('   ' + (', '.join(same) if same else 'yok'))

print()
print('=== AYNI KURAL MI ===')


def check(label, ok, why):
    print('   %-46s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


fb = read('GameUiController.Feedback.cs')
spawn = fb[fb.index('private void SpawnFallingCubes('):]
spawn = spawn[:spawn.index('private static GridPos FallOrigin(')]
check('dusen kupler CardCubeTile soruyor', 'ViewUtil.CardCubeTile(' in spawn,
      'SpawnFallingCubes elin kuralini kullanmiyor')
check('dusen kupler duz renk karesi DEGIL', 'ElementColor(card.Elements[0])' not in spawn,
      'SpawnFallingCubes hala element rengiyle duz kare ciziyor')
fx = read('FallingCubeFx.cs')
check('FallingCubeFx dokuyu giydiriyor', 'Sprite tile' in fx and 'ViewUtil.ApplyTile(' in fx,
      'FallingCubeFx doku almiyor')
cv = read('CardVisual.cs')
check('el ayni kurali kullaniyor', 'ViewUtil.CardCubeTile(' in cv,
      'CardVisual kendi kopyasini tasiyor - el ile dusus ayrisabilir')
check('lab hedefli blogu gercek hedefle kuruyor', 'BlockCard.Designed(' in read('GameUiController.AnimationLab.cs'),
      'lab hedefli blogu hedef kupu olmadan kuruyor')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
