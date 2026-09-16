# -*- coding: utf-8 -*-
# ELMAS KAZMA / DIAMOND QUARRY - statik kontrol.
#
# Bu dosyanin korudugu UC sey var.
#
# 1) VIEW HICBIR SEY SECMIYOR. Hangi obsidyenlerin kirildigi ve ne kadar odedigi Core'un
#    raporu (DestroyCubes'in DONDURDUGU hucreler, kirilmadan once alinan kupler, dokumden
#    olculen puan). View tahtada obsidyen aramaz.
#
# 2) SIRA MESAJDIR. Temizlik normal kupleri goturur; obsidyen VEKIL olarak ayakta kalir, temizlik
#    dalgasi bitince elmas kazma onu kirar. Eskiden zorla kirma hicbir patlama listesine girmedigi
#    icin obsidyen temizligin altinda sessizce kayboluyordu.
#
# 3) PATLAMANIN RENGI DEGISMIS HALI DEGIL. Rezonans -> buyuyen kristal catlak -> kisa capraz
#    darbe -> darbe ekseninde sikisma -> KUPUN KENDI YUZUNDEN kamalar + soguk toz -> puan.
from __future__ import print_function
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
fail = []


def check(label, ok, why):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8-sig').read()


def strip_comments(src):
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return '\n'.join(l for l in src.split('\n')
                     if not l.strip().startswith('//') and not l.strip().startswith('///'))


def method(src, signature):
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


def num(src, name, default=-999.0):
    m = re.search(name + r'\s*=\s*(-?[\d.]+)f?;', src)
    return float(m.group(1)) if m else default


def between(label, v, lo, hi, why):
    check('%s %.3f in [%.3f, %.3f]' % (label, v, lo, hi), lo <= v <= hi, why)


joker = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'ElementJokers.cs'))
kazma = method(joker, 'public sealed class ElmasKazmaJoker')
view_raw = read('Assets', 'Scripts', 'View', 'QuarryBreakView.cs')
view = strip_comments(view_raw)
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'QuarryShapes.cs'))
ctrl = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Quarry.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
rebate = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Rebate.cs'))
lab = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. CORE SECIYOR, VIEW OYNATIYOR ===')
check('LastQuarry [NotSaved]', re.search(r'\[field: NotSaved\]\s*public QuarryVisuals LastQuarry', kazma) is not None,
      'rapor kayda giriyor')
check('kupler YOK EDILMEDEN once alinıyor', kazma.index('before[obsidian[i]] = cube.Value;')
      < kazma.index('DestroyCubes(obsidian, true, true)'), 'kirilan kupun yuzu kayip')
check('hucreler DestroyCubes`in dondurdugu liste', 'report.Cells.Add(cracked[i]);' in kazma,
      'reddedilen bir kup de ciziliyor')
check('puan dokumden OLCULUYOR (flat + late flat, olcekte)',
      'int paidBefore = score.FlatBonus + score.LateFlat;' in kazma
      and '(score.FlatBonus + score.LateFlat - paidBefore) * score.ScoreScale' in kazma,
      'puan kopyalaniyor')
check('View tahtada obsidyen ARAMIYOR', 'CellsOfKind' not in view and 'CellsOfKind' not in ctrl
      and 'CountCubesOfKind' not in view, 'View kurali tekrar ediyor')
check('View puani hesaplamiyor (PointsPerObsidian yok)', 'PointsPerObsidian' not in view,
      'View kendi puanini uretiyor')
check('yazilan sayi raporun (Amount = Points / PointsEach)',
      'Amount = report.PointsEach' in view and 'Amount = report.Points' in view, 'sayi rapordan degil')
check('test: rapor gercek temizlikte dogrulaniyor',
      'the report names exactly the obsidian the pickaxe took' in tests
      and "the points are the joker's own share of the turn" in tests, 'rapor dogrulanmamis')

print()
print('=== 2. SIRA: TEMIZLIK -> OBSIDYEN KALIR -> ELMAS KIRAR ===')
check('vekiller yenilemede kurulur (taslar bir kare bile kaybolmaz)',
      'SyncQuarry();' in method(fb, 'private void RefreshAll(') and 'quarry.Prepare(' in ctrl,
      'obsidyen temizlikten once yok oluyor')
check('kirma patlama aninda baslar, temizlik dalgasini bekler',
      'PlayQuarry(report);' in method(fb, 'private void PlayExplosionFeedback(')
      and 'BoardCleanseView.Style.ChargeUpDuration + BoardCleanseView.Style.WaveSeconds' in ctrl,
      'kazma temizlikle ayni anda / onceden calisiyor')
between('temizlik sonrasi sakinlik (sn)', num(view, 'Settle'), 0.06, 0.10, 'bekleme cok uzun/kisa')
check('baslamayan hazirlik 3 sn sonra yine oynar (takili kalmaz)', 'clock - preparedAt > 3f' in view,
      'vekil sonsuza kadar kalabilir')
check('olay KIMLIKLE eslenir; RESET oynatilmis sayar',
      'ReferenceEquals(r, lastBegun)' in view and 'quarry.MarkSeen(' in ctrl and 'Serial' not in view,
      'tekrar / atlama')
check('yuz: tahtanin son gosterdigi (karanlik raunt karanlik kalir)', 'boardView.TryCubeLook(cell' in ctrl,
      'karanlik raunt sizdiriyor')
check('patlama dili YOK (FlashCells / ShakeCamera / duman / flas)',
      'FlashCells' not in view + ctrl and 'ShakeCamera' not in view + ctrl and 'Smoke' not in view
      and 'flash' not in view.lower(), 'normal patlama yeniden boyanmis')
check('dev kazma sprite`i yok', 'pickaxe' not in view.lower() or 'Resources.Load' not in view,
      'kazma ikonu')

print()
print('=== 3. VURUSLAR ===')
between('rezonans (sn)', num(view, 'Attune'), 0.09, 0.12, 'rezonans suresi')
between('rezonans olcegi', num(view, 'AttuneScale'), 1.01, 1.02, 'bounce gibi')
check('rezonans: soguk yuz + YALNIZ ust/sol kenar yansimasi + merkez stres noktasi',
      'QuarryShapes.Plate' in view and 'QuarryShapes.Rim' in view and 'HazineShapes.Mote' in view
      and 'float top' in method(shapes, 'public static Sprite Rim') and 'float bottom' not in method(shapes, 'public static Sprite Rim'),
      'rezonans secim kutusu gibi')
between('catlak buyume (sn)', num(view, 'Crack'), 0.13, 0.22, 'catlak hizi')
between('catlak kalinligi (px)', num(view, 'CrackPx'), 1, 3, 'catlak kalin')
check('catlak BUYUYEREK (kosu boyunca gorunen uzunluk)', 'float shown = total * g;' in view,
      'catlak aninda beliriyor')
check('catlak koseli: dogru kosular + kucuk donusler', 'for (int k = 0; k < 3; k++)' in method(view, 'private static void AddRun('),
      'catlak egri / duz tek cizgi')
check('ana catlak + 1 ikincil + en fazla 2 dal (orumcek agi yok)',
      'int branches = n <= 4 ? 2' in view, 'catlak agi')
between('darbe hazirligi (sn)', num(view, 'Anticipation'), 0.05, 0.08, 'hazirlik')
between('hazirlik kaymasi (px)', num(view, 'AnticipationPx'), 1, 2, 'hazirlik kaymasi')
check('hazirlik kaymasi darbenin TERSINE', 'offset = -strikeDir * Style.AnticipationPx' in view, 'yon yanlis')
between('darbe suresi (sn)', num(view, 'Strike'), 0.045, 0.07, 'darbe suresi')
between('darbe uzunlugu (kup)', num(view, 'StrikeLength'), 0.8, 1.2, 'darbe boyu')
between('darbe genisligi (px)', num(view, 'StrikePx'), 3, 7, 'darbe kalinligi')
spread = num(view, 'StrikeAngleSpread')
check('darbe acisi 45 +- %.0f -> 35..55 (ve iki kosegen)' % spread, 45 - spread >= 35 and 45 + spread <= 55
      and '180f - strike' in view, 'aci disinda')
check('darbe KONIK: basta parlak, arkasi sifira incelir',
      'Mathf.Pow(x / 0.82f, 1.4f)' in method(shapes, 'public static Sprite Strike'), 'lazer / kilic izi')
check('darbe catlak kavsagina iner', 'Vector2 junction = at + s.J * size;' in method(view, 'private void PaintStone('),
      'darbe rastgele yere')
between('sikisma (sn)', num(view, 'Compression'), 0.06, 0.09, 'sikisma suresi')
between('darbe boyunca sikisma', num(view, 'CompressAlong'), 0.94, 0.97, 'sikisma miktari')
between('darbeye dik genisleme', num(view, 'CompressAcross'), 1.01, 1.03, 'dik genisleme')
check('sikisma DARBE EKSENINDE (pivot darbeye donuk, cocuk geri donuk)',
      's.Pivot.rotation = Quaternion.Euler(0f, 0f, degrees);' in view
      and 'Quaternion undo = Quaternion.Euler(0f, 0f, -degrees);' in view, 'sikisma eksensiz')
between('kirilma (sn)', num(view, 'Shatter'), 0.14, 0.22, 'kirilma suresi')
check('parcalar KUPUN KENDI YUZUNDEN kesilir', 'QuarryShapes.Wedge(s.Tile, poly, key)' in view
      and 'OverrideGeometry' in shapes, 'genel parcacik yeniden boyanmis')
check('kamalar catlak kavsagindan gecen cizgilerle', 'QuarryShapes.SplitSquare(s.J, s.Lines)' in view,
      'parcalar catlakla ilgisiz')
between('buyuk parca mesafe min (px)', num(view, 'BigShardPxMin'), 6, 14, 'buyuk parca')
between('buyuk parca mesafe max (px)', num(view, 'BigShardPxMax'), 6, 14, 'buyuk parca')
between('kucuk parca mesafe min (px)', num(view, 'SmallShardPxMin'), 8, 20, 'kucuk parca')
between('kucuk parca mesafe max (px)', num(view, 'SmallShardPxMax'), 8, 20, 'kucuk parca')
check('buyuk parca 5-18 derece, kucuk 20-70 derece', 'rng.Range(5f, 18f)' in view and 'rng.Range(20f, 70f)' in view,
      'donus')
check('agir: hizli cikar hemen yavaslar, 1 -> 0.7, alfa soner',
      'float move = 1f - (1f - k) * (1f - k) * (1f - k);' in view and 'Mathf.Lerp(1f, 0.7f, k)' in view,
      'parca konfeti gibi')
check('parca kenarinda soguk yansima', 'shard.Edge' in view, 'parca kenari yansimasiz')
between('elmas toz omur min (sn)', num(view, 'DustLifeMin'), 0.10, 0.22, 'toz omru')
between('elmas toz omur max (sn)', num(view, 'DustLifeMax'), 0.10, 0.22, 'toz omru')
check('toz eskenar dortgen (simli nokta degil)', 'Mathf.Abs(u) * 1.4f + Mathf.Abs(v)' in method(shapes, 'public static Sprite Dust'),
      'glitter')
check('tas basina en fazla 1 parilti, toplam <= 6', num(view, 'GlintBudget') <= 6 and 'if (s.Glint)' in view,
      'parilti yagmuru')
check('butceler: parca <= 24, toz <= 30', num(view, 'ShardBudget') <= 24 and num(view, 'DustBudget') <= 30,
      'parcacik limiti')

print()
print('=== 4. PUAN ===')
check('1-3 tas kendi degeri, 4+ tek TOPLAM', 'if (n <= 3)' in method(view, 'private void Build('), 'yazi spami')
check('puan KIRILMADAN once yok (dogum = kirilma + gecikme)',
      'BornAt = s.ShatterAt + Style.ValueDelay' in view and 'BornAt = lastBreak + Style.ValueDelay' in view,
      'puan kirilmadan once')
between('deger gecikmesi (sn)', num(view, 'ValueDelay'), 0.04, 0.08, 'deger dogumu')
between('deger tutma (sn)', num(view, 'ScoreHold'), 0.12, 0.18, 'tutma')
check('deger 0.70 -> 1.08 -> 1', 'Mathf.Lerp(0.70f, 1.08f' in view, 'deger pop')
check('deger rengi fildisi + soguk mavi golge (altin degil)',
      'ValueInk = new Color(0.97f, 0.97f, 0.93f)' in view and 'ValueShade = new Color(0.08f, 0.14f, 0.24f)' in view,
      'renk kimligi')
between('skor tepkisi', num(view, 'ScorePunch'), 0.05, 0.07, 'skor punch')
check('skor tek yazardan, buz-beyazi ton', 'quarry.ScoreScale' in rebate and 'totalText' not in view,
      'iki efekt skoru ezer')
check('oz gercek TOPLAM capasina', 'quarry.ScoreAnchor = ScoreWorldAnchor;' in ctrl, 'oz yanlis yere')
between('genel parilti opaklik', num(view, 'GlobalGlintAlpha'), 0.04, 0.08, 'genel parilti')
between('genel parilti suresi', num(view, 'GlobalGlintTime'), 0.08, 0.12, 'genel parilti')

print()
print('=== 5. KADEME, KANCALAR, LAB ===')
between('kademe (sn)', num(view, 'Stagger'), 0.02, 0.04, 'kademe')
check('cok tasta dalga sikisir, 10+ darbe 3 tik', 'Style.StaggerSpan / Mathf.Max(1, n - 1)' in view
      and num(view, 'Waves') == 3 and num(view, 'WaveThreshold') == 10, 'hepsi ayni kare ya da uzun')
check('ses: tetik, catlak, darbe, kirilma, odul', all(('Sound' + k) in view for k in ['Trigger', 'Crack', 'Strike', 'Shatter', 'Reward']),
      'ses kancasi eksik')
check('dokunsal: tek / cok (spam yok)', "Say(Haptic, HapticSingle)" in view and "Say(Haptic, HapticMulti)" in view,
      'dokunsal spam')
check('debug yalnizca editor / debug build', 'Application.isEditor || Debug.isDebugBuild' in view, 'debug')
n = len(re.findall(r'AddAnim\("elmas kazma: ', lab))
check('en az 17 lab girisi (%d)' % n, n >= 17, 'lab eksik')
check('9 debug anahtari', all(('QuarryBreakView.Layers.' + f) in lab for f in [
    'ShowDiamondTargets', 'ShowObsidianProxy', 'ShowCrackPaths', 'ShowStrikeAxis', 'ShowCompressionDebug',
    'ShowShardsDebug', 'ShowCrystalDust', 'ShowScoreValue', 'ShowGlobalResonance']), 'debug eksik')
check('lab puani jokerden okuyor', 'joker.PointsPerObsidian * session.Config.Scoring.ScoreScale' in lab,
      'lab kendi sayisi')
check('lab temizlik sahnesi gercek temizligi oynatir', 'EmitSweepConfetti();' in method(lab, 'private void AnimQuarry('),
      'temizlik -> obsidyen kalir okunamiyor')
check('havuzdan gelen parca yerel konumu SIFIRLANIR (vekil pivottan kaymaz)',
      'r.transform.localPosition = Vector3.zero;' in method(view, 'private SpriteRenderer Rent(')
      and 's.Proxy.transform.localPosition = Vector3.zero;' in view,
      'vekiller tahtanin disina kayiyor')
check('lab obsidyeni ZORLA kirar (DestroyCube obsidyeni reddeder, tas tahtada kalir)',
      'board.DestroyCubeForced(p);' in method(lab, 'private void AnimQuarry('),
      'labda obsidyen tahtada kaliyor - kirilmamis gibi gorunur')
check('lab oyunun yolundan (Prepare / Begin)', 'quarry.Prepare(report);' in lab and 'quarry.Begin(report' in lab,
      'lab kopya oynatiyor')
check('RESET durdurur ve katmanlari acar', 'StopQuarry();' in method(lab, 'private void AnimResync()')
      and 'QuarryBreakView.Layers.AllOn();' in method(lab, 'private void AnimResync()'), 'RESET eksik')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('elmas kazma: hepsi tamam')
