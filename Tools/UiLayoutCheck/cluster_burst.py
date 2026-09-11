# "Patlama: N hucre" (ClusterBurstView): yapisal kontroller.
#
# Brief'in "kesinlikle" dediklerini koddan dogrular - hepsi "derleniyor"un goremeyecegi seyler:
#   1. Style'da okunmayan ayar yok; brief'in ayar listesi eksiksiz.
#   2. Butun gevsek grup patlamalari bu sistemden geciyor; satir isini ona dokunmuyor, soguk
#      "kaldirma" eski sessiz dilde.
#   3. Yasaklar: tum alan / kamera katmani, Time.timeScale, property block, rastgele (Random) yok,
#      beyaz yildiz spam'i yok, krem panel yok.
#   4. Kamera: varsayilan olarak HIC oynamiyor (tasarim karari), yalniz grup patlamasi olan tur da.
#   5. Zamanlama: basinc 80-160 ms, catlak basincin icinde, kucuk N <= 0.55 s, buyuk N <= 0.80 s.
#   6. Lab: tum N'ler, iki renk, daginik grup, siralama.
#   7. Katmanli malzeme kirilmasi: dort debris ailesi ayri hiz/donus/boyutla, sekil cesitliligi,
#      kupun kendi dokusundan kesilen parcalar, catlak desenleri, LOD kademeleri, kup karakteri.
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
    print('   %-62s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'ClusterBurstView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
act = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Activation.cs'))
board = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in
             re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
body = view[end:]
dead = [n for n in style if not re.search(r'\bStyle\.%s\b' % n, body)
        and not re.search(r'\bClusterBurstView\.Style\.%s\b' % n, fb)]
print('   %d ayar, %d okunuyor' % (len(style), len(style) - len(dead)))
for n in dead:
    print('   OLU: Style.%s' % n)
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['PressureDuration', 'PressureStrength', 'FractureOpacity', 'FractureDuration',
          'FractureVariationCount', 'MajorFragmentCountMin', 'MajorFragmentCountMax',
          'MajorFragmentSizeMin', 'MajorFragmentSizeMax', 'MajorFragmentSpeed', 'MajorFragmentDrag',
          'MajorFragmentRotation', 'SecondaryCount', 'SecondarySize', 'SecondarySpeed',
          'MicroDebrisCount', 'MicroDebrisSpeed', 'MicroDebrisLifetime', 'HotSpeckCount',
          'HotSpeckSpeed', 'HotSpeckLifetime', 'GhostShellOpacity', 'GhostShellDuration',
          'CoreStrength', 'CoreSize', 'CoreDuration', 'RadialDelay', 'RadialJitter',
          'RippleStrength', 'RippleDuration', 'HeatResidueStrength', 'HeatResidueDuration',
          'LargeNParticleCompensation', 'ScreenImpulseStrength', 'HitStopFrames']
missing = [w for w in wanted if w not in style]
check('brief\'in %d ayari Style\'da' % len(wanted), not missing, 'eksik ayar: %s' % missing)

print()
print('=== 2. KIM NEREDEN GECIYOR ===')
flash = method(fb, 'private bool FlashCells(')
check('FlashCells -> clusterBurst.Play', 'clusterBurst.Play(' in flash, 'FlashCells yeni sistemi cagirmiyor')
check('FlashCells icinde eski kare/parcacik YOK',
      'CellFlashFx' not in flash and 'BurstParticles' not in flash and 'blastFx' not in flash,
      'FlashCells eski kareleri de ciziyor')
line = method(fb, 'private void FlashLine(')
check('FlashLine hala isin, kume YOK', 'lineSweep.Play(' in line and 'lineBurst.Play(' in line
      and 'clusterBurst' not in line, 'satir yolu degismis')
lift = method(fb, 'private bool LiftCells(')
check('kaldirma soguk ve sessiz', 'Palette.Cold' in lift and 'Shake' not in lift, 'LiftCells soguk degil')
emit = method(fb, 'private void EmitBlastParticles(')
check('tur raporu: kaldirilanlar patlama degil (yok olan -> PlayRemoval, kalan -> LiftCells)',
      'LiftKindAt(' in emit and 'PlayRemoval(removed)' in emit and 'LiftCells(marked' in emit,
      'LiftedCells patlama gibi ciziliyor')
check('eski imza hicbir yerde kalmadi', not re.findall(r'FlashCells\([^;]*?,\s*\d+\s*(?:,|\))', fb + act + lab),
      'eski FlashCells cagrisi')
views = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Views.cs'))
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('view kuruluyor, menude gizleniyor, kosu bitince duruyor',
      'AddComponent<ClusterBurstView>()' in views and 'clusterBurst.gameObject.SetActive(visible)' in menus
      and 'clusterBurst.Stop()' in menus, 'yasam dongusu eksik')
check('csproj dosyayi iceriyor', 'ClusterBurstView.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
print('=== 3. YASAKLAR ===')
white = [l.strip() for l in view.split('\n') if 'WhiteSprite' in l]
check('duz beyaz kare yalnizca "hic sanat yok" yedegi', len(white) == 1, 'WhiteSprite katman: %s' % white)
check('tum alan / kamera katmani YOK', 'WorldRect' not in view and 'Camera' not in view and 'Screen.' not in view,
      'alan/ekran boyutunda cizim olabilir')
check('Time.timeScale YOK (hit-stop yalnizca efektin saati)', 'timeScale' not in view, 'hit-stop global')
check('property block / yeni material YOK', 'MaterialPropertyBlock' not in view and 'new Material(' not in view,
      'batching bozuluyor')
check('efekt kamerayi kendisi oynatmiyor', 'ShakeCamera' not in view, 'view kamerayi biliyor')
check('rastgele YOK - her sey hucreden (deterministik)', 'Random.' not in view, 'UnityEngine.Random kullaniliyor')
check('Mathf.SmoothStep kenar gibi KULLANILMAMIS', not re.findall(r'Mathf\.SmoothStep\((?!0f, 1f,)', view), 'SmoothStep yanlis')
orders = dict((n, int(v)) for n, v in re.findall(r'private const int (\w+Order) = (-?\d+);', view))
check('siralama 5..12', orders and all(5 <= v <= 12 for v in orders.values()), 'siralama tasiyor: %s' % orders)
hot = re.search(r'Color\.HSVToRGB\(h, rich, v\),\s*Color\.Lerp\(main, Color\.white, ([\d.]+)f\)', view)
check('en sicak renk beyaza %s (<= 0.55, krem panel yok)' % (hot.group(1) if hot else '?'),
      hot is not None and float(hot.group(1)) <= 0.55, 'Hot beyaza fazla yakin')
check('flare seyrek (her %d kupten biri + hero)' % style['FlareEvery'], style['FlareEvery'] >= 3, 'yildiz spam')
check('cekirdek kompakt (CoreSize %.2f <= 0.35)' % style['CoreSize'], style['CoreSize'] <= 0.35, 'core blob')

print()
print('=== 4. KAMERA ===')
check('ekran darbesi varsayilan KAPALI (ScreenImpulseStrength = 0)', style['ScreenImpulseStrength'] == 0,
      'grup patlamasi ekrani oynatiyor')
check('FlashCells darbeyi yalnizca ayar > 0 ise verir', 'ImpulseFor(' in flash and 'if (impulse > 0f)' in flash,
      'darbe kosulsuz')
check('guc patlamasi / supurge sarsmiyor', 'Shake' not in method(act, 'private void PlayPowerBlast(')
      and 'Shake' not in method(act, 'private IEnumerator SupurgeBlastRoutine('), 'sabit sarsinti kalmis')
check('yalnizca grup patlamasi olan tur sarsmiyor',
      'if (report.CubesExploded > 0 || report.DynamiteTriggered || report.CleanSweep)' in method(fb, 'private void HandleBlastFeedback('),
      'Hedefli/gec patlama turu sarsiyor')
check('hit-stop en fazla 2 kare', style['HitStopFrames'] <= 2, 'hit-stop uzun')

print()
print('=== 5. ZAMANLAMA ===')
pre = style['PressureDuration']
last_break = pre + style['RadialMaxSpread'] + style['RadialJitter']
small_end = pre + style['CoreDuration'] + style['HeatResidueDuration']
debris_tail = max(style['MajorFragmentLifetime'] * 1.2 * 1.15, style['SecondaryLifetime'] * 1.15 + 0.008,
                  style['MicroDebrisLifetime'] * 1.2 + 0.025)
check('basinc %.0f ms (80-160)' % (pre * 1000), 0.08 <= pre <= 0.16, 'basinc penceresi disi')
check('catlak basincin icinde (%.0f ms < %.0f ms)' % (style['FractureDuration'] * 1000, pre * 1000),
      style['FractureDuration'] < pre, 'catlak basinctan uzun')
check('ic cekirdek %.0f ms (40-100)' % (style['CoreDuration'] * 1000), 0.04 <= style['CoreDuration'] <= 0.10, 'core suresi')
check('hayalet kabuk %.0f ms (50-120)' % (style['GhostShellDuration'] * 1000), 0.05 <= style['GhostShellDuration'] <= 0.12,
      'ghost suresi')
check('kucuk N sonu %.2f s (<= 0.55)' % max(small_end, pre + debris_tail), max(small_end, pre + debris_tail) <= 0.55,
      'kucuk N uzun')
check('buyuk N sonu %.2f s (<= 0.80)' % (last_break + 0.034 + debris_tail), last_break + 0.034 + debris_tail <= 0.80,
      'buyuk N uzun')
check('jitter halkalari bozmuyor', style['RadialJitter'] < style['RadialDelay'] / 3, 'jitter buyuk')

print()
print('=== 6. LAB ===')
labels = ['patlama: N hücre (hücre ayarı)', 'patlama: element blokları', 'patlama: her element sırayla',
          'patlama: dağınık N hücre', "patlama: tüm N'ler sırayla", 'kangren: satır ölür',
          'süpürge gecikmeli patlama']
check('"güç patlaması" lab girisi yok (patlama: N hücre ile ayni cagri)', 'güç patlaması' not in lab_raw,
      'tekrar eden lab girisi geri gelmis')
check('lab girisleri', all(w in lab_raw for w in labels), 'labda eksik: %s' % [w for w in labels if w not in lab_raw])
faces = method(lab, 'private List<ClusterBurstView.Look> AnimCubeFaces(')
check('lab GERCEK bloklari veriyor (CardCubeTile - elde/alanda cizilen kural)', 'ViewUtil.CardCubeTile(' in faces,
      'lab blok yuzlerini uyduruyor')
def calls(src, name):
    """Every call of `name(`, whole: read to its matching parenthesis, so a delegate inside
    the arguments (with its own `;`) cannot cut it short."""
    found = []
    for m in re.finditer(re.escape(name) + r'\(', src):
        depth, j = 0, m.end() - 1
        while j < len(src):
            depth += {'(': 1, ')': -1}.get(src[j], 0)
            if depth == 0:
                break
            j += 1
        found.append(src[m.start():j + 1])
    return found


lab_blasts = [c for c in calls(lab, 'FlashCells') if re.match(r'FlashCells\(Anim\w*Cells\(', c)]
check('lab patlamalarinin hepsi blok yuzu veriyor (%d)' % len(lab_blasts),
      lab_blasts and all('AnimCubeFaces(' in c for c in lab_blasts), 'yuzsuz lab patlamasi: %s' % [c for c in lab_blasts if 'AnimCubeFaces(' not in c])
every = method(lab, 'private IEnumerator ClusterEveryElementRoutine(')
check('her element sirasi enum\'dan, gercek bloklarla', 'System.Enum.GetValues(typeof(BlockElement))' in every
      and 'AnimCubeFaces(element)' in every, 'element sirasi eksik')
check('FlashCells lab yuzlerini kabul ediyor', 'faces[i % faces.Count]' in flash, 'faces parametresi yok')
check('hucre ayari 1, 5, 12, 20, 40\'i kapsiyor', all(re.search(r'\b%d\b' % n, re.search(r'AnimCellCounts = \{([^}]*)\}', lab).group(1))
                                                      for n in (1, 5, 12, 20, 40)), 'hucre ayari eksik')
check('lab kapaninca dizi duruyor', 'StopAnimBurstSequence();' in method(lab, 'private void CloseAnimationLab('), 'dizi suruyor')

print()
print('=== 7. KATMANLI MALZEME KIRILMASI ===')
brk = method(view, 'private void Break(')
kinds = ['Kind.Major', 'Kind.Secondary', 'Kind.Micro', 'Kind.Speck']
check('dort debris ailesi ayri ayri uretiliyor', all(k in brk for k in kinds), 'aile eksik')
sp = [style[k] for k in ('MajorFragmentSpeed', 'SecondarySpeed', 'MicroDebrisSpeed', 'HotSpeckSpeed')]
check('hiz sirasi major < secondary < micro < speck (%s)' % sp, sp == sorted(sp) and len(set(sp)) == 4, 'hiz katmani yok')
rot = [style[k] for k in ('MajorFragmentRotation', 'SecondaryRotation', 'MicroDebrisRotation')]
check('donus sirasi major < secondary < micro (%s)' % rot, rot == sorted(rot) and len(set(rot)) == 3, 'donus katmani yok')
check('major boyutu hucrenin %%%d-%d\'i (10-25)' % (style['MajorFragmentSizeMin'] * 100, style['MajorFragmentSizeMax'] * 100),
      0.10 <= style['MajorFragmentSizeMin'] and style['MajorFragmentSizeMax'] <= 0.25, 'major dev')
check('major %d-%d adet (1-3)' % (style['MajorFragmentCountMin'], style['MajorFragmentCountMax']),
      style['MajorFragmentCountMin'] >= 1 and style['MajorFragmentCountMax'] <= 3, 'major sayisi')
shapes = {}
for name in ('MajorShapes', 'SecondaryShapes', 'MicroShapes'):
    block = view[view.index('float[][] %s' % name):]
    block = block[:block.index('};')]
    shapes[name] = block.count('new[]')
check('sekil cesitliligi (major %d, secondary %d, micro %d)' % (shapes['MajorShapes'], shapes['SecondaryShapes'], shapes['MicroShapes']),
      shapes['MajorShapes'] >= 4 and shapes['SecondaryShapes'] >= 3 and shapes['MicroShapes'] >= 4, 'sekil az')
cp = view[view.index('float[][][] CrackPresets'):]
cp = cp[:cp.index('\n        };')]
presets = cp.count('new[]\n') if 'new[]\n' in cp else len(re.findall(r'new\[\]\s*\n\s*\{', cp))
check('catlak deseni >= 3 (%d)' % presets, presets >= 3, 'catlak cesidi az')
check('parcalar kupun KENDI dokusundan (OverrideGeometry)', 'OverrideGeometry(' in view and 'FragmentSprite(look.Tile' in brk,
      'parcalar dokudan kesilmiyor')
check('kesilemezse golgeli yedek sekil', 'MaterialSprite(' in method(view, 'private static Sprite FragmentSprite('), 'yedek yok')
check('major sicak kenar (rim) tasiyor ve sogur', 'RimSprite(' in brk and 'q.Rim' in method(view, 'private void UpdateParticles('),
      'sicak kenar yok')
play = method(view, 'public void Play(')
check('LOD kademeleri (5 / 12 / 20)', 'n <= 5' in play and 'n <= 12' in play and 'n <= 20' in play, 'LOD yok')
check('kup karakteri (buyuk parca / kirinti agirlikli)', 'profile' in brk and 'b.Seed[i] % 3u' in brk, 'her kup ayni')
check('aile butceleri (Max*)', all('Style.Max%s' % n in play for n in ('Major', 'Secondary', 'Micro', 'Specks')), 'butce yok')
check('kademeli salim (secondary/micro/speck gecikmeli)', brk.count('q.Delay =') >= 3, 'tek puskurme')
check('bazi major parcalar yakina duser', 'Style.MajorFallNear' in brk, 'hepsi uzaga gidiyor')
check('dis halka momentumu kisa', 'Style.OuterRingMomentum' in brk, 'momentum farki yok')
check('kup Play karesinde kabuk (bosluk yok)', 'Place(b.Shell[i], cells[i], b.CubeSize' in play, 'kabuk gec cikiyor')
check('kabuk karonun KENDI materyalini giyer (su dalgalanir, ates akar)',
      'Rent(b.Looks[i].Tile, ShellOrder, ViewUtil.TileMaterial(' in play, 'kabuk donuk cizilir')
check('havuzdan gelen renderer materyali sifirlanir', 'r.sharedMaterial = wanted' in method(view, 'private SpriteRenderer Rent('),
      'eski su materyali baska sprite\'a bulasabilir')
check('FlashCells kupun yuzunu soruyor; BoardView sakliyor', 'TryCubeLook(' in flash
      and 'vacated[gp] = new VacatedCube' in method(board, 'public void Refresh('), 'kup yuzu yok')
check('dalga: bekleyen kup one dogru seyirir (ripple)', 'Style.RippleStrength' in method(view, 'private void PaintCells('),
      'ripple yok')
check('isi / pus komsu sayisina bolunur', 'Mathf.Sqrt(b.Light[i])' in brk and 'Mathf.Sqrt(b.Light[i])' in method(view, 'private static void PaintBroken('),
      'buyuk grupta isi corbasi')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
