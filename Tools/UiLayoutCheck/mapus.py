# -*- coding: utf-8 -*-
# MAPUS / MUHURLU HUCRE - statik kontrol.
#
# Bu dosya, "Mapus bir hucreyi baska renge boyamak DEGILDIR" tasariminin kodda hala duruyor
# oldugunu her calistirmada dogrular. Kontrol ettigi seyler kural degil, TASARIM KARARLARI: hangi
# primitif kullanildigi, neyin View'da hesaplanmadigi, hangi tuzaklara geri dusulmedigi.
#
# Unity calistirmadan bakabildigim tek gercek kanit derleyici ve bu dosya; ekran goruntusunu
# kullanici veriyor.
from __future__ import print_function
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8').read()


def strip_comments(src):
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return '\n'.join(l for l in src.split('\n')
                     if not l.strip().startswith('//') and not l.strip().startswith('///'))


def method(src, signature):
    i = src.index(signature)
    depth = 0
    started = False
    for j in range(i, len(src)):
        if src[j] == '{':
            depth += 1
            started = True
        elif src[j] == '}':
            depth -= 1
            if started and depth == 0:
                return src[i:j + 1]
    return src[i:]


def num(src, pattern):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else None


fail = []


def check(label, ok, why):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


boss = strip_comments(read('Assets', 'Scripts', 'Core', 'Bosses', 'Definitions',
                           'HarassBosses.cs'))
boss_raw = read('Assets', 'Scripts', 'Core', 'Bosses', 'Definitions', 'HarassBosses.cs')
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Bosses', 'MapusVisuals.cs'))
queries = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Queries.cs'))
view = strip_comments(read('Assets', 'Scripts', 'View', 'MapusSealView.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'MapusSealView.cs')
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'MapusShapes.cs'))
shapes_raw = read('Assets', 'Scripts', 'View', 'MapusShapes.cs')
pit = read('Assets', 'Resources', 'Shaders', 'MapusPit.shader')
iron = read('Assets', 'Resources', 'Shaders', 'MapusIron.shader')
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. HUCRE BOYANMIYOR, CUKURA CEVRILIYOR ===')
check('cukur icin ayri bir shader var',
      'ProjectBlock/MapusPit' in pit and 'PitMaterial()' in view,
      'hucre hala duz renk')
check('derinlik DELIK gibi isikli (kubbe degil): uzak ic duvar aydinlik',
      'toLight' in pit and 'saturate(-facing)' in pit and 'saturate(facing)' in pit,
      'cukur kubbe gibi golgeleniyor')
check('bosluk SIYAH degil, kendi (soguk indigo) tonu var',
      num(view, r'PitVoid = new Color\(([\d.]+)f') is not None
      and num(view, r'PitVoid = new Color\(([\d.]+)f') > 0.02,
      'ortada saf siyah disk - render`da delik gibi')
check('eski duz mavi hucre rengi karartildi (artik yalnizca cokerken gorunuyor)',
      (num(boardview, r'SealedColor = new Color\(([\d.]+)f') or 1) < 0.12,
      'hucre hala mavi boyaniyor')

print('=== 2. DEMIR: POLIGON, DAIRE DEGIL ===')
check('siluetler POLIGON primitifiyle pisiyor (dovme demir)',
      'private static float Poly(' in shapes and 'Poly(x, y, shaft' in shapes,
      'demir kaynasmis dairelerden - organik cikar, cicek gibi okunur')
check('kaburganin kancasi var ve HEPSI AYNI YONE doniyor (iris, pusula gulu degil)',
      'hook' in shapes and 'iris' in shapes_raw.lower(),
      'dort uc simetrik ic gosteriyor - pusula gulu')
root = num(shapes, r'(-0\.9\d)f, -1\.00f,')
check('her siluet KENDI KUTUSUNU dolduruyor (View`daki sayilar ne diyorsa o)',
      root is not None and abs(root) >= 0.85,
      'sekil kutusunun yarisinda - her tuning sayisi yarisi kadar cizer')
check('LATCH LIP kucuk: sank genisligini gecmiyor',
      'float lipOut = head + 0.22f' in shapes,
      'uc genisliyor - siluet ortada dar iki ucu genis: bu bir PERVANE KANADI')
check('sürgü NEREDEYSE PARALEL kenarli (uca dogru sivrilen = yine blade)',
      'float shank = 0.74f' in shapes and 'float neck = 0.62f' in shapes,
      'sürgü uca dogru sivriliyor - dort tanesi rotor olur')
check('KUTLE KENARDA: yuva sürgüden GENIS',
      (num(view, r'SocketWidth = ([\d.]+)f') or 0)
      > 1.5 * (num(view, r'RibWidth = ([\d.]+)f') or 1),
      'kütle merkeze yakin - dort parca gobekte bulusuyor: ROTOR')
check('merkezde gercek bir bosluk kaliyor (hub degil)',
      (num(view, r'RibReach = ([\d.]+)f') or 1) <= 0.34,
      'sürgüler merkezde bulusuyor - hub')
check('surgu SLIDE ediyor, 90 derece acilmiyor',
      (num(view, r'SpawnRibTilt = ([\d.]+)f') or 90) <= 5,
      'petal gibi aciliyor')
check('CIFTLER sirayla kapaniyor (dort parca ayni anda = fan)',
      'SpawnPairOffset' in view and 'IdlePairDelay' in view,
      'dort surgu ayni anda hareket ediyor')
check('PERVANE TESTI kodda var (siluet, malzeme degil)',
      'SilhouetteTest' in view and '_Debug' in iron,
      'silueti tek basina gorecek bir mod yok - iki pas malzeme ayarlayarak kaybedildi')
check('KENAR KUTLESI testi kodda var',
      'MassTest' in view and '_MassKind' in iron, 'kutle dagilimi gozle olculemiyor')
check('kanca tuzagi yorumda yaziyor (fan blade / shuriken)',
      'fan blade' in shapes_raw and 'shuriken' in shapes_raw,
      'sonraki pas ayni pervane tuzagina duser')
check('surgunun BASI bloklu ve sanktan dar (flare = blade)',
      'head, 0.70f' in shapes and 'head - 0.02f, reach' in shapes,
      'surgunun ucu genisliyor')
check('damga HARF DEGIL: ic ice kirik yaylar',
      'Arc(' in shapes and 'guilloche' in shapes_raw.lower()
      and 'Ring(' not in shapes,
      'damga bir glife benziyor')
check('damga tuzaklari yorumda yaziyor (G harfi, tally, anahtar deligi)',
      'letter G' in shapes_raw and 'keyhole' in shapes_raw,
      'gelecek pas ayni glif tuzagina duser')
check('kilit ikonu / asma kilit yok',
      'padlock' not in shapes.lower() and 'padlock' not in view.lower(),
      'kilit ikonu')

check('demir tahtadan ACIK: govde degeri board`un uzerinde',
      (num(view, r'IronBody = new Color\(([\d.]+)f') or 0) >= 0.20,
      'demir board kadar koyu - dort duz siyah sekil')
check('kaburga demirin ust ucunda duruyor',
      (num(view, r'RibTone = ([\d.]+)f') or 0) >= 0.8, 'kaburga cok koyu')
check('kaburganin PIT`e dusen temas golgesi var (uzerine basilmis degil, kapanmis)',
      'RibShadows' in view and 'RibShadowStrength' in view, 'kaburga hucreye basilmis gibi')
check('kaburganin ortasinda oluk var (duz poligon hissini kirar)',
      '_Groove' in iron and 'RibGroove' in view, 'kaburga duz poligon')

print('=== 3. ISIK DUNYA UZAYINDA (dort kaburga TEK mekanizma) ===')
check('demir shaderi dunya uzayinda isikliyor',
      'worldRight' in iron and 'TransformObjectToWorldDir' in iron,
      'her kaburga kendi yonunden isikli - dort ayri cisim gibi okunur')
check('yuk demiri KOYULASTIRIYOR, parlatmiyor',
      '_Press' in iron and 'lerp(body, _Deep.rgb, saturate(_Press)' in iron,
      'baski altinda demir parliyor')
check('isigin TABANI var: hicbir yuzey tamamen isiksiz kalmiyor',
      '0.32 + 0.68 * saturate(facing)' in iron,
      'yalniz yonlu isik - her parcanin yarisi board kadar koyu kaliyor')
check('kontur isigi siluetin KENDI alpha rampasindan',
      'tex.a * (1.0 - tex.a) * 4.0' in iron, 'kontur dikdortgen kenardan')
check('_FaceHalf tuzagi iki shaderda da var',
      '_FaceHalf' in iron and '_FaceHalf' in pit,
      'sprite 1 birim varsayimi - preste bir kere yandi')

check('muhur MADALYON: dis kenar + ic govde + damga (tek disk degil)',
      'SealBody' in view and 'SealBodyScale' in view and 'SealShadow' in view,
      'muhur tek duz kirmizi disk - LED gibi')

print('=== 4. SATIR/SUTUN: BASKI, CIZGI DEGIL ===')
check('baski hucre hucre, tek dev overlay degil',
      'BuildPressure(' in view and 'AddMark(' in view, 'tek buyuk overlay')
check('isaret GRID KENARINDA duruyor (blogun ustune cizilmiyor)',
      'NotchInset' in view and (num(view, r'NotchInset = ([\d.]+)f') or 0) >= 0.85,
      'isaret hucrenin ortasindan geciyor')
check('satir ust/alt kenara, sutun sol/saga - iki eksen ayirt ediliyor',
      'column\n' in view or 'm.Column' in view,
      'iki eksen ayni gorunuyor')
check('baski GOLGE degil, DEMIR - iki zeminde birden okunmasinin tek yolu',
      'PaintIron(m.Renderer' in view and 'IronMaterial())' in method(view, 'private void AddMark('),
      'baski duz koyu bir renk - parlak kupte gorunmez, karanlik tahtada gorunmez')
check('baski demirin en koyu ucunda (renkli cizgi degil)',
      (num(view, r'PressureTone = ([\d.]+)f') or 1) <= 0.2, 'baski acik renkli')
check('isaret KISA - hucre kenarinin bir parcasi, satiri gecen bir cubuk degil',
      (num(view, r'NotchLength = ([\d.]+)f') or 2) <= 0.85
      and (num(view, r'NotchWidth = ([\d.]+)f') or 1) <= 0.15,
      'isaret hucreyi bastan basa geciyor - parmaklik clipart')
check('mesafeyle azaliyor ama SIFIRA inmiyor',
      'PressureFalloff' in view and 'PressureFar' in view,
      'uzak hucrelerde hat olmus gibi gorunmuyor')
check('hucre hucre gecikmeyle yayiliyor (isin degil)',
      'PressureDeployPerCell' in view and 'm.Distance' in view,
      'baski tek karede beliriyor - isin gibi')
check('kupun sprite`i / materyali degistirilmiyor',
      'SetCubeKind' not in view and 'CubeMaterialColor' not in view,
      'View kupu boyuyor')

print('=== 5. CORE = GERCEK, VIEW = SUNUM ===')
check('boss kendi raporunu yaziyor (MapusSealVisuals)',
      'class MapusSealVisuals' in visuals and 'LastSeal' in boss, 'rapor yok')
check('rapor KAYDEDILMIYOR ([NotSaved])',
      '[field: NotSaved]' in boss_raw and 'LastSeal' in boss_raw,
      'per-turn View verisi save`e giriyor')
check('hattin ne kadar dolu oldugu Core`un sorgusu',
      'RowGapCount' in queries and 'ColumnGapCount' in queries
      and 'board.RowGapCount' in boss,
      'View hat doluluğunu kendi hesapliyor')
check('o sorgu patlama kuralinin BEKLEDIGI seyi sayiyor (optional haric, olu hat -1)',
      'optional[x, iy]' in method(queries, 'public int RowGapCount(')
      and 'RowIsKilled' in method(queries, 'public int RowGapCount('),
      'ikinci bir "dolu hat" tanimi uretilmis')
check('View hangi hucrenin muhurlu oldugunu SECMIYOR',
      'NextInt' not in view and 'Random.Range' not in view_raw,
      'View kendi hedefini seciyor')
check('View "hat tam da bu hucre yuzunden tutuluyor" kararini vermiyor',
      'RowHeldAlone' in view and 'HeldByTheSealAlone' in visuals,
      'View hat durumunu kendi cikariyor')
check('tasindi mi / tutuldu mu karari Core`dan',
      'Moved' in visuals and 'Released' in visuals,
      'View spawn/despawn karari veriyor')
check('ayni hucre tekrar gelince prison YENIDEN kurulmuyor',
      'standing' in method(view, 'public void Sync(BoardView view'),
      'her repaint hapishaneyi bastan kuruyor')

print('=== 6. MUHUR 3 TURA KADAR KALIYOR ===')
check('tutus sayaci kuralin kendisinden',
      'TurnsHeld' in visuals and 'MaxTurns' in visuals and 'turnsOnCell' in boss,
      'View tur sayiyor')
check('cap birakmasi AYRI bir olay (oyuncunun penceresi)',
      'Released' in view and 'ReleaseOpenExtra' in view,
      'cap birakmasi sirandan bir despawn gibi')
check('birakirken demir gerektiginden GENIS aciliyor',
      'w.Released' in method(view, 'private void PaintSide('),
      'pencere fark edilmiyor')

print('=== 7. HAREKET DILI ===')
check('kaburga socket`ten DONEREK geliyor (kayarak degil)',
      'SpawnRibTilt' in view and 'Rotate(inward, tilt)' in view,
      'kaburga kayiyor - tween gibi')
check('agir duruş: bounce/overshoot yok',
      'Heavy(' in view and 'private static float Heavy(' in view,
      'yaylanma var')
check('muhur kuyudan YUKSELIYOR (pop degil)',
      'SpawnSealStartScale' in view and 'rises out of the pit' in view_raw.lower(),
      'muhur pop yapiyor')
check('eski ve yeni hapishane ORTUSUYOR ama iki canli muhur olmuyor',
      'MoveOverlap' in view and 'DespawnClock = 0f' in view,
      'iki tam muhur ayni anda')
check('idle bir MEKANIZMA kontrolu (nefes alan canli degil)',
      'IdlePairDelay' in view and 'WARDEN CHECK' in view_raw,
      'idle nefes gibi')
check('reddetme: kirmizi X / sarsinti / yazi yok',
      'shake' not in view.lower() and 'Shake' not in view_raw
      and 'error' not in view.lower(),
      'reddetmede UI hatasi var')
check('partikul birincil tasarim degil',
      'ParticleSystem' not in view_raw, 'partikul sistemi')

print('=== 8. PALET ===')
check('neon / alarm rengi yok',
      not re.search(r'new Color\(1f?\.?0?f?, 0f, 0f', view),
      'saf kirmizi')
check('muhur donuk garnet (parlak degil)',
      (num(view, r'SealWarm = new Color\(([\d.]+)f') or 1) <= 0.6,
      'muhur cok parlak')
check('sicaklik glow`a cikmiyor: yalniz ic gradyan + zemine cok az yansima',
      '_Heat' in iron and 'SealFloorWarmth' in view
      and (num(view, r'SealFloorWarmth = ([\d.]+)f') or 1) <= 0.5,
      'muhur isiyor gibi parliyor')
check('beyaza gitmiyor', 'Color.white' not in view, 'beyaza gidiyor')

print('=== 9. LAB ===')
mapus_scenes = re.search(r'private enum AnimMapusScene\s*\{(.*?)\n        \}', lab, re.S)
names = [x.strip().rstrip(',') for x in mapus_scenes.group(1).split('\n')
         if x.strip()] if mapus_scenes else []
check('mapus sahneleri tam (>= 16)', len(names) >= 16, 'sahne sayisi %d' % len(names))
check('lab GERCEK hedeflemeyi calistiriyor',
      'boss.RetargetOn(' in lab, 'lab kendi hucresini seciyor')
check('hedefleme TEK yerde (round ve lab ayni Choose`u cagiriyor)',
      boss.count('private GridPos? Choose(') == 1
      and 'Choose(round.Board, rng)' in boss and 'Choose(board, rng)' in boss,
      'lab ikinci bir hedefleme uygulamasi')
check('AnimRotFill null `reserved` kabul ediyor (mapus sahneleri oyle cagiriyor)',
      'reserved != null && reserved.Contains' in lab,
      'mapus sahneleri NullReferenceException atar - korunacak hucresi olmayan sahne '
      'bos bir HashSet uydurmak zorunda kalmamali')
check('lab kendi tahtasini kuruyor (round`un Core state`ine dokunmuyor)',
      'animMapusBoard' in lab and 'new GameBoard(w, h)' in method(
          lab, 'private IEnumerator MapusRoutine('),
      'lab round tahtasini muhurluyor')
check('etiket raporun GERCEK icerigini yaziyor',
      'AnimMapusLabel(' in lab and 'seal.RowGaps' in lab, 'etiket sahnenin niyetini yaziyor')
for toggle in ['ShowPit', 'ShowSockets', 'ShowRibs', 'ShowSeal', 'ShowBrand',
               'ShowRowPressure', 'ShowColumnPressure', 'ShowWarmth']:
    check('lab anahtari: %s' % toggle, 'MapusSealView.Layers.' + toggle in lab_raw,
          '%s anahtari yok' % toggle)
check('ham bolumdeki eski Mapus girisi kalkti',
      'Mapus mühürlü hücre ve Tılsım' not in lab_raw,
      'ham bolumde hala eski Mapus girisi var')
check('resync mapus sahnesini durduruyor',
      'StopAnimMapus();' in method(lab, 'private void AnimResync()'),
      'resync mapus sahnesini durdurmuyor')

print('=== 10. KURAL TESTLERI ===')
check('hedefli muhur icin Core testi var',
      'Boss_MapusSealsTheLineNearestCompletion' in tests, 'test yok')
check('kesisme tercihi icin test var',
      'Boss_MapusPrefersTheCrossingOfTwoThreats' in tests, 'kesisme testi yok')
check('testler kosucuya bagli',
      tests.count('Boss_MapusSealsTheLineNearestCompletion') >= 2, 'test cagirilmiyor')
check('cap testi var', 'past the cap it MUST let go' in tests, 'cap testi yok')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('mapus / muhurlu hucre: hepsi tamam')
