# "Soguk sublimlesme" (CryoSublimationView): yapisal kontroller.
#
#   1. Style'da okunmayan ayar yok; brief'in ayar listesi eksiksiz.
#   2. Varyant 3 olarak kayitli, rastgele secimde; yalnizca YOK OLAN hucreler.
#   3. Shader'lar: Resources'ta, 2D renderer'a etiketli; maskeler dokudan, asinma ALFAYI keser
#      (yalniz solma degil); C#'in verdigi her ozellik shader'da; shader yoksa yedek yol.
#   4. Diger iki varyantla karismaz: kuyu/maske/yarik yok, kup yerinde kalir, kucultulmez, asagi
#      dusmez; parcaciklar yukari (toz iceri) gider; patlama/kamera/zaman/rastgele yok.
#   5. Maskeler: 5 buz + 5 asinma varyanti, siralamayla (esit hizda) kurulu, dogrusal+bilinear,
#      bir kez uretilir; buhar sayilari brief'in araliklarinda; 20 ustunde yalniz secili kuplerde.
#   6. Renk: buz beyaz degil, neon degil; toz beyaz degil.
#   7. Zamanlama brief'in pencerelerinde; 8. hata ayiklama, lab ve yasam dongusu.
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
    print('   %-66s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


def maybe(path):
    p = os.path.join(ROOT, *path)
    return open(p, encoding='utf-8').read() if os.path.exists(p) else ''


view = strip_comments(read('Assets', 'Scripts', 'View', 'CryoSublimationView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
shader = maybe(('Assets', 'Resources', 'Shaders', 'CryoSublimation.shader'))
vapour = maybe(('Assets', 'Resources', 'Shaders', 'CryoVapour.shader'))

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
dead = [n for n in style if not re.search(r'\bStyle\.%s\b' % n, view[end:])]
print('   %d ayar, %d okunuyor' % (len(style), len(style) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['ThermalDrainDuration', 'ThermalDrainStrength', 'FrostDuration', 'FrostAmount', 'FrostMaskVariationCount',
          'FrostEdgeSoftness', 'OriginalColorRetention', 'LastWarmCoreDuration', 'LastWarmCoreSize',
          'SublimationDuration', 'ErosionSpeed', 'ErosionSoftness', 'ErosionVariationCount', 'RibbonCountMin',
          'RibbonCountMax', 'RibbonWidth', 'RibbonLength', 'RibbonRiseSpeed', 'RibbonLateralDrift', 'RibbonLifetime',
          'RibbonOpacity', 'WispCount', 'WispLifetime', 'WispSpeed', 'MoteCount', 'MoteSpeed', 'MoteLifetime',
          'ShellOpacity', 'ShellHoldDuration', 'ShellCollapseDuration', 'ShellCollapseAmount', 'ResidueStrength',
          'ResidueDuration', 'CenterOutDelay', 'TimingJitter', 'LargeNDetailCompensation']
missing = [w for w in wanted if w not in style]
check('brief\'in %d ayari Style\'da' % len(wanted), not missing, 'eksik ayar: %s' % missing)

print()
print('=== 2. VARYANT ===')
enum = fb[fb.index('enum RemovalVariant'):]
enum = enum[:enum.index('};') + 2]
check('RemovalVariant.CryoSublimation kayitli ve rastgele listede',
      'CryoSublimation' in enum and 'RemovalVariant.PhaseFold, RemovalVariant.CryoSublimation' in enum,
      'varyant listede yok')
removal = method(fb, 'private bool PlayRemoval(')
check('secilirse cryoSublimation.Play', 'case RemovalVariant.CryoSublimation:' in removal
      and 'cryoSublimation.Play(' in removal, 'varyant oynatilmiyor')
check('secim her kaldirmada rastgele', 'Random.Range(0, RemovalVariants.Length)' in removal, 'rastgele secim yok')
emit = method(fb, 'private void EmitBlastParticles(')
check('yalnizca YOK OLANLAR (tasinan/donusen eski izde)', 'LiftKindAt(i) == LiftKind.Removed' in emit
      and 'PlayRemoval(removed)' in emit and 'LiftCells(marked' in emit, 'ayrim yok')
check('kupun kendi yuzu (TryCubeLook) - vekil kup', 'TryCubeLook(' in removal, 'kup yuzu yok')

print()
print('=== 3. SHADER ===')
check('CryoSublimation.shader Resources/Shaders altinda', bool(shader), 'shader yok')
check('CryoVapour.shader Resources/Shaders altinda', bool(vapour), 'buhar shader\'i yok')
check('ikisi de 2D renderer\'a etiketli (Universal2D)', '"LightMode" = "Universal2D"' in shader
      and '"LightMode" = "Universal2D"' in vapour, 'Universal2D etiketi yok')
check('buz ve asinma dokudan (_MaskTex, R ve G)', '_MaskTex' in shader and ').r;' in shader and ').g;' in shader,
      'maske yok')
check('asinma KUTLEYI keser (mass = texel.a * (1.0 - gone))', 'float mass = texel.a * (1.0 - gone);' in shader,
      'asinma yalniz renk - kup solar')
check('kabuk ve pus yalniz kutlenin gittigi yerde', 'float empty = texel.a * gone * saturate(haze + shell);' in shader,
      'kabuk kupun ustunde')
check('maske yonu hucreden (buz 8 donus, asinma ayna)', '(seed >> 17) & 7u' in view and '(seed >> 21) & 1u' in view
      and '_FrostAxes' in shader and '_ErodeFlip' in shader, 'her hucrede ayni desen')
check('kabuk ice cokerken kenarlar iceri (_Inset)', '_Inset.x * q.x' in shader and '_Inset.w * q.y' in shader,
      'cokme yok')
ids = re.findall(r'Shader\.PropertyToID\("(\w+)"\)', view)
missing_props = [p for p in ids if p not in shader]
check('C#\'in verdigi %d ozelligin hepsi shader\'da' % len(ids), ids and not missing_props,
      'shader\'da olmayan ozellik: %s' % missing_props)
check('shader bulunamazsa/desteklenmezse yedek yol', 'shader.isSupported' in view and 'cryoMaterial == null' in view,
      'yedek yol yok')
check('buhar shader\'i olmazsa serit yok (cokmez)', 'if (vapourMaterial == null)' in method(view, 'private void AddVapour('),
      'buhar shader\'i yoksa null materyal')
check('property block tek ve yeniden kullaniliyor', view.count('new MaterialPropertyBlock()') == 1, 'her karede block')

print()
print('=== 4. DIGER VARYANTLARLA KARISMAZ ===')
check('kuyu / maske / yarik yok', not any(w in view for w in ('SpriteMask ', 'VisibleInsideMask', '_Seam', 'Clip')),
      'kuyu/yarik dili')
paint = method(view, 'private void PaintCube(')
check('kup yerinde kalir (hep c.At\'ta cizilir)', paint.count('Place(c.Body, c.At, size,') == 2
      and 'c.At +' not in paint.split('c.Shadow')[0], 'kup yerinden oynuyor')
check('kup kucultulmez (scale-to-zero yok)', 'Place(c.Body, c.At, size *' not in paint
      and 'Place(c.Body, c.At, size * (' not in paint, 'kup kuculuyor')
fl = method(view, 'private void AddFlecks(')
check('parcaciklar yukari: hiz y bileseni pozitif', 'Style.MoteSpeed * dice.Range(0.7f, 1.1f)' in fl
      and 'Vector2.up * rise' in fl, 'parcacik yukari gitmiyor')
check('son toz ICERI (disari patlamaz)', '-f.From.normalized * inward' in fl, 'toz disari gidiyor')
check('patlama / kamera / zaman / partikul sistemi yok',
      not any(w in view for w in ('ShakeCamera', 'Camera', 'timeScale', 'ParticleSystem', 'Explode')),
      'patlama gibi davraniyor')
check('rastgele yok (hucre hash\'i)', 'Random.' not in view, 'UnityEngine.Random kullaniliyor')
check('additive/parlama yok (buhar alfa karisimi)', 'Blend One One' not in vapour and 'Blend SrcAlpha One\n' not in vapour
      and 'Blend SrcAlpha OneMinusSrcAlpha' in vapour, 'buhar parliyor')

print()
print('=== 5. MASKELER VE BUHAR ===')


def presets(name):
    blk = view[view.index('float[][] %s' % name):]
    blk = blk[:blk.index('};')]
    return [[float(x) for x in re.findall(r'-?\d+(?:\.\d+)?(?=f)', b)] for b in re.findall(r'new\[\] \{([^}]*)\}', blk)]


cores, patches, lobes = presets('CoreShapes'), presets('FrostPatches'), presets('ErosionLobes')
check('5 cekirdek, 5 buz maskesi, 5 asinma maskesi', len(cores) == len(patches) == len(lobes) == 5, 'varyant sayisi')
check('her buz maskesinde 3-5 yama', all(3 <= len(p) // 4 <= 5 for p in patches), 'yama sayisi')
check('her asinma maskesinde 2-3 lob (2-4 koken)', all(2 <= len(l) // 4 <= 3 for l in lobes), 'lob sayisi')
check('cekirdekler ayni yerde degil', len(set((c[0], c[1]) for c in cores)) == 5, 'cekirdek tekrar')
check('maskeler siralamayla (esit hizli cephe)', 'Equalise(frost)' in view and 'Equalise(leave)' in view, 'siralama yok')
atlas = method(view, 'private static Texture2D MaskAtlas(')
check('doku dogrusal, bilinear, bir kez', 'TextureFormat.RGBA32, false, true' in atlas and 'FilterMode.Bilinear' in atlas
      and 'if (maskAtlas != null)' in atlas, 'doku pikselli/sRGB/her karede')
check('karede doku/mesh uretimi yok', 'new Texture2D' not in method(view, 'private void Update(')
      and 'new Mesh' not in method(view, 'private void BuildStrip('), 'karede uretim')
check('serit 1-3', 1 <= style['RibbonCountMin'] <= style['RibbonCountMax'] <= 3, 'serit sayisi')
check('ikincil sis 2-5', 2 <= style['WispCount'] <= 5, 'sis sayisi')
check('mikro buz tozu 4-10', 4 <= style['MoteCount'] <= 10, 'toz sayisi')
check('son toz 4-8', 4 <= style['FinalDustCount'] <= 8, 'son toz')
lo, hi = style['RibbonLength'] * 0.7, style['RibbonLength'] * 1.15
check('serit boyu hucrenin %.2f-%.2f kati (0.5-1.4)' % (lo, hi), lo >= 0.5 and hi <= 1.4, 'serit boyu')
check('serit kupun kucuk bir kismi (kok genisligi %.2f hucre <= 0.2)' % (style['RibbonWidth'] * 1.15),
      style['RibbonWidth'] * 1.15 <= 0.2, 'serit kupu kapliyor')
av = method(view, 'private void AddVapour(')
check('20 ustu: yalniz secili kuplerde tek serit', 'ribbons = hero ? Mathf.Min(1, max) : 0;' in av, 'LOD yok')
check('seritler bir onde bir arkada', 'r % 2 == 0 ? FrontVapourOrder : BackVapourOrder' in av, 'derinlik yok')
orders = dict((n, int(v)) for n, v in re.findall(r'private const int (\w+) = (-?\d+);', view))
check('arka serit < kup < on serit', orders['BackVapourOrder'] < orders['CubeOrder'] < orders['FrontVapourOrder'],
      'siralama karisik')

print()
print('=== 6. RENK ===')
cols = dict((n, tuple(float(x) for x in v.split(','))) for n, v in
            re.findall(r'private static readonly Color (\w+) = new Color\(([^)]*)\)', view.replace('f', '')))
for name in ('FrostColour', 'VapourColour', 'DustColour', 'SlateColour'):
    h, s, v = colorsys.rgb_to_hsv(*cols[name][:3])
    check('%s beyaz degil (max %.2f < 0.9), neon degil (doygunluk %.2f < 0.3)' % (name, max(cols[name][:3]), s),
          max(cols[name][:3]) < 0.9 and s < 0.3, '%s beyaz/neon' % name)
check('kabuk ici saf siyah degil', min(cols['HollowColour'][:3]) >= 0.03, 'saf siyah')

print()
print('=== 7. ZAMANLAMA ===')


def timeline(var):
    v = 1 + var
    fs = style['ThermalDrainDuration'] * 0.6
    core_at = fs + style['FrostDuration'] * v
    dead = core_at + style['LastWarmCoreDuration']
    ss = dead - 0.04
    se = ss + style['SublimationDuration'] * v
    he = se + style['ShellHoldDuration']
    ce = he + style['ShellCollapseDuration']
    return dict(fs=fs, core_at=core_at, dead=dead, ss=ss, se=se, he=he, ce=ce, re=ce + style['ResidueDuration'])


T = timeline(0.05)
ends = [T['re'], T['he'] + style['ShellCollapseDuration'] * 0.25 + 0.04 + 0.24,
        T['ss'] + 0.01 + 2 * 0.045 + 0.02 + style['RibbonLifetime'] * 1.15,
        T['ss'] + 0.05 + 0.5 * style['SublimationDuration'] + style['WispLifetime'] * 1.2,
        T['ss'] + 0.02 + style['SublimationDuration'] * 0.45 + style['MoteLifetime'] * 1.15]
total = max(ends)
T0 = timeline(0)
check('tek hucre toplam %.3f s (0.7-1.0, en uzun hucre)' % total, 0.7 <= total <= 1.0, 'sure brief disi')
check('isi cekilmesi %.0f ms (60-140)' % (style['ThermalDrainDuration'] * 1000), 0.06 <= style['ThermalDrainDuration'] <= 0.14,
      'isi cekilmesi')
check('buz %.2f-%.2f s, cekirdek olur %.2f-%.2f s (brief 0.08-0.36)' % (T0['fs'], T0['core_at'], T0['core_at'], T0['dead']),
      T0['fs'] <= 0.10 and 0.24 <= T0['dead'] <= 0.40, 'buz penceresi')
check('suplimlesme %.2f-%.2f s (brief 0.30-0.60)' % (T0['ss'], T0['se']), 0.25 <= T0['ss'] <= 0.36 and 0.52 <= T0['se'] <= 0.66,
      'suplimlesme penceresi')
check('kabuk bekler %.0f ms (60-140)' % (style['ShellHoldDuration'] * 1000), 0.06 <= style['ShellHoldDuration'] <= 0.14, 'kabuk')
check('cokme 1-3 px (%.1f px, 64 px hucre)' % (style['ShellCollapseAmount'] * 64), 1 <= style['ShellCollapseAmount'] * 64 <= 3,
      'cokme miktari')
check('kalinti %.0f ms (100-250)' % (style['ResidueDuration'] * 1000), 0.10 <= style['ResidueDuration'] <= 0.25, 'kalinti')
check('hucre gecikmesi %.0f ms/hucre (20-45)' % (style['CenterOutDelay'] * 1000), 0.02 <= style['CenterOutDelay'] <= 0.045,
      'dalga')

print()
print('=== 8. HATA AYIKLAMA, LAB, YASAM DONGUSU ===')
layers = view[view.index('public static class Layers'):]
keys = ('ShowThermalDrain', 'ShowFrostMask', 'ShowLastColorCore', 'ShowSublimationErosion', 'ShowVaporRibbons',
        'ShowFrostShell', 'ShowFinalDust', 'ShowColdResidue')
check('8 hata ayiklama anahtari, hepsi okunuyor', all(k in layers and view.count('Layers.' + k) >= 1 for k in keys),
      'anahtar eksik/okunmuyor')
labels = ['kaldırılan hücreler 3: soğuk süblimleşme (hücre ayarı)', 'kaldırılan hücreler 3: soğuk süblimleşme (element blokları)',
          'ısı çekilmesi aç/kapa', 'buz maskesi aç/kapa', 'son renk çekirdeği aç/kapa', 'kütle aşınması aç/kapa',
          'buhar şeritleri aç/kapa', 'buz kabuğu aç/kapa', 'buz tozu aç/kapa', 'soğuk iz aç/kapa']
check('lab girisleri', all(w in lab_raw for w in labels), 'labda eksik: %s' % [w for w in labels if w not in lab_raw])
check('lab kapaninca anahtarlar geri acilir', 'CryoSublimationView.Layers.AllOn()' in method(lab, 'private void CloseAnimationLab('),
      'anahtarlar kapali kalabilir')
views = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Views.cs'))
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('view kuruluyor, menude gizleniyor, kosu bitince duruyor', 'AddComponent<CryoSublimationView>()' in views
      and 'cryoSublimation.gameObject.SetActive(visible)' in menus and 'cryoSublimation.Stop()' in menus, 'yasam dongusu eksik')
check('csproj dosyayi iceriyor', 'CryoSublimationView.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
