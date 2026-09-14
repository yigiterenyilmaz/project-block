# "Soguk kuyu" (ColdSinkView): yapisal kontroller.
#
#   1. Style'da okunmayan ayar yok; brief'in ayar listesi eksiksiz.
#   2. Ayrim: Core her kaldirilan hucrenin TURUNU yaziyor (yok oldu / tasindi / donustu), kuyu
#      yalnizca YOK OLAN hucrelerde oynuyor; digerleri eski sessiz izde. Varyant rastgele secilir.
#   3. Geometri: dusen kup asla bir sonraki hucrenin agzina ulasmaz (maskeler birbirini gosterir)
#      ve dusus bittiginde tamamen on dudagin arkasindadir.
#   4. Maske: kup yalniz dususte maskelenir, havuzdan donen renderer materyal/maske tasimaz,
#      dususe kadar karonun kendi materyali.
#   5. Yasaklar: patlama/sarsinti/flas/kamera/timeScale yok, parcaciklar ICERI gider, rastgele yok
#      (varyant secimi disinda), saf siyah ve elektrik mavisi yok.
#   6. Zamanlama brief'in pencerelerinde; 7. lab ve yasam dongusu.
import colorsys
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
fail = []


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8').read()


def strip_comments(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return '\n'.join(re.sub(r'//.*$', '', line) for line in s.split('\n'))


def method(src, signature):
    i = src.index(signature)
    rest = src[i + len(signature):]
    m = re.search(r'\n        (private|public|internal|protected) ', rest)
    return src[i:i + len(signature) + (m.start() if m else len(rest))]


def check(label, ok, why):
    print('   %-64s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'ColdSinkView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
report = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'TurnReport.cs'))
scoring = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'RoundEngine.Scoring.cs'))

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
dead = [n for n in style if not re.search(r'\bStyle\.%s\b' % n, view[end:])]
print('   %d ayar, %d okunuyor' % (len(style), len(style) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['OpenDuration', 'InnerDepth', 'InnerScale', 'RimStrength', 'RimWidth', 'DetachDuration', 'DetachDrop',
          'FallDuration', 'FallScaleEnd', 'FallScaleBend', 'FallBrightnessEnd', 'FallDesaturation',
          'FallRotationAmount', 'FrontLipDepth', 'MoteCountMin', 'MoteCountMax', 'MoteSpeed', 'MoteLifetime',
          'EmptyHoldDuration', 'CloseDuration', 'CloseEase', 'ResidueStrength', 'ResidueDuration',
          'CenterOutDelay', 'DelayJitter', 'LargeNDetailCompensation']
missing = [w for w in wanted if w not in style]
check('brief\'in %d ayari Style\'da' % len(wanted), not missing, 'eksik ayar: %s' % missing)

print()
print('=== 2. YOK OLAN / TASINAN / DONUSEN ===')
check('Core: LiftKind { Removed, Relocated, Transformed }',
      all(k in report for k in ('Removed,', 'Relocated,', 'Transformed')) and 'LiftKindAt(' in report, 'LiftKind yok')
tags = {
    'ReleasePress': 'Removed', 'ForgetCard': 'Removed', 'EscalateBoards': 'Relocated',
    'FlingBoardsOutward': 'Relocated', 'SpreadGangrene': 'Transformed'}
for fn, kind in tags.items():
    body = scoring[scoring.index(fn + '('):]
    body = body[:body.index('\n        }\n')]
    check('%s -> LiftKind.%s' % (fn, kind), 'LiftKind.%s' % kind in body, '%s yanlis etiketli' % fn)
emit = method(fb, 'private void EmitBlastParticles(')
check('yalnizca YOK OLANLAR kuyuya (PlayRemoval); tasinan sokulmede, donusen kangrende',
      'LiftKindAt(i) == LiftKind.Removed' in emit and 'PlayRemoval(removed)' in emit
      and 'LiftCells' not in emit, 'ayrim yok')
removal = method(fb, 'private bool PlayRemoval(')
check('varyant her kaldirmada rastgele (tekrar serbest)', 'Random.Range(0, RemovalVariants.Length)' in removal,
      'rastgele secim yok')
check('kuyu varyanti kayitli', 'ColdSink' in fb[fb.index('enum RemovalVariant'):fb.index('enum RemovalVariant') + 200],
      'varyant enum\'da yok')
check('kaldirma kupun kendi yuzunu kullanir (TryCubeLook)', 'TryCubeLook(' in removal, 'kup yuzu yok')

print()
print('=== 3. GEOMETRI ===')
mouth = 0.82
lowest = 0.0
for i in range(101):
    e = i / 100.0
    s = mouth * (1 - (1 - style['FallScaleEnd']) * e ** style['FallScaleBend'])
    d = style['DetachDrop'] + style['FallDrop'] * e ** style['FallDropBend']
    lowest = max(lowest, d + s / 2)
next_mouth = 1 - mouth / 2
check('kupun en alt kenari %.3f < komsu agzi %.3f (hucre)' % (lowest, next_mouth), lowest < next_mouth,
      'dusen kup alttaki kuyunun maskesinde gorunur')
s_end = mouth * style['FallScaleEnd']
top_end = style['DetachDrop'] + style['FallDrop'] - s_end / 2
check('dusus sonunda tamamen dudagin arkasinda (ust kenar %.3f >= %.3f)' % (top_end, mouth / 2), top_end >= mouth / 2 - 1e-3,
      'kup dusus sonunda hala gorunuyor - yalniz kuculuyor')

print()
print('=== 4. MASKE VE MATERYAL ===')
cube = method(view, 'private void PaintCube(')
check('kup dususte maskelenir (VisibleInsideMask)', 'pit.Cube.maskInteraction = SpriteMaskInteraction.VisibleInsideMask' in cube,
      'maske yok - yalniz scale-down olur')
orders = dict((n, int(v)) for n, v in re.findall(r'private const int (\w+) = (-?\d+);', view))
check('maske araligi kupu iki yandan kapsar (%d < %d < %d)' % (orders['MaskBack'], orders['CubeOrder'], orders['MaskFront']),
      orders['MaskBack'] < orders['CubeOrder'] < orders['MaskFront'], 'maske araligi kupu kapsamiyor')
check('maske araligi enfeksiyonunkiyle (1-4) cakismaz', orders['MaskBack'] > 4, 'enfeksiyon maskesiyle cakisiyor')
rent = method(view, 'private SpriteRenderer Rent(')
check('havuzdan gelen renderer materyal ve maskeyi birakir',
      'r.maskInteraction = SpriteMaskInteraction.None' in rent and 'r.sharedMaterial = wanted' in rent, 'havuz kirli')
check('dususe kadar karonun kendi materyali (su dalgalanir)', 'ViewUtil.TileMaterial(pit.Look.Tile)' in method(view, 'public void Play('),
      'kup donuk baslar')
check('kup Play karesinde cizilir (bosluk yok)', 'Place(pit.Cube, cells[i], sink.CubeSize' in method(view, 'public void Play('),
      'kup bir kare kaybolur')

print()
print('=== 5. YASAKLAR ===')
check('patlama/sarsinti/kamera/timeScale yok', not any(w in view for w in ('ShakeCamera', 'Camera', 'timeScale', 'Explode')),
      'efekt patlama gibi davraniyor')
check('rastgele yok (hucre hash\'i)', 'Random.' not in view, 'UnityEngine.Random kullaniliyor')
throw = method(view, 'private void ThrowMotes(')
check('parcaciklar ICERI gider (hedef kuyunun merkezi)', 'q.To = pit.At' in throw, 'parcaciklar disari gidiyor')
check('yeni material yok', 'new Material(' not in view, 'material uretiliyor')
cols = dict((n, tuple(float(x) for x in v.split(','))) for n, v in
            re.findall(r'private static readonly Color (\w+) = new Color\(([^)]*)\)', view.replace('f', '')))
check('kuyu saf siyah degil (void %s)' % (cols['VoidColour'],), min(cols['VoidColour']) >= 0.02, 'saf siyah portal')
sat = colorsys.rgb_to_hsv(*cols['RimColour'])[1]
check('isik elektrik mavisi degil (kenar doygunlugu %.2f < 0.35)' % sat, sat < 0.35, 'neon/elektrik mavisi')

print()
print('=== 6. ZAMANLAMA ===')
open_d, det = style['OpenDuration'], style['DetachDuration']
total = open_d * 0.7 + det + style['FallDuration'] * (1 + style['FallVariation']) + style['EmptyHoldDuration'] \
    + style['CloseDuration'] * (1 + style['CloseVariation']) + style['ResidueDuration']
check('zemin cokmesi %.0f ms (80-140)' % (open_d * 1000), 0.08 <= open_d <= 0.14, 'acilis penceresi')
check('kopma %.0f ms (50-100)' % (det * 1000), 0.05 <= det <= 0.10, 'kopma penceresi')
check('bos kuyu %.0f ms (80-160)' % (style['EmptyHoldDuration'] * 1000), 0.08 <= style['EmptyHoldDuration'] <= 0.16, 'bekleme')
check('kalinti %.0f ms (100-250)' % (style['ResidueDuration'] * 1000), 0.10 <= style['ResidueDuration'] <= 0.25, 'kalinti')
check('tek hucre toplam %.2f s (<= 1.05)' % total, total <= 1.05, 'efekt uzun')
check('hucre gecikmesi %.0f ms/hucre (20-50)' % (style['CenterOutDelay'] * 1000), 0.02 <= style['CenterOutDelay'] <= 0.05,
      'dalga temposu')

print()
print('=== 7. LAB VE YASAM DONGUSU ===')
labels = ['kaldırılan hücreler: rastgele varyant', 'kaldırılan hücreler 1: soğuk kuyu',
          'yürüyen merdiven: alan yukarı kayar', 'merkezkaç kuvveti: küpler dışa itilir', 'kangren: satır ölür']
check('lab girisleri', all(w in lab_raw for w in labels), 'labda eksik: %s' % [w for w in labels if w not in lab_raw])
check('boss sahneleri kendi tahtasinda, gercek tahta koduyla (ShiftRowsUp, FlingCubesOutward)',
      'board.ShiftRowsUp(motions, moves)' in lab_raw and 'board.FlingCubesOutward(motions, moves)' in lab_raw
      and 'boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter)' in lab_raw
      and 'StopAnimBossLift();' in lab_raw, 'boss sahnesi uydurma ya da gercek tahtaya dokunuyor')
check('eski iz artik ortadaki hucrelerde oynamiyor', 'LiftCells(AnimCells()' not in lab_raw, 'ortada yok olan kupler')
views = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Views.cs'))
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('view kuruluyor, menude gizleniyor, kosu bitince duruyor', 'AddComponent<ColdSinkView>()' in views
      and 'coldSink.gameObject.SetActive(visible)' in menus and 'coldSink.Stop()' in menus, 'yasam dongusu eksik')
check('csproj dosyayi iceriyor', 'ColdSinkView.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
