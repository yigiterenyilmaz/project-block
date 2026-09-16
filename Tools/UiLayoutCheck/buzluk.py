# -*- coding: utf-8 -*-
# BUZLUK / ICE CRUST TAKEOVER - statik kontrol.
#
# Bu dosyanin korudugu IKI sey var.
#
# 1) ANIMASYON OYUNUN GERCEK KURALINI ANLATIYOR MU?
#    Buzluk'un kurali "su mavilesir" degil: SU BIR DUVARA ULASTIGI ICIN DONAR. Donmanin hangi
#    kenardan basladigi bu efektin tek konusu. Kubun ortasindan buyuyen ya da yanlis kenardan
#    gelen bir donma, oyuncuya var olmayan bir kural ogretir.
#
# 2) BU BIR KABUK MU, YOKSA RENK DEGISIMI MI?
#    Ilk gecis tek bir progress degeriyle yuzu bir buz rengine lerp ediyordu. Her karede makul
#    gorunuyordu ve tahtada "su RECOLOR oldu" diye okunuyordu: su ile buz, tek bir sprite'in iki
#    parlakligiydi. Bu dosyadaki kurallarin yarisi o gecisin geri gelmesini engellemek icin var -
#    pisirilmis alan, kristal parmaklar, geodesic film, cizilmeyen sivi cep, uc fiziksel katman.
from __future__ import print_function
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

fail = []


def check(label, ok, why):
    print('   %-70s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


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


def num(src, pattern, default=None):
    """The default is passed rather than written as `num(...) or 0`, because 0.0 IS FALSY in
    Python and that idiom silently replaces a real zero with the fallback. It cost one false
    failure here already: SlowAt is 0 by design and the check read it as 9."""
    m = re.search(pattern, src)
    return float(m.group(1)) if m else default


joker_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'ElementJokers.cs')
joker = strip_comments(joker_raw)
visuals_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'FreezeVisuals.cs')
visuals = strip_comments(visuals_raw)
queries = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Queries.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'IceFreezeView.cs')
view = strip_comments(view_raw)
growth_raw = read('Assets', 'Scripts', 'View', 'IceGrowth.cs')
growth = strip_comments(growth_raw)
shader = read('Assets', 'Resources', 'Shaders', 'CryoFreeze.shader')
util_raw = read('Assets', 'Scripts', 'View', 'ViewUtil.cs')
util = strip_comments(util_raw)
board = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))

print('=== 1. KURAL TEK YERDE, LAB DA ONU CALISTIRIYOR ===')
freeze = method(joker, 'public static FreezeVisuals FreezeOn(')
check('donma TEK bir metotta (FreezeOn)',
      'public static FreezeVisuals FreezeOn(' in joker
      and 'LastFreeze = FreezeOn(turn.Round.Board' in joker,
      'kural iki yere yazilmis - biri otekiyle ayni seyi soylemeyi birakir')
check('lab AYNI metodu cagiriyor, kendi listesini yazmiyor',
      'BuzlukJoker.FreezeOn(board' in lab,
      'lab donan hucreleri elle diziyor - yanlis kenardan donsa kimse fark etmez')
check('DUVAR TANIMI tek yerde: IsOnEdge, EdgeSidesOf a devrediyor',
      'return EdgeSidesOf(pos) != BoardSides.None;' in queries,
      'iki ayri duvar tanimi - biri gun gelir otekiyle ayni cevabi vermez')
check('duvar, IsInside a soruluyor (delik ve erozyon da duvardir)',
      method(queries, 'public BoardSides EdgeSidesOf(').count('IsInside') >= 5,
      'duvar "dis cerceve" diye sabitlenmis - delik kenarindaki su hic donmuyormus gibi cizilir')
check('donma yalnizca SU uzerinde ve tur OTURDUKTAN sonra',
      'CellsOfKind(CubeKind.Water)' in freeze and 'AfterTurnScored' in joker,
      'ucus halindeki su donuyor - kural tam da bunu yapmiyor')

print('=== 2. RAPOR GERCEGI TASIYOR ===')
check('her donan hucre HANGI DUVARLARA degdigini tasiyor',
      'public BoardSides Sides' in visuals,
      'view duvari kendi tahmin eder - donma yanlis kenardan gelir')
check('KOSE icin birden cok bayrak (Flags)',
      '[System.Flags]' in visuals_raw,
      'kose iki duvara degiyor ama rapor tek yon tasiyor')
check('her hedefin ESKI kupu tasiniyor',
      'public Cube Was' in visuals,
      'donusumun ilk yarisi cizilemez - tahtada zaten buz var')
check('rapor [NotSaved] ve her turda yeni SERIAL',
      'LastFreeze' in joker and '[field: NotSaved]' in joker_raw
      and '++freezeSerial' in joker and 'Serial = serial' in joker,
      'per-turn rapor kayda siziyor ya da her repaint animasyonu bastan baslatiyor')

print('=== 3. DONMA DUVARDAN GELIYOR ===')
check('alan DUVAR icin pisiyor, cephe formulle cizilmiyor',
      'float2 WallSpace(' in shader and '_TileA' in shader,
      'donma merkezden buyuyor ya da tek bir formul cephesi - kuralin soyledigi sey degil')
check('kose: IKI alan orneklenip MIN ile birlesiyor',
      'min(F.x, B.x)' in shader and '_TileB.w > 0.5' in shader,
      'kosede tek kenar seciliyor - iki kristal alanin ortada bulusmasi kayboluyor')
check('view en fazla IKI duvar veriyor (uc duvar = merkezden donma)',
      'private static void Walls(' in view and 'at most two' in view_raw,
      'uc duvar birbirini goturur, sonuc merkezden buyuyen bir donma olur')
check('yonler RAPORDAN geliyor, viewde turetilmiyor',
      'Walls(cell.Sides, out wallA, out wallB)' in view,
      'view duvari kendi hesapliyor - kuralin ikinci bir kopyasi')

print('=== 3B. BU BIR KABUK, RENK DEGISIMI DEGIL ===')
check('alan PISIRILIYOR: parmak/film/bulut/kenar dort kanal',
      'R  FINGER ARRIVAL' in growth_raw and 'G  FILM ARRIVAL' in growth_raw,
      'sekil bir shader formulu - kristal buyumesi formulle cizilemez')
check('KRISTAL PARMAKLAR gercekten buyuyor (yurunen yol, catallanma)',
      'private static void Walk(' in growth and 'forkAt' in growth,
      'parmak yok - geriye tek bir cephe kalir, yani wipe')
check('parmagin yonu HAFIZALI (spirale donmuyor)',
      '- ang * 0.018f' in growth,
      'hafizasiz rastgele yuruyus - uzun parmak kendi uzerine kivriliyor')
check('parmagin yumusak kenari SINIRLI',
      'd > far' in growth and 'EdgeBand' in growth,
      'sinirsiz mesafe rampasi - parmak hucrenin dortte biri kadar sisiyor')
check('FILM bir GEODESIC (bulanikligin degil, bir cephenin sonucu)',
      'private static float[] Geodesic(' in growth,
      'film her yerde ayni anda beliriyor - yayilma hissi yok')
check('SIVI CEP CIZILMIYOR, filmin ulasmadigi yer',
      'float liquid = 1.0 - crust;' in shader,
      'cep elle cizilmis - merkezden kuculen bir daire olur')
check('su YALNIZ sivi cepte hareket ediyor',
      'float motion = _Slow * liquid;' in shader,
      'donmus bolgede de swirl donuyor - kabuk bir film gibi okunur')
check('film 1 i ASIYOR (yoksa son cep hic kapanmiyor)',
      (num(view, r'FilmOver = ([\d.]+)f') or 0) > 1.0,
      'son texel hic muhurlenmiyor - hapsolmus cep parlak mavi bir delik olarak kaliyor')
check('kenar WALL tarafinda daha KALIN',
      'WallRimBias' in growth and (num(growth, r'WallRimBias = ([\d.]+)f') or 0) > 1.2,
      'donmus kupte artik hangi duvardan geldigi okunmuyor')
check('varyantlar var (bir kenar dolusu buz ayni cizim degil)',
      (num(growth, r'Variants = (\d+)') or 0) >= 3 and 'VariantA' in view,
      'her buz kupu ayni kristal alani - dokulu bir kaplama gibi tekrar eder')
check('pisirme DETERMINISTIK (UnityEngine.Random degil)',
      'private sealed class Lcg' in growth and 'UnityEngine.Random' not in growth,
      'kayittan donen tahta baska bir buz giyer')
check('alan LINEAR (dort kanal da VERI)',
      'TextureFormat.RGBA32, false, true)' in growth,
      'sRGB cozumu her varis zamanini bukuyor - donma kendi ortasinda yanlis hizda akar')

print('=== 4. AYRI BUZ ASSETI YOK: BUZ, SUYUN KENDI PIKSELLERI ===')
check('block_ice.png YOK (ve olmamali)',
      not os.path.exists(os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Blocks',
                                      'block_ice.png')),
      'ayri bir buz karosu eklenmis - o zaman bu sistemin yarisi gereksiz')
check('buz karosu SUYUN dokusundan kesiliyor',
      'Sprite.Create(water.texture' in util and 'case CubeKind.Ice: return IceTile;' in util,
      'buz baska bir gorsele baglanmis - donan kup, duran kupten baska bir sey olur')
check('buz KENDI materyalini aliyor (su materyali degil)',
      'if (tile == iceTile)' in util and 'Material ice = IceMaterial;' in util,
      'buz su materyaliyle ciziliyor - donduktan sonra akmaya devam eder')
check('final buz, yuzun KENDI parlakligindan kuruluyor (duz renge lerp degil)',
      '_ColdTint.rgb * (lum * 0.34 + 0.46)' in shader,
      'duz bir maviye lerp - kupun cercevesi, bevel i ve ust isigi gider; sonuc bir PLAKA')
check('UC FIZIKSEL KATMAN var (hapsolmus su / sutlu govde / kirag kenar)',
      'LAYER ONE: the water BURIED' in shader and 'LAYER TWO: the MILKY ICE BODY' in shader
      and 'LAYER THREE: the FROSTED CRYSTAL RIM' in shader,
      'tek katman - "su biraz beyazladi" seviyesinde kalir')
check('sutlu govdenin KENDI kalinlik degisimi var (duz plaka degil)',
      '_IceBodyVar' in shader and 'float milk = saturate(F.z' in shader,
      'duz bir sutlu dikdortgen - cam UI karosu gibi okunur')
check('parmaklar FINAL kupte damar olarak kaliyor',
      '_FingerAmount' in shader and 'stay in the finished cube as veins' in shader,
      'kristal yapi donusumle birlikte kayboluyor - geriye duz bir yuzey kaliyor')
check('parmagin cekirdegi mavi, kenari beyaz (kesit)',
      'lerp(_FrostColour.rgb, _VeinColour.rgb, depth)' in shader,
      'duz tek renk parmak - cizilmis bir cizgi gibi, kesiti olan bir filament gibi degil')
check('SU GORUNURLUGU ~0.20-0.30 arasinda',
      0.18 <= (num(view, r'InnerWater = ([\d.]+)f') or 0) <= 0.34,
      'ictek swirl ya cok net (su gibi okunur) ya hic yok (gri cam)')
check('kabuk kalinlastikca su DAHA DA gomuluyor',
      '_InnerFade' in shader and (num(view, r'InnerFade = ([\d.]+)f') or 0) > 0.03,
      'kalinlasma asamasinin gorsel karsiligi yok')
check('shader yoksa kup yine de gorunuyor',
      'No cryo shader' in util_raw,
      'shader yoksa buz kupu kayboluyor')

print('=== 5. ZAMAN CIZELGESI: BES ASAMA, TEK MATERYAL ===')
single = (num(view, r'LockAt = ([\d.]+)f') or 0) + (num(view, r'LockFor = ([\d.]+)f') or 0)
check('tek kup ~0.75-0.95 sn (%.2f sn)' % single, 0.70 <= single <= 0.98,
      'tek bir kup icin cok kisa - "hemen donuveriyor" hissi geri gelir (sartname 56)')
check('BES AYRI ilerleme var, tek bir progress degil',
      all(k in view for k in ('SlowAt', 'SeedsAt', 'FingersAt', 'FilmAt', 'ThickAt'))
      and all(k in shader for k in ('_Slow', '_Seeds', '_Fingers', '_Film', '_Thick')),
      'tek progress egrisi - animasyon tek bir gecise indirgenir (sartname 94-95)')
stages = [(num(view, r'%sAt = ([\d.]+)f' % k, 0), num(view, r'%sFor = ([\d.]+)f' % k, 0))
          for k in ('Slow', 'Seeds', 'Fingers', 'Film', 'Thick')]
overlap = sum(1 for i in range(len(stages) - 1)
              if stages[i][0] + stages[i][1] > stages[i + 1][0])
check('asamalar CAKISIYOR (%d/4 gecis)' % overlap, overlap >= 3,
      'asamalar arka arkaya diziliyor - 0.8 sn hantal hissettirir (sartname 58)')
check('FILM en uzun asama (gozun takip ettigi sey o)',
      stages[3][1] == max(x[1] for x in stages),
      'filmin kapanmasi aceleye getirilmis - asil izlenen sey o')
check('duran buz ile animasyon AYNI sayilar (devir teslim yok)',
      'film = Style.FilmOver;' in view and 'cell.Start == float.NegativeInfinity' in view,
      'duran buz ayri bir yoldan ciziliyor - ikisi bir gun birbirinden ayrilir')
check('SU HAREKETI once basliyor (asama A gercekten var)',
      num(view, r'SlowAt = ([\d.]+)f', 9) <= 0.02 and stages[0][0] < stages[1][0],
      'akis bir anda kesiliyor - sprite degistirme hissi, tam da kacinilan sey')
check('KRISTAL KILIDI kucuk (yuzde 3 un altinda) ve once SISIYOR',
      1.0 < (num(view, r'LockPeak = ([\d.]+)f') or 0) <= 1.03 and 'LockSwell' in view,
      'zipzip bir cartoon bounce, ya da suyun donunca genlesmesi hic soylenmiyor')

print('=== 5B. HUCRESINDE KALIYOR ===')
# Buz kupu, yanindaki normal kupten DAHA BUYUK bir blok gibi gorunemez. Bu bolumdeki her kural
# bir kez gercekten kirildi: kilit olcegi her karede kendi uzerine carpiliyordu, 60fps'te kupu
# %6.6, labda 0.25x'te %29 buyutuyordu - ve bir sonraki repaint'e kadar oyle kaliyordu.
check('kilit olcegi BIR TABANDAN yaziliyor, renderer`dan geri okunmuyor',
      'cell.BaseScale = Mathf.Abs(r.transform.localScale.x);' in view
      and 'float side = cell.BaseScale * k;' in view,
      'olcek her karede kendi uzerine carpiliyor - kup kademe kademe buyuyup oyle kaliyor')
check('geri okuma kalibi HIC yok',
      'Mathf.Abs(s.x) * k' not in view,
      'eski bilesik olcek kalibi geri gelmis')
ceiling = num(view, r'ScaleCeiling = ([\d.]+)f', 9)
check('OLCEK TAVANI tanimli (%.3f)' % ceiling, 1.0 < ceiling <= 1.03,
      'bu efektin bir kubu ne kadar buyutebilecegi hicbir yerde yazmiyor')
for k in ('LockPeak', 'LockSwell'):
    v = num(view, r'%s = ([\d.]+)f' % k, 9)
    check('%s tavanin altinda (%.3f)' % (k, v), 1.0 < v <= ceiling,
          '%s kubu hucresinden tasiriyor' % k)
check('son hal TAM taban (normal kuple ayni dis olcu)',
      'if (!seating)' in view,
      'animasyon bitince kup normal boyutuna donmuyor')
check('PARILTILAR kubun ICINDE kelepceli',
      'GlintInside' in view and num(view, r'GlintInside = ([\d.]+)f', 9) < 1.0
      and 'Mathf.Clamp(off.x' in view,
      'kirag iki hucre arasindaki bosluga dusuyor - buz kendi karesinden tasiyor')
rim = num(growth, r'RimThickness = ([\d.]+)f', 9)
bias = num(growth, r'WallRimBias = ([\d.]+)f', 9)
check('kirag kenari ince (%.3f hucre, duvar tarafi %.3f)' % (rim, rim * bias),
      rim <= 0.055 and rim * bias <= 0.07,
      'koyu tahtada cepecevre parlak bir bant, kupu daha buyuk bir blok gibi gosterir')
check('kenarin gucu de kisilmis',
      num(view, r'RimAmount = ([\d.]+)f', 9) <= 0.65,
      'kenar hala cok beyaz - silueti sisiriyor')

print('=== 6. SESSIZ KALACAK: OLCU VE YASAKLAR ===')
check('EKRAN SARSINTISI / FLASH / KAMERA yok',
      'Shake' not in view and 'timeScale' not in view and 'flash' not in view.lower(),
      'patlama dili - bu bir donma')
check('parilti sayisi hem kup hem efekt basina kelepceli',
      'GlintCap' in view and 'glints.Count < Style.GlintCap' in view,
      'kar firtinasi')
check('kup basina en fazla 4 parilti',
      num(view, r'LockGlints = (\d+)', 9) <= 4
      and num(view, r'RimGlints = (\d+)', 9) <= 4,
      'kup basina cok fazla parcacik (sartname 80, 128)')
check('cok kup dondugunde TOPLAM sure kelepceli (~1.15 sn)',
      'TotalCap' in view and num(view, r'TotalCap = ([\d.]+)f', 9) <= 1.20
      and 'Mathf.Min(stagger' in view,
      '5 hucre = 5 ayri sinematik (sartname 59, 123)')
check('stagger var ama kucuk (bir soguk dalga)',
      0.04 <= (num(view, r'Stagger = ([\d.]+)f') or 0) <= 0.075,
      'hepsi ayni karede ya da arada cok uzun bosluk')
check('bosta bekleyen buz NEFES ALMIYOR (sadece ara sira parilti)',
      'IdleMin' in view and 'whole cube pulsing' in view_raw and 'breath' not in view.lower(),
      'butun kup nabiz gibi atiyor - bir UI elemani olur')

print('=== 7. YASAK RENKLER ===')
cold = re.search(r'Cold = new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)', view)
r, g, b = (float(cold.group(i)) for i in (1, 2, 3)) if cold else (0, 1, 1)
check('neon camgobegi degil (r=%.2f, g=%.2f, b=%.2f)' % (r, g, b),
      r > 0.35 and not (r < 0.2 and g > 0.9 and b > 0.9),
      '#00FFFF - sartnamenin ilk yasakladigi renk')
check('mora kacmiyor (b, g den cok onde degil)', b - g < 0.35, 'mor buz - yasak listesinde')
check('yesilimsi aqua degil', g <= b, 'yesile calan aqua - slime gibi okunur')
check('saf beyaza gitmiyor', min(r, g, b) < 0.95,
      'beyaz kare - kabul testinin ikinci FAIL maddesi')

print('=== 8. TAHTA VE OYUN ===')
check('buz her repaint te yeniden yaziliyor (PRESENCE)',
      'sawIce' in board and 'Ice.Sync(this)' in board,
      'tahta her degisiklikte buzun uzerine duz su boyar')
check('tahta yeniden kurulurken efekt korunuyor',
      'keepIce' in board,
      'donma ortasinda yok edilirse kuplerin property block u kalir')
check('hucre buz olmaktan cikinca property block TEMIZLENIYOR',
      'r.SetPropertyBlock(null)' in view,
      'havuzlanmis renderer eski donmayi tasir - oraya gelen yeni kup buz gibi gorunur')
check('oyun da ayni seam den geciyor',
      'boardView.Ice.Play(boardView, buzluk.LastFreeze)' in fb,
      'lab kendi kopyasini oynatiyor - retiming labda gorunmez')
check('SES kancalari birakilmis',
      'SoundFreezeBegin' in view and 'SoundCrystalLock' in view,
      'ses tasarimi icin tutamak yok')

_fresh = strip_comments(read('Assets', 'Scripts', 'View', 'IceFreezeView.cs'))
check('rapor KENDISIYLE karsilastiriliyor (seriyle degil)',
      'ReferenceEquals(report, lastPlayed)' in _fresh and 'Serial == ' not in _fresh,
      'seri her yeni jokerde 1 den basliyor - yeni kosuda ilk animasyon sessizce atlanir')

print('=== 9. LAB ===')
for scene in ('AnimIceScene.Bottom', 'AnimIceScene.Top', 'AnimIceScene.Left',
              'AnimIceScene.Right', 'AnimIceScene.CornerBottomLeft',
              'AnimIceScene.CornerTopRight', 'AnimIceScene.FiveAlongEdge',
              'AnimIceScene.Stress', 'AnimIceScene.HoleWall',
              'AnimIceScene.WaterBesideIce', 'AnimIceScene.AgainstObsidian'):
    check('lab sahnesi: %s' % scene, scene in lab_raw, '%s sahnesi yok' % scene)
check('KURAL TESTI sahnesi var (delik de duvardir)',
      'HoleWall' in lab_raw and 'A WALL IS NOT THE RIM' in lab_raw,
      'delik-duvar kurali gozle dogrulanamiyor')
check('KABUL sahnesi var (buz ile suyun yan yana hali)',
      'WaterBesideIce' in lab_raw,
      '"bu artik su degil" testi yapilamiyor')
check('duvar yonu hata ayiklamasi var',
      'ShowField' in lab_raw and '_Debug' in shader,
      'donmanin dogru kenardan geldigi gozle ayirt edilemiyor')
check('her asama TEK BASINA izlenebiliyor',
      'AnimIceOnly(' in lab_raw and lab_raw.count('AnimIceOnly(') >= 7,
      'bes asama tek tek gorulemiyor')
check('SIVI CEP ve alanlar boyanabiliyor',
      'AnimIceField(' in lab_raw and lab_raw.count('AnimIceField(') >= 5,
      'cebin gercekten son muhurlenmemis bolge oldugu gozle dogrulanamiyor')
for switch in ('ShowFrostSeeds', 'ShowCrystalFingers', 'ShowIceFilm', 'ShowShellThickness',
               'ShowFinalRim', 'ShowClouding', 'ShowTrappedWater', 'ShowLockGlints'):
    check('lab anahtari: %s' % switch, switch in lab_raw, '%s anahtari yok' % switch)
check('asama anahtarlari sahne KURULMADAN once uygulaniyor',
      method(lab, 'private void AnimIceOnly(').index('IceFreezeView.Layers.AllOn()')
      < method(lab, 'private void AnimIceOnly(').index('AnimIce(scene)'),
      'kapali katman yine de cizilir')
check('animasyon saati OLCEKLI (lab yavaslatabilsin)',
      'clock += Time.deltaTime;' in view,
      '0.25x kabul testi calistirilamaz')
check('RESET donmayi da durduruyor',
      'StopAnimIce();' in method(lab, 'private void AnimResync()'),
      'resync sahneyi durdurmuyor')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('buzluk: hepsi tamam')
