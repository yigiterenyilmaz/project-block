# Boss ic hareketi (BossMoveView): "Yuruyen merdiven" STEP CARRY ve "Merkezkac" RADIAL PUSH.
#
#   1. Style: brief'in ortak + merdiven + merkezkac ayarlari eksiksiz, okunmayan ayar yok.
#   2. Core: YALNIZ RAPOR - CellMove (From, To, Step, Cube, Source, Mirror) ve BoardMotionSource;
#      iki tahta islemi hayatta kalan her kupu yaziyor; eski cagrilar ayni; rapora iletiliyor; testler.
#   3. Akis: tahta yeniden cizildigi karede (suyun arkasina ertelenmeden); kaynak ve hedef Core'dan,
#      view hesaplamiyor; kopanlar Momentum Peel'e ayni kalkisla, bir daha oynatilmiyor.
#   4. Vekil: hedef hucreler inene kadar bos (yeniden cizimde de); inince geri; govde solmuyor,
#      donmuyor, kaynaktan hedefe duz gidiyor; kendi materyali.
#   5. Yasaklar: parcacik / iz / parlama / sarsinti / rastgele yok.
#   6. Olculer ve zamanlama brief'in araliklarinda; iki karakter farkli (merkezkac daha sert kalkis).
#   7. Sureklilik: sokulme bosslarin kalkisini kullaniyor ve frenlemiyor.
#   8. Lab, hata ayiklama, yasam dongusu.
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
    print('   %-70s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'BossMoveView.cs'))
peel = strip_comments(read('Assets', 'Scripts', 'View', 'MomentumPeelView.cs'))
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
report = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'TurnReport.cs'))
lines = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Lines.cs'))
scoring = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'RoundEngine.Scoring.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
dead = [n for n in style if not re.search(r'\bStyle\.%s\b' % n, view[end:])]
print('   %d ayar, %d okunuyor' % (len(style), len(style) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['MoveDuration', 'LiftAmount', 'ShadowLiftStrength', 'ShadowSettleStrength', 'ScaleResponse', 'SettleDuration',
          'TimingVariation', 'EscalatorMoveDuration', 'EscalatorAnticipationDuration', 'EscalatorAnticipationCompression',
          'EscalatorLiftAmount', 'EscalatorEaseIn', 'EscalatorEaseOut', 'EscalatorSettleAmount', 'EscalatorSettleDuration',
          'EscalatorBottomRowRevealDuration', 'CentrifugalMoveDuration', 'CentrifugalPreloadAmount',
          'CentrifugalPreloadDuration', 'CentrifugalAccelerationStrength', 'CentrifugalDecelerationStrength',
          'CentrifugalDirectionalStretch', 'CentrifugalCrossCompression', 'CentrifugalShadowLag',
          'CentrifugalLandingOvershoot', 'CentrifugalLandingSettleDuration', 'CentrifugalTimingVariation']
missing = [w for w in wanted if w not in style]
check('brief\'in %d ayari Style\'da' % len(wanted), not missing, 'eksik ayar: %s' % missing)

print()
print('=== 2. CORE: YALNIZ RAPOR ===')
check('CellMove: From, To, Step, Cube, Source, Mirror', 'public readonly struct CellMove' in report
      and all(('public readonly %s %s;' % t) in report for t in (('GridPos', 'To'), ('BoardMotionSource', 'Source'))),
      'CellMove eksik')
check('BoardMotionSource { None, Escalator, Centrifuge }', 'public enum BoardMotionSource' in report
      and 'Escalator,' in report and 'Centrifuge' in report, 'kaynak yok')
check('rapor: BoardMoves', 'public IReadOnlyList<CellMove> BoardMoves' in report, 'rapor tasimiyor')
shift = method(lines, 'public List<GridPos> ShiftRowsUp(List<LiftMotion> motions, List<CellMove> moves)')
fling = method(lines, 'public List<GridPos> FlingCubesOutward(List<LiftMotion> motions, List<CellMove> moves)')
check('merdiven: hayatta kalan her kup, alttaki hucreden', 'moves.Add(new CellMove(new GridPos(x + MinX, y - 1 + MinY)' in shift
      and 'BoardMotionSource.Escalator' in shift, 'merdiven hareketi yazilmiyor')
check('merkezkac: yerine konan her kup, kaynak ve hedefiyle', fling.index('cells[tx, ty] = cube;') < fling.index('moves.Add(new CellMove(')
      and 'BoardMotionSource.Centrifuge' in fling, 'merkezkac hareketi yazilmiyor')
check('eski cagrilar ayni', 'return ShiftRowsUp(motions, null);' in lines and 'return FlingCubesOutward(motions, null);' in lines
      and 'return ShiftRowsUp(null);' in lines and 'return FlingCubesOutward(null);' in lines, 'eski cagri degisti')
check('RoundEngine iki boss icin de rapora iletiyor (ayna dahil)', scoring.count('currentReport.AddBoardMoves(moves)') == 2
      and scoring.count('AddMirrorMoves(moves, mirrorMoves)') == 2, 'rapora iletilmiyor')
check('Core testleri hareketi dogruluyor', 'report.BoardMoves' in tests and 'FlingCubesOutward(pushMotions, pushMoves)' in tests
      and 'ShiftRowsUp(new List<LiftMotion>(), rideMoves)' in tests, 'test yok')

print()
print('=== 3. AKIS ===')
fin = method(fb, 'private void FinalizePlacement(')
check('yeniden cizildigi karede, suyun arkasina ertelenmeden', 'PlayBoardMotion(report);' in fin
      and fin.index('RefreshAll(report);') < fin.index('PlayBoardMotion(report);') < fin.index('WaterFallFrames'),
      'hareket ertelenebilir')
motion = method(fb, 'private void PlayBoardMotion(')
check('hayatta kalanlar ve kopanlar ayni anda', 'PlayBoardMoves(report.BoardMoves)' in motion
      and 'PlayForcedExit(carried, motions)' in motion, 'ayri zamanlarda')
moves_fn = method(fb, 'private bool PlayBoardMoves(')
check('kaynak ve hedef Core\'dan (m.From / m.To), kup Core\'dan', 'CellToWorld(m.From)' in moves_fn
      and 'CellToWorld(m.To)' in moves_fn and 'targets.Add(m.To)' in moves_fn
      and 'FindOwnedCard(m.Cube.SourceCardId)' in moves_fn and 'sources.Add(m.Source)' in moves_fn, 'hedef Core\'dan degil')
check('view hedef/yon HESAPLAMIYOR', 'Mathf.Sign(' not in view and 'Mathf.Sign(' not in moves_fn
      and 'MinX' not in view, 'view hesapliyor')
emit = method(fb, 'private void EmitBlastParticles(')
check('kopanlar ikinci kez oynatilmiyor', 'PlayForcedExit(' not in emit, 'cift oynatma')
check('ayna dunyadaki hareket ayna tahtada', 'mirrorBoardView' in moves_fn and 'm.Mirror != mirror' in moves_fn, 'ayna yok')

print()
print('=== 4. VEKIL VE TESLIM ===')
check('tahta: HoldCells / ReleaseCells', 'public void HoldCells(' in boardview and 'public void ReleaseCells(' in boardview,
      'tahta API yok')
refresh = method(boardview, 'public void Refresh(')
check('yeniden cizimde tutulan hucreler yine bos', 'BlankHeld(cell);' in refresh, 'yeniden cizim kupu gosterir')
check('element nabzi tutulan hucreye kup cizmez', 'kindCache[cell.X - board.MinX, cell.Y - board.MinY] = null;' in boardview,
      'nabiz kupu geri cizer')
check('yeni tahtada tutulan hucre kalmaz', 'heldCells.Clear();' in method(boardview, 'public void Rebuild('), 'rebuild')
play = method(view, 'public void Play(')
check('hedefler oynarken tutuluyor, inince birakiliyor', 'board.HoldCells(batch.Held)' in play
      and 'batch.Board.ReleaseCells(batch.Held)' in method(view, 'private void Release('), 'teslim yok')
paint = method(view, 'private void PaintMover(')
check('govde hic solmuyor (alfa 1)', 'PlaceAxes(m.Body, body, size * sx, size * sy, tint, 1f);' in paint, 'govde soluyor')
check('kaynaktan hedefe duz cizgi', 'm.From + (m.To - m.From) * along' in paint, 'yol duz degil')
check('donme yok', 'localRotation = Quaternion.identity' in method(view, 'private static void PlaceAxes(')
      and 'Rotate' not in paint, 'donuyor')
check('kendi materyali (su/ates animasyonu surer)', 'ViewUtil.TileMaterial(m.Look.Tile)' in play, 'materyal kayboluyor')
check('ust uste binme: golge < kup < yuzey', re.search(r'ShadowOrder = (\d+)', view) and
      int(re.search(r'ShadowOrder = (\d+)', view).group(1)) < int(re.search(r'BodyOrder = (\d+)', view).group(1))
      < int(re.search(r'SurfaceOrder = (\d+)', view).group(1)), 'siralama')

print()
print('=== 5. YASAKLAR ===')
check('parcacik / iz / sarsinti / kamera / zaman / rastgele yok',
      not any(w in view for w in ('ParticleSystem', 'TrailRenderer', 'LineRenderer', 'ShakeCamera', 'Camera', 'timeScale', 'Random.')),
      'yasak var')
check('merdivende yana kayma/golge gecikmesi yok', 'p.ShadowLag = 0f;' in view and 'p.Cross = 0f;' in view, 'merdiven kayiyor')

print()
print('=== 6. OLCULER ===')
esc_total = style['EscalatorAnticipationDuration'] + style['EscalatorMoveDuration'] + style['EscalatorSettleDuration']
cen_total = style['CentrifugalPreloadDuration'] + style['CentrifugalMoveDuration'] + style['CentrifugalLandingSettleDuration']
check('merdiven %.3f s (0.20-0.36)' % esc_total, 0.20 <= esc_total <= 0.36, 'merdiven suresi')
check('merkezkac %.3f s (0.20-0.36)' % cen_total, 0.20 <= cen_total <= 0.36, 'merkezkac suresi')
for name in ('EscalatorLiftAmount', 'LiftAmount'):
    px = style[name] * 64
    check('%s %.1f px (1-3)' % (name, px), 1.0 <= px <= 3.0, 'kalkma %s' % name)
check('merdiven olcek tepkisi %%%.1f (<= 3)' % (style['ScaleResponse'] * 100), style['ScaleResponse'] <= 0.03, 'olcek')
check('merdiven yuku %%%.1f / %.1f px (<= %%1.5)' % (style['EscalatorAnticipationCompression'] * 100,
                                                    style['EscalatorAnticipationCompression'] * 64),
      style['EscalatorAnticipationCompression'] <= 0.015, 'yuk')
check('merdiven asma %.1f px (<= 1)' % (style['EscalatorSettleAmount'] * 64), style['EscalatorSettleAmount'] * 64 <= 1.0, 'asma')
check('merdiven yuk %.0f ms (30-60)' % (style['EscalatorAnticipationDuration'] * 1000),
      0.03 <= style['EscalatorAnticipationDuration'] <= 0.06, 'yuk suresi')
check('merdiven senkron (%.0f ms)' % (style['TimingVariation'] * 1000), style['TimingVariation'] <= 0.005, 'senkron degil')
check('alt satir acilisi %.0f ms (100-180)' % (style['EscalatorBottomRowRevealDuration'] * 1000),
      0.10 <= style['EscalatorBottomRowRevealDuration'] <= 0.18, 'alt satir')
check('merkezkac on yukleme %.1f px (0.5-1.5)' % (style['CentrifugalPreloadAmount'] * 64),
      0.5 <= style['CentrifugalPreloadAmount'] * 64 <= 1.5, 'on yukleme')
check('merkezkac uzama %%%.1f (1-4), capraz %%%.1f (1-2)' % (style['CentrifugalDirectionalStretch'] * 100,
                                                          style['CentrifugalCrossCompression'] * 100),
      0.01 <= style['CentrifugalDirectionalStretch'] <= 0.04 and 0.01 <= style['CentrifugalCrossCompression'] <= 0.02, 'uzama')
check('merkezkac asma %.1f px (0.5-1.5)' % (style['CentrifugalLandingOvershoot'] * 64),
      0.5 <= style['CentrifugalLandingOvershoot'] * 64 <= 1.5, 'asma')
check('merkezkac kup arasi en fazla %.0f ms (<= 20)' % (style['CentrifugalTimingVariation'] * 2000),
      style['CentrifugalTimingVariation'] * 2 <= 0.02, 'fazla gecikme')
ei_c = 0.35 + (0.08 - 0.35) * min(max(style['CentrifugalAccelerationStrength'], 0), 1)
check('merkezkac daha sert kalkar (hizlanma payi %.2f < merdiven %.2f)' % (ei_c, style['EscalatorEaseIn']),
      ei_c < style['EscalatorEaseIn'], 'iki karakter ayni')

print()
print('=== 7. SUREKLILIK ===')
check('sokulme bossun kalkisini kullaniyor', 'BossMoveView.LaunchRelease(p.Source)' in peel
      and 'BossMoveView.Launch(p.Source, t, out ignored) * batch.Cell' in peel, 'kalkis ayri')
travel = method(peel, 'private static float BodyTravel(')
check('tasinan kup frenlemiyor (arkadaki satir yetismesin)', travel.index('return BossMoveView.Launch(') < travel.index('BodyBrake'),
      'fren var')

print()
print('=== 8. LAB, HATA AYIKLAMA, YASAM DONGUSU ===')
labels = ['Yürüyen Merdiven — İç Hareket', 'Merkezkaç — İç Hareket', 'iç hareket hata ayıklama: gölge tepkisi',
          'iç hareket hata ayıklama: ölçek tepkisi', 'iç hareket hata ayıklama: hareket vektörü',
          'iç hareket hata ayıklama: hedef hücre', "iç hareket hata ayıklama: hareket proxy'si"]
check('lab girisleri', all(w in lab_raw for w in labels), 'labda eksik: %s' % [w for w in labels if w not in lab_raw])
test = method(lab, 'private IEnumerator BoardMoveTestRoutine(')
check('lab gercek tahta koduyla ve oyunun kapilarindan', 'board.ShiftRowsUp(motions, moves)' in test
      and 'board.FlingCubesOutward(motions, moves)' in test and 'PlayBoardMoves(moves)' in test
      and 'PlayForcedExit(lifted, motions)' in test, 'lab ayri yol')
check('merkezkac testi tek boyutlu tahtada (merkez sabit kalir)', ': 7;' in test, 'merkez testi yok')
close = method(lab, 'private void CloseAnimationLab(')
check('lab kapaninca hareket biter, anahtarlar varsayilana', 'bossMove.Stop()' in close
      and 'BossMoveView.Layers.Defaults()' in close and close.index('bossMove.Stop()') < close.index('AnimResync()'),
      'lab kapanisi')
views = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Views.cs'))
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('view kuruluyor, menude gizleniyor, kosu bitince duruyor', 'AddComponent<BossMoveView>()' in views
      and 'bossMove.gameObject.SetActive(visible)' in menus and 'bossMove.Stop()' in menus, 'yasam dongusu')
check('csproj dosyayi iceriyor', 'BossMoveView.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
