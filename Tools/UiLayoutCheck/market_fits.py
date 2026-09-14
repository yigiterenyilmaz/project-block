# DIKEY MARKET SIGIYOR MU, KAYIYOR MU, DESTELERI ORTUYOR MU?
# Butun sayilar koddan okunuyor.
import re

L = open(r'C:\Users\yigit\project-block\Assets\Scripts\View\UiLayout.cs', encoding='utf-8').read()
M = open(r'C:\Users\yigit\project-block\Assets\Scripts\View\MarketView.cs', encoding='utf-8').read()
CARD_H, CARD_W = 1.8, 1.35

def val(body, k):
    m = re.search(r'\b%s = ([\d.]+)f?;' % k, body)
    assert m, k
    return float(m.group(1))

port = L[L.index('private void LoadPortrait()'):L.index('public void Resolve')]
cons = L[L.index('public const float PortraitBoardFill'):L.index('private void LoadPortrait()')]
FILL=val(cons,'PortraitBoardFill'); HUD=val(cons,'PortraitHudBand'); GAP=val(cons,'PortraitBoardGap')
FOOT=val(cons,'PortraitHandFoot'); PGAP=val(cons,'PortraitPileGap'); PINS=val(cons,'PortraitPileInset')
BOARD=val(port,'BoardWorldSize'); CARDS=val(port,'CardScale'); PILES=val(port,'PileScale')
TOPR=val(port,'MarketTopReserve'); SIDER=val(port,'MarketSideReserve')
FOOTH=val(port,'MarketFooterHeight'); BLOCKS=val(port,'MarketBlockSection'); NAMED=val(port,'MarketNamedSection')
TITLE=val(M,'DemoTitleHeight'); ROWGAP=val(M,'DemoRowGap')

bad=0
for name,w,h in [('9:16',1080,1920),('9:19.5',1080,2340),('3:4',1536,2048)]:
    a=w/h
    stack=HUD+GAP+FOOT+CARD_H*CARDS+PGAP+(CARD_H+0.18)*PILES
    fill=min(FILL, 1.0/(a*(1.0+stack/BOARD))*0.985)
    half=BOARD*0.5/fill; ortho=half/a
    cardHalf=CARD_H*CARDS/2; handCy=-ortho+FOOT+cardHalf
    pileHalf=(CARD_H+0.18)*PILES/2
    pileY=handCy+cardHalf+PGAP+pileHalf; pileTop=pileY+pileHalf
    botR=val(port,'MarketBottomReserve')
    pxMin=-ortho+botR; pxMax=ortho-TOPR
    pw=(half-SIDER)*2
    win=(pxMax-TITLE)-(pxMin+FOOTH)
    content=BLOCKS+NAMED*2+ROWGAP*2
    checks=[
      ('panel ekrana sigiyor', pw<=half*2+1e-6),
      ('panel ekranin cogunu kapliyor', (pxMax-pxMin)/(2*ortho) > 0.70),
      ('pencere pozitif', win>0.5),
      ('kaydirma dogru', (content>win) == (max(0,content-win)>0.001)),
    ]
    f=[c for c,ok in checks if not ok]; bad+=len(f)
    print('%-7s ortho %.2f  panel %.2f gen x %.2f yuk  pencere %.2f  icerik %.2f  kaydirma %.2f'
          % (name, ortho, pw, pxMax-pxMin, win, content, max(0,content-win)))
    print('        %s' % ('TAMAM' if not f else 'SORUN: '+', '.join(f)))
print()
print('SONUC: ' + ('dikey market saglam' if bad==0 else '%d SORUN' % bad))
raise SystemExit(1 if bad else 0)
