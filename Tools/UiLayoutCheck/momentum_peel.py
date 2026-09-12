# "Soguk sokulme" (MomentumPeelView): yapisal kontroller.
#
#   1. Style: brief'in ayar listesi eksiksiz, okunmayan ayar yok.
#   2. Core: hareket bilgisi YALNIZ RAPOR - LiftMotion / LiftReason; tahta islemleri her giden kup
#      icin bir kayit yaziyor, eski cagrilar aynen kaliyor; RoundEngine rapora iletiyor; testler var.
#   3. Yonlendirme: yalniz TASINAN (Relocated + hareketli) kupler sokulmeye gider; yon Core'dan
#      (m.Step) gelir, view yon hesaplamaz; yok olanlar kaldirma varyantina, donusenler eski ize.
#   4. Shader: Resources'ta, Universal2D, dilim dot(yuz, yon) ile, tahta dikdortgeninden kirpma;
#      C#'in verdigi her ozellik shader'da; shader yoksa yedek yol.
#   5. Diger dillerle karismaz: kuyu/maske/yarik/buz/patlama/kamera/rastgele yok; kiymiklar dar
#      konide; hucreye kare boya yok.
#   6. Olculer ve zamanlama brief'in araliklarinda.
#   7. Lab girisleri, hata ayiklama anahtarlari, yasam dongusu.
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
    print('   %-68s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'MomentumPeelView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
report = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'TurnReport.cs'))
lines = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Lines.cs'))
scoring = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'RoundEngine.Scoring.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')
shader_path = os.path.join(ROOT, 'Assets', 'Resources', 'Shaders', 'MomentumPeel.shader')
shader = open(shader_path, encoding='utf-8').read() if os.path.exists(shader_path) else ''

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
dead = [n for n in style if not re.search(r'\bStyle\.%s\b' % n, view[end:])]
print('   %d ayar, %d okunuyor' % (len(style), len(style) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['TensionDuration', 'TensionStretch', 'CrossCompression', 'LayerCountMin', 'LayerCountMax',
          'LayerThicknessVariation', 'LayerStartDelay', 'LayerDelayStep', 'LayerPerpendicularOffset',
          'LeadingSpeed', 'SecondarySpeed', 'TrailingSpeed', 'TrailingLag', 'TrailingSnapAmount',
          'StreakLength', 'StreakStretch', 'StreakLifetime', 'DesaturationStrength', 'ColdInfluence',
          'FleckCount', 'FleckConeAngle', 'FleckLifetime', 'ResidueStrength', 'ResidueLength',
          'ResidueDuration', 'TimingJitter', 'LargeCountDetailCompensation']
missing = [w for w in wanted if w not in style]
check('brief\'in %d ayari Style\'da' % len(wanted), not missing, 'eksik ayar: %s' % missing)

print()
print('=== 2. CORE: YALNIZ RAPOR ===')
check('LiftReason { None, ExitedBoard, NoGround, Blocked }', 'public enum LiftReason' in report
      and all(k in report for k in ('ExitedBoard', 'NoGround', 'Blocked')), 'sebep yok')
check('LiftMotion: From, Step, Reason, Cube, Mirror', 'public readonly struct LiftMotion' in report
      and all(('public readonly %s %s;' % t) in report for t in (('GridPos', 'From'), ('GridPos', 'Step'),
                                                                ('LiftReason', 'Reason'), ('Cube', 'Cube'), ('bool', 'Mirror'))),
      'hareket kaydi eksik')
check('rapor: LiftMotionAt, hucreyle ayni sirada', 'public LiftMotion LiftMotionAt(' in report
      and 'liftMotions.Add(' in report, 'rapor hareketi tasimiyor')
shift = method(lines, 'public List<GridPos> ShiftRowsUp(List<LiftMotion> motions, List<CellMove> moves)')
fling = method(lines, 'public List<GridPos> FlingCubesOutward(List<LiftMotion> motions, List<CellMove> moves)')
check('eski cagrilar aynen (ShiftRowsUp() / FlingCubesOutward() yeniye null verir)',
      'return ShiftRowsUp(null);' in lines and 'return FlingCubesOutward(null);' in lines, 'eski cagri degisti')
check('merdiven: kenardan cikan + delige binen icin kayit (2 yer)', shift.count('motions.Add(') == 2
      and 'LiftReason.ExitedBoard' in shift and 'LiftReason.NoGround' in shift, 'merdiven kaydi eksik')
check('merkezkac: gercek adim (dx, dy) ve uc sebep', 'new GridPos(dx, dy)' in fling and 'LiftReason.Blocked' in fling
      and 'LiftReason.NoGround' in fling and 'LiftReason.ExitedBoard' in fling, 'merkezkac kaydi eksik')
check('kayit tahta degismeden once/sonra ayni adimda (kural ayni)', 'lost.Add(lostAt);' in fling, 'fling degisti')
check('RoundEngine iki boss icin de rapora iletiyor', scoring.count('AddLiftedCells(lost, LiftKind.Relocated, motions)') == 2,
      'rapora iletilmiyor')
check('ayna dunya isaretleniyor', 'AddMirrorMotions(motions, mirrorMotions)' in scoring and 'OnMirror()' in scoring,
      'ayna ayrimi yok')
check('Core testleri hareketi dogruluyor', 'LiftMotionAt(' in tests and 'LiftReason.NoGround' in tests
      and 'FlingCubesOutward(motions)' in tests and 'ShiftRowsUp(rideMotions)' in tests, 'test yok')

print()
print('=== 3. YONLENDIRME ===')
emit = method(fb, 'private void EmitBlastParticles(')
motion_fn = method(fb, 'private void PlayBoardMotion(')
check('yok olan -> kaldirma varyanti; tasinan+hareketli -> sokulme (tahtanin hareketiyle); gerisi -> eski iz',
      'PlayRemoval(removed)' in emit and 'LiftCells(marked' in emit and 'motion.Reason != LiftReason.None' in emit
      and 'PlayForcedExit(' not in emit and 'PlayForcedExit(carried, motions)' in motion_fn, 'yonlendirme eksik')
exitm = method(fb, 'private bool PlayForcedExit(')
check('yon Core\'dan (m.Step), kup Core\'dan (m.Cube), baslangic hucresi m.From',
      'm.Step.X' in exitm and 'FindOwnedCard(m.Cube.SourceCardId)' in exitm and 'CellToWorld(m.From)' in exitm,
      'yon/kup Core\'dan gelmiyor')
check('view yon TAHMIN etmiyor (Sign / merkeze gore hesap yok)',
      'Mathf.Sign(' not in view and 'Mathf.Sign(' not in exitm and 'step.normalized' in view,
      'view yon hesapliyor')
check('ayna dunyadaki kup ayna tahtada oynar', 'mirrorBoardView' in exitm and 'm.Mirror != mirror' in exitm, 'ayna yok')
check('tahta dikdortgeni gidiyor (kirpma icin)', 'view.WorldRect' in exitm, 'kirpma bilgisi yok')

print()
print('=== 4. SHADER ===')
check('MomentumPeel.shader Resources/Shaders altinda', bool(shader), 'shader yok')
check('2D renderer\'a etiketli (Universal2D)', '"LightMode" = "Universal2D"' in shader, 'etiket yok')
check('dilim yone DIK: duz adimda duz, caprazda yuvarlak L (ucgen kiymik yok)',
      'float s = Slice(input.face, d);' in shader and 'sqrt(gap * gap + k * k)' in shader, 'dilim yone bagli degil')
check('govde kopan dilimin altina tasar (koyu kesik cizgisi yok)', 'front + 2f * BandSoftness' in view, 'kesik cizgisi')
check('engelli kup kendi hucresinde kirpilir (engele karismaz)', 'p.Reason == LiftReason.Blocked' in view
      and 'new Rect(p.At.x - cellSize * 0.5f' in view, 'engelin icine giriyor')
check('tahta dikdortgeninden kirpma, kosede yuvarlak (mesafe)', '_Clip' in shader and 'length(max(past, 0.0))' in shader,
      'kirpma yok')
check('bukulme dogrusal: capa + uzama + gecis (tepe noktada)', 'anchor + (along - anchor) * _Motion.y + _Motion.x' in shader,
      'deformasyon yok')
ids = re.findall(r'Shader\.PropertyToID\("(\w+)"\)', view)
missing_props = [p for p in ids if p not in shader]
check('C#\'in verdigi %d ozelligin hepsi shader\'da' % len(ids), ids and not missing_props, 'eksik: %s' % missing_props)
check('shader yoksa/desteklenmezse yedek yol', 'shader.isSupported' in view and 'peelMaterial != null' in view, 'yedek yok')
check('property block tek ve yeniden kullaniliyor', view.count('new MaterialPropertyBlock()') == 1, 'her karede block')
check('parlama yok (alfa karisimi)', 'Blend SrcAlpha OneMinusSrcAlpha' in shader and 'Blend One One' not in shader, 'additive')

print()
print('=== 5. DIGER DILLERLE KARISMAZ ===')
check('kuyu / maske / yarik / buz yok', not any(w in view for w in ('SpriteMask ', 'VisibleInsideMask', '_Seam', '_Frost', 'Vector2.down')),
      'baska varyantin dili')
check('patlama / kamera / zaman / partikul / rastgele yok',
      not any(w in view for w in ('ShakeCamera', 'Camera', 'timeScale', 'ParticleSystem', 'Explode', 'Random.')),
      'patlama gibi')
check('kiymiklar dar konide (%.0f derece <= 20)' % style['FleckConeAngle'], style['FleckConeAngle'] <= 20, 'kiymik genis')
check('hucreye kare boya yok (SoftSquare yok, iz ince serit)', 'SoftSquare' not in view
      and 'PlaceRect(p.Residue, centre, length, 0.3f * batch.CubeSize' in view, 'hucre boyasi')

print()
print('=== 6. OLCULER VE ZAMANLAMA ===')
check('gerilme %%%.0f (4-10)' % (style['TensionStretch'] * 100), 0.04 <= style['TensionStretch'] <= 0.10, 'gerilme')
check('capraz sikisma %%%.1f (1-4)' % (style['CrossCompression'] * 100), 0.01 <= style['CrossCompression'] <= 0.04, 'sikisma')
check('katman %d-%d (3-4)' % (style['LayerCountMin'], style['LayerCountMax']),
      3 <= style['LayerCountMin'] <= style['LayerCountMax'] <= 4, 'katman sayisi')
px = style['LayerPerpendicularOffset'] * 64
check('katmanlar arasi %.1f px (1-3)' % px, 1.0 <= px <= 3.0, 'katman araligi')
check('serit en fazla %.2f hucre (0.3-1.0)' % style['StreakLength'], 0.3 <= style['StreakLength'] <= 1.0, 'serit boyu')
reach = float(re.search(r'StreakOutsideReach = ([\d.]+)f', view).group(1))
check('tahta disinda en fazla %.2f hucre gorunur (0.2-0.5)' % reach, 0.2 <= reach <= 0.5, 'disari tasma')
check('hareket izi %.0f ms (80-180)' % (style['ResidueDuration'] * 1000), 0.08 <= style['ResidueDuration'] <= 0.18, 'iz')
check('kup basina ofset %.0f ms (15-30)' % (style['TimingJitter'] * 1000), 0.015 <= style['TimingJitter'] <= 0.03, 'ofset')
check('kiymik %d (2-5)' % style['FleckCount'], 2 <= style['FleckCount'] <= 5, 'kiymik sayisi')
lead = style['LayerStartDelay']
worst_release = lead + 2 * style['LayerDelayStep'] * 1.25 + style['LayerDelayStep'] + style['TrailingLag'] * 1.2
best_release = lead + style['LayerDelayStep'] * 0.75 + style['LayerDelayStep'] + style['TrailingLag'] * 0.8
worst = max(worst_release + style['StreakLifetime'] * 1.1 * 1.1, worst_release + 0.04 + style['ResidueDuration']) \
    + 2 * style['TimingJitter']
best = best_release + style['StreakLifetime'] * 1.1 * 0.9
check('toplam %.2f-%.2f s (0.35-0.55)' % (best, worst), best >= 0.33 and worst <= 0.55, 'sure brief disi')
check('ilk ayrilma %.0f ms (50-140)' % (lead * 1000), 0.05 <= lead <= 0.14, 'ilk ayrilma')

print()
print('=== 7. LAB, HATA AYIKLAMA, YASAM DONGUSU ===')
labels = ['yürüyen merdiven: alan yukarı kayar, üst satır sökülür', 'merkezkaç kuvveti: küpler dışa itilir, kenardakiler sökülür',
          'boss hareketiyle atılma: her basışta sıradaki yön', 'boss hareketiyle atılma: deliğe çıkış',
          'boss hareketiyle atılma: engelli hedef', 'sökülme hata ayıklama: gerilme', 'sökülme hata ayıklama: katmanlar',
          'sökülme hata ayıklama: kıymıklar', 'sökülme hata ayıklama: hareket izi', 'sökülme hata ayıklama: tahta maskesi']
check('lab girisleri', all(w in lab_raw for w in labels), 'labda eksik: %s' % [w for w in labels if w not in lab_raw])
check('lab sahneleri gercek tahta koduyla ve ayni kapidan (PlayForcedExit)', 'board.ShiftRowsUp(motions, moves)' in lab
      and 'board.FlingCubesOutward(motions, moves)' in lab and 'PlayForcedExit(lifted, motions)' in lab, 'lab ayri yol')
check('8 yon', lab.count('new GridPos(') >= 8 and 'AnimPeelSteps' in lab, '8 yon yok')
layers = view[view.index('public static class Layers'):]
keys = ('ShowTension', 'ShowLamination', 'ShowFlecks', 'ShowResidue', 'ShowBoardClip')
check('5 hata ayiklama anahtari, hepsi okunuyor', all(k in layers and view.count('Layers.' + k) >= 1 for k in keys),
      'anahtar eksik')
check('lab kapaninca anahtarlar geri acilir', 'MomentumPeelView.Layers.AllOn()' in method(lab, 'private void CloseAnimationLab('),
      'anahtar kapali kalabilir')
views = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Views.cs'))
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('view kuruluyor, menude gizleniyor, kosu bitince duruyor', 'AddComponent<MomentumPeelView>()' in views
      and 'momentumPeel.gameObject.SetActive(visible)' in menus and 'momentumPeel.Stop()' in menus, 'yasam dongusu')
check('csproj dosyayi iceriyor', 'MomentumPeelView.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
