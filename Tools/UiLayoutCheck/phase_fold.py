# "Soguk katlama" (PhaseFoldView): yapisal kontroller.
#
#   1. Style'da okunmayan ayar yok; brief'in ayar listesi eksiksiz.
#   2. Varyant 2 olarak kayitli, rastgele secimde; yalnizca YOK OLAN hucreler (cold_sink.py de bakar).
#   3. Shader: Resources'ta, 2D renderer'a etiketli, yarikta KIRPIYOR; C#'in verdigi her ozellik
#      shader'da var. Shader yoksa yedek yol plakayi yariga SIKISTIRIR (asla komsuya kaymaz).
#   4. Soguk kuyu ile karismaz: kuyu/maske/asagi dusus yok, rastgele yok (hucre hash'i).
#   5. Yarik lokal ve dar, yonu hucreden; en fazla 3 katman, 1-3 px arayla; plaka dusus sonunda
#      yarigi TAMAMEN gecer.
#   6. Zamanlama 0.65-0.95 s; 7. hata ayiklama anahtarlari, lab ve yasam dongusu.
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


view = strip_comments(read('Assets', 'Scripts', 'View', 'PhaseFoldView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
shader_path = os.path.join(ROOT, 'Assets', 'Resources', 'Shaders', 'PhaseFold.shader')
shader = open(shader_path, encoding='utf-8').read() if os.path.exists(shader_path) else ''

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
dead = [n for n in style if not re.search(r'\bStyle\.%s\b' % n, view[end:])]
print('   %d ayar, %d okunuyor' % (len(style), len(style) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['CompressionDuration', 'CompressionStrength', 'FlattenDuration', 'BevelSuppression',
          'DepthShadowSuppression', 'DesaturationStrength', 'ColdInfluence', 'LayerCount', 'LayerOffset',
          'LayerOpacityFalloff', 'RecompressDuration', 'SeamOpenDuration', 'SeamWidth', 'SeamLength',
          'SeamColdRimStrength', 'TravelDuration', 'TravelEase', 'FlexStrength', 'FlexDelay', 'TrailingEdgeLag',
          'FinalPlateThickness', 'NegativeGhostOpacity', 'NegativeGhostDuration', 'SeamHoldDuration',
          'SeamCloseDuration', 'HairlineResidueStrength', 'HairlineResidueDuration', 'CenterOutDelay',
          'VisualJitter', 'LargeNDetailCompensation']
missing = [w for w in wanted if w not in style]
check('brief\'in %d ayari Style\'da' % len(wanted), not missing, 'eksik ayar: %s' % missing)

print()
print('=== 2. VARYANT ===')
enum = fb[fb.index('enum RemovalVariant'):]
enum = enum[:enum.index('};') + 2]
check('RemovalVariant.PhaseFold kayitli ve rastgele listede',
      'PhaseFold' in enum and 'RemovalVariant.ColdSink, RemovalVariant.PhaseFold' in enum, 'varyant listede yok')
removal = method(fb, 'private bool PlayRemoval(')
check('secilirse phaseFold.Play', 'case RemovalVariant.PhaseFold:' in removal and 'phaseFold.Play(' in removal,
      'varyant oynatilmiyor')

print()
print('=== 3. SHADER ===')
check('PhaseFold.shader Resources/Shaders altinda (build\'e girer)', bool(shader), 'shader yok')
check('2D renderer\'a etiketli (Universal2D)', '"LightMode" = "Universal2D"' in shader, 'Universal2D etiketi yok')
check('yarikta kirpar (clip)', 'clip(' in shader and '_SeamPoint' in shader, 'kirpma yok - yalniz kuculur')
ids = re.findall(r'Shader\.PropertyToID\("(\w+)"\)', view)
missing_props = [p for p in ids if p not in shader]
check('C#\'in verdigi %d ozelligin hepsi shader\'da' % len(ids), ids and not missing_props,
      'shader\'da olmayan ozellik: %s' % missing_props)
check('shader bulunamazsa/desteklenmezse yedek yol', 'shader.isSupported' in view and 'foldMaterial == null' in view,
      'yedek yol yok')
paint = method(view, 'private void PaintFold(')
check('yedek yolda plaka yariga SIKISIR (komsuya kaymaz)', 'Mathf.Min(offset + half, seamInner)' in paint,
      'yedek yolda plaka yarigi gecip komsu hucreye kayar')
check('property block tek ve yeniden kullaniliyor', view.count('new MaterialPropertyBlock()') == 1, 'her karede block')

print()
print('=== 4. SOGUK KUYU ILE KARISMAZ ===')
check('kuyu / maske yok (SpriteMask)', 'SpriteMask ' not in view and 'VisibleInsideMask' not in view, 'maske/kuyu var')
check('asagi dusus yok (Vector2.down hareketi yok)', 'Vector2.down' not in paint, 'plaka asagi dusuyor')
check('rastgele yok (hucre hash\'i)', 'Random.' not in view, 'UnityEngine.Random kullaniliyor')
check('patlama / kamera / zaman yok', not any(w in view for w in ('ShakeCamera', 'Camera', 'timeScale', 'ParticleSystem')),
      'patlama gibi davraniyor')

print()
print('=== 5. YARIK, KATMANLAR, CIKIS ===')
check('yarik lokal: kenarin %%%d\'i (45-70)' % (style['SeamLength'] * 100), 0.45 <= style['SeamLength'] <= 0.70, 'yarik uzun/kisa')
px = style['SeamWidth'] * 64
check('yarik dar: 64px hucrede %.1f px (1-3)' % px, 1.0 <= px <= 3.0, 'yarik genis')
check('yarik yonu hucreden (4 kenar)', '(seed >> 5) & 3u' in method(view, 'public void Play('), 'yon hucreden degil')
check('en fazla 3 katman', 2 <= style['LayerCount'] <= 3, 'katman sayisi')
lpx = style['LayerOffset'] * 64
check('katmanlar %.1f px arayla (1-3)' % lpx, 1.0 <= lpx <= 3.2, 'katman araligi')
check('plaka dusus sonunda yarigi tamamen gecer', 'offset = (seamInner + halfEnd + 0.02f * cell) * e' in paint,
      'plaka yarikta takili kalir')
check('bukulme: onde cekme + arkada gecikme + daralma', '_Lead' in shader and '_Trail' in shader and '_Pinch' in shader
      and 'Style.TrailingEdgeLag' in paint, 'bukulme yok')
check('negatif iz merkezden disa silinir', '_Dissolve' in shader and 'Smooth(k)' in method(view, 'private void PaintGhost('),
      'iz silinmiyor')

print()
print('=== 6. ZAMANLAMA ===')
cd, fd, rd = style['CompressionDuration'], style['FlattenDuration'], style['RecompressDuration']
fs = cd * 0.6
fe = fs + fd
rs = fe - rd * 0.4
re_ = rs + rd
ts = re_ + 0.02
te = ts + style['TravelDuration'] * 1.05
ge = te - 0.06 + style['NegativeGhostDuration']
sce = te + style['SeamHoldDuration'] + style['SeamCloseDuration'] * 1.05
he = sce + style['HairlineResidueDuration']
total = max(he, ge)
check('toplam %.3f s (0.65-0.95)' % total, 0.65 <= total <= 0.95, 'sure brief disi')
check('yarik acilisi %.0f ms (60-120)' % (style['SeamOpenDuration'] * 1000), 0.06 <= style['SeamOpenDuration'] <= 0.12, 'acilis')
check('yarik bekler %.0f ms (60-120)' % (style['SeamHoldDuration'] * 1000), 0.06 <= style['SeamHoldDuration'] <= 0.12, 'bekleme')
check('negatif iz %.0f ms (100-250)' % (style['NegativeGhostDuration'] * 1000), 0.10 <= style['NegativeGhostDuration'] <= 0.25, 'iz')
check('kil cizgi %.0f ms (50-150)' % (style['HairlineResidueDuration'] * 1000), 0.05 <= style['HairlineResidueDuration'] <= 0.15, 'kil cizgi')
check('hucre gecikmesi %.0f ms/hucre (20-45)' % (style['CenterOutDelay'] * 1000), 0.02 <= style['CenterOutDelay'] <= 0.045, 'dalga')

print()
print('=== 7. HATA AYIKLAMA, LAB, YASAM DONGUSU ===')
layers = view[view.index('public static class Layers'):]
check('5 hata ayiklama anahtari', all(k in layers for k in ('ShowCompressionMarks', 'ShowLayerSplit', 'ShowFlex',
                                                         'ShowNegativeGhost', 'ShowSeamMask')), 'anahtar eksik')
labels = ['kaldırılan hücreler 2: soğuk katlama (hücre ayarı)', 'kaldırılan hücreler 2: soğuk katlama (element blokları)',
          'sıkıştırma izleri aç/kapa', 'katman ayrışması aç/kapa', 'bükülme aç/kapa', 'negatif iz aç/kapa',
          'yarık maskesi aç/kapa']
check('lab girisleri', all(w in lab_raw for w in labels), 'labda eksik: %s' % [w for w in labels if w not in lab_raw])
check('lab kapaninca anahtarlar geri acilir', 'PhaseFoldView.Layers.AllOn()' in method(lab, 'private void CloseAnimationLab('),
      'anahtarlar kapali kalabilir')
views = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Views.cs'))
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('view kuruluyor, menude gizleniyor, kosu bitince duruyor', 'AddComponent<PhaseFoldView>()' in views
      and 'phaseFold.gameObject.SetActive(visible)' in menus and 'phaseFold.Stop()' in menus, 'yasam dongusu eksik')
check('csproj dosyayi iceriyor', 'PhaseFoldView.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
