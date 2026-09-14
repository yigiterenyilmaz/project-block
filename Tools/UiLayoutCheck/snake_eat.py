# YILAN'IN BLOK YEMESI: 31 KABUL KRITERI, YAPISAL OLARAK
#
# Bu animasyonun tek bir ayrimi var ve her sey ondan cikiyor: BLOGU agza tasimiyoruz, BLOGUN
# MADDESINI agza tasiyoruz. Kucultup kaydirmak "tween'li silinen bir tile" demek. Asagidaki
# kontroller o ayrimi ve onun etrafindaki yasaklari kodun icinde tutuyor - cunku bu efektin
# bozulma yolu tek tek her parcanin kaybolmasi degil, sessizce "kucult + ustune renkli cizgi"
# haline geri donmesi.

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


eat_raw = read('Assets', 'Scripts', 'View', 'SnakeEatView.cs')
eat = strip_comments(eat_raw)
view = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeView.cs'))
shader = strip_comments(read('Assets', 'Resources', 'Shaders', 'SnakeEat.shader'))
strand = strip_comments(read('Assets', 'Resources', 'Shaders', 'SnakeFilament.shader'))
lab = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs'))
style = dict((n, float(v)) for n, v in
             re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', eat))

print('=== 1. LOCOMOTION VE HAZIRLIK (1-4) ===')
check('1 yeme, kaymanin devami: tek saat, ayri klip yok',
      'private float BiteClock(Act a)' in view and 'SnakeEatView.Style.Total' in view,
      'yeme ayri bir klip')
check('2 isirmadan once coil var (bas geri, boyun toplanir)',
      'CoilStart' in eat and 'CoilDistance' in eat
      and 'SnakeEatView.NeckGather(BiteClock(act))' in view, 'coil yok')
check('3 agiz acilirken hard pop yok (gecis ortasinda takas + crossfade)',
      'Style.MouthOpenDuration * 0.4f' in view
      and 'Style.TopologyCrossfadeDuration' in view, 'agiz pat diye aciliyor')
check('4 bas + boyun birlikte hamle ediyor',
      'LungeDistance' in eat and 'NeckCompression' in eat, 'bas tek basina firliyor')

print()
print('=== 2. BLOGUN FIZIKSEL OLARAK EKSILMESI (5-9, 18, 22, 23) ===')
check('5 blok basta kendi gercek yuzunu koruyor',
      'proxy.sprite = food.Tile;' in eat and '_MainTex' in shader, 'blok kendi yuzunu kaybediyor')
check('6 blok bir anda scale-down OLMUYOR',
      'EatenBlockFinalScale' not in view and 'EatenBlockCompression' not in view,
      'blok kuculuyor')
check('7 cikarma YILANA BAKAN taraftan basliyor (agza dogru, gidis yonu degil)',
      'new Vector4(-facing.x, -facing.y' in eat and 'saturate(0.5 - dot(p, d))' in shader,
      'uzak yuzden yiyor')
check('8 siluet gercekten kademeli eksiliyor (alpha cikarma frontundan)',
      'c.a *= 1.0 - gone;' in shader and '_Progress' in shader, 'siluet eksilmiyor')
check('9 duz wipe DEGIL: birkac buyuk lob',
      '_Lobes' in shader and 'cos((across + ph) * TAU)' in shader
      and 'cos((across * 2.0 - ph * 1.7) * TAU)' in shader, 'duz wipe')
check('23 sinir anti-aliased, pixel/noise dissolve yok',
      '_Softness' in shader and 'noise' not in shader.lower()
      and 'frac(sin' not in shader, 'noise dissolve')
check('11 blok maddesi azaldikca isigi cekiliyor, materyali kaliyor',
      '_Drain' in shader and 'float grey = dot(c.rgb' in shader, 'blok sadece kirpiliyor')
check('18 blok kucuduldukce temas golgesi de azaliyor',
      'CellResidueStrength' in eat and 'extraction < 0.9f' in eat, 'golge sabit')

print()
print('=== 3. FILAMENTLER (10-17, 19, 48, 49) ===')
fil = method(eat, 'private void Build(Filament f, float k, float t)')
check('10 filamentler blogun KALAN maddesinden doguyor',
      'float depth = Mathf.Lerp(extraction, Mathf.Min(extraction + 0.3f, 1f),' in eat
      and 'Vector2 source = cell + facing * ((depth - 0.5f) * cellSize)' in eat,
      'filament havadan cikiyor')
check('10b filament kaynagina BAGLI kaliyor (bastan tuketilmiyor)',
      'float from = Smooth(Mathf.Clamp01((k - 0.6f) / 0.4f)) * 0.9f;' in fil,
      'filament blokla arasinda bosluk var')
# EN BUYUK HATA BURADAYDI: renk BLOGUN TINT'inden aliniyordu. Kendi boyasini tasiyan bir tile'in
# tinti BEYAZ (altin, obsidyen, boyali kartlar) - yani tum palet beyazdan turetiliyordu; korumali
# bir kupte ise o cagri MAGENTA donduruyor, birebir pembe. Blogun gercek rengi View'da var:
# ViewUtil.CubeMaterialColor. Look artik onu da tasiyor.
check('11 renk BLOGUN MADDESINDEN (tint degil, tablo yok)',
      'Color c = look.Paint.a > 0f ? look.Paint : look.Colour;' in eat
      and 'public Color Paint;' in strip_comments(
          read('Assets', 'Scripts', 'View', 'ClusterBurstView.cs'))
      and 'Paint = ViewUtil.CubeMaterialColor(cube)' in strip_comments(
          read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
      and 'CubeKind' not in eat, 'renk tintten aliniyor')
check('11b korumali kupun magenta yikamasi eat rengine girmiyor',
      'public static Color CubeMaterialColor(Cube cube)' in strip_comments(
          read('Assets', 'Scripts', 'View', 'ViewUtil.cs')), 'magenta sizmasi')
# EKRANDAKI MAGENTA'NIN GERCEK SEBEBI BUYDU: SpriteRenderer'a null materyal vermek onu
# varsayilana DONDURMEZ - Unity o renderer'i MAGENTA HATA MATERYALIYLE cizer ve tint'i tamamen
# yok sayar. Yani isigin, motelerin, cekirdegin, toplayicinin rengi ne olursa olsun ekranda duz
# pembe cikiyordu. Onizleme bunu yakalayamaz (orada Unity materyali yok), o yuzden kontrol burada.
check('hicbir renderer materyalsiz BIRAKILMIYOR (magenta hata materyali)',
      'r.sharedMaterial = null' not in eat
      and 'plainMaterial = r.sharedMaterial;' in eat
      and 'if (plainMaterial != null && r.sharedMaterial != plainMaterial)' in eat,
      'materyalsiz renderer = magenta')
check('63/64 SABIT pembe/magenta hicbir yerde yok',
      not any(w in eat for w in ('magenta', 'Magenta', '0.85f, 0.2f, 0.85f'))
      and 'pink' not in eat.lower(), 'sabit pembe')
check('63b eski DUZ yutma izleri (Mark.Streak) kaldirildi',
      'BiteIngestionStreak' not in strip_comments(
          read('Assets', 'Scripts', 'View', 'SnakeVfxController.cs'))
      and 'BlockIngestStart' not in view, 'duz pembe cizgiler duruyor')
check('12 main + secondary hiyerarsisi',
      'MainFilamentWidth' in eat and 'SecondaryFilamentCount' in eat
      and 'public bool Main;' in eat, 'tek tip filament')
check('13/18/19 lazer degil: cekirdek + govde + omuz, yani HACIM',
      'float core = pow(saturate(1.0 - x / 0.36), 2.0);' in strand
      and 'float body' in strand and 'float shoulder' in strand
      and 'LineRenderer' not in eat, 'lazer / duz cizgi')
check('20 cekirdek HUE korur: olceklenir, beyaza gitmez',
      'input.color.rgb * 1.22' in strand and '1.0, 1.0, 1.0' not in strand, 'beyaz cekirdek')
check('14/85 kivrim YOLUN UZUNLUGUYLA oranli (lup yapmiyor)',
      'float reach = Mathf.Clamp01(straight.magnitude' in eat, 'kisa yolda lup')
check('69 yol boyunca ayrilip SON kisimda birlesiyor',
      'FilamentConvergeShare' in eat and 'float along = dice.Range(0.3f, 0.58f);' in eat,
      'paralel besli cizgi')
check('13b ana akis gercekten kalin (8-12% hucre)',
      0.08 <= style['MainFilamentWidth'] <= 0.12, 'ana akis stroke kalinliginda')
check('13c main / secondary / micro hiyerarsisi',
      'MicroFilamentCount' in eat and style['MainFilamentWidth']
      > style['SecondaryFilamentWidth'] > style['MicroFilamentWidth'], 'hiyerarsi yok')
check('29/30 kafada SADECE lokal yansiyan isik var',
      'private void PaintHeadLight(float t)' in eat and 'HeadReflectionStrength' in eat
      and style['HeadReflectionStrength'] <= 0.3, 'kafa boyaniyor')
check('shader fallback var (magenta hata materyali imkansiz)',
      'Fallback "Sprites/Default"' in read('Assets', 'Resources', 'Shaders',
                                           'SnakeFilament.shader'), 'magenta riski')
check('14 hafif kivrimla agza gidiyor (bezier)',
      'private static Vector2 Bezier(' in eat and 'FilamentCurveStrength' in eat,
      'duz cizgi')
check('15 akis kaynak -> agiz, ters akis yok',
      'float packet = flow + Style.FilamentMouthAcceleration' in fil
      and 'FilamentFlowSpeed' in eat, 'akis okunmuyor')
check('16 akis mesh RENKLERINE isleniyor (kaydirmali neon doku yok)',
      'mesh.SetColors(stripColours);' in fil and '_Time' not in strand
      and 'scroll' not in strand.lower(), 'uv kaydiran neon')
check('17 kaynak->orta->agiz renk gecisi (material -> energy -> hot core)',
      'Color.Lerp(material, energy, along / 0.5f)' in fil
      and 'Color.Lerp(energy, hotCore' in fil, 'tek duz renk')
check('19 hero filament daha belirgin',
      style['MainFilamentWidth'] > style['SecondaryFilamentWidth']
      and style['MainFilamentOpacity'] > style['SecondaryFilamentOpacity'], 'hiyerarsi yok')
check('49 akis miktari cikarma HIZINA bagli (kutle korunumu illuzyonu)',
      'extractionRate' in eat and 'float rate = Mathf.Clamp01(extractionRate' in eat,
      'akis sabit')
check('50/51 yogunluk crescendo: sabit degil',
      'private static float Extraction(float t)' in eat
      and 'main < 0.2f' in eat and 'main < 0.65f' in eat, 'sabit yogunluk')

print()
print('=== 4. PARCACIKLAR VE EMME (16, 20, 21, 71) ===')
motes = method(eat, 'private void PaintMotes(float t, float dt)')
check('20/21 moteler blogun maddesinden kopuyor ve HEPSI iceri gidiyor',
      'Bezier(m.From, m.Lift, Cavity()' in motes and 'MoteCount' in eat, 'mote disari saciliyor')
check('71 agza yaklastikca hizlaniyor',
      'Style.MoteMouthAcceleration' in motes, 'emme hissi yok')
check('62 butce: main 1, secondary az, mote az',
      style['SecondaryFilamentCount'] <= 6 and style['MoteCount'] <= 8
      and 'int wantMain = 1;' in eat, 'parcacik spam')

print()
print('=== 5. SON CEKIRDEK VE AGIZ (24-35, 57-61) ===')
check('24/25 son %20 yogun bir cekirdege donuyor',
      'ExtractionBeforeCore' in eat and 'private void PaintCore(float t)' in eat, 'cekirdek yok')
check('26 son parca kisa bir an direniyor',
      'ResidualCoreHoldDuration' in eat
      and 'Style.ResidualCoreStart + Style.ResidualCoreHoldDuration' in eat, 'micro hold yok')
check('21/27/28 agiz icinde blok renginde kucuk bir yogunlasma',
      'private void PaintMouth(float t)' in eat and 'MouthEnergyRadius' in eat
      and style['MouthEnergyRadius'] <= 0.22, 'agizda enerji topu')
check('29/30 enerji birikimi cikarmayla artiyor, peak kisa, beyaza patlamiyor',
      'float build = Mathf.Clamp01(extraction * 1.15f);' in eat
      and 'Mathf.Lerp(energyColour.r, 1f, 0.3f)' in eat, 'beyaz patlama')
check('22/32 cene kapanmasi son cikarmayla ORTUSUYOR',
      style['MouthCloseStart'] < style['ResidualCoreStart'] + style['FinalStrandDuration'] + 0.1,
      'ayri kliplermis gibi')
check('34 elektrik snap yok',
      'lightning' not in eat.lower() and 'spark' not in eat.lower(), 'elektrik')
check('57/58 filamentler basin ARKASINDA, yuzunun ustunden gecmiyor',
      'private const int FilamentOrder = 5;' in eat, 'enerji basin yuzunde')
check('59/60 basin tamami blok rengine boyanmiyor',
      'AccentColour' in view and 'pose.AccentColour = eat.FoodColour;' in view
      and 'GulpColorStrength' in eat and style['GulpColorStrength'] <= 0.6, 'bas boyaniyor')
check('61 goz efekti yok', 'eye' not in eat.lower(), 'gozler parliyor')

print()
print('=== 6. YUTMA VE BUYUME (36-45) ===')
check('36/37 yutma paketi HEAD -> NECK -> BODY, her biri bir beat geride',
      'index * SnakeEatView.Style.GulpSegmentDelay' in view
      and 'GulpHeadStrength' in eat and 'GulpNeckStrength' in eat
      and 'GulpBodyStrength' in eat, 'paket yolculuk etmiyor')
check('38/39 her segmentte sisme + highlight + renkli iz, segmentin tamami degil',
      'cross += Style.GulpStrength * felt * SnakeEatView.Style.GulpCoreWidth' in view
      and 'pose.Sheen += 0.3f * felt;' in view
      and style['GulpCoreWidth'] <= 0.5, 'segment boyaniyor')
check('40 renk head>neck>body>swollen diye zayifliyor',
      style['GulpHeadStrength'] > style['GulpNeckStrength'] > style['GulpBodyStrength']
      > style['SwollenColorResidue'], 'renk zayiflamiyor')
check('42/43 swollen segment gulp GELDIGINDE doguyor, bagimsiz pop degil',
      't >= SnakeEatView.Style.SwollenStart' in view and 'swollenIndex = 1;' in view,
      'swollen pat diye biniyor')
check('44 overshoot: gecip geri oturuyor',
      'SnakeEatView.Style.SwollenOvershoot' in view
      and 'SnakeEatView.Style.SwollenRelaxDuration' in view, 'overshoot yok')
check('45 swollen yuzeyi bir sure gergin, sonra normale donuyor',
      'SnakeEatView.Style.SwollenSurfaceTension' in view, 'yuzey tepkisi yok')
check('28 buyume CORE sonucundan', 'CopyInto(body, a.To);' in view, 'view kendi uzatiyor')

print()
print('=== 7. OZEL BLOKLAR VE YASAKLAR (46, 47, 30, 88) ===')
check('46/47 butun blok turleri AYNI dili kullaniyor, sadece renk degisiyor',
      'CubeKind' not in eat and 'Obsidian' not in eat and 'Gold' not in eat,
      'blok tipine ozel dal')
check('46b cok koyu yuz (obsidian) isik tasiyacak kadar kaldiriliyor',
      'if (max < 0.34f)' in eat, 'obsidian sonuyor')
check('30 genel kaldirma efektlerinden hicbiri kullanilmiyor',
      not any(w in eat for w in ('PlayRemoval', 'ColdSink', 'PhaseFold', 'Cryo', 'CellFlashFx',
                                 'ClusterBurstView.Play', 'MomentumPeel', 'DynamiteBlast')),
      'kaldirma efekti')
forbidden = ('ParticleSystem', 'TrailRenderer', 'LineRenderer', 'ShakeCamera', 'timeScale',
             'Random.', 'System.Linq', 'Explode', 'Flash', 'Portal', 'BlackHole', 'Confetti',
             'Smoke', 'Shard')
check('88 yasak liste: %s' % ', '.join(forbidden[:6]),
      not any(w in eat for w in forbidden), 'yasak var')
check('88b yeni materyal her karede uretilmiyor, renderer/strip havuzlu',
      'private SpriteRenderer Rent(' in eat and 'private Strip RentStrip(' in eat
      and 'spareStrips.Push(s);' in eat, 'havuz yok')
check('87 MaterialPropertyBlock, calisma aninda doku uretimi yok',
      'MaterialPropertyBlock' in eat and 'new Texture2D' not in eat
      and 'SnakeVfxController.SoftDot()' in eat, 'doku uretiyor')
check('76/77 blok proxy kendi yuzunu/rengini tasiyor, cift cizim yok',
      'ClusterBurstView.Look eaten' in eat and 'food.Colour' in eat, 'proxy yuzunu kaybediyor')

print()
print('=== 8. SHADER YOKKEN (fallback) ===')
check('shader yoksa efekt sessizce kaybolmuyor, eski yolla yenir',
      'if (shader == null)' in eat and 'Fallback "Sprites/Default"' in eat_raw.replace('\n', '\n')
      or 'if (shader == null)' in eat, 'shader yoksa blok duruyor')
check('filament shaderi yoksa moteler tek basina tasiyor',
      'return null;   // no shader, no filaments' in eat_raw, 'strip null kontrolu yok')

print()
print('=== 9. SES KANCALARI (81) ===')
wanted = ['Lock', 'MouthOpen', 'Contact', 'ExtractionStart', 'ExtractionPeak', 'FinalCore',
          'MouthClose', 'Gulp', 'SwollenSettle']
missing = [w for w in wanted if 'Beat.' + w not in eat]
check("brief'in 9 ses kancasi tanimli ve tetikleniyor", not missing and 'Sounded(beat, at)' in eat,
      'eksik kanca: %s' % missing)

print()
print('=== 10. AYARLAR (86) ===')
names = sorted(style)
dead = [n for n in names if len(re.findall(r'\b' + n + r'\b', eat)) < 2
        and ('Style.' + n) not in view]
print('   %d ayar, %d okunuyor' % (len(names), len(names) - len(dead)))
check('her ayar gercekten okunuyor', not dead, 'okunmayan: %s' % ', '.join(dead))
wanted_knobs = [
    'TargetLockDuration', 'CoilDuration', 'CoilDistance', 'NeckCompression',
    'MouthOpenDuration', 'LungeDistance', 'LungeDuration', 'SurfaceActivationDuration',
    'ExtractionDuration', 'ExtractionDirectionBias', 'ExtractionSoftness',
    'ExtractionVariationCount', 'ExtractionEdgeLightStrength', 'ExtractionEdgeWidth',
    'MainFilamentWidth', 'MainFilamentOpacity', 'SecondaryFilamentCount',
    'SecondaryFilamentWidth', 'SecondaryFilamentOpacity', 'FilamentCurveStrength',
    'FilamentFlowSpeed', 'FilamentMouthAcceleration', 'MoteCount', 'MoteSpeed', 'MoteLifetime',
    'MoteMouthAcceleration', 'ResidualCoreSize', 'ResidualCoreStrength',
    'ResidualCoreHoldDuration', 'MouthEnergyStrength', 'MouthEnergyRadius',
    'MouthCloseDuration', 'FinalStrandDuration', 'GulpDuration', 'GulpHeadStrength',
    'GulpNeckStrength', 'GulpBodyStrength', 'GulpColorStrength', 'GulpSegmentDelay',
    'SwollenOvershoot', 'SwollenRelaxDuration', 'SwollenColorResidue',
    'SwollenSurfaceTension', 'CellResidueStrength', 'CellResidueDuration',
    'MicroFilamentCount', 'MicroFilamentWidth', 'MicroFilamentOpacity',
    'FilamentConvergeShare', 'HeadReflectionStrength']
absent = [w for w in wanted_knobs if w not in style]
check("brief'in %d ayari tanimli" % len(wanted_knobs), not absent, 'eksik: %s' % absent)

print()
print('=== 11. ZAMAN CIZELGESI (78) ===')
total = max(style['SwollenStart'] + style['SwollenRelaxDuration'],
            max(style['GulpStart'] + style['GulpDuration'],
                style['MouthCloseStart'] + style['MouthCloseDuration']))
check('toplam %.0f ms (700-950)' % (total * 1000), 0.7 <= total <= 0.95, 'sure')
check('80 gorsel peak cikarmanin ortasinda, cene kapanmasinda degil',
      style['ExtractionStart'] + style['ExtractionDuration'] * 0.55 < style['MouthCloseStart'],
      'peak cenede')
check('fazlar ORTUSUYOR (ayri klipler degil)',
      style['MouthOpenStart'] < style['TargetLockDuration'] + style['CoilDuration']
      and style['MouthCloseStart'] < style['ResidualCoreStart'] + style['FinalStrandDuration']
      and style['GulpStart'] < style['MouthCloseStart'] + style['MouthCloseDuration'],
      'fazlar sirayla')

print()
print('=== 12. LAB (82-85) ===')
check('82 isirma icin kendi bolumu var',
      'yılan: ısırma (hero) - blok blok' in lab, 'lab bolumu yok')
for name, key in (('renk', 'AnimSnakeScene.EatColour'), ('su', 'AnimSnakeScene.EatWater'),
                  ('ates', 'AnimSnakeScene.EatFire'), ('altin', 'AnimSnakeScene.EatGold'),
                  ('obsidyen', 'AnimSnakeScene.EatObsidian')):
    check('82b %s bloku test ediliyor' % name, key in lab, '%s testi yok' % name)
check('83 dort yon test ediliyor', 'AnimSnakeScene.BiteDirections' in lab, 'yon testi yok')
check('84 yavas cekim 0.5x ve 0.25x',
      'Time.timeScale = 0.5f;' in lab and 'Time.timeScale = 0.25f;' in lab, 'yavas cekim yok')
switches = re.findall(r'public static bool (\w+)', strip_comments(
    method(eat, 'public static class Layers')))
missing_toggle = [w for w in switches if 'SnakeEatView.Layers.' + w not in lab]
check('85/89 %d hata ayiklama anahtari, hepsi labda' % len(switches),
      len(switches) >= 13 and not missing_toggle, 'labda yok: %s' % missing_toggle)
wanted_layers = ['ShowExtractionMask', 'ShowErosionEdge', 'ShowMainRibbon',
                 'ShowSecondaryRibbons', 'ShowMotes', 'ShowMouthCollector',
                 'ShowHeadReflection', 'ShowFinalCore', 'ShowGulpPackage']
check("89 brief'in 9 katman anahtari tanimli",
      not [w for w in wanted_layers if w not in switches],
      'eksik: %s' % [w for w in wanted_layers if w not in switches])
check('lab kapaninca varsayilanlara donuyor', 'SnakeEatView.Layers.Defaults();' in lab,
      'anahtarlar acik kaliyor')
check('csproj dosyayi iceriyor',
      'SnakeEatView.cs' in read('ProjectBlock.View.csproj'), 'derlemede yok')

print()
if problems:
    print('SONUC: %d SORUN' % len(problems))
    for p in problems:
        print('  - %s' % p)
    sys.exit(1)
print('SONUC: temiz')
