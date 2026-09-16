# -*- coding: utf-8 -*-
# DEPREM / SEISMIC COLLAPSE - statik kontrol.
#
# Bu dosyanin korudugu UC sey var.
#
# 1) VIEW HICBIR SEY HESAPLAMIYOR. "Yikilabilir kuplerin dortte biri, rastgele" kuralinin her
#    parcasi bir tuzak: hangisi yikilabilir, dortte bir nasil yuvarlanir, rastgele hangilerini
#    secti. Dusen kupler Core'un GERCEKTEN bosalttigi hucreler, dusmeden once alinan halleriyle.
#
# 2) BU BIR PATLAMA DEGIL. Eski hali her hucrede toz patlamasi + patlama sesi + KAMERA sarsintisiydi:
#    satir patlamasinin dili, satir olmadan. Deprem arenanin dengesini kaybetmesi; kamera sabit,
#    tahta sarsiliyor, kupler zemine gomuluyor.
#
# 3) GOMULME KUCULME DEGIL. Kuculup kaybolan kup "silindi" diye okunur. Bir sey batarken ZEMIN CIZGISI
#    onun yuzune tirmanir. Ve yarik uc kez pisirildi: tek duz kenarli pürüzlü bir sekil 2D'de ufuk
#    cizgisidir - once cam agaci, sonra dag silueti cikti.
from __future__ import print_function
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

fail = []


def check(label, ok, why):
    print('   %-70s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8').read()


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


def num(src, pattern, default=None):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else default


joker_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'BoardJokers.cs')
joker = strip_comments(joker_raw)
visuals_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'QuakeVisuals.cs')
view_raw = read('Assets', 'Scripts', 'View', 'QuakeCollapseView.cs')
view = strip_comments(view_raw)
shapes_raw = read('Assets', 'Scripts', 'View', 'QuakeShapes.cs')
shapes = strip_comments(shapes_raw)
shader = read('Assets', 'Resources', 'Shaders', 'QuakeDrop.shader')
board = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
activation = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Activation.cs'))
controller = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. KURAL CORE`DA, TEK YERDE ===')
check('secim TEK bir statikte (ChooseCollapse)',
      'public static List<GridPos> ChooseCollapse(' in joker
      and 'ChooseCollapse(board, ctx.Rng, CollapseFraction)' in joker,
      'secim iki yerde yazili - biri digeriyle ayni hucreleri secmeyi birakir')
check('lab AYNI kurali calistiriyor',
      'DepremJoker.ChooseCollapse(board' in lab,
      'lab hedefleri elle diziyor - kuralin secmeyecegi bir kupu dusurebilir')
check('rapor GERCEKTEN bosaltilan hucreler (DestroyCubes donusu)',
      'IReadOnlyList<GridPos> gone = ctx.Round.DestroyCubes(' in joker
      and 'foreach (GridPos cell in gone)' in joker,
      'rapor istenen listeyi tasiyor - reddedilen bir yikim, hala duran bir kupu dusmus gosterir')
check('kupler DUSMEDEN ONCE kaydediliyor',
      joker.index('standing[cell] = cube.Value') < joker.index('ctx.Round.DestroyCubes('),
      'kupun hali yikimdan sonra okunuyor - tahta artik ne dustugunu soyleyemez')
check('rapor [NotSaved] ve her depremde YENI nesne',
      '[field: NotSaved]' in joker_raw and 'LastQuake = report' in joker
      and 'var report = new QuakeVisuals()' in joker,
      'rapor kayda siziyor ya da ayni nesne tekrar kullaniliyor')
check('CoreTests raporu ve secimi cakiliyor',
      'Deprem_ReportsWhatActuallyFell' in tests and 'Deprem_ChoosesOnlyWhatCanBeBroughtDown' in tests,
      'rapor ya da secim bozulsa kimse fark etmez')

print('=== 2. VIEW HICBIR SEY HESAPLAMIYOR ===')
# Ilk yazilisinda '0.25' literalini ariyordu ve catlak acilisina, sarsinti anahtarina, debug rengine
# takildi - hicbiri dortte bir kurali degil. Aranan sey KURALIN parcalari.
check('viewde dortte bir / yuvarlama / secim yok',
      'CollapseFraction' not in view and 'Ceiling' not in view and 'ChooseCollapse' not in view
      and 'NextInt' not in view,
      'view kac kupun ve hangilerinin dusecegini kendi hesapliyor')
check('viewde yikilabilirlik kararini VERMIYOR (yalniz DEV isaretinde okunuyor)',
      view.count('IsExternallyDestructible') == 1
      and 'IsExternallyDestructible' in method(view, 'private void BuildDebugMarks()'),
      'view hangi kupun kirilabilecegine yeniden karar veriyor')
check('rapor KIMLIGIYLE eslesiyor (sayac ya da seriyle degil)',
      'ReferenceEquals(report, lastPlayed)' in view and 'CollapseCount' not in view,
      'sayac/seri - yeni kosuda ilk deprem atlanir, yuklemede eskisi tekrar oynar')
check('eski seenQuakes sozlugu SILINMIS',
      'seenQuakes' not in controller and 'seenQuakes' not in activation,
      'hic temizlenmeyen sayac sozlugu hala duruyor')
check('oyun rapora ayni seam`den gidiyor',
      'boardView.Quake.Play(boardView, deprem.LastQuake)' in activation
      and 'SyncQuake();' in fb,
      'oyun baska bir yoldan oynatiyor - lab`daki zamanlama oyunda gorunmez')

print('=== 3. BU BIR PATLAMA DEGIL ===')
sync = method(activation, 'private void SyncQuake()')
check('KAMERA SARSILMIYOR',
      'ShakeCamera' not in view and 'ShakeCamera' not in sync and 'cam.transform' not in view,
      'kamera sarsintisi - patlama dili, arena degil ekran sarsiliyor')
check('toz patlamasi / patlama sesi / FlashCells yok',
      'blastFx' not in sync and 'Explode' not in sync and 'FlashCells' not in view
      and 'ClusterBurst' not in view,
      'normal patlama kanali - "joker bu kupleri patlatti" okunur')
check('SKOR popup`i yok',
      'Popup' not in view and 'Score' not in view,
      'puan vermeyen bir kurtarma puan gibi gorunuyor')
check('TAHTA sarsiliyor: BoardView.SetTremor',
      'owner.SetTremor(' in view and 'public void SetTremor(' in board,
      'sarsinti tahtanin kendisinde degil')
check('sarsinti BASKIYLA birlesiyor, ikinci bir yazici degil',
      'private void ApplyArenaTransform()' in board
      and method(board, 'public void SetPressure(').count('ApplyArenaTransform()') == 1
      and method(board, 'public void SetTremor(').count('ApplyArenaTransform()') == 1,
      'uzatma baskisi ile deprem transform`u ayri yaziyor - son yazan kazanir')
check('sarsinti KONTROLLU impuls, beyaz gurultu degil',
      'Keys(k, new[] { 0f, 0.25f, 0.55f, 0.80f, 1f }' in view and 'Random.' not in view,
      'rastgele titresim - vibrasyon motoru spam`i')
check('sarsinti genligi kucuk (px <= 2, derece <= 0.3)',
      num(view, r'TremorX = ([\d.]+)f / 64f', 9) <= 2.0
      and num(view, r'TremorDegrees = ([\d.]+)f', 9) <= 0.3,
      'arena okunamaz hale gelecek kadar sallaniyor')
check('renk dili: toz tas grisi-kahve, isik/kivilcim yok',
      'Dust = new Color(0.42f, 0.38f, 0.33f' in view and 'Spark' not in view and 'Glint' not in view,
      'odul kivilcimi - deprem bir ceza/kurtarma, odul degil')
check('kirintilar YUKARI patlamiyor',
      '-cell * (0.1f' in view and 'Gravity = -3f * cell' in view,
      'kirintilar yukari firliyor - patlama dili')

print('=== 4. GOMULME: ZEMIN CIZGISI KUPUN YUZUNE TIRMANIYOR ===')
check('kirpma cizgisi kupun TABANINDAN yarigin yakin kenarinin OTESINE yukseliyor',
      'Mathf.Lerp(bottom, nearEdge + Style.ClipOvershoot * cell, fall)' in view,
      'kup sadece kuculup kayboluyor - "silindi" okunur, "gomuldu" degil')
check('kirpma RENDERER BASINA (SpriteMask degil)',
      'SpriteMask' not in view and '_Clip' in shader and 'SetPropertyBlock(block)' in view,
      'SpriteMask menzilindeki her sprite`i acar - yan yana dusen kupler birbirinin yariginda gorunur')
check('kup TABANI etrafinda donuyor (yerinde fırildak degil)',
      "Rotate about the cube's BASE" in view_raw and 'new Vector3(t.At.x + shear, bottom, 0f) + up' in view,
      'kup kendi merkezinde donuyor - yariga devrilmiyor')
check('kup asla YUKARI ziplamiyor',
      '- Style.StressSink * cell * stress' in view and '- Style.DetachSink * cell * detach' in view
      and '- Style.DropSettle * cell * fall' in view,
      'kup havaya kalkiyor - zemin onu asagi aliyor olmali')
check('dususte hareket ONCE, kuculme SONRA (yerinde kuculme degil)',
      num(view, r'ScaleEnd = ([\d.]+)f', 0) >= 0.5,
      'kup noktaya kadar kuculuyor - zemin cizgisi yerine kuculme tasiyor')
check('zemin cizgisini tasiyan bir KENAR (ledge) var',
      'QuakeShapes.Ledge' in view and 'clipLocalY' in view,
      'cizgi gorunmez - kup hicbir seyin arkasina girmiyor gibi')
check('shader yoksa kup yine de gidiyor',
      'DropMaterial == null ? 1f - fall' in view,
      'shader yoksa kup ekranda takili kaliyor')

print('=== 5. YARIK: CATLAK, AGAC/DAG/PORTAL DEGIL ===')
check('yarigin IKI kenari da puruzlu (tek duz kenar = ufuk cizgisi)',
      'float top = th * (0.85f + Wig(cu, seed)' in shapes
      and 'float bot = th * (0.80f + Wig(cu, seed + 2.1f)' in shapes,
      'duz bir kenarin ustunde yukselen sekil - cam agaci / dag silueti')
check('en genis noktasi ORTADA degil',
      'Widest OFF its middle' in shapes_raw,
      'simetrik tek tepeli sekil - bir nesne gibi okunur')
check('puruz UC uyumsuz frekans (testere disi degil)',
      'Mathf.Sin(t * 19f' in shapes and 'Mathf.Sin(t * 33.7f' in shapes and 'Mathf.Sin(t * 57.1f' in shapes,
      'tekrar eden kenar - testere disi')
check('siyah degil, ve UZAK kenar isik aliyor (yoksa gorunmuyor)',
      'CrackInside = new Color(0.045f' in shapes and 'FarRim' in shapes,
      'koyu hucre uzerinde komur renkli catlak hic gorunmuyor - ilk render bunu gosterdi')
check('kilcal catlaklar var',
      'hair = Mathf.Max(hair' in shapes,
      'catlak degil, bir leke')
check('yarik hucrenin ICINDE kaliyor',
      'const float halfLength = 0.31f' in shapes and num(view, r'CrackJitterX = ([\d.]+)f', 9) <= 0.06,
      'yarik komsu hucreye tasiyor')
check('yarik PARLAYARAK kapanmiyor',
      'glow' not in view.lower() and 'heal' not in view.lower(),
      'sihirli iyilesme - zemin oturmali')
check('dokular KARE, oran pisirilmis',
      'new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size)' in shapes,
      'kare olmayan doku - localScale x ile y farkli seyler soyler')

print('=== 6. DALGALAR VE OLCU ===')
span = (num(view, r'DropAt = ([\d.]+)f', 0) + num(view, r'DropFor = ([\d.]+)f', 0)
        + num(view, r'CloseFor = ([\d.]+)f', 0))
check('tek kupun zinciri ~0.5-0.6 sn (%.2f)' % span, 0.45 <= span <= 0.65,
      'tek kup icin cok uzun ya da kisa')
check('TOPLAM tavan <= 1.1 sn',
      num(view, r'TotalCap = ([\d.]+)f', 9) <= 1.1 and 'room / lastStart' in view,
      'cok kup dusunce deprem uzuyor - dalgalar sikistirilmali')
check('dalga HASH ile, soldan saga degil',
      'Phase(report.Cells[i], report.Seed)' in view,
      'soldan saga suprulme - tarama gibi okunur')
check('hedefler ONCE global sarsinti, SONRA gorunuyor',
      num(view, r'Reveal = ([\d.]+)f', 0) >= 0.05,
      'hedefler ilk karede ele veriliyor')
check('toz ve kirinti KELEPCELI',
      'dust.Count < Style.DustCap' in view and 'crumbs.Count < Style.CrumbCap' in view
      and num(view, r'DustCap = (\d+)', 99) <= 30 and num(view, r'CrumbCap = (\d+)', 99) <= 36,
      'parcacik firtinasi')
check('hedef sayisi arttikca kupa dusen parca azaliyor',
      'n <= 4 ? 4 : n <= 12 ? 3 : 2' in view,
      '20 kupte her kupe tam detay - camur')
check('saat OLCEKLI (lab yavaslatabilsin)',
      'clock += Time.deltaTime;' in view,
      '0.25x kabul testi calistirilamaz')
check('ses ve haptik kancalari',
      'SoundCubeDrop' in view and 'HapticPeak' in view,
      'ses/haptik tasarimi icin tutamak yok')

print('=== 7. LAB ===')
for scene in ('One', 'Three', 'Five', 'Ten', 'Twenty', 'MixedColours', 'Near', 'Far', 'Edge',
              'Corner', 'FullBoard'):
    check('lab sahnesi: %s' % scene, 'AnimQuakeScene.%s' % scene in lab_raw, '%s yok' % scene)
check('her vurus TEK BASINA izlenebiliyor',
      lab_raw.count('AnimQuakeOnly(') >= 8,
      'vuruslar tek tek gorulemiyor')
check('anahtarlar sahne KURULMADAN once uygulaniyor',
      method(lab, 'private void AnimQuakeOnly(').index('QuakeCollapseView.Layers.AllOn()')
      < method(lab, 'private void AnimQuakeOnly(').index('AnimQuake(AnimQuakeScene.One)'),
      'kapali katman yine de cizilir')
check('VEKIL testi var (vekilsiz kupler aninda kaybolur)',
      'PROXY TEST' in lab_raw,
      'vekilin ne is yaptigi gorulemiyor')
check('DEV isaretleri: hedef / fay fazi / yarik siniri',
      'ShowTargets' in lab_raw and 'ShowFaultPhase' in lab_raw and 'ShowFissureBounds' in lab_raw,
      'hangi kupun neden dustugu gozle ayirt edilemiyor')
check('tas (obsidyen/altin) DUSMEYECEGI gorulebiliyor',
      'Stone a quake cannot touch' in lab_raw,
      'kuralin kirilmaz kupleri atladigi gozle dogrulanamiyor')
check('0.25x ve 0.5x',
      'deprem: one cube at 0.25x' in lab_raw and 'deprem: five cubes at 0.5x' in lab_raw,
      'vuruslar yavas izlenemiyor')
check('RESET depremi de durduruyor',
      'StopAnimQuake();' in method(lab, 'private void AnimResync()'),
      'resync sahneyi durdurmuyor, arena egik kalabilir')
check('tahta yeniden kurulurken efekt korunuyor',
      'keepQuake' in board,
      'deprem ortasinda Rebuild - tahta bir derecenin kesri kadar egik kalir')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('deprem: hepsi tamam')
