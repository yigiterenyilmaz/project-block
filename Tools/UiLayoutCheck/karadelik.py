# -*- coding: utf-8 -*-
# KARA DELIK / SINGULARITY - statik kontrol.
#
# Korudugu seyler:
# 1) VIEW HICBIR SEY HESAPLAMIYOR. Yutulanlar, cekilenler, cokus, puan, kutle, yutulan deste ve
#    reddedilen itmeler Core'un raporu (LastTurn, LastDeckSwallow, AnchorRefusals). Etki halkasi
#    Core'un yardimcisi (InfluenceAt); View kendi yaricapini uydurmaz. Lab gercek yercekimini kosar.
# 2) UC KATMAN. Olay ufku (detaysiz siyah, ince tek tarafli kirilma kenari), toplanma diski (iki bant,
#    iki hiz, iceri akan madde, lokal sicak vurgu), bukulme (halka 1 ve 2, halka 3 sifir, yalniz
#    cizim: hucre/hitbox yerinde).
# 3) YUTMA FADE DEGIL. Spagettilesme + ufkun arkasina girme (renderer basina maske), disari patlama
#    yok, kutle nabzi var. Cokus siradan temizlik dalgasini oynatmaz. Deste yutma yalniz secilen desteyi
#    etkiler, bos deste ile yutulmus deste ayri gorunur.
from __future__ import print_function
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
fail = []


def check(label, ok, why):
    print('   %-78s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8-sig').read()


def strip_comments(src):
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return '\n'.join(l for l in src.split('\n') if not l.strip().startswith('//'))


V = 'Assets/Scripts/View/'
view = strip_comments(read(V + 'BlackHoleView.cs'))
ctrl = strip_comments(read(V + 'GameUiController.BlackHole.cs'))
lab = strip_comments(read(V + 'GameUiController.AnimBlackHole.cs'))
feedback = strip_comments(read(V + 'GameUiController.Feedback.cs'))
boardview = strip_comments(read(V + 'BoardView.cs'))
joker = strip_comments(read('Assets/Scripts/Core/Jokers/Definitions/KaraDelikJoker.cs'))
hole = read('Assets/Resources/Shaders/BlackHole.shader')
matter = read('Assets/Resources/Shaders/BlackHoleMatter.shader')

print('1) view hesaplamiyor')
check('etki halkasi Core yardimcisindan (InfluenceAt)', 'KaraDelikJoker.InfluenceAt(' in view, 'view kendi yaricapini uyduruyor')
check('view kutle/puan/deste secimi hesaplamiyor', 'SwallowedThisRound' not in view and 'DrawCount' not in view
      and 'PointsPerCube' not in view, 'view kural sayilarina bakiyor')
check('puan Core raporundan (PointsEach)', 'prepared.PointsEach' in view, 'puan raporu okunmuyor')
check('kutle Core sayacindan (SetMass <- SwallowedThisRound / GoalFor)',
      'joker.SwallowedThisRound' in ctrl and 'KaraDelikJoker.GoalFor(' in ctrl, 'kutle kaynagi yanlis')
check('reddedilen itmeler Core logundan (AnchorRefusals)', 'model.AnchorRefusals' in view, 'red tepkisi uyduruluyor')
check('rapor kimlikle eslesiyor (ReferenceEquals)', 'ReferenceEquals(report, prepared)' in view
      and 'Serial ==' not in view, 'rapor seri numarasiyla eslesiyor')
check('lab gercek yercekimini kosuyor (RunGravity / RunCollapse / Place / ShiftRowsUp)',
      all(k in lab for k in ['KaraDelikJoker.RunGravity(', 'KaraDelikJoker.RunCollapse(', 'board.Place(', 'board.ShiftRowsUp(']),
      'lab kopya kural kullaniyor')
check('joker ve lab ayni statik adimlari kullaniyor', 'Eat(board, report, destroy)' in joker
      and 'public static bool RunGravity(' in joker, 'joker ile lab ayrisiyor')

print('2) uc katman')
check('ufuk: detaysiz siyah', 'float3 black = float3(0.006, 0.007, 0.016);' in hole, 'ufuk rengi')
check('ufuk kenari tek tarafli (duz halka ikon olur)', 'pow(saturate(cos(a - _Angle - 0.9) * 0.5 + 0.5), 3.0)' in hole,
      'kenar her yonde esit')
check('disk: iki bant, dis bant 0.75x', 'OuterRatio = 0.75f' in view and 'float outer = Band(' in hole, 'disk bantlari')
check('disk: madde iceri akiyor (zamanli radyal kayma)', '_Clock * 0.45' in hole, 'disk sert doku gibi donuyor')
check('disk hizi 20-35 derece/sn', re.search(r'DiskSpeed = (2\d|3[0-5])f', view) is not None, 'disk hizi')
check('kutle katmani HUD halkasi degil (tanecikli, dalgali)', 'pow(WrapNoise(' in hole and 'sin(a * 3.0 + _Seed)' in hole,
      'kutle halkasi duz daire')
check('bukulme yalniz halka 1 ve 2 (Reach)', 'KaraDelikJoker.Reach' in view, 'bukulme siniri')
check('bukulme malzemesi ortak, hucre basina MaterialPropertyBlock', 'r.GetPropertyBlock(block)' in view
      and 'new Material(' in view and view.count('new Material(') == 2, 'hucre basina malzeme')
check('bukulen su/ates kendi hareketini koruyor (warp kopyalaniyor)', 'CopyWarp(own, block)' in view, 'warp kayboluyor')
check('buz kupu bukulmuyor (Buzluk malzemesi korunuyor)', 'CubeKind.Ice' in view, 'buz malzemesi eziliyor')
check('board delik hucresini bu katmana birakiyor', 'BlackHoleView.Available' in boardview
      and 'BlackHoles.Sync(this)' in boardview, 'board delik hucresine kare ciziyor')

print('3) yutma, cokus, deste')
check('ufuk maskesi renderer basina (SpriteMask degil)', '_Occlude' in matter and 'SpriteMask' not in view,
      'ortak maske baska vekilleri de gizler')
check('spagettilesme: radyal 1.40, tegetsel 0.25', '1.40f' in view and '0.25f, (u - 0.75f)' in view, 'spagetti degerleri')
check('yutma disari parcacik sacmiyor (yalniz nabiz)', 'Pulses.Add(clock)' in view, 'kutle nabzi yok')
check('coklu yutma kademeli, toplam sikistirilmis', 'SwallowSpan / (swallows.Count - 1)' in view, 'kademe yok')
check('3 ustu tek biriken toplam', 'PerBlockTickMax' in view and 'rolling' in view, 'puan yazisi yagmuru')
check('cekme gercek from -> to, hedef hucre tutuluyor', 'held.Add(pull.To)' in view and 'owner.HoldCells(held)' in view,
      'sahte izgara hareketi')
check('cokus: siradan temizlik dalgasi ve sarsinti oynamiyor',
      'report.CleanSweep && !sweepIsHoleCollapse' in feedback and 'ordinarySweep' in feedback, 'cokus siradan temizlik gibi')
check('cokus: mesafe kovalariyla dalga', 'distance BUCKETS' in read(V + 'BlackHoleView.cs'), 'hucre hucre seri')
check('cokus: kupler kendi renginde patliyor', 'ViewUtil.CubeMaterialColor(p.Cube)' in view, 'patlama rengi')
check('deste yutma: yalniz secilen deste, olay gosterimi view karari degil',
      'ev.FromDrawPile ?' in ctrl and 'report.FromDrawPile' in view, 'deste secimi view de')
check('yutulan deste bos desteden ayri (kalinti yalniz yutulmus + bos iken)',
      'voidedPiles.Contains(true) && round.Deck.DrawCount == 0' in ctrl, 'bos ve yutulmus ayni')
check('delik yokken gecici ufuk tahta kenarinda', 'BoardEdgeToward(' in view, 'delik yokken yutma cizilmiyor')

print()
if fail:
    print('KARA DELIK: %d HATA' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('KARA DELIK: hepsi tamam')
