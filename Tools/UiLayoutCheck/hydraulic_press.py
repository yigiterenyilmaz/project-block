# Hidrolik Pres (HydraulicPressView / CompressedCubeView / PressureVesselView):
# sikistirma, basinc altinda saklama, yonlu bosaltma.
#
#   1. CORE = GERCEK: View hicbir yonu, itmeyi, bos bolmeyi HESAPLAMIYOR - hepsi rapordan.
#   2. RAPOR: sikistirma ve acilma ayri ayri yaziliyor; gameplay degismedi (baseline ayni).
#   3. KAYIT: rapor alanlari [NotSaved] - Yilan'da tam bu alan kaydi cokertmisti.
#   4. KATMAN: dort kup HACMINI kaybediyor (materyal duzlestirme), sadece scaleY DEGIL.
#   5. BOS BOLME: null quadrant negatif iz olarak temsil ediliyor, asla uydurma kup yok.
#   6. KABUK: slate rengi tasarimcinin; dikisler, cukur, geri sayim izleri kabugun kendi yuzeyi.
#   7. GERI SAYIM: tur sayisi POWER'dan; View tur saymiyor.
#   8. ITME: her kup kendi rapor edilen adimiyla; alan disina giden kenarda DURMUYOR.
#   9. ENGEL: altin/obsidyen kalkan/kivilcim acmiyor; yon degistirme YALNIZ kosede.
#  10. COKUS: TNT gibi disa degil, ICE gocuyor; altin/obsidyen ezilerek gidiyor; odul VFX yok.
#  11. YASAKLAR: beyaz flash, ekran sarsintisi, rastgelelik, sprite takasi, yeni materyal ornegi.
#  12. LAB: 23 sahne + anahtarlar, oyunun kapisindan; ham bolumdeki eski giris kalkti.
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
    m = re.search(r'\n        (private|public|internal|protected|static) ', rest)
    return src[i:i + len(signature) + (m.start() if m else len(rest))]


def num(src, pattern):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else None


def check(label, ok, why):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


press = strip_comments(read('Assets', 'Scripts', 'View', 'HydraulicPressView.cs'))
press_raw = read('Assets', 'Scripts', 'View', 'HydraulicPressView.cs')
cube = strip_comments(read('Assets', 'Scripts', 'View', 'CompressedCubeView.cs'))
cube_raw = read('Assets', 'Scripts', 'View', 'CompressedCubeView.cs')
vessel = strip_comments(read('Assets', 'Scripts', 'View', 'PressureVesselView.cs'))
vessel_raw = read('Assets', 'Scripts', 'View', 'PressureVesselView.cs')
shell_sh = read('Assets', 'Resources', 'Shaders', 'PressShell.shader')
lamina_sh = read('Assets', 'Resources', 'Shaders', 'PressLamina.shader')
board = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Press.cs'))
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Powers', 'HydraulicPressVisuals.cs'))
power = strip_comments(read('Assets', 'Scripts', 'Core', 'Powers', 'Definitions',
                            'WorkshopPowers.cs'))
engine = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'RoundEngine.Scoring.cs'))
serializer = strip_comments(read('Assets', 'Scripts', 'Core', 'Save',
                                 'ContentStateSerializer.cs'))
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
activation = strip_comments(read('Assets', 'Scripts', 'View',
                                'GameUiController.Activation.cs'))
drag = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Drag.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
vu = strip_comments(read('Assets', 'Scripts', 'View', 'ViewUtil.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')
views = [press, cube, vessel]
view_names = ['HydraulicPressView', 'CompressedCubeView', 'PressureVesselView']

print('=== 1. CORE = GERCEK, VIEW HICBIR SEYE KARAR VERMIYOR ===')
# The two CHOREOGRAPHY views may not ask the board anything at all: every position, step, side
# and casualty arrives in a Scene built from the report. (CompressedCubeView is different on
# purpose - it is a PRESENCE layer and asks the board which cells hold a press, exactly as
# GangreneView and SnakeView ask which cells hold rot or snake. That is not a rules decision, and
# it is checked separately below.)
forbidden = ['GetCube(', 'IsInside(', 'PushAway', 'CanCompressAt', 'ResolveFullLines',
             'GameBoard ']
for name, src in [('HydraulicPressView', press), ('PressureVesselView', vessel)]:
    # BoardView is handed in for HoldCells/ReleaseCells only, which is not a rules question.
    hits = [f for f in forbidden if f in src]
    check('%s kurallara soru sormuyor' % name, not hits,
          '%s board sorgusu yapiyor: %s' % (name, hits))
check('CompressedCubeView yalniz VARLIK soruyor (hangi hucre pres), karar vermiyor',
      'Kind != CubeKind.Compressed' in cube
      and not any(f in cube for f in ['PushAway', 'CanCompressAt', 'PressAxis']),
      'CompressedCubeView kural karari veriyor')
check('ezilen tasin rengi RAPORUN soyledigi turden seciliyor, kendi bakisindan degil',
      'c.Kind == CubeKind.Gold ? GoldLamina' in vessel
      and 'Casualty' in vessel, 'ezilen tasin turu View icinde turetiliyor')
check('itme yonu rapordan geliyor (Shove.Step), View turetmiyor',
      'public Vector2 Step;' in press and 'Step = s.Step' in press,
      'itme adimi View icinde hesaplaniyor')
check('engellenen yon rapordan geliyor (Denial)',
      'public struct Denial' in press and 'Succeeded = test.Succeeded' in fb,
      'engel bilgisi View icinde turetiliyor')
check('kose ekseni rapordan (DiagonalAxis), View komsuya bakmiyor',
      'DiagonalAxis = report.DiagonalAxis' in fb
      and 'run.DiagonalAxis' in press,
      'kose ekseni View icinde secilyor')
check('bos bolme rapordan (Occupied = cube.HasValue)',
      'Occupied = cube.HasValue' in fb, 'bos bolme tahmin ediliyor')
check('cokus ayak izi rapordan (DetonatedCells), kendi yaricapindan degil',
      'report.DetonatedCells' in fb and 'scene.Casualties' in vessel,
      'cokus kendi yaricapini uyduruyor')

print('=== 2. RAPOR: SIKISTIRMA VE ACILMA, GAMEPLAY DEGISMEDI ===')
check('iki rapor tipi var', 'class PressCompressionVisuals' in visuals
      and 'class PressReleaseVisuals' in visuals, 'rapor tipleri eksik')
for field in ['Cells', 'Swallowed', 'CompressedCell', 'OccupiedCount']:
    check('sikistirma raporu %s tasiyor' % field, ('public' in visuals and field in visuals),
          '%s eksik' % field)
for field in ['AlreadyFree', 'Tests', 'Pushes', 'DetonatedCells', 'DetonatedKinds',
              'CubesPushedOff', 'DiagonalAxis', 'Rerouted']:
    check('acilma raporu %s tasiyor' % field, field in visuals, '%s eksik' % field)
check('PressPush kendi adimini ve alan-disi bayragini tasiyor',
      'public GridPos Step;' in visuals and 'public bool LeftBoard;' in visuals,
      'itme raporu eksik')
check('PressPush sirasi preste 0 (basinc yakina once ulasir)',
      'public int Order;' in visuals, 'itme sirasi yok')
check('PressureTest reddi ve reddedeni yaziyor',
      'public bool Succeeded;' in visuals and 'public GridPos BlockedAt;' in visuals
      and 'public CubeKind BlockedKind;' in visuals, 'red bilgisi eksik')
check('yon degistirme bayragi var (IsReroute)', 'public bool IsReroute;' in visuals,
      'reroute bayragi yok')
# The old signatures must still exist and delegate, so nothing the rules call has changed.
check('raporsuz Compress hala var ve yeni surume devrediyor',
      'internal Cube?[] Compress(GridPos anchor)' in board
      and 'return Compress(anchor, out ignored);' in board,
      'eski Compress imzasi bozuldu')
check('raporsuz Expand hala var ve yeni surume devrediyor',
      'internal PressExpansion Expand(GridPos anchor, Cube?[] swallowed)' in board
      and 'return Expand(anchor, swallowed, out ignored);' in board,
      'eski Expand imzasi bozuldu')
check('raporlayan surumler PUBLIC (lab gercek kurallari calistirabilsin)',
      'public Cube?[] Compress(GridPos anchor, out PressCompressionVisuals visuals)' in board
      and 'public PressExpansion Expand(GridPos anchor, Cube?[] swallowed' in board,
      'lab gercek kurallara erisemez')
check('RoundEngine.ReleasePress raporu gecis yapiyor',
      'out PressReleaseVisuals visuals' in engine, 'engine raporu iletmiyor')
check('kural govdesi degismedi: tek yon tercihi yatay-sonra-dikey',
      'PushAway(from, dx, 0, result, report, wantedCell)' in board
      and 'PushAway(from, 0, dy, result, report, wantedCell)' in board,
      'kose yon tercihi degismis')

print('=== 3. KAYIT: RAPOR ALANLARI KAYDEDILMIYOR ===')
check('NotSaved kapisi duruyor', 'class NotSavedAttribute' in serializer
      and 'NotSavedAttribute), false' in serializer, 'NotSaved kapisi yok')
for prop in ['LastCompression', 'LastRelease', 'BrokenWhileShut']:
    m = re.search(r'\[field: NotSaved\]\s*\n\s*public [^\n]*' + prop, power)
    check('%s [NotSaved] isaretli' % prop, m is not None,
          '%s kayda giriyor - Yilan`da bu kaydi cokertmisti' % prop)

print('=== 4. KATMAN: HACIM KAYBI, SADECE SCALEY DEGIL ===')
check('duzlestirme shaderda materyal isi yapiyor (_Flatten + _Average)',
      '_Flatten' in lamina_sh and '_Average' in lamina_sh, 'duzlestirme shaderi eksik')
check('kontrast yuzun KENDI ortalamasina dogru kisiliyor',
      '_Average.rgb + (c.rgb - _Average.rgb)' in lamina_sh,
      'duzlestirme ortalamaya gore yapilmiyor')
check('ortalama renk PAINT`ten turetiliyor (boyali karo tinti beyazdir)',
      'look.Paint.a > 0f' in press, 'ortalama tintten turetiliyor - her sey beyaza duzlesir')
check('silueti de veriyor (_Press vertex sikismasi), yalniz olcek degil',
      '_Press' in lamina_sh and 'posOS.y *= 1.0 - saturate(_Press)' in lamina_sh,
      'siluet sikismasi yok')
# The thickness rule lives in section 4b now: a pressed cube KEEPS its footprint in a top-down
# board, so a low value here is the bar bug rather than a tight tolerance.
check('cikista hacim GERI geliyor (_Relief), capraz gecis degil',
      '_Relief' in lamina_sh and 'RestoreReliefStart' in press,
      'hacim geri gelmiyor')
check('materyal kimligi korunuyor: kupun kendi karosu ciziliyor',
      'Rent(q.Look.Tile' in press, 'katman kendi karosunu tasimiyor')

# THE FAILURE THIS SECTION EXISTS FOR: the first pass drew each lamina as a sprite squashed to a
# fifth of its height, which in a TOP-DOWN board is not a flattened cube - it is a coloured bar.
# A pressed cube keeps its FOOTPRINT and loses its BEVEL.
print('=== 4b. LAMINA BIR CUBIN EZILMISI, CUBUK DEGIL ===')
thick = num(press, r'LaminaThickness = ([\d.]+)f')
check('lamina footprint`i koruyor (yukseklik >= 0.7)', thick is not None and thick >= 0.7,
      'lamina cubuga donuyor: yukseklik %s' % thick)
check('hacim SHADING`den gidiyor (_Volume), olcekten degil',
      '_Volume' in lamina_sh and 'VolumeId' in press, 'bevel shader ile kaldirilmiyor')
check('plakanin kendi kalinligi var (_PlateEdge: alt dudak + ust yakalama)',
      '_PlateEdge' in lamina_sh and 'PlateEdgeId' in press, 'plaka sticker gibi')
check('vertex sikismasi kucuk (%20`nin altinda)',
      'saturate(_Press) * 0.16' in lamina_sh, 'vertex sikismasi hala cubuk yapiyor')
check('lamina KENDI karosuyla ciziliyor',
      'Rent(q.Look.Tile' in press and 'Rent(s.Look.Tile' in press,
      'lamina kupun karosunu kullanmiyor')
for bad in ['Color.magenta', 'Color.cyan', 'debugPurple', 'fixedPlateColor']:
    check('sabit debug rengi yok: %s' % bad, bad not in press, 'lamina sabit renk kullaniyor')
check('yigindaki her katman altindakine golge birakiyor',
      'StackLayerShadow' in press, 'katmanlar arasi golge yok')

print('=== 4c. CENELER CERCEVE DEGIL, DORT PARCA ===')
jl = num(press, r'JawLength = ([\d.]+)f')
check('cene kenarin tamamini kaplamiyor (< 0.8)', jl is not None and jl < 0.8,
      'dort cene birlesip kare cerceve olusturuyor: %s' % jl)
check('her cenenin kendi temas golgesi var', 'JawShadowStrength' in press,
      'ceneler tahtanin ustunde durmuyor')
check('ceneler geri cekiliyor, yok olmuyor', 'JawRetractDuration' in press,
      'ceneler snap ile kayboluyor')

print('=== 4d. KABUK YIGININ USTUNE KAPANIYOR ===')
check('dort kepenk var', 'PaintShutters' in press and 'run.Shutters' in press,
      'kabuk tek levha olarak beliriyor')
check('kepenkler solmuyor: geldikleri anda opak',
      'They are solid the moment they exist' in press_raw
      and 'c.a = k > 0.02f ? 1f - Ease' in press,
      'kepenkler fade-in ediyor')
check('kabuk yuzeyi ancak kepenkler ortunce tamamlaniyor',
      '(k - 0.45f) / 0.55f' in press, 'kabuk yuzeyi bagimsiz fade ediyor')

print('=== 5. BOS BOLME: NEGATIF IZ, UYDURMA KUP YOK ===')
check('shaderda negatif mod var', '_Negative' in lamina_sh
      and 'if (_Negative > 0.5)' in lamina_sh, 'negatif iz modu yok')
check('negatif iz nesne gibi gorunmuyor (ici oyuluyor, kenari zayif)',
      'hollow' in lamina_sh, 'negatif iz duz bir kare')
check('bos bolme icin kup karosu kiralanmiyor',
      'else if (!q.Occupied && Layers.ShowNullImprints)' in press,
      'bos bolme kup olarak ciziliyor')
check('geri donen bos bolme BOS kaliyor (iz sonra siliniyor)',
      'NullRestoreImprint' in press, 'bos bolme geri gelirken iz silinmiyor')

print('=== 6. KABUK: TASARIMCININ SLATE`I, KENDI YUZEYI ===')
check('slate tek yerden geliyor (ViewUtil.CubeMaterialColor)',
      'CubeKind.Compressed: return new Color(0.30f, 0.34f, 0.42f)' in vu,
      'slate rengi degismis')
check('View slate`i kendi yazmiyor, o tek yerden aliyor',
      'ViewUtil.CubeMaterialColor(' in cube and 'CubeKind.Compressed' in cube,
      'View slate`i kopyalamis')
for part in ['_Seams', '_Dimple', '_Corner', '_Gloss', '_Scars', '_Stress',
             '_EdgeLead', '_Collapse']:
    check('kabuk shaderi %s tasiyor' % part, part in shell_sh, '%s eksik' % part)
check('saklanan renk hafizasi dikisin ALTINDA karisiyor (sticker degil)',
      '_Memory' in shell_sh and 'nearSeam' in shell_sh, 'renk hafizasi yuzeye yapistirilmis')
check('kabuk fade-in etmiyor: dort kepenk yiginin ustune KAPANIYOR',
      'PaintShutters' in press and 'float size = run.Cube;' in press,
      'kabuk hala olceklenip soluyor')
check('kabuk tek noktadan boyaniyor (ApplyShell) - dort yerde drift yok',
      'public static void ApplyShell' in cube
      and 'CompressedCubeView.ApplyShell' in press
      and 'CompressedCubeView.ApplyShell' in vessel,
      'kabuk ozellikleri birden fazla yerde yaziliyor')
check('shader yoksa tahtanin kendi kupu birakiliyor (plate kapatilmiyor)',
      's.Plate.enabled = false;' in cube, 'shader yoksa duz kare cizilyor')

# A sprite is one unit across only when its pixels-per-unit equals its pixel size. The generated
# rounded plate is 64px at 128 PPU - HALF a unit - so a shader that reads `positionOS.xy * 2` as a
# -1..1 face is really getting -0.5..0.5, and every corner feature lands off the face. That is how
# the countdown marks came to be drawn nowhere at all while the seams and the dimple looked fine.
print('=== 6b. YUZ KOORDINATI VARSAYILMIYOR ===')
for sh, name in [(shell_sh, 'PressShell'), (lamina_sh, 'PressLamina')]:
    check('%s yuzu sprite`in KENDI olcusunden turetiyor' % name,
          '_FaceHalf' in sh and 'positionOS.xy / max(_FaceHalf' in sh,
          '%s tek-birim sprite varsayiyor' % name)
    check('%s bir birim oldugunu VARSAYMIYOR' % name,
          'positionOS.xy * 2.0' not in sh,
          '%s hala positionOS.xy * 2.0 kullaniyor' % name)
check('yuz yarim-olcusu her cizimde besleniyor',
      'FaceHalfOf(renderer)' in cube and 'FaceHalfOf(p.Renderer)' in press
      and 'FaceHalfOf(s.Renderer)' in vessel, '_FaceHalf beslenmiyor')
check('yarim-olcu sprite`in gercek bounds`undan',
      'renderer.sprite.bounds.size' in cube, 'yarim-olcu sabit yazilmis')
# With a correct face, a crease has to lie INSIDE it and be long enough to read at cell size.
inner = num(cube, r'ScarInner = ([\d.]+)f')
outer = num(cube, r'ScarOuter = ([\d.]+)f')
half = num(cube, r'ScarHalfWidth = ([\d.]+)f')
dimr = num(cube, r'DimpleRadius = ([\d.]+)f')
# A scar runs along the quadrant diagonal, so its far end sits at outer/sqrt(2) on each axis.
check('cizikler yuzun ICINDE kaliyor', outer is not None and outer <= 1.0,
      'cizik yuzun disina tasiyor: %s' % outer)
check('cizik 64px kupte okunur uzunlukta (>= 8px)', (outer - inner) * 32 >= 8.0,
      'cizik cok kisa: %.1f px' % ((outer - inner) * 32))
check('cizik merkezdeki cukurdan sonra basliyor', dimr is not None and inner > dimr,
      'cizik cukurun icinden cikiyor')

print('=== 7. GERI SAYIM: BASINC YASLANMASI, NOKTA SAYACI DEGIL ===')
check('View tur saymiyor: SetCountdown disaridan besleniyor',
      'public void SetCountdown(int left, int total)' in cube, 'SetCountdown yok')
check('besleme POWER`in kendi sayisindan',
      'press.TurnsLeft, press.TurnsCompressed' in fb, 'tur sayisi View`da uretiliyor')
# THE FAILURE THIS REPLACES: four orange dots, one per turn - a cooldown LED strip. The countdown
# has to be the STATE OF THE MATERIAL, so a player reads "this is under a lot of pressure" rather
# than counting lights.
for gone in ['_Notches', '_NotchGeom', 'NotchRadius', 'NotchOffset']:
    check('nokta sayaci kaldirildi: %s yok' % gone,
          gone not in shell_sh and gone not in cube, '%s hala duruyor' % gone)
check('sayac GOMULU CIZIK: daire/nokta degil, konik oluk',
      '_Scars' in shell_sh and '_ScarGeom' in shell_sh
      and 'halfW = _ScarGeom.z * (1.0 - 0.8 * t)' in shell_sh,
      'sayac hala nokta')
check('cizik once bir OYUK, sonra renk',
      'c.rgb *= 1.0 - _ScarGeom.w * saturate(scarMark);' in shell_sh,
      'cizik boyali cizgi gibi')
check('amber yalniz KILITLENME aninda, kalici degil',
      '_Glints' in shell_sh and 'ScarGlintDuration' in cube
      and (num(cube, r'ScarGlintDuration = ([\d.]+)f') or 1) <= 0.2,
      'amber kalici sayac rengi')
check('tur ilerledikce KABUK yasleniyor (cukur/dikis/bevel/golge)',
      all(k in cube for k in ['WaitDimplePerTurn', 'WaitSeamPerTurn', 'WaitBevelPerTurn',
                              'WaitShadowTightPerTurn', 'WaitShadowDarkPerTurn']),
      'tur farki yalniz cizik sayisinda')
check('kabuk yaslanmasi shadera da gidiyor (_Load)',
      '_Load' in shell_sh and 'LoadId' in cube, 'shader yasi bilmiyor')
check('tur bir karede degil, PRESSURE TICK ile geliyor',
      'BeginPressureTick' in cube and 'TickClock' in cube
      and 0.14 <= (num(cube, r'TickDuration = ([\d.]+)f') or 0) <= 0.22,
      'tur tek karede degisiyor')
check('tik yalniz olcek tween`i degil: kenar + cukur + dikis + golge birlikte',
      all(k in cube for k in ['TickEdgeInset', 'TickDimpleStrength', 'TickSeamStrength',
                              'TickSettle']),
      'tik tek ozelligi oynatiyor')
check('tik kenarlari ICERI bastiriyor (negatif lead), olcek degil',
      'float inset = -(Style.TickEdgeInset' in cube, 'tik kupu olcekliyor')
check('ayni turu tekrar bildirmek tik tetiklemiyor',
      'if (now > was)' in cube, 'her repaint tik atiyor')
check('idle dongusu tur ilerledikce hizlaniyor',
      'IdleCyclePerTurn' in cube and 'IdleStrengthPerTurn' in cube,
      'her turda ayni idle')
check('son turda ince yuzey gerilimi var, alarm yok',
      'FinalSurfaceStrain' in cube and '_Strain' in shell_sh, 'son tur ayirt edilmiyor')
check('son turda bile amber baskin degil (residue <= 0.2)',
      (num(cube, r'FinalStressResidue = ([\d.]+)f') or 1) <= 0.2,
      'kapsul turuncu kupe donuyor')
check('kup nefes almiyor: olcek degil cukur/dikis oynuyor',
      'transform.localScale' not in method(cube, 'private void PaintAll()'),
      'idle kupu olcekliyor')
check('lab: dort bekleme durumu + ayri TUR ILERLET dugmesi',
      'AnimPressAdvanceTurn' in lab and lab_raw.count('AnimPressScene.Idle') >= 4,
      'tik tek basina izlenemiyor')

print('=== 8. ITME: RAPOR EDILEN ADIM, KENARDA DURMA YOK ===')
check('alan disina giden kup hizini koruyup devam ediyor',
      'EjectSpeed' in press and 'Mathf.Max(0f, mine - Style.PushEnd) * Style.EjectSpeed' in press,
      'tasan kup kenarda duruyor')
check('hedef hucreler kopyalar varana kadar bos tutuluyor',
      'board.HoldCells(run.Held)' in press and 'ReleaseCells(run.Held)' in press,
      'iki kup ayni hucrede gorunebiliyor')
check('geri gelen 2x2 de tutuluyor (katman varmadan kup gorunmesin)',
      'if (scene.Quads[i].Occupied)' in press and 'run.Held.Add(scene.Quads[i].Cell)' in press,
      'geri gelen kupler katmanlardan once gorunuyor')
check('hidrolik easing var (yavas basinc -> guclu hareket -> sert oturma)',
      'private static float Hydraulic(float k)' in press, 'duz lerp kullanilmis')
check('itilen kupte kucuk deformasyon var (%1-3)',
      0.01 <= (num(press, r'PushDeformation = ([\d.]+)f') or 0) <= 0.04,
      'deformasyon brief disinda')
check('golge hareket yonunun tersine gecikiyor',
      'PushShadowLag' in press and 'at - step * (moving' in press, 'golge gecikmesi yok')
check('basinc cephesi isin/halka degil: dusuk opaklikli slate bant',
      'PaintFront' in press and 'new Color(0.46f, 0.5f, 0.58f, alpha)' in press,
      'basinc cephesi isin gibi')
check('zincir kademesi kucuk (10-30 ms)',
      0.008 <= (num(press, r'PushStagger = ([\d.]+)f') or 0) <= 0.035,
      'kademe brief disinda')

print('=== 9. ENGEL: KALKAN YOK, YON DEGISTIRME YALNIZ KOSEDE ===')
for word in ['Shield', 'shield', 'Spark', 'spark', 'Glow', 'glow']:
    check('engel dilinde "%s" yok' % word, word not in press,
          'engelde kalkan/kivilcim/parilti var: %s' % word)
check('engellenen kup kipirdamiyor: yalniz temas izi ciziliyor',
      'PaintBlockMark' in press and 'BlockedTargetResponse' in press,
      'engellenen kup hareket ediyor')
check('altin ve obsidyen ayri okunuyor',
      'BlockedKind == CubeKind.Obsidian ? 0.5f : 0.36f' in press,
      'altin ve obsidyen ayni ciziliyor')
check('kabuk bir piksel basip bir piksel geri cekiliyor',
      'BlockedShellPressure' in press and 'BlockedRecoil' in press, 'bas-geri cek yok')
check('basinc kabuk icinden obur yone aktariliyor',
      'PaintReroute' in press and 'PressureRerouteDuration' in press, 'aktarim yok')
check('yon degistirme SADECE raporun soyledigi yerde oynatiliyor',
      'd.IsReroute' in press or 'IsReroute = test.IsReroute' in fb,
      'reroute her yerde oynatiliyor')
check('engel testi brief suresinde (50-90 ms)',
      0.05 <= (num(press, r'BlockedTestDuration = ([\d.]+)f') or 0) <= 0.09,
      'engel testi suresi brief disinda')

print('=== 10. COKUS: ICE GOCME, EZILME, ODUL YOK ===')
check('ICE gocuyor: kenarlar merkeze, orta duruyor',
      '_Collapse' in shell_sh and 'crush = -face * _Collapse' in shell_sh,
      'cokus disa sisiyor - TNT gibi')
check('gocme suresi brief araliginda (60-100 ms)',
      0.06 <= (num(vessel, r'InwardCollapseDuration = ([\d.]+)f') or 0) <= 0.1,
      'gocme suresi brief disinda')
check('balon yok: olcek artisi %5`in altinda',
      (num(vessel, r'StrainAmount = ([\d.]+)f') or 1) <= 0.05,
      'cokus oncesi balon gibi sisiyor')
check('basinc dugumu kisa tutuluyor (20-40 ms)',
      0.02 <= (num(vessel, r'KnotHold = ([\d.]+)f') or 0) <= 0.045,
      'dugum suresi brief disinda')
check('dalga IZGARAYA uygun (kare + capraz etki), daire degil',
      'BurstSquareness' in vessel and 'BurstCross' in vessel, 'dalga daire')
check('dalga ayak izi raporun en uzak hucresinden turetiliyor',
      'if (d > reach)' in vessel and 'BurstOvershoot' in vessel,
      'dalga kendi yaricapini uyduruyor')
check('beyaz yok: merkez soluk SICAK CELIK',
      'BurstCore = new Color(0.78f, 0.72f, 0.62f)' in vessel
      and 'Color.white' not in vessel, 'dalgada beyaz flash var')
check('altin ince amber-altin, obsidyen koyu mor lamina olarak eziliyor',
      'GoldLamina = new Color(0.78f, 0.6f, 0.26f)' in vessel
      and 'ObsidianLamina = new Color(0.29f, 0.21f, 0.38f)' in vessel,
      'ezilen tas kendi materyalini tasimiyor')
check('ezilme kirilma/sarapnel degil: duzlesme + kisa tasima',
      'ShearThickness' in vessel and 'ShearTravel' in vessel
      and 'Shard' not in vessel and 'Debris' not in vessel,
      'tas sarapnele ayriliyor')
check('tasima kisa (0.2-0.4 hucre)',
      0.2 <= (num(vessel, r'ShearTravel = ([\d.]+)f') or 0) <= 0.4,
      'tasima mesafesi brief disinda')
check('presin kendi kabugu ezilmiyor - o gocen sey',
      'if (c.Kind == CubeKind.Compressed)' in vessel and 'continue;' in vessel,
      'presin kendisi de ezilmis gibi ciziliyor')
check('parcacik butcesi kontrollu (<=16)',
      (num(vessel, r'FleckCount = (\d+)') or 99) <= 16, 'parcacik butcesi asiliyor')
check('parcalar kare konfeti degil: ince egimli dilimler',
      'len * 2.6f, len * 0.7f' in vessel, 'parcacik kare')
check('kalinti kisa (200-300 ms)',
      0.2 <= (num(vessel, r'ResidueDuration = ([\d.]+)f') or 0) <= 0.32,
      'kalinti suresi brief disinda')
for word in ['Coin', 'coin', 'Confetti', 'confetti', 'Sparkle', 'sparkle', 'Reward', 'reward']:
    check('cokuste "%s" yok' % word, word not in vessel, 'cokuste odul VFX var: %s' % word)

print('=== 11. YASAKLAR ===')
for name, src in zip(view_names, views):
    check('%s kamerayi oynatmiyor' % name,
          'ShakeCamera' not in src and 'Shake(' not in src,
          '%s ekrani sarsiyor' % name)
    check('%s rastgelelik kullanmiyor' % name,
          'Random.' not in src, '%s Random kullaniyor' % name)
    check('%s her cagrida yeni materyal uretmiyor' % name,
          src.count('new Material(') <= 1, '%s materyal ornegi uretiyor' % name)
    if name != 'CompressedCubeView':
        # The transient effects pool; the PRESENCE layer keeps one plate per standing press and
        # drops it when the cell stops being one, which is what GangreneView does too.
        check('%s renderer havuzlu (Rent/Return)' % name,
              'private SpriteRenderer Rent(' in src and 'private void Return(' in src,
              '%s havuz kullanmiyor' % name)
    check('%s MaterialPropertyBlock ile cizilyor' % name,
          'MaterialPropertyBlock' in src, '%s per-renderer materyal kullaniyor' % name)
check('CompressedCubeView kare basina ayirma yapmiyor (tek blok, paylasimli materyal)',
      cube.count('new MaterialPropertyBlock()') == 1
      and 'new GameObject' not in method(cube, 'private void PaintAll()'),
      'idle katmani kare basina ayirma yapiyor')
check('shader yoksa duzgun dusuyor (iki view de null kontrol ediyor)',
      'LaminaMaterial() != null' in press and 'ShellMaterial() != null' in cube,
      'shader yoksa cokuyor')
for sh, name in [(shell_sh, 'PressShell'), (lamina_sh, 'PressLamina')]:
    check('%s Universal2D etiketli' % name, '"LightMode" = "Universal2D"' in sh,
          '%s 2D renderer`da cizilmez' % name)
    check('%s fallback tasiyor' % name, 'Fallback "Sprites/Default"' in sh,
          '%s fallback`siz' % name)
check('sprite takasi yok: preslenmis kup icin ayri sprite dosyasi eklenmedi',
      not os.path.exists(os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Blocks',
                                      'compressed.png')),
      'preslenmis kup sprite takasiyla yapilmis')
check('pres hicbir seyi yikmiyor: aktivasyonda kume patlamasi calismiyor',
      'if (PlayPressCompression(round))' in activation
      and 'return;' in activation, 'aktivasyonda yanlis patlama kaliyor')
check('hedefleme dort hucreyi ayri ayri boyamiyor (kendi cercevesi var)',
      'ShowPressPreview' in boardview and 'aiming is HidrolikPresPower' in drag,
      'hedefleme hala patlama rengiyle dort hucre boyuyor')
check('hedeflemede ok yok', 'Arrow' not in method(boardview,
      'public void ShowPressPreview(IReadOnlyList<GridPos> cells, bool valid)'),
      'hedeflemede ok var')

print('=== 12. ZAMANLAMA: BRIEF PENCERELERI ===')
order = [
    ('cene', 0.0, num(press, r'JawAppearDuration = ([\d.]+)f')),
    ('basinc', num(press, r'PressureBuildStart = ([\d.]+)f'),
     num(press, r'PressureBuildEnd = ([\d.]+)f')),
    ('duzlesme', num(press, r'FlattenStart = ([\d.]+)f'),
     num(press, r'FlattenEnd = ([\d.]+)f')),
    ('toplanma', num(press, r'ConvergeStart = ([\d.]+)f'),
     num(press, r'ConvergeEnd = ([\d.]+)f')),
    ('kabuk', num(press, r'ShellCloseStart = ([\d.]+)f'),
     num(press, r'ShellCloseEnd = ([\d.]+)f')),
]
ok = all(a is not None and b is not None and a < b for _, a, b in order)
check('sikistirma pencereleri sirali ve ortusuyor', ok, 'sikistirma zamanlamasi bozuk')
starts = [a for _, a, _ in order]
check('her asama oncekinin icinde basliyor (sert kesme yok)',
      all(starts[i] < starts[i + 1] for i in range(len(starts) - 1)),
      'asamalar sirayla degil')
total = num(press, r'ShellSettleEnd = ([\d.]+)f')
check('sikistirma toplami brief araliginda (0.65-0.78 sn)',
      total is not None and 0.65 <= total <= 0.78,
      'sikistirma toplami brief disinda: %s' % total)
rel = num(press, r'RestoreSettleEnd = ([\d.]+)f')
check('temiz acilma toplami brief araliginda (0.55-0.65 sn)',
      rel is not None and 0.55 <= rel <= 0.65, 'acilma toplami brief disinda: %s' % rel)
push = num(press, r'PushSettleEnd = ([\d.]+)f')
check('itmeli acilma toplami brief araliginda (0.55-0.70 sn)',
      push is not None and 0.55 <= push <= 0.70, 'itmeli acilma brief disinda: %s' % push)
check('idle dongusu brief araliginda (2.0-3.5 sn)',
      2.0 <= (num(cube, r'IdlePressurePeriod = ([\d.]+)f') or 0) <= 3.5,
      'idle dongusu brief disinda')

print('=== 13. LAB: 23 SAHNE, ANAHTARLAR, OYUNUN KAPISI ===')
scenes = re.search(r'private enum AnimPressScene\s*\{(.*?)\n        \}', lab, re.S)
names = [x.strip() for x in scenes.group(1).replace('\n', '').split(',') if x.strip()] \
    if scenes else []
check('23 pres sahnesi tanimli', len(names) == 23, 'sahne sayisi %d' % len(names))
entries = lab_raw.count('AnimPress(AnimPressScene.')
check('her sahnenin katalogda bir girisi var', entries >= 23,
      'katalog girisi %d' % entries)
check('lab GERCEK kurallari calistiriyor (Compress + Expand)',
      'board.Compress(anchor, out squeeze)' in lab
      and 'board.Expand(anchor, swallowed, out open)' in lab,
      'lab kurallari kopyalamis')
check('lab oyunun kendi dikislerinden geciyor',
      'PlayPressScene(PressSqueezeSceneOf(squeeze))' in lab
      and 'PlayPressReleaseReport(open)' in lab,
      'lab kendi animasyon kopyasini cagiriyor')
check('lab sahnesi bitisini TUTUYOR (resync etmiyor)',
      'AnimResync' not in method(lab, 'private IEnumerator PressRoutine(AnimPressScene scene)'),
      'lab sahnesi kendini sifirliyor')
check('raporun soyledigini yaziya dokuyor (etiket)',
      'private static string AnimPressLabel' in lab, 'sahne etiketi yok')
for toggle in ['ShowJaws', 'ShowLaminae', 'ShowNullImprints', 'ShowShell',
               'ShowPressureFront', 'ShowPushResponse']:
    check('lab anahtari: HydraulicPressView.%s' % toggle,
          'HydraulicPressView.Layers.' + toggle in lab_raw, '%s anahtari yok' % toggle)
for toggle in ['ShowSeams', 'ShowDimple', 'ShowScars', 'ShowShadow']:
    check('lab anahtari: CompressedCubeView.%s' % toggle,
          'CompressedCubeView.Layers.' + toggle in lab_raw, '%s anahtari yok' % toggle)
for toggle in ['ShowCollapse', 'ShowBurst', 'ShowShear', 'ShowResidue']:
    check('lab anahtari: PressureVesselView.%s' % toggle,
          'PressureVesselView.Layers.' + toggle in lab_raw, '%s anahtari yok' % toggle)
check('ham bolumdeki eski pres girisi kalkti (ve olu kodu da)',
      'RAW - Hidrolik pres' not in lab_raw and 'AnimRawScene.Press' not in lab_raw,
      'ham bolumde hala eski pres girisi/olu kodu var')
check('lab kapanisi/sifirlamasi pres sahnesini durduruyor',
      'StopAnimPress();' in method(lab, 'private void AnimResync()'),
      'resync pres sahnesini durdurmuyor')

print('=== 14. CORE TESTLERI ===')
check('rapor icin Core testi var',
      'HidrolikPres_TheSqueezeAndTheReleaseAreReportedForTheView' in tests,
      'rapor testi yok')
check('test kosucusuna bagli',
      tests.count('HidrolikPres_TheSqueezeAndTheReleaseAreReportedForTheView') >= 2,
      'test cagirilmiyor')
for what in ['patch order', 'NULL', 'Order counts outward', 'detonates - it is not rerouted',
             'opened the other way', 'footprint']:
    check('test kapsami: %s' % what, what in tests, '%s testi yok' % what)

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('hidrolik pres: hepsi tamam')
