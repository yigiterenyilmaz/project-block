# DIKEY YERLESIM GERCEKTEN SIGIYOR MU?
#
# UiLayout.Resolve'un matematigini birebir yansitir ve koddaki DEGERLERI okur - burada
# elle yazilmis sayi yok. Her seyi ekranin gorunen alanina karsi olcer.

import re

SRC = r'C:\Users\yigit\project-block\Assets\Scripts\View\UiLayout.cs'
CARD_H = 1.8   # CardVisual.BodyHeight


def val(body, name):
    m = re.search(r'\b%s = (-?[\d.]+)f?;' % name, body)
    assert m, name + ' bulunamadi'
    return float(m.group(1))


src = open(SRC, encoding='utf-8').read()
port = src[src.index('private void LoadPortrait()'):src.index('public void Resolve')]
consts = src[src.index('public const float PortraitBoardFill'):src.index('private void LoadPortrait()')]

FILL = val(consts, 'PortraitBoardFill')
HUD = val(consts, 'PortraitHudBand')
GAP = val(consts, 'PortraitBoardGap')
FOOT = val(consts, 'PortraitHandFoot')
PILEGAP = val(consts, 'PortraitPileGap')
INSET = val(consts, 'PortraitPileInset')
BOARD = val(port, 'BoardWorldSize')
SPACING = val(port, 'HandSpacing')
CARDS = val(port, 'CardScale')
PILES = val(port, 'PileScale')

print('koddan okunan: tahta %.2f  doluluk %.2f  kart %.2f  aralik %.2f  deste %.2f'
      % (BOARD, FILL, CARDS, SPACING, PILES))
print()

bad = 0
for name, w, h in [('9:16   ', 1080, 1920), ('9:19.5 ', 1080, 2340), ('3:4 tablet', 1536, 2048)]:
    aspect = w / h
    stack = (HUD + GAP + FOOT + CARD_H * CARDS + PILEGAP + (CARD_H + 0.18) * PILES)
    fits = 1.0 / (aspect * (1.0 + stack / BOARD))
    fill = min(FILL, fits * 0.985)
    half = BOARD * 0.5 / fill
    ortho = half / aspect
    top, bottom = ortho, -ortho
    board_cy = top - HUD - GAP - BOARD / 2
    board_bottom = board_cy - BOARD / 2
    card_half = CARD_H * CARDS / 2
    hand_cy = bottom + FOOT + card_half
    hand_top = hand_cy + card_half
    pile_half = (CARD_H + 0.18) * PILES / 2
    pile_y = hand_top + PILEGAP + pile_half
    pile_top = pile_y + pile_half
    hand_span = 4 * SPACING + 1.35 * CARDS     # 5 kart

    checks = [
        ('tahta ekranda', board_bottom > bottom and board_cy + BOARD / 2 < top),
        ('el ekranda', hand_cy - card_half > bottom),
        ('el tahtayla cakismiyor', hand_top < board_bottom),
        ('desteler tahtayla cakismiyor', pile_top < board_bottom),
        ('desteler elle cakismiyor', pile_y - pile_half > hand_top - 0.001),
        ('el yatayda siger', hand_span < half * 2),
        ('desteler yatayda siger', half - INSET + pile_half * 0.75 < half + 0.3),
    ]
    fails = [c for c, ok in checks if not ok]
    bad += len(fails)
    print('%s doluluk %.0f%%  ortho %.2f  yariw %.2f  tahta[%.2f..%.2f]  el %.2f  deste %.2f'
          % (name, fill * 100, ortho, half, board_bottom, board_cy + BOARD / 2, hand_cy, pile_y))
    print('   el acikligi %.2f / %.2f kullanilabilir   %s'
          % (hand_span, half * 2, 'TAMAM' if not fails else 'SORUN: ' + ', '.join(fails)))

print()
print('SONUC: ' + ('her oranda siger' if bad == 0 else '%d SORUN' % bad))
raise SystemExit(1 if bad else 0)
