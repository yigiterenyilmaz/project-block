# MASAUSTU PROFILI ESKI SABITLERLE AYNI MI?
#
# Vaat suydu: UiLayout.Desktop, bu degisiklikten ONCEKI dosyalarda duran sayilarin birebir
# kopyasi. Bunu iddia etmek yerine olcuyoruz - degerleri git'ten, HEAD'deki (yani dokunulmamis)
# dosyalardan cekip yeni profildekilerle karsilastiriyoruz.

import re
import subprocess

REPO = r'C:\Users\yigit\project-block'


def old(path):
    return subprocess.check_output(['git', 'show', 'HEAD:' + path],
                                   cwd=REPO).decode('utf-8', 'replace')


def now(path):
    with open(REPO + '\\' + path.replace('/', '\\'), encoding='utf-8') as f:
        return f.read()


def num(text, pattern, label):
    m = re.search(pattern, text)
    assert m, 'BULUNAMADI: ' + label
    return tuple(round(float(g), 6) for g in m.groups())


gui = old('Assets/Scripts/View/GameUiController.cs')
card = old('Assets/Scripts/View/CardLayerView.cs')
views = old('Assets/Scripts/View/GameUiController.Views.cs')
joker = old('Assets/Scripts/View/JokerBarView.cs')
power = old('Assets/Scripts/View/PowerBarView.cs')
badge = old('Assets/Scripts/View/GameUiController.BossBadge.cs')
market = old('Assets/Scripts/View/MarketView.cs')
scene = old('Assets/Scenes/enes.unity')

expected = {
    'OrthoSize': num(scene, r'orthographic size: ([\d.]+)', 'kamera'),
    'BoardWorldSize': num(gui, r'maxBoardWorldSize = ([\d.]+)f', 'tahta kutusu'),
    'BoardCenter': num(gui, r'BoardCenter = new Vector2\((-?[\d.]+)f, (-?[\d.]+)f\)', 'tahta merkezi'),
    'HandCenter': num(card, r'HandCenter = new Vector2\((-?[\d.]+)f, (-?[\d.]+)f\)', 'el merkezi'),
    'HandSpacing': num(card, r'HandSpacing = ([\d.]+)f', 'el araligi'),
    'HandFanSpan': num(card, r'HandFanSpan = ([\d.]+)f', 'yelpaze'),
    'HandFanSpanMax': num(card, r'HandFanSpanMax = ([\d.]+)f', 'yelpaze maks'),
    'HandMinSpacing': num(card, r'HandMinSpacing = ([\d.]+)f', 'en dar aralik'),
    'DrawPile': num(card, r'DrawPilePos = new Vector2\((-?[\d.]+)f, (-?[\d.]+)f\)', 'cekme destesi'),
    'DiscardPile': num(card, r'DiscardPilePos = new Vector2\((-?[\d.]+)f, (-?[\d.]+)f\)', 'iskarta'),
    'CanvasReference': num(views, r'referenceResolution = new Vector2\(([\d.]+)f, ([\d.]+)f\)', 'canvas'),
    'ScoreFont': num(views, r'TextAnchor\.UpperCenter, (\d+), new Color\(1f, 0\.86f', 'skor punto'),
    'MessageFont': num(views, r'TextAnchor\.UpperCenter, (\d+), new Color\(1f, 0\.92f', 'mesaj punto'),
    'InfoFont': num(views, r'InfoFontSize = (\d+)', 'bilgi punto'),
    'InfoWidth': num(views, r'InfoWidth = ([\d.]+)f', 'bilgi genisligi'),
    'ScoreTop': num(views, r'"TotalText", new Vector2\(0\.5f, 1f\),\s*\n\s*new Vector2\(0f, -([\d.]+)f\)', 'skor y'),
    'MessageTop': num(views, r'"MessageText", new Vector2\(0\.5f, 1f\),\s*\n\s*new Vector2\(0f, -([\d.]+)f\)', 'mesaj y'),
    'JokerPanel': num(joker, r'PanelWidth = ([\d.]+)f;\s*\n\s*private const float PanelHeight = ([\d.]+)f', 'joker paneli'),
    'JokerGap': num(joker, r'PanelGap = ([\d.]+)f', 'joker araligi'),
    'PowerPanel': num(power, r'PanelWidth = ([\d.]+)f;\s*\n\s*private const float PanelHeight = ([\d.]+)f', 'guc paneli'),
    'PowerGap': num(power, r'PanelGap = ([\d.]+)f', 'guc araligi'),
    'CornerInset': num(joker, r'CornerInset = ([\d.]+)f', 'kose payi'),
    'BadgeSize': num(badge, r'BossBadgeSize = ([\d.]+)f', 'rozet'),
    'BadgeMargin': num(badge, r'BossBadgeMargin = ([\d.]+)f', 'rozet payi'),
    'MarketTopReserve': num(market, r'DemoTopReserve = ([\d.]+)f', 'market ust'),
    'MarketBottomReserve': num(market, r'DemoBottomReserve = ([\d.]+)f', 'market alt'),
    'MarketSideReserve': num(market, r'DemoSideReserve = ([\d.]+)f', 'market yan'),
}

# UiLayout.LoadDesktop icindeki degerler
layout = now('Assets/Scripts/View/UiLayout.cs')
body = layout[layout.index('private void LoadDesktop()'):
                      layout.index('private void LoadPortrait()')]

got = {}
for name in expected:
    m = re.search(r'\b%s = new Vector2\((-?[\d.]+)f, (-?[\d.]+)f\)' % name, body)
    if not m:
        m = re.search(r'\b%s = (-?[\d.]+)f?;' % name, body)
    assert m, 'UiLayout.LoadDesktop icinde yok: ' + name
    got[name] = tuple(round(float(g), 6) for g in m.groups())

bad = 0
for name in sorted(expected):
    same = expected[name] == got[name]
    if not same:
        bad += 1
    print('  %-20s %-22s %s %s'
          % (name,
             ','.join(str(v) for v in expected[name]),
             '==' if same else '!=',
             ','.join(str(v) for v in got[name])))

print()
print('%d deger karsilastirildi, %d fark' % (len(expected), bad))
print('SONUC: ' + ('MASAUSTU YERLESIMI DEGISMEDI' if bad == 0
                   else 'FARK VAR - PC yerlesimi kaymis'))
raise SystemExit(1 if bad else 0)
