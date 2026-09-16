# -*- coding: utf-8 -*-
# MEYDAN OKUMA / CHALLENGE CONTRACT - statik kontrol.
#
# Bu dosyanin korudugu DORT sey var.
#
# 1) VIEW HICBIR SEY HESAPLAMIYOR. Hangi hat, kac tur, kac puan, kacinci deneme, basarildi mi -
#    hepsi Core'un. View'da "/ 2" yok: yeni deger raporun degeri; durust hat yoksa jeton
#    NextBonus ile bekler.
#
# 2) SARI CERCEVE DEGIL. Ilk mock ince bir dikdortgen cikti. Ray hucre hucre plakalar, ic tarafa
#    hafif isik; uclarda agir kelepceler. Bloklar asla boyanmaz, hatta dolgu panel yok.
#
# 3) HAREKET DILI SONUCU SOYLER. Basari DISARI acilir (geri tepme, parcalar, odul skora);
#    kacis ICERI toplanir (raylar kendi kelepcesine sarilir, jeton kesilir, yarisi soner, kalan
#    yeni degerle yeni hatta gider). Yeniden hedef asla aninda degisim degil.
#
# 4) ZAMANLAMA. Olay, onu doguran satir patlarken oynar; yenileme sadece oynatilmis olaydan
#    sonraki canli durumu verir.
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


def num(src, pattern, default):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else default


def arr(src, name):
    m = re.search(name + r'\s*=\s*\{([^}]*)\}', src)
    return [float(x.strip().rstrip('f')) for x in m.group(1).split(',')] if m else []


def colour(src, name):
    m = re.search(name + r'\s*=\s*new Color\(([^)]*)\)', src)
    return tuple(float(x.strip().rstrip('f')) for x in m.group(1).split(',')[:3])


def lum(c):
    def ch(v):
        return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
    return 0.2126 * ch(c[0]) + 0.7152 * ch(c[1]) + 0.0722 * ch(c[2])


def contrast(a, b):
    la, lb = lum(a), lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


joker = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'MeydanOkumaJoker.cs'))
report = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'ChallengeVisuals.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'ChallengeContractView.cs')
view = strip_comments(view_raw)
shapes_raw = read('Assets', 'Scripts', 'View', 'ChallengeShapes.cs')
shapes = strip_comments(shapes_raw)
ctrl = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Challenge.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
rebate = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Rebate.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. CORE ANLATIYOR, VIEW HESAPLAMIYOR ===')
check('LastEvent [NotSaved], baslangic suresi [NotSaved] (kayit formati degismedi)',
      re.search(r'\[field: NotSaved\]\s*public ChallengeVisuals LastEvent', joker) is not None
      and re.search(r'\[NotSaved\]\s*private int markDeadline', joker) is not None,
      'kayit formati degisti ya da rapor kayda giriyor')
check('bes olay da jokerde yaziliyor',
      all(('ChallengeEvent.' + e) in joker for e in ['Started', 'Ticked', 'Succeeded', 'Failed', 'Expired']),
      'bir olay raporlanmiyor')
check('odeme OLCULUYOR (RoundScore once/sonra)',
      'int scoreBefore = turn.Round.RoundScore;' in joker
      and 'before.ScoreDelta = turn.Round.RoundScore - scoreBefore;' in joker,
      'skor sayisi kopyalaniyor')
check('kacis ve yeni hat TEK olay (ayni rapor nesnesi)',
      'Live(missed);' in joker, 'kacis ve yeni hedef iki ayri olay - View arada bos kalir')
check('yarilanmis deger Core`dan (NextBonus = BaseBonus >> attemptsMade)',
      'before.NextBonus = BaseBonus >> attemptsMade;' in joker,
      'sonraki deger raporda yok')
check('aciliyet TEK tanim (ChallengeVisuals.UrgencyOf) ve View onu kullaniyor',
      'public static ChallengeUrgency UrgencyOf(' in report
      and 'ChallengeVisuals.UrgencyOf(' in view,
      'aciliyet iki yerde tanimli')
check('View yarilamiyor (">> 1", "/ 2", "* 0.5f" deger ustunde yok)',
      re.search(r'(Bonus|tokenValue|value)\s*(>>\s*1|/\s*2|\*\s*0\.5f)', view) is None,
      'View bonusu kendi boluyor')
check('View jokeri, tahta patlamasini ya da rastgeleligi okumuyor',
      'MeydanOkumaJoker' not in view and 'ExplodedRows' not in view and 'ExplodedRows' not in ctrl
      and 'Random.' not in view,
      'View kurali tekrar hesapliyor')
check('jeton sayisi gosterilen degerden ("+" + value)',
      '"+" + value' in view, 'jeton sayisi rapordan gelmiyor')
check('testler: olaylar tur tur, odeme gercek oyunla, aciliyet',
      'MeydanOkuma_ReportsEveryTurnOfTheDare' in tests and 'MeydanOkuma_ReportsThePayment' in tests
      and 'MeydanOkuma_UrgencyHasOneDefinition' in tests,
      'rapor dogrulanmamis')

print()
print('=== 2. SARI CERCEVE DEGIL, BLOKLAR BOYANMAZ ===')
rail_half = num(shapes, r'RailHalf = ([\d.]+)f', 1)
check('ray ince: toplam kalinlik <= 0.06 hucre (%.3f)' % (rail_half * 2), rail_half * 2 <= 0.06,
      'ray hatti kapatiyor')
check('ray hucre basina PLAKA: sinirlarda bosluk + boncuk',
      'plateLength' in method(shapes, 'private static Color RailAt(') and 'bead' in method(shapes, 'private static Color RailAt('),
      'ray duz bir cizgi - cerceve gibi okunur')
check('ray ic tarafa hafif isik (yalniz +v), dolgu degil (<= 0.25)',
      'v > 0f ?' in method(shapes, 'private static Color RailAt(')
      and num(method(shapes, 'private static Color RailAt('), r'2\.2f\) \* ([\d.]+)f', 1) <= 0.25,
      'hat boyaniyor ya da isik iki yana')
check('rayin ic isigi hatta DONUK (bir ray 180 cevrilir)',
      'baseAngle + (sign > 0f ? 180f : 0f)' in view, 'isik disari bakiyor')
check('uclarda AGIR kelepce: omurga + kancalar + muhur (pad, yarik, cekirdek)',
      all(k in method(shapes, 'private static Color ClampAt(') for k in ['spine', 'hook', 'pad', 'slit']),
      'uc duz cizgi - cerceve')
check('kelepce ucu kapatan ince kopru YOK (dikdortgen kapanmaz)',
      'bridge' not in shapes, 'uclarda ince cizgi - kapali dikdortgen')
check('View bloklara dokunmuyor (BoardView / tint / wash yok)',
      'BoardView' not in view and 'SetRotWash' not in view and 'HoldCells' not in view,
      'bloklar boyaniyor')
check('akis zerreleri 0.05-0.12 opaklik',
      all(0.05 <= a <= 0.12 for a in arr(view, 'MoteAlpha')), 'akis cok belirgin ya da yok')
speeds = arr(view, 'MoteSpeed')
check('akis hizi sakin 0.25-0.40, gerilim 0.45-0.60, son 0.70-0.90 (%s)' % speeds,
      len(speeds) == 3 and 0.25 <= speeds[0] <= 0.40 and 0.45 <= speeds[1] <= 0.60 and 0.70 <= speeds[2] <= 0.90,
      'aciliyet akis hizinda okunmuyor')
check('zerre sayisi 2-4', all(2 <= c <= 4 for c in arr(view, 'MoteCount')), 'zerre fazla')
check('zerreler uclarda solar (isinlanma yok)', 'Mathf.Sin(u * Mathf.PI)' in view, 'zerre uclarda patliyor')
inset = arr(view, 'InsetPx')
check('ray gerilimde 1-2 px, sonda 1-2 px daha iceri (%s)' % inset,
      inset[0] == 0 and 1 <= inset[1] <= 2 and 1 <= inset[2] - inset[1] <= 2, 'sure raylardan okunmuyor')
check('kelepce 0 / 1 / 2 px iceri', arr(view, 'ClampInPx') == [0, 1, 2], 'kelepce gerilmiyor')
check('son tur nabzi 1.025, 0.8-1.2 sn',
      num(view, r'HeartbeatScale = ([\d.]+)f', 0) == 1.025
      and 0.8 <= num(view, r'HeartbeatEvery = ([\d.]+)f', 0) <= 1.2, 'nabiz alarm gibi')
check('alarm yok (kirmizi yanip sonme / kamera / ekran flasi)',
      'ShakeCamera' not in view and 'Color.red' not in view and 'flash' not in view.lower(),
      'alarm dili')
tw = num(view, r'TokenWidth = ([\d.]+)f', 0) * 2 * num(shapes, r'TokenHalfWidth = ([\d.]+)f', 0)
check('jeton genisligi 0.55-0.75 hucre (%.2f)' % tw, 0.55 <= tw <= 0.75, 'jeton boyu spec disi')
face = colour(shapes_raw, 'Face')
ink = colour(view_raw, 'Ink')
check('jeton sayisi okunur: fildisi / koyu yuz %.1f >= 4.5' % contrast(ink, face),
      contrast(ink, face) >= 4.5, 'sayi okunmuyor')
check('(altin ustune fildisi 4.5 alti - bu yuzden yuz koyu: %.2f)' % contrast(ink, colour(shapes_raw, 'Gold')),
      contrast(ink, colour(shapes_raw, 'Gold')) < 4.5, 'kontrol hesabi bozuk')
check('jeton kendiliginden yuzmuyor / nefes almiyor',
      'Mathf.Sin(clock' not in method(view, 'private void PaintToken('), 'jeton hover ediyor')
check('bosta parilti 3-5 sn arayla',
      3.0 <= num(view, r'GlintEveryMin = ([\d.]+)f', 0) and num(view, r'GlintEveryMax = ([\d.]+)f', 9) <= 5.0,
      'parilti dongusu')

print()
print('=== 3. HAREKET DILI ===')
grow = num(view, r'GrowTime = ([\d.]+)f', 0)
check('kurulum: ray 160-240 ms (%.0f)' % (grow * 1000), 0.16 <= grow <= 0.24, 'kurulum hizi')
check('kilit: 0.9 -> 1.06 -> 1, ~90 ms',
      num(view, r'LockPunch = ([\d.]+)f', 0) == 1.06 and 0.07 <= num(view, r'LockTime = ([\d.]+)f', 0) <= 0.11
      and 'Mathf.Lerp(0.9f, Style.LockPunch' in view, 'kilit anı')
check('ust ray A`dan, alt ray B`den buyur (her kelepce kendi rayini toplar)',
      'int k = side == 0 ? i : n - 1 - i;' in view, 'geri cekme kelepceye degil merkeze')
check('basari: kelepce DISARI 2-4 px', 2 <= num(view, r'RecoilPx = ([\d.]+)f', 0) <= 4
      and 'PaintClamps(clampsOld, old, 1f, recoil * old.Pixel' in view, 'basari iceri gidiyor')
check('basari: 4-8 parca, hattin DISINA', 4 <= num(view, r'Shards = (\d+)', 0) <= 8
      and 'line.Across * (side * line.Cell * (1.6f' in view, 'parcalar yanlis yone')
check('basari: jeton 1 -> 1.15 -> 0.98 -> 1', num(view, r'TokenPunch = ([\d.]+)f', 0) == 1.15
      and 'Mathf.Lerp(Style.TokenPunch, 0.98f' in view, 'jeton tepkisi')
check('basari: skor 1 -> 1.08 -> 0.99 -> 1', num(view, r'ScorePunch = ([\d.]+)f', 0) == 0.08
      and '-0.12f' in method(view, 'private void PaintEssence('), 'skor tepkisi')
check('basari: oz gercek TOPLAM capasina (ScoreWorldAnchor)',
      'challenge.ScoreAnchor = ScoreWorldAnchor;' in ctrl, 'oz rastgele yere gidiyor')
check('skor etiketi tek yazardan', 'challenge.ScoreScale' in rebate and 'totalText' not in view,
      'iki efekt skoru ezer')
check('kacis: geri cekme 120-180 ms', 0.12 <= num(view, r'RetractTime = ([\d.]+)f', 0) <= 0.18,
      'geri cekme hizi')
check('kacis: raylar SIFIRA toplanir (grow 1 -> 0)',
      'float grow = 1f - Ease01((t - Style.RetractFrom) / Style.RetractTime);' in view, 'ray aninda kayboluyor')
check('kacis: kesik 70-100 ms', 0.07 <= num(view, r'CutTime = ([\d.]+)f', 0) <= 0.10, 'kesik hizi')
check('kacis: yarimlar 2-4 px ayrilir', 2 <= num(view, r'SplitPx = ([\d.]+)f', 0) <= 4, 'ayrilma')
check('kacis: bir yari soner ve 0.65`e kuculur', 'Mathf.Lerp(1f, 0.65f, gone)' in view, 'yarilanma gorunmuyor')
check('kacis: yeni deger 0.8 -> 1.05 -> 1 ile dogar', 'Mathf.Lerp(0.8f, 1.05f, nt / 0.06f)' in view,
      'sayi bir karede degisiyor')
check('kacis: jeton kuculur ve matlasir (deneme basina)',
      arr(view, 'AttemptScale')[0] > arr(view, 'AttemptScale')[1] > arr(view, 'AttemptScale')[2]
      and 'AttemptTint' in view, 'yipranma yok')
tt = num(view, r'TravelTime = ([\d.]+)f', 0)
check('tasinma 220-360 ms (%.0f)' % (tt * 1000), 0.22 <= tt <= 0.36, 'tasinma hizi')
check('inis 1.05 -> 0.97 -> 1', 'Mathf.Lerp(1.05f, 0.97f' in view, 'inis')
check('yeni kelepceler inisten 30-70 ms sonra',
      0.03 <= num(view, r'RebuildDelay = ([\d.]+)f', 0) <= 0.07, 'kurulum inisle cakisiyor')
check('yeniden hedef ASLA aninda degil (yeni kontrat seyahat bitince kurulur)',
      'liveBuiltAt = clock + Style.TravelFrom + Style.TravelTime + Style.RebuildDelay;' in view,
      'eski kapan, yeni acil')
failTotal = num(view, r'TravelFrom = ([\d.]+)f', 0) + tt
check('kacis dizisi (kesik -> tasinma sonu) 0.65-0.95 sn (%.2f)' % failTotal, 0.65 <= failTotal <= 0.95,
      'kacis cok uzun/kisa')
check('son kacis: yeni hedef yok, kalan yari coker + 4-6 bronz toz',
      4 <= num(view, r'Dust = (\d+)', 0) <= 6 and 'Dust(at, unit)' in view, 'kontrat temiz kapanmiyor')
check('kacista patlama/duman/sarsinti yok',
      'Shatter' not in method(view, 'private void PaintToken(') and 'Smoke' not in view, 'kacis patliyor')
check('durust hat yoksa jeton BEKLER (NextBonus ile)',
      'parked = true;' in view and 'ev.NextBonus' in view, 'jeton kayboluyor ya da yanlis deger')
check('sesler ve dokunsal kancalar (8 ses + 2 dokunsal)',
      view.count('public const string Sound') >= 8 and view.count('public const string Haptic') == 2,
      'kanca eksik')

print()
print('=== 4. ZAMANLAMA VE YASAM ===')
check('olay satir patlarken oynuyor (PlayExplosionFeedback)',
      'PlayChallengeEvent();' in method(fb, 'private void PlayExplosionFeedback('),
      'odul patlamadan once geliyor')
check('yenileme yalnizca oynatilmis olaydan sonra canli durumu veriyor',
      'if (!challenge.HasPlayed(joker.LastEvent))' in ctrl and 'SyncChallenge();' in method(fb, 'private void RefreshAll('),
      'yeni hat sebep gorunmeden beliriyor')
check('olay KIMLIKLE eslenir', 'ReferenceEquals(ev, lastPlayed)' in view and 'Serial' not in view,
      'yeni kosunun ilk olayi atlanir')
check('RESET son olayi oynatilmis sayar (kontrat kaybolmaz)',
      'challenge.MarkPlayed(' in method(ctrl, 'private void StopChallenge('), 'RESET sonrasi kontrat gorunmez')
check('hat, tahtanin gercek oyun karelerinden (duzensiz tahta)',
      'board.IsInside(p)' in method(ctrl, 'private ChallengeContractView.Line LocateChallengeLine('),
      'bosluklarin ustune ray')
check('jeton ekrana sigan uca konur', 'Fits(first_' in ctrl, 'jeton ekrandan tasiyor')
check('olcekli saat, havuzlu', 'Time.deltaTime' in method(view, 'private void Update(')
      and 'Stack<SpriteRenderer> pool' in view, 'lab zaman olcegi / havuz')
check('debug yalnizca editor / debug build', 'Application.isEditor || Debug.isDebugBuild' in view,
      'debug son surumde gorunur')

print()
print('=== 5. LABORATUVAR ===')
n = len(re.findall(r'AddAnim\("meydan okuma: ', lab))
check('en az 23 sahne girisi (%d)' % n, n >= 23, 'lab eksik')
check('9 debug anahtari', all(('Layers.' + f) in lab for f in [
    'ShowChallengeTarget', 'ShowRailBounds', 'ShowClampAnchors', 'ShowBonusTokenAnchor',
    'ShowRemainingTurns', 'ShowAttemptIndex', 'ShowEnergyCurrent', 'ShowSuccessLink', 'ShowRetargetPath']),
      'debug eksik')
check('0.5x ve 0.25x basari/kacis', 'success at 0.25x' in lab and 'miss at 0.5x' in lab, 'yavas izleme yok')
check('lab sayilari jokerden okuyor (BaseBonus, DeadlineFor)',
      'joker.BaseBonus >> (attempt - 1)' in lab and 'joker.DeadlineFor(gaps)' in lab, 'lab kendi sayisini yaziyor')
check('lab basarida hatti gercekten patlatiyor (FlashLine)',
      'FlashLine(board, successRow' in lab, 'sebep-sonuc gorulemiyor')
check('uzun / kisa / duzensiz tahta', 'new GameBoard(7, 7, new[] { new GridPos(7, 3)' in lab
      and 'LongRow ? 11' in lab and 'ShortRow ? 5' in lab, 'tahta cesitleri yok')
check('RESET durduruyor ve katmanlari aciyor',
      'StopChallenge();' in method(lab, 'private void AnimResync()')
      and 'ChallengeContractView.Layers.AllOn();' in method(lab, 'private void AnimResync()'),
      'RESET eksik')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('meydan okuma: hepsi tamam')
