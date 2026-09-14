# Parazit / konak kup (ParasiteHostView + ParasiteShapes + ParasiteHarness):
# kubun uzerinde yasayan simbiyotik organizma, tasidigi joker, ve baglarin kopmasi.
#
#   1. BASE CUBE: kendi rengini/materyalini KORUYOR - magenta yikama kimlik olmaktan cikti.
#   2. SARMA: kup boyanmiyor, SARILIYOR - yari saydam zar + bantlar + cekirdek.
#   3. CORE = GERCEK: her reddetme kurallardan raporlaniyor, yonuyle birlikte.
#   4. VIEW KARAR VERMIYOR: hangi kup konak, kim biniyor, hangi yonden geldi - hepsi rapordan.
#   5. YOLCU KIMLIGI: joker'in KENDI rengi; kupun materyalinden de parazitin plumundan da degil.
#   6. KENETLENME: cekirdek uyanir -> zar yayilir -> bantlar olusur -> kenar sarilir -> yolcu.
#   7. IDLE: KUP CIKMAYA CALISIR, zar yerel olarak kabarir, parazit bastirir.
#   8. KORUMA: kalkan degil; kuvvet tarafinda zar gerilir, karsi taraf sikisir.
#   9. KOPMA: zar hat yonunde YIRTILIR, bantlar sirayla kopar, yolcu gorunur olur.
#  10. YASAKLAR: neon pembe, tam hucre magenta, kalkan/baloncuk, surekli parlama, konfeti.
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


host = strip_comments(read('Assets', 'Scripts', 'View', 'ParasiteHostView.cs'))
host_raw = read('Assets', 'Scripts', 'View', 'ParasiteHostView.cs')
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'ParasiteShapes.cs'))
shapes_raw = read('Assets', 'Scripts', 'View', 'ParasiteShapes.cs')
sh = read('Assets', 'Resources', 'Shaders', 'ParasiteHarness.shader')
mem = read('Assets', 'Resources', 'Shaders', 'ParasiteMembrane.shader')
drain = read('Assets', 'Resources', 'Shaders', 'ParasiteDrain.shader')
profile = strip_comments(read('Assets', 'Scripts', 'View', 'ParasiteContrast.cs'))
profile_raw = read('Assets', 'Scripts', 'View', 'ParasiteContrast.cs')
mesh = strip_comments(read('Assets', 'Scripts', 'View', 'ParasiteMembraneMesh.cs'))
vu = strip_comments(read('Assets', 'Scripts', 'View', 'ViewUtil.cs'))
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'ParasiteVisuals.cs'))
board = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Lines.cs'))
joker = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions',
                            'ParazitJoker.cs'))
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. BASE CUBE KENDI RENGINI KORUYOR ===')
check('eski %55 magenta yikama kimlik olmaktan cikti',
      '0.85f, 0.2f, 0.85f' not in vu, 'magenta yikama hala duruyor')
check('kalan berelenme cok hafif (<= 0.2)',
      (num(vu, r'HostBruiseStrength = ([\d.]+)f') or 1) <= 0.2,
      'konak kupun kendi rengi hala boguluyor')
check('berelenme parazitin KENDI govde rengi, parlak magenta degil',
      'HostBruise = new Color(0.35f, 0.18f, 0.38f)' in vu,
      'berelenme rengi parazitin paletinde degil')
check('parazit ayri katman olarak ciziliyor',
      'class ParasiteHostView' in host and 'public ParasiteHostView Parasite' in boardview,
      'parazit hala kupun rengine karisiyor')

# THE FAILURES THIS SECTION EXISTS FOR, in order: a magenta wash on the cube ("this one is pink"),
# then rounded-rectangle parts bolted to the corners ("a UI lock"). The cube is not MARKED and it is
# not CLAMPED - it is WRAPPED, by a translucent film that does not let go.
print('=== 2. SARMA: ZAR ANA KATMAN ===')
check('zar var ve KENDI shaderi ile ciziliyor',
      'ParasiteMembrane' in mem and 'MembraneMaterial()' in host, 'zar yok')
check('zar bir MESH uzerinde - yerel bulge icin',
      'ParasiteMembraneMesh' in host and 'MeshFilter' in host and 'Subdivisions' in mesh,
      'zar tek quad - yerel bulge yapamaz')
check('mesh bolunmus (>= 4x4)', (num(mesh, r'Subdivisions = (\d+)') or 0) >= 4,
      'mesh yeterince bolunmemis')
check('zarin konturu KARE DEGIL: dusuk frekansli loblar',
      'ContourRadius' in mem and 'lobes' in mem, 'zar kare')
check('zar yari saydam (opaklik <= 0.7)',
      (num(host, r'MembraneOpacity = ([\d.]+)f') or 1) <= 0.7, 'zar opak, kupu kapatiyor')
check('zarin kalinligi degisiyor; INCE yerlerde kupun rengi geciyor',
      '_Thick' in mem and 'thickness' in mem, 'zar duz opaklikta')
check('zar kenardan ASAGI sarkiyor (wrap)',
      '_EdgeWrap' in mem and 'MembraneEdgeWrap' in host, 'zar yuzeyde duruyor, sarmiyor')
check('zar kupten buyuk ciziliyor ki sarabilsin',
      (num(host, r'MembraneOversize = ([\d.]+)f') or 0) > 1.0, 'zar kupten kucuk')

# THE TWO THINGS THAT MAKE IT A PARASITE RATHER THAN AN OVERLAY: the block is visibly LOSING ITS
# COLOUR under the wrap, and the wrap has DEAD regions. Without these the film reads as clean glass,
# which is exactly what the previous pass looked like.
print('=== 2a. RENK EMME + NEKROTIK YAMALAR ===')
check('kupun rengi YEREL olarak emiliyor (ayri drain katmani)',
      'ParasiteDrain' in drain and 'DrainMaterial()' in host, 'renk emme yok')
check('emme kupun KENDI spritei uzerinden - global tint degil',
      '_MainTex' in drain and 'h.Look.Colour' in host, 'emme global tint')
check('emme zarin KALINLIGINI takip ediyor (kalin yerde cok, ince yerde az)',
      'covered * thickness' in drain and 'float coverage = pow(' in drain,
      'emme her yerde ayni')
check('emme zarin KONTURUYLA maskeleniyor',
      'ContourRadius' in drain and 'covered' in drain, 'emme tum kupu kapliyor')
# Checked by SHAPE: saturation is only partly taken (a lerp, not a replace) and what is left is
# leaned toward the dead colour rather than toward grey or black.
check('griye/siyaha gitmiyor: doygunluk kismen aliniyor, kalan olu mauve`a yasliyor',
      '_Dead' in drain and 'lerp(tex.rgb, lum.xxx, _Desat * strength)' in drain
      and 'lerp(c, _Dead.rgb' in drain, 'kup griye donuyor')
check('kup bastirinca rengi GERI geliyor', '_Relief' in drain and 'ReliefId2' in host,
      'bulge`da renk geri gelmiyor')
check('mucadeleden sonra o bolge biraz DAHA emilmis kaliyor',
      '_Stain' in drain and 'AfterDrainStrength' in host and 'h.StainClock' in host,
      'mucadelenin bedeli yok')
check('nekrotik yamalar var (2-4, pisirilmis, duzensiz)',
      'ParasiteShapes.Patch(' in host and 'BakePatch' in shapes,
      'nekrotik yama yok')
check('yamalar kare degil: isirik alinmis organik lob',
      'Mathf.Min(d, -bite' in shapes, 'yama disk/kare')
check('yamalar mat ve neredeyse isiksiz (olu doku)',
      'PaintHarness(r, 0.12f, 0.2f, 0f, 0f, -1f, -1f' in host, 'yamalar parlak')
check('yama sayisi 2-4', (num(host, r'PatchCountMin = (\d+)') or 0) >= 2
      and (num(host, r'PatchCountMax = (\d+)') or 9) <= 4, 'yama sayisi brief disinda')
check('yamalar host basina STABIL secılıyor (her kare rastgele degil)',
      'Random01(host.Cell, 100 + i)' in host, 'yamalar her kare degisiyor')
check('damarlar IKINCIL: ince ve dusuk opaklik',
      (num(host, r'VeinWidth = ([\d.]+)f') or 1) <= 0.03
      and (num(host, r'VeinOpacity = ([\d.]+)f') or 1) <= 0.6, 'damarlar baskin')
check('damarlar egri ve uca dogru inceliyor',
      'PaintVein' in host and '1f - t * 0.6f' in host, 'damarlar duz cizgi')
check('palet KOYU: zar cam gibi acik degil',
      'DeepPlum = new Color(0.12f' in host and 'BodyViolet = new Color(0.26f' in host,
      'palet hala acik')
check('zar parlakligi dusuk (cam olmasin)',
      (num(host, r'MembraneSheen = ([\d.]+)f') or 1) <= 0.2, 'zar hala parliyor')

# THE FAILURE THIS SECTION EXISTS FOR: a dark plum parasite on an OBSIDIAN cube is dark-on-dark and
# the whole organism disappears. One fixed palette means the mechanic is invisible on one of the two
# blocks that most need reading.
print('=== 2d. TABANA GORE KONTRAST (OBSIDYEN) ===')
check('palet konagin materyaline gore uretiliyor',
      'ParasiteContrastProfile' in profile and 'h.Contrast' in host,
      'tek sabit palet - obsidyende kayboluyor')
check('karanlik taban icin ayri (ash/lilac) degerler var',
      'AshSkin' in profile and 'AshEdge' in profile and 'AshPatchEdge' in profile,
      'karanlik taban icin acilma yok')
check('karanlik olcusu luminance`tan turetiliyor',
      'darkness = Mathf.Clamp01(1f - lum' in profile, 'karanlik olcusu yok')
check('yalniz KONTRAST oynuyor, ton ailesi degil (hepsi plum/mauve lerp)',
      profile.count('Color.Lerp(Base') >= 6, 'karanlik tabanda hue degisiyor')
check('neon degil: karanlik tabanda bile beyaza gitmiyor',
      'Color.Lerp(c.Edge, Color.white, 0.08f' in profile, 'karanlik tabanda beyaz rim')
check('kontur rimi yalniz karanlik tabanda aciliyor',
      'RimStrength = darkness * 0.85f' in profile, 'rim her yerde acik')
check('zar ve harness rimi shaderda destekliyor',
      '_Rim' in mem and '_Rim' in sh and 'RimId' in host, 'rim shaderda yok')
check('harness rimi siluetin KENDI konturunu kullaniyor (alpha rampasi)',
      'tex.a * (1.0 - tex.a) * 4.0' in sh, 'rim dikdortgen kenardan')
check('nekrotik yamanin kendi kenar isigi var (obsidyende tek ayirici)',
      'pc.Edge = pc.PatchEdge' in host, 'yama kenari yok')
check('emme de tabana gore olceklenıyor (siyah kupte emilecek renk yok)',
      'DrainScale' in profile and 'h.Contrast.DrainScale' in host,
      'emme her tabanda ayni')
check('lab obsidyen icin ayri hero testleri tasiyor',
      'IdleObsidian' in lab and 'TearObsidian' in lab, 'obsidyen testi yok')

print('=== 2e. EMME GERCEKTEN OLDURUYOR ===')
check('emme kaplamaya gore KADEMELI (curve > 1)',
      '_Curve' in drain and (num(host, r'DrainCoverageCurve = ([\d.]+)f') or 0) > 1.0,
      'emme her yerde ayni siddette')
check('materyalin HIGHLIGHT`i da soneruyor - sadece renk filtresi degil',
      '_Kill' in drain and 'DrainHighlightKill' in host
      and "A cube's life is in its highlight" in open(
          os.path.join(ROOT, 'Assets', 'Resources', 'Shaders', 'ParasiteDrain.shader'),
          encoding='utf-8').read(),
      'yuzeyin canlılığı sonmuyor')
check('emme guclu (>= 0.8)', (num(host, r'DrainStrength = ([\d.]+)f') or 0) >= 0.8,
      'emme cok zayif - kup hala saglikli')
check('doygunluk kaybi belirgin (>= 0.7)',
      (num(host, r'DrainSaturationLoss = ([\d.]+)f') or 0) >= 0.7, 'doygunluk kaybi zayif')

print('=== 2f. KALIN KIVRIMLAR + NEGATIF ALAN + KENAR KIVRILMASI ===')
check('kalin sargi kivrimlari var',
      '_FoldA' in mem and 'FoldShade(' in mem and 'FoldFor(' in host,
      'kivrim yok - zar hala dumduz')
check('kivrimlar ZARIN ICINDE cizilmis, uzerine KONMAMIS',
      'PaintFold(' not in host and 'FoldOrder' not in host and 'FoldSegment' not in shapes,
      'kivrim ayri parca olarak ciziliyor - ust uste binen segment zinciri BORUDUR, '
      've kubu gecen bir boru kablodur')
check('kivrimin kendi silueti YOK: kalinlik + isik (cunku kivrim zarin kendisi)',
      'thickness *= 1.0 + swell' in mem and 'foldLight' in mem, 'kivrim ayri bir cisim')
check('kivrimin isikli yanagi ve KIRISIGI var (simetrik highlight = boru)',
      'crease' in mem and 'lit' in mem and 'fall' in mem, 'kivrim silindir gibi')
check('gerginlik kivrimlari DUZLESTIRIYOR (gergin carsafta kivrim az olur)',
      'FoldTensionFlatten' in host and 'FoldVec(' in host, 'gerginlik kivrimi etkilemiyor')
check('emme de kivrimi takip ediyor (kup en cok kivrimin altinda oluyor)',
      'FoldSwell(' in drain and 'swell' in drain, 'kivrim ve emme farkli yerlerde')
check('kivrim rengi zarin KENDI rengi (ustune baska renk konmuyor)',
      'lerp(1.0, foldLight' in mem, 'kivrim ayri renk')
check('zarda NEGATIF ALAN var (kup oradan kendisi gorunuyor)',
      '_WindowA' in mem and '_WindowB' in mem and 'WindowVec(' in host,
      'zar delisksiz - hucre uzerinde filtre gibi')
check('delikte EMME de yok (kup rengini koruyor)',
      '_WindowA' in drain and 'holes' in drain, 'delikte kup yine de oluyor')
check('deligin kenarinda kalinlasan bir dudak var (delgec degil)',
      'lipRing' in mem, 'delik zimbayla acilmis gibi')
check('delik YUVARLAK DEGIL: kendi lobleri var',
      'WindowAt(' in mem and 'WindowAt(' in drain, 'delik daire - durum lambasi gibi okunuyor')
check('tek bir harmonik baskin degil (3 lob = yonca, daireden beter)',
      mem.count('sin(a * ') >= 3 and '0.15 * sin(a * 2.0' in mem, 'delik yonca sekilli')
check('iki delik ESIT DEGIL (yarik ve cizik, bir cift lamba degil)',
      'WindowSecondScale' in host, 'iki delik ayni boyda')
check('delikte kup TAM iyilesmiyor (parlak doygun disk = lamba)',
      '1.0 - 0.72 * smoothstep' in drain, 'delikte kup piril piril')
check('kenar kivrilmasi ESIT DEGIL (her yerde ayni bant = cerceve)',
      '_Curl' in mem and 'wrapHere' in mem, 'kenar bandi her yerde ayni')

print('=== 2g. YUVA BIR KABUK, YOLCU KATMANLI ===')
check('yuvanin buyume sirtlari var (tek highlight`li boncuk degil)',
      '_Ridges' in sh and 'CoreRidges' in host, 'yuva duz boncuk')
check('yuvanin KENDI kontur isigi var (karanlik kupte kayboluyordu)',
      'nest.RimStrength' in host and 'CoreRimBoost' in host, 'yuva karanlikta kayboluyor')
check('yuva onceki halinden buyuk (>= 0.25)',
      (num(host, r'CoreSize = ([\d.]+)f') or 0) >= 0.25, 'yuva hala kucuk')
check('yolcu UC KATMAN (dis kabuk / kendi rengi / soluk ic)',
      'EssenceOuter' in host and 'EssenceInner' in host and 'PaintEssenceShell' in host,
      'yolcu tek duz nokta')
check('uc katman da yolcunun KENDI renginden turuyor',
      'Style.EssenceOuterDarken' in host and 'Style.EssenceInnerLighten' in host,
      'katmanlar siyaha/beyaza gidiyor - kimlik kayboluyor')
check('her kabugun kendi siralamasi var',
      'EssenceOuterOrder' in host and 'EssenceInnerOrder' in host,
      'ayni sortingOrder - hangisi ustte belirsiz')

print('=== 2b. SARMA BANTLARI + CEKIRDEK ===')
check('2-4 bant (kose basina bir tane DEGIL)',
      2 <= (num(host, r'RibCount = (\d+)') or 0) <= 4, 'bant sayisi yanlis')
check('bantlar yuzu CAPRAZ geciyor, koseden merkeze degil',
      'rib.From' in host and 'rib.To' in host and 'Mathf.Cos(a)' in host,
      'bantlar koseden merkeze')
check('bantlar egri: bezier uzerinde segment zinciri',
      'Bezier(' in host and 'RibSegments' in host, 'bantlar duz')
check('bant ortada kalin, uclarda ince',
      'RibEndWidth' in host and 'RibMidWidth' in host, 'bant sabit kalinlikta')
check('cekirdek tam merkezde DEGIL', 'CoreOffset' in host and 'CoreOffsetOf' in host,
      'cekirdek tam merkezde')
check('cekirdek organik silueti kullaniyor', 'ParasiteShapes.Heart' in host, 'cekirdek daire')
check('parazit kubu bogmuyor: cekirdek kupun ~1/4`i',
      0.18 <= (num(host, r'CoreSize = ([\d.]+)f') or 1) <= 0.28, 'cekirdek kupu kapatiyor')
check('oz okunacak kadar buyuk',
      0.08 <= (num(host, r'EssenceSize = ([\d.]+)f') or 1) <= 0.15, 'oz nokta gibi')
check('parcalar ViewUtil.RoundedSprite ile CIZILMIYOR',
      'ViewUtil.RoundedSprite' not in host, 'parazit hala yuvarlak dikdortgen olcekliyor')

print('=== 2c. IDLE: KUP CIKMAYA CALISIYOR, PARAZIT BASTIRIYOR ===')
check('yerel bulge var (zarin bir bolgesi kalkiyor)',
      '_Bulge' in mem and 'IdleBulgeStrength' in host, 'yerel bulge yok')
check('bulge vertex asamasinda - gercek deformasyon',
      'pos.xy += away * lift' in mem, 'bulge yalniz renkte')
check('bulge bolgesinde zar INCELIYOR (kupun rengi gorunuyor)',
      'thickness *= 1.0 - saturate(input.lift)' in mem, 'bulge zari incelmiyor')
check('bulge onceden tanimli bolgelerden seciliyor, her kare rastgele degil',
      'BulgeRegions' in host, 'bulge her kare rastgele')
check('kup cok az yukleniyor (<= %2), hucreden cikmiyor',
      (num(host, r'IdleCubeLoad = ([\d.]+)f') or 1) <= 0.02, 'kup hucreden cikiyor')
check('bantlar bulge`a cevap veriyor', 'IdleRibTension' in host, 'bantlar tepkisiz')
check('cekirdek TERS yone cekiliyor (tutuyor)',
      'IdleCoreCounter' in host and 'offset -= bulgeDir.normalized' in host,
      'cekirdek tutmuyor')
check('yolcu bulge aninda canlaniyor', 'IdleEssenceBoost' in host, 'yolcu tepkisiz')
check('sonra parazit BASTIRIYOR (restrain fazi)',
      'restrain' in host and 'the wrap wins and presses it back' in host_raw,
      'bastirma fazi yok')
check('idle seyrek (>= 2.5 sn)',
      (num(host, r'IdleMinInterval = ([\d.]+)f') or 0) >= 2.5, 'idle cok sik')
check('ikinci, daha seyrek dolasim idle`i var',
      'CirculationMinInterval' in host and '_Vein' in mem, 'ikinci idle yok')
check('idle`da parcacik yok',
      'Flecks.Add' not in method(host, 'private void Update()'), 'idle parcacik uretiyor')

check('yakalama BEDELI var: geri bastirinca tum yuz bir an soluyor',
      'WitherPulseStrength' in host and 'WitherClock' in host,
      'mucadelenin bedeli yok - idle bir tik gibi')
check('her mucadele KALICI bir iz birakiyor (birikimli, tavanli)',
      'StainDepth' in host and 'AfterDrainGain' in host and 'AfterDrainMax' in host,
      'iz kaliyor ama birikmiyor')
check('iz cok yavas siliniyor (yara izi, flas degil)',
      (num(host, r'AfterDrainDecay = ([\d.]+)f') or 1) <= 0.05, 'iz cok cabuk gidiyor')

print('=== 3. CORE = GERCEK, RAPORDAN ===')
check('reddetme tipi Core`da tanimli', 'enum HostRefusalKind' in visuals, 'reddetme tipi yok')
check('reddetme hucre + tur + YON tasiyor',
      all(k in visuals for k in ['public GridPos Cell;', 'public HostRefusalKind Kind;',
                                 'public GridPos Step;', 'public bool HasDirection;']),
      'reddetme raporu eksik')
check('rapor merkezi bogazlardan yaziliyor (DestroyCube / DestroyCubeForced)',
      board.count('HostRefusals.Add') == 2, 'reddetmeler her cagri yerinde ayri yaziliyor')
check('yonu olmayan deneme yon UYDURMUYOR', 'HasDirection = false' in board,
      'yonsuz deneme yon uyduruyor')
check('yolcu kimligi Core`dan',
      'struct BoundJokerIdentity' in visuals and 'PassengerIdentity' in joker,
      'yolcu kimligi View`da turetiliyor')
check('konak hucresi jokerden', 'public GridPos? HostPosition' in joker,
      'View hangi kupun konak oldugunu kendi buluyor')
check('View konak aramiyor, joker soyluyor', 'parasite.HostPosition' in fb, 'View konak ariyor')
check('reddetme rapordan geciyor', 'round.MainBoard.HostRefusals.Refusals' in fb,
      'reddetme View`da uretiliyor')

print('=== 4. YOLCU KIMLIGI ===')
check('oz rengi joker DEF ID`sinden',
      'PassengerColour' in host and 'passenger.DefId' in host, 'yolcu rengi kimlikten gelmiyor')
check('yolcu rengi kupun materyalinden ALINMIYOR',
      'CubeMaterialColor' not in host, 'yolcu rengi kupun materyalinden')
check('yolcu yoksa parazitin plumu KULLANILMIYOR (bos soket gibi okunmasin)',
      'UnknownPassenger = new Color(0.78f, 0.6f, 0.3f)' in host,
      'yolcu yoksa plum kullaniliyor')

print('=== 5. KENETLENME (ENVELOPMENT) ===')
check('parazit pop etmiyor: asamalar var',
      all(k in host for k in ['SeatCoreWake', 'SeatMembraneSpread', 'SeatRibForm',
                              'SeatEdgeGrip', 'SeatEssence', 'SeatFinalClamp']),
      'parazit tek karede beliriyor')
check('once CEKIRDEK uyaniyor', 'SeatCoreStartScale' in host, 'cekirdek fade-in ediyor')
check('zar cekirdekten YAYILIYOR',
      'float spread = Ease(Span(h.SeatClock, Style.SeatCoreWake' in host,
      'zar hazir beliriyor')
check('bantlar zarin icinden yukseliyor', 'SeatRibStagger' in host, 'bantlar ayni anda')
check('zar sonra kenarlari sariyor', 'SeatEdgeGrip' in host, 'kenar sarma yok')
check('yolcu EN SON uyaniyor', 'float ignite = Ease(Span(h.SeatClock,' in host,
      'yolcu once uyaniyor')
check('kup nefes almiyor: Paint kupun olcegine dokunmuyor',
      'localScale' not in method(host, 'private void Paint(HostPiece h)'),
      'kup olcekleniyor')

print('=== 6. KORUMA: KALKAN DEGIL, SIKI SARMA ===')
for word in ['Shield', 'shield', 'Bubble', 'bubble', 'Aura', 'aura']:
    check('koruma dilinde "%s" yok' % word, word not in host,
          'korumada kalkan/baloncuk var: %s' % word)
check('kuvvet yonu onemli', 'h.ClampFrom' in host and 'ClampMembraneStretch' in host,
      'koruma yonsuz')
check('kuvvet tarafinda zar geriliyor, karsi taraf sikisyor',
      'ClampMembraneStretch' in host and 'ClampOppositeTighten' in host,
      'zar yonlu cevap vermiyor')
check('bantlar geriliyor, cekirdek sikisyor',
      'ClampRibTension' in host and 'ClampCoreCompression' in host,
      'koruma tek ozellik oynatiyor')
check('kup kuvvete cok az yukleniyor (<= %3)',
      (num(host, r'ClampCubeLoad = ([\d.]+)f') or 1) <= 0.03, 'kup hucreden cikiyor')
check('zorla tasima ayri koreografi', 'GroundLockLoad' in host and 'ClampIsMove' in host,
      'zorla tasima ayrilmamis')
check('korumada parcacik yok',
      'Flecks.Add' not in method(host, 'public void PlayRefusal(Refusal refusal)'),
      'koruma parcacik uretiyor')

print('=== 7. KOPMA: ZAR YIRTILIYOR ===')
check('zar hat yonunde YIRTILIYOR', '_Tear' in mem and 'h.TearDir' in host, 'zar yirtilmiyor')
check('yirtik duz lazer kesigi degil: kontur duzensiz', 'ragged' in mem, 'yirtik duz cizgi')
check('yirtigin yonu Core`un hattindan', 'report.ExplodedRows' in fb,
      'yirtik yonu View`da uyduruluyor')
check('parazit ONCE son kez sikiyor', 'RuptureFinalGrip' in host, 'son kavrama yok')
check('bantlar SIRAYLA kopuyor', 'RuptureRibStagger' in host, 'hepsi ayni anda kopuyor')
check('bant ORTADAN kopuyor, iki yari kendi ucuna cekiliyor',
      'rib.BreakAt' in host and 'the strand parting' in host_raw, 'bant yok oluyor')
check('kopma noktasindaki segmentler ONCE inceliyor',
      'thin to nothing first' in host_raw, 'bant aniden kayboluyor')
check('zar cekirdege dogru geri toplaniyor (peel)',
      'RupturePeelDistance' in host and 'peel' in host, 'zar eriyip akiyor')
check('cekirdek aciga cikiyor ve yolcu GORUNUR oluyor',
      'RuptureCoreReveal' in host and 'RuptureEssenceReveal' in host,
      'yolcu sessizce kayboluyor')
check('yolcu kendi rengiyle ICE cokuyor, patlamiyor',
      'RuptureEssenceCollapse' in host and 'toPlum' in host, 'yolcu patliyor')
check('parcacik butcesi kucuk (kopma <= 6, oz <= 5)',
      (num(host, r'RuptureFleckCount = (\d+)') or 99) <= 6
      and (num(host, r'CoreMoteCount = (\d+)') or 99) <= 5, 'parcacik butcesi asiliyor')
check('kalinti kisa (<= 0.2 sn)',
      (num(host, r'RuptureResidue = ([\d.]+)f') or 1) <= 0.2, 'kalinti uzun')
# ClusterBurstView.Look is a TYPE NAME the host carries, not an explosion of its own.
check('kopma ikinci bir patlama uretmiyor',
      'Explosion' not in host and 'BurstView.Play' not in host
      and 'FlashCells' not in host, 'kopma kendi patlamasini uretiyor')

check('zar yirtilirken RENK GERI GELIYOR (bir anlik, sonra hat aliyor)',
      'DeathColourReturn' in host and 'release' in method(host, 'private void PaintDrain('),
      'olurken renk geri gelmiyor')
check('o geri donus SNAP (yavas fade = efekt kapaniyor demek)',
      (num(host, r'DeathColourReturn = ([\d.]+)f') or 1) <= 0.08,
      'renk yavas donuyor - efekt sonu gibi')

print('=== 8. YASAKLAR / MALZEME ===')
check('palet plum/violet/rose; sabit magenta yok',
      all(k in host for k in ['DeepPlum', 'BodyViolet', 'WarmRose'])
      and 'Color.magenta' not in host, 'palet disina cikilmis')
check('harness surekli parlamiyor: sheen yalniz olayda',
      '_Sheen' in sh and 'negative for none' in sh, 'harness surekli parliyor')
check('emissive degil: shader kendi paletinden lerpliyor',
      'lerp(_Deep.rgb, _Body.rgb' in sh, 'harness emissive')
check('rastgelelik yok (lab kare kare karsilastirilabilsin)',
      'Random.' not in host and 'Random01' in host, 'Random kullaniliyor')
check('havuzlu renderer',
      'private SpriteRenderer Rent(' in host and 'private void Return(' in host, 'havuz yok')
# Two shared materials, one per shader (harness + membrane) - never one PER HOST.
check('materyal shader basina bir tane, host basina degil',
      host.count('new Material(') <= 3, 'materyal ornegi uretiyor')
check('palet host basina HESAPLANIYOR, materyal klonlanmiyor',
      'ParasiteContrastProfile.For' in host, 'palet host basina materyal uretiyor')
check('siluetler BIR KEZ pisiyor, host basina degil',
      'if (heart == null)' in shapes and 'hideFlags = HideFlags.HideAndDontSave' in shapes,
      'siluet her host icin yeniden uretiliyor')
check('MaterialPropertyBlock ile ciziliyor', 'MaterialPropertyBlock' in host,
      'per-renderer materyal')
check('shader yoksa duzgun dusuyor', 'No shader: flat plum' in host_raw, 'shader yoksa cokuyor')
check('iki shader de Universal2D',
      '"LightMode" = "Universal2D"' in sh and '"LightMode" = "Universal2D"' in mem,
      'shader 2D renderer`da cizilmez')
check('yuz koordinati sprite`in kendi olcusunden',
      '_FaceHalf' in sh and 'positionOS.xy / max(_FaceHalf' in sh,
      'tek-birim sprite varsayiyor')

print('=== 9. SIRALAMA ===')
order = [num(host, name + r' = (\d+)') for name in
         ['DrainOrder', 'ShadowOrder', 'MembraneOrder', 'RibOrder', 'CoreOrder',
          'EssenceOrder']]
check('emme < golge < zar < bant < cekirdek < oz',
      all(order[i] < order[i + 1] for i in range(len(order) - 1)),
      'siralama yanlis: %s' % order)

print('=== 10. LAB + TESTLER ===')
scenes = re.search(r'private enum AnimHostScene\s*\{(.*?)\n        \}', lab, re.S)
names = [x.strip() for x in scenes.group(1).replace('\n', '').split(',') if x.strip()] \
    if scenes else []
# 15 base scenes plus the two OBSIDIAN hero tests the dark-on-dark problem needs.
check('konak sahneleri tam (>= 26)', len(names) >= 26, 'sahne sayisi %d' % len(names))
check('lab GERCEK kurallari calistiriyor',
      'board.DestroyCube(cell)' in lab and 'board.DestroyCubeForced(cell)' in lab,
      'lab reddetmeyi taklit ediyor')
check('lab kurallarin KENDI raporunu oynatiyor', 'board.HostRefusals.Refusals' in lab,
      'lab kendi reddetmesini uyduruyor')
check('lab konagi kurallarla isaretliyor', 'board.SetCubeProtected(cell)' in lab,
      'lab korumayi kendi taklit ediyor')
check('yolcu kimlik testi birden fazla joker geziyor',
      'AnimHostPassengers' in lab and lab.count('"robot_supurge"') >= 1, 'yolcu testi tek renk')
for toggle in ['ShowMembrane', 'ShowFolds', 'ShowWindows', 'ShowPatches', 'ShowVeins',
               'ShowRibs', 'ShowCore', 'ShowEssence', 'ShowDeformation', 'ShowShadows']:
    check('lab anahtari: %s' % toggle, 'ParasiteHostView.Layers.' + toggle in lab_raw,
          '%s anahtari yok' % toggle)
check('lab emmeyi dort kademede gosteriyor (0 / ince / orta / agir)',
      all(k in lab for k in ['DrainNone', 'DrainThin', 'DrainMedium', 'DrainHeavy']),
      'emme kademeleri lab`da yok')
check('lab her katmani TEK BASINA gosterebiliyor',
      all(k in lab for k in ['MembraneOnly', 'PatchesOnly', 'FoldsOnly', 'NestOnly']),
      'katmanlar tek tek yargilanamiyor')
check('lab tum yasami OBSIDYEN uzerinde de oynatiyor',
      'LifecycleObsidian' in lab, 'tam yasam yalniz aydinlik kupte')
check('kademe/tek-katman testleri konak KURULMADAN once ayarlaniyor',
      'AnimHostSetup(scene);' in method(lab, 'private IEnumerator HostRoutine('),
      'kapatilan katman yine de kiralaniyor')
check('AllOn emme kademesini de geri aliyor',
      'DrainOverride = -1f;' in method(host, 'public static void AllOn()'),
      'lab kademesi lab`dan cikinca kaliyor')
check('ham bolumdeki eski parazit girisi kalkti',
      'RAW - Parazit' not in lab_raw and 'AnimRawScene.Parasite' not in lab_raw,
      'ham bolumde hala eski parazit girisi var')
check('resync konak sahnesini durduruyor',
      'StopAnimHost();' in method(lab, 'private void AnimResync()'),
      'resync konak sahnesini durdurmuyor')
check('rapor icin Core testi var', 'Parazit_EveryRefusalIsReportedForTheView' in tests,
      'rapor testi yok')
check('test kosucusuna bagli',
      tests.count('Parazit_EveryRefusalIsReportedForTheView') >= 2, 'test cagirilmiyor')
for what in ['a destroy has no direction', 'the direction the force came from',
             "the parasite's one real weakness"]:
    check('test kapsami: %s' % what, what in tests, '%s testi yok' % what)

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('parazit / konak kup: hepsi tamam')
