# YILAN YENILGISI: 30 KABUL KRITERI, YAPISAL OLARAK
#
# Bu animasyonun tek bir cumlesi var ve o cumle "yilan renkli patladi" DEGIL: yilanin raunt
# boyunca yuttugu renkler artik serbest kaliyor. Asagidaki kontroller o cumleyi ve etrafindaki
# yasaklari kodun icinde tutuyor - cunku bu efektin bozulma yolu tek tek parcalarin kaybolmasi
# degil, sessizce "scale-to-zero + birkac particle" haline ya da her raunt ayni gokkusagina
# geri donmesi.

import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
problems = []


def read(*parts):
    with open(os.path.join(ROOT, *parts), encoding='utf-8') as f:
        return f.read()


def strip_comments(src):
    out = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return re.sub(r'^\s*(//|///).*$', '', out, flags=re.M)


def check(label, ok, why=''):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        problems.append(why or label)


def method(src, signature):
    i = src.index(signature)
    depth = 0
    j = src.index('{', i)
    for k in range(j, len(src)):
        if src[k] == '{':
            depth += 1
        elif src[k] == '}':
            depth -= 1
            if depth == 0:
                return src[i:k + 1]
    return src[i:]


raw = read('Assets', 'Scripts', 'View', 'SnakeDefeatView.cs')
dv = strip_comments(raw)
view = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeView.cs'))
eat = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeEatView.cs'))
skin = read('Assets', 'Resources', 'Shaders', 'SnakeSkin.shader')
strand = read('Assets', 'Resources', 'Shaders', 'SnakeFilament.shader')
vfx = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeVfxController.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs'))
style = dict((n, float(v)) for n, v in
             re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', dv))

print('=== 1. AYRI BIR FINAL, VE YILAN BIR ANDA KAYBOLMUYOR (1-3) ===')
check('1 normal kuyruk kesiginden ayri bir sistem',
      'public sealed class SnakeDefeatView' in dv
      and 'Length = SnakeDefeatView.Style.Total' in view, 'yenilgi kesikle ayni')
check('2 govde bir anda yok olmuyor: kendi zaman cizelgesi var',
      style['Total'] if 'Total' in style else True, 'toplam yok')
check('2b govde LOOSE parca olarak firlatilmiyor (silinme okumasi)',
      'Release(a.From[i]' not in view, 'segmentler firlatiliyor')
# BU YUZDEN LABDA HICBIR RENK CIKMIYORDU: son parca da siradan bir kesik olarak alininca yenilgi
# vuruşu BOS bir govdeyle basliyor - gerilecek parca, renklerin gorunecegi yuzey ve acilacak bir
# merkez yok - ve butun blok asla dogru olamayacak bir `a.From.Count > 0` korumasinin arkasinda
# kaliyordu. Son parca artik kesilmiyor: bossun kendi finali onu aliyor.
check('2c yenilgi vuruşu BOS govdeyle baslamiyor (son parca kesilmiyor)',
      'if (turn.Defeated && cuts > 0)' in view
      and 'for (int i = 0; i < cuts; i++)' in view
      and 'CopyInto(end.From, standing.Count > 0 ? standing : turn.BodyBefore);' in view
      and 'CopyInto(body, a.From);' in method(view, 'private void OnActStarted(Act a)'),
      'yenilgi bos govdeyle basliyor - hicbir sey cizilmez')
check('3 final tension okunuyor (gerilme + %2-4)',
      'public static float Tension(float t)' in dv
      and 0.02 <= style['FinalTensionStrength'] <= 0.05
      and 'cross += SnakeDefeatView.Tension(dt2);' in view, 'gerilme yok')

print()
print('=== 2. RENKLER RAUNTTAN GELIYOR (4-6) ===')
check('4 yenen blok renkleri presentation HISTORY olarak tutuluyor',
      'public static void Remember(Color material' in dv
      and 'SnakeDefeatView.Remember(material, energy, hotCore);' in eat, 'history yok')
check('4b history GAMEPLAY degil: sadece View, raunt basinda temizleniyor',
      'public static void ForgetHistory()' in dv
      and 'SnakeDefeatView.ForgetHistory();' in fb
      and 'Remember' not in read('Assets', 'Scripts', 'Core', 'Bosses', 'Definitions',
                                 'SnakeBoss.cs'), 'history gameplay tarafinda')
check('5 palet raunda gore degisiyor (benzer renkler tek family, baskin onde)',
      'public static void Palette(List<Color> into' in dv
      and 'private static bool Near(Color a, Color b)' in dv
      and 'counts[j] > counts[j - 1]' in dv, 'palet sabit')
check('5b en fazla %d renk' % int(style['MaxPaletteColors']),
      3 <= style['MaxPaletteColors'] <= 6, 'palet siniri')
check('6 SABIT gokkusagi yok: hicbir yerde elle yazilmis 6 renk dizisi',
      'Color.red' not in dv and 'Color.magenta' not in dv
      and 'rainbow' not in dv.lower() and 'HSVToRGB' not in dv, 'gokkusagi')
check('6b hic yemediyse fallback KONTROLLU (rastgele RGB degil)',
      'into.Add(new Color(0.24f, 0.78f, 0.62f));' in dv and 'Random' not in dv,
      'rastgele fallback')

print()
print('=== 3. RENKLER YUZEY ALTINDA (7-8) ===')
check('7 renk cepleri DERININ ALTINDAN geliyor (shader), sticker degil',
      '_Pocket0' in skin and 'c = lerp(c, c * mixed * 2.0, show * 0.85);' in skin
      and 'pose.Pocket0 = c;' in view, 'renk sticker gibi ustte')
check('7b cepler birbirine karisip camur olmuyor (en gucli piksel hue\'yu aliyor)',
      'float3 mixed = pocket / pocketWeight;' in skin, 'renkler camur')
check('7c yilanin kendi rengi SONUYOR ama griye donmuyor',
      'public static float Drain(float t)' in dv and style['BaseColorDrain'] <= 0.75
      and 'drain = Mathf.Max(drain, SnakeDefeatView.Drain(dt2));' in view, 'ceset grisi')
check('8 renkler MERKEZE akiyor',
      'Vector2.Lerp(from, Vector2.zero, gathered)' in view
      and 'ColorFlowDuration' in dv, 'akis yok')

print()
print('=== 4. COKUS VE YUMAK (9-12, 17) ===')
col = method(dv, 'public static float Collapse(float t, int index, int count)')
check('9 cokus UNIFORM scale-down DEGIL (uclar once)',
      'CollapseAsymmetry' in col and 'fromMiddle' in col, 'uniform scale')
check('9b her segment SONUNDA sifira ULASIYOR',
      '(k - start) / Mathf.Max(1f - start, 1e-3f)' in col, 'segment hic kaybolmuyor')
check('10 govde kuculdukce renk yogunlasiyor (yumak sikisiyor)',
      'Mathf.Lerp(1.35f, 0.85f, Smooth(k))' in dv, 'yogunlasma yok')
check('11 yumak birkac rengin sarilmis hali (tek duz nokta degil)',
      'int strands = Mathf.Min(palette.Count, 4);' in dv, 'tek nokta')
check('12 yumak magic orb degil: kusursuz kure yok',
      'r * 0.6f, r * 0.44f' in dv and 'orb' not in dv.lower(), 'magic orb')
check('17/24 golge kutleyle birlikte gidiyor',
      'public static float ShadowLeft(float t)' in dv
      and 'pose.ShadowFade = SnakeDefeatView.ShadowLeft(act.Clock);' in view
      and 'pose.ShadowFade > 0f ? pose.ShadowFade : 1f' in vfx, 'golge kaliyor')

print()
print('=== 5. HUZMELER (13-18) ===')
build = method(dv, 'private void Build(Ribbon r, float k)')
check('13 yumak PATLAMIYOR, aciliyor (hüzmeler)',
      'private void Release()' in dv and 'RibbonCountMin' in dv, 'patlama')
check('14 hüzmeler KIVRIMLI (bezier), duz beam degil',
      'private static Vector2 Bezier(' in build or 'Bezier(centre, bow, tip' in build,
      'duz beam')
check('14b her biri kendi yayinda (radial burst degil)',
      'r.Bow' in build and 'dice.Range(-0.35f, 0.35f)' in dv, 'radial burst')
check('15 tapered: kokte orta, %20de en genis, uçta sifir',
      'Mathf.Lerp(0.75f, 1.15f, s / 0.2f)' in build
      and 'Mathf.Lerp(1.15f, 0f' in build, 'sabit kalinlik')
check('16 her hüzme kendi renk kimligini koruyor (beyaza gitmiyor)',
      'Color.Lerp(r.Colour, r.Core' in build
      and 'input.color.rgb * 1.22' in strand, 'renkler beyaza karisiyor')
check('17 lazer degil: cekirdek + govde + omuz hacmi',
      'float core = pow(saturate(1.0 - x / 0.36), 2.0);' in strand
      and 'LineRenderer' not in dv, 'lazer')
check('18 mesafe kontrollu (%.1f-%.1f hucre)'
      % (style['RibbonLengthMin'], style['RibbonLengthMax']),
      0.8 <= style['RibbonLengthMin'] and style['RibbonLengthMax'] <= 2.4, 'mesafe')
check('19b akis TEK YONLU, tekrar eden UV kaydirma yok',
      'RibbonFlowStrength' in build and '_Time' not in strand, 'uv kaydiran neon')
check('26/27 hero hüzme + ince iplikler',
      'public bool Hero;' in dv and 'SecondaryThreadCount' in dv, 'hiyerarsi yok')

print()
print('=== 6. MOTELER (19-20, 31-35) ===')
mo = method(dv, 'private void PaintMotes(float dt)')
check('19 moteler HUZME UÇLARINDAN cozuluyor (merkezden patlamiyor)',
      'ribbons[(int)(dice.Next() * ribbons.Count) % ribbons.Count]' in mo
      and 'from.Length * dice.Range(0.72f, 1f)' in mo, 'merkezden konfeti')
check('20 kare konfeti yok: uc aile (yuvarlak / fleck / sliver)',
      'm.Shape == 2 ? 1.7f' in mo and 'm.Shape == 1 ? 0.62f' in mo, 'kare konfeti')
check('35 mote rengi hüzmesinin rengi',
      'Colour = c' in mo and 'from.Colour' in mo, 'hepsi teal')
check('34 hüzme hizini tasiyip yavasliyor',
      'Style.MoteDrag' in mo and 'm.Velocity *= 1f /' in mo, 'surtunme yok')
check('32 butce: %d mote' % int(style['MoteCount']),
      12 <= style['MoteCount'] <= 24, 'mote butcesi')

print()
print('=== 7. TAHTA (21-23, 37-44, 58) ===')
wave = method(dv, 'private void PaintWave()')
check('21 tahta dalgasi COK HAFIF',
      style['BoardWaveStrength'] <= 0.09 and style['CellReflectionStrength'] <= 0.09,
      'dalga fazla guclu')
check('22 hücre KENARI yakalıyor, hücre dolmuyor',
      'private static Sprite CellRim()' in dv and 'Rent(CellRim(), BoardOrder)' in wave,
      'hücre doluyor')
check('40 her hücre farkli renk ALMIYOR (cogunlukla baskin renk)',
      'pick % 4 == 0' in wave and 'palette[0]' in wave, 'gokkusagi tahta')
check('58 tahtanin kendi hücreleri HIC degistirilmiyor (geri alinacak state yok)',
      'SetRotWash' not in dv and 'PaintCellState' not in dv
      and 'boardView' not in dv, 'tahta state degisiyor')
check('44 dalga suresi %.0f ms (250-450)' % (style['BoardWaveDuration'] * 1000),
      0.25 <= style['BoardWaveDuration'] <= 0.45, 'dalga suresi')
check('45 son parilti kisa (%.0f ms)' % (style['FinalGlintDuration'] * 1000),
      style['FinalGlintDuration'] <= 0.09, 'parilti uzun')

print()
print('=== 8. YASAKLAR (25, 26, 38, 88) ===')
forbidden = ('ShakeCamera', 'Camera', 'timeScale', 'ParticleSystem', 'TrailRenderer',
             'LineRenderer', 'Explode', 'Flash', 'Confetti', 'Smoke', 'Portal', 'Firework',
             'System.Linq', 'Random.')
check('25/26 ekran flashi, sarsinti, kamera, parcacik sistemi, LINQ yok',
      not any(w in dv for w in forbidden), 'yasak var')
check('38 peak BEYAZLA degil renkle: hicbir yerde beyaza patlama yok',
      'Color.white' not in dv and '1f, 1f, 1f' not in dv, 'beyaz patlama')
check('36 genel kaldirma efektlerinden hicbiri kullanilmiyor',
      not any(w in dv for w in ('PlayRemoval', 'ColdSink', 'PhaseFold', 'Cryo', 'CellFlashFx',
                                'ClusterBurstView.Play', 'DynamiteBlast')), 'kaldirma efekti')
check('86 havuzlu renderer/strip, paylasilan materyal, karede tahsis yok',
      'private SpriteRenderer Rent(' in dv and 'private Strip RentStrip(' in dv
      and 'spareStrips.Push(s);' in dv, 'havuz yok')
# MAGENTA TUZAGI: SpriteRenderer'a null materyal vermek onu varsayilana dondurmez, Unity o
# renderer'i magenta hata materyaliyle cizer ve tint'i yok sayar. Bu tuzak isirmada bir kez
# yakalandi; burada da olamaz.
check('hicbir renderer materyalsiz BIRAKILMIYOR (magenta hata materyali)',
      'r.sharedMaterial = null' not in dv and 'plainMaterial = r.sharedMaterial;' in dv,
      'materyalsiz renderer = magenta')

print()
print('=== 9. SES KANCALARI ===')
wanted = ['Tension', 'ColorsAwaken', 'ColorKnot', 'RibbonRelease', 'BoardWave', 'Complete']
missing = [w for w in wanted if 'Beat.' + w not in dv]
check("brief'in 6 ses kancasi tanimli ve tetikleniyor",
      not missing and 'Sounded(beat, centre)' in dv, 'eksik: %s' % missing)

print()
print('=== 10. ZAMAN CIZELGESI VE AYARLAR ===')
check('toplam %.0f ms (900-1200)' % (style['Total'] if 'Total' in style else
                                     max(style['FinalGlintStart'] + style['FinalGlintDuration'],
                                         style['BoardWaveStart'] + style['BoardWaveDuration'])
                                     * 1000),
      0.9 <= max(style['FinalGlintStart'] + style['FinalGlintDuration'],
                 max(style['RibbonStart'] + style['RibbonDuration'] + style['MoteLifetime'],
                     style['BoardWaveStart'] + style['BoardWaveDuration'])) <= 1.2,
      'sure')
check('peak yumagin ACILDIGI an (cene/tahta degil)',
      style['RibbonStart'] > style['KnotStart']
      and style['RibbonStart'] < style['BoardWaveStart'], 'peak yerinde degil')
check('fazlar ORTUSUYOR (ayri klipler degil)',
      style['ColorPocketStart'] < style['FinalTensionDuration']
      and style['ColorFlowStart'] < style['ColorPocketStart'] + style['ColorPocketDuration']
      and style['KnotStart'] < style['CollapseStart'] + style['CollapseDuration'],
      'fazlar sirayla')
names = sorted(style)
dead = [n for n in names if len(re.findall(r'\b' + n + r'\b', dv)) < 2
        and ('Style.' + n) not in view]
print('   %d ayar, %d okunuyor' % (len(names), len(names) - len(dead)))
check('her ayar gercekten okunuyor', not dead, 'okunmayan: %s' % ', '.join(dead))
wanted_knobs = [
    'FinalTensionDuration', 'FinalTensionStrength', 'ColorPocketStrength',
    'ColorPocketSoftness', 'ColorPocketCount', 'ColorFlowDuration', 'ColorFlowSpeed',
    'CollapseDuration', 'CollapseAsymmetry', 'BaseColorDrain', 'KnotSize', 'KnotIntensity',
    'KnotHold', 'RibbonCountMin', 'RibbonCountMax', 'RibbonLengthMin', 'RibbonLengthMax',
    'RibbonWidth', 'RibbonCurveStrength', 'RibbonDuration', 'RibbonFlowStrength',
    'SecondaryThreadCount', 'MoteCount', 'MoteSpeed', 'MoteLifetime', 'MoteDrag',
    'BoardWaveRadius', 'BoardWaveDuration', 'BoardWaveCellDelay', 'BoardWaveStrength',
    'CellReflectionStrength', 'FinalGlintStrength', 'FinalGlintDuration',
    'ShadowCollapseDuration', 'MaxPaletteColors']
absent = [w for w in wanted_knobs if w not in style]
check("brief'in %d ayari tanimli" % len(wanted_knobs), not absent, 'eksik: %s' % absent)

print()
print('=== 11. LAB ===')
check('yenilgi icin ayri bolum',
      'yılan: yenilgi - çalınan renklerin serbest kalması' in lab, 'lab bolumu yok')
check('palet on ayarlari elle verilebiliyor',
      'public static void OverrideHistory(' in dv and 'AnimSnakeDefeat(' in lab,
      'palet override yok')
for name in ('kırmızı + mavi + altın', 'mor + camgöbeği + yeşil',
             'altın + obsidyen + kırmızı', 'altı renk'):
    check('preset: %s' % name, name in lab, 'preset yok: %s' % name)
check('hic yememis hali de test edilebiliyor', 'new Color[0]' in lab, 'bos palet testi yok')
check('ceyrek hiz testi', 'Time.timeScale = 0.25f;' in lab, 'yavas cekim yok')
switches = re.findall(r'public static bool (\w+)', strip_comments(
    method(dv, 'public static class Layers')))
missing_toggle = [w for w in switches if 'SnakeDefeatView.Layers.' + w not in lab]
check('%d katman anahtari, hepsi labda' % len(switches),
      len(switches) >= 9 and not missing_toggle, 'labda yok: %s' % missing_toggle)
check('lab kapaninca varsayilanlara donuyor', 'SnakeDefeatView.Layers.Defaults();' in lab,
      'anahtarlar acik kaliyor')
check('csproj dosyayi iceriyor',
      'SnakeDefeatView.cs' in read('ProjectBlock.View.csproj'), 'derlemede yok')

print()
if problems:
    print('SONUC: %d SORUN' % len(problems))
    for p in problems:
        print('  - %s' % p)
    sys.exit(1)
print('SONUC: temiz')
