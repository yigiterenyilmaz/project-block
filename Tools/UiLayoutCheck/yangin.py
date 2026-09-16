# -*- coding: utf-8 -*-
# YANGIN / ATES CEPHESI - statik kontrol.
#
# Bu dosyanin korudugu sey su: animasyon, oyunun GERCEK kuralini anlatiyor mu?
# Yangin tek kullanimda YALNIZ BIR HALKA yayilir. Tutusan kup baskasini tutusturmaz.
# Ikinci bir halka ciziliyormus gibi gorunen bir efekt, oyuncuya var olmayan bir kurali ogretir -
# ve bu, cirkin bir animasyondan cok daha pahaliya patlar.
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


def num(src, pattern):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else None


joker_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'ElementJokers.cs')
joker = strip_comments(joker_raw)
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'SpreadVisuals.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'FireSpreadView.cs')
view = strip_comments(view_raw)
shader = read('Assets', 'Resources', 'Shaders', 'FireBloom.shader')
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
board = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))

print('=== 1. KURAL TEK YERDE, LAB DA ONU CALISTIRIYOR ===')
spread = method(joker, 'public static SpreadVisuals SpreadOn(')
check('yayilma TEK bir metotta (SpreadOn)',
      'public static SpreadVisuals SpreadOn(' in joker
      and 'SpreadOn(ctx.Round.Board, SpreadKind' in joker,
      'kural iki yere yazilmis - biri otekiyle ayni seyi soylemeyi birakir')
check('lab AYNI metodu cagiriyor, kendi listesini yazmiyor',
      'SpreadJoker.SpreadOn(board, CubeKind.Fire' in lab,
      'lab hedefleri elle diziyor - ikinci halka cizse kimse fark etmez')
check('ONCE TOPLA SONRA CEVIR korunuyor (tek halka bu satirda)',
      'targets.Add(neighbour)' in spread
      and spread.index('targets.Add(neighbour)') < spread.index('board.SetCubeKind'),
      'yururken cevriliyor - tek kullanimda tahta komple yanar')

print('=== 2. RAPOR GERCEGI TASIYOR ===')
check('kaynaklar ve hedefler ayri ayri bildiriliyor',
      'public readonly List<GridPos> Sources' in visuals
      and 'public readonly List<SpreadIgnition> Targets' in visuals,
      'view kaynakla hedefi ayirt edemez')
check('her hedef HANGI YONDEN tutustugunu tasiyor',
      'public readonly List<GridPos> From' in visuals,
      'yanik izi kupun yanlis kenarinda cikar')
check('her hedefin ESKI kupu tasiniyor',
      'public Cube Was' in visuals,
      'donusumun ilk yarisi cizilemez - tahta zaten ates')
check('rapor [NotSaved] ve her kullanimda yeni SERIAL',
      'LastSpread' in joker and '[field: NotSaved]' in joker_raw
      and '++spreadSerial' in joker and 'Serial = serial' in joker,
      'per-turn rapor kayda siziyor ya da her repaint animasyonu bastan baslatiyor')

print('=== 3. TEK HALKA GORSEL OLARAK DA KORUNUYOR ===')
play = method(view, 'public void Play(')
check('ISINMA yalnizca report.Sources uzerinde',
      'for (int i = 0; i < report.Sources.Count; i++)' in play
      and 'ShowSourceWarmup' in play,
      'tutusan kup de isiniyor - yayilacakmis gibi okunuyor')
check('ALEV DILI yalnizca lit.From (yani kaynaklar) uzerinden cikiyor',
      'lit.From[k]' in play and 'MakeLick(source, at' in play,
      'yeni ateslerden ikinci halka alevi cikiyor - oyunda olmayan kural')
check('yon RAPORDAN geliyor ve EKSENE oturtuluyor (4-komsu)',
      'Direction(from, at)' in view and 'Mathf.Sign(d.x)' in view,
      'capraz yon - 4-komsu kuralinin ureteme yecegi bir yon')

# HUCRE SINIRI. Yayilma KUPLERI cevirir, bos hucreye ates YARATMAZ. Kaynagin uzerindeki isinma
# halesi 1.25 hucre genisti (nefesle 1.44) - yani dortte biri her komsunun uzerine tasiyordu ve o
# komsular cogu zaman BOS. Bos hucrenin uzerindeki turuncu isik, oyuncuya "bos yerler de tutusuyor"
# der; kuralin tam tersi. Hucre basina cizilen hicbir sey kendi hucresini asamaz.
bound = num(view, r'CellBound = ([\d.]+)f') or 0.0
for name, breath in (('WarmGlow', 1.1), ('WarmHalo', 1.15)):
    size = num(view, r'%s = ([\d.]+)f' % name) or 0.0
    check('%s en genis halinde kendi hucresinde kaliyor (%.2f hucre)' % (name, size * breath),
          0.0 < size * breath <= bound,
          '%s komsu hucreye tasiyor - bos hucre tutusuyormus gibi okunuyor' % name)
check('hale KODDA da hucreye kelepceli (sayi kaysa bile tasmaz)',
      'Mathf.Min(Style.WarmHalo' in view and 'Style.CellBound)' in view,
      'sinir yalnizca bir sabitte - biri sayiyi buyutunce sessizce komsuya tasar')
check('KURAL testte cakili: bos komsu asla tutusmuyor',
      'the empty cell above the fire stayed empty'
      in read('Tools', 'CoreTests', 'JokerTests.cs'),
      'bos hucre kurali yalnizca gorselde - Core tarafinda bir sey kirilirsa kimse fark etmez')

print('=== 4. TAHTA GERI CEKILIYOR, HUCRELER GERI VERILIYOR ===')
check('yanan hucreler HoldCells ile bosaltiliyor',
      'view.HoldCells(held)' in view,
      'Core zaten atese cevirdi - altta cevap duruyorken donusum cizilemez')
check('Stop HER durumda hucreleri geri veriyor',
      'owner.ReleaseCells(held)' in method(view, 'public void Stop()'),
      'raunt boyunca tahtada bos hucre kalir')
check('bitince hucre tek tek geri veriliyor',
      'owner.ReleaseCells(new[] { b.Cell })' in view,
      'butun hucreler en sona kadar bos bekliyor')
check('tahta yeniden kurulurken efekt korunuyor',
      'keepFire' in board,
      'yanma ortasinda yok edilirse tahtada geri verilmemis delikler kalir')

print('=== 5. DONUSUM: CEPHE, CAPRAZ GECIS DEGIL ===')
check('iki yuz TEK cephe paylasiyor (_Invert)',
      '_Invert' in shader and 'Paint(b.Was, b, front, true' in view
      and 'Paint(b.Fire, b, front, false' in view,
      'cross-fade - "kup degistirildi" der, "ates aldi" demez')
check('cephe IKI DUSUK FREKANSLI dalga ile tirtikli',
      'sin(across * 9.0' in shader and 'sin(across * 15.0' in shader,
      'yuksek frekans = gurultu, yanan kenar degil')
check('eski kup TINTLENMIYOR, rengi cekiliyor ve isleniyor',
      '_Drain' in shader and '_Soot' in shader and 'grey' in shader,
      'kirmizi tint - istenmeyen sey tam olarak buydu')
check('cephenin arkasinda EMBER DAMARLARI var ve orada kaliyor',
      '_Veins' in shader and 'saturate(1.0 - (_Front - d) / 0.42)' in shader,
      'damarlar butun yuze yayilan cizgilere donuyor')
check('shader yoksa kup yine de donuyor',
      'No shader: the old face simply gives way' in view_raw,
      'shader yoksa hicbir sey gorunmuyor')

print('=== 6. HER KULLANIMDA CALISACAK: OLCU ===')
total = sum((num(view, r'%s = ([\d.]+)f' % k) or 0)
            for k in ('Warm', 'LickTravel', 'Catch', 'Heat', 'Settle'))
check('cekirdek zincir ~1 saniyenin altinda (%.2f sn)' % total, total < 1.0,
      'tek bir joker kullanimi icin cok uzun')
check('EKRAN SARSINTISI / FLASH / KAMERA yok',
      'Shake' not in view and 'timeScale' not in view and 'flash' not in view.lower(),
      'patlama dili - bu bir tutusma, TNT degil')
check('koz sayisi hem hucre hem efekt basina kelepceli',
      'EmbersPerCell' in view and 'EmberCap' in view and 'CrowdedAbove' in view,
      'particle spam')
check('YALNIZ ATES: ayni rapor Taskin`i da tasiyor, o baska bir dil',
      'report.Kind != CubeKind.Fire' in view,
      'su, atesin diliyle ciziliyor')

print('=== 7. LAB ===')
for scene in ('AnimFireScene.One', 'AnimFireScene.Four', 'AnimFireScene.TwoIntoOne',
              'AnimFireScene.Crowded', 'AnimFireScene.NoReSpread'):
    check('lab sahnesi: %s' % scene, scene in lab_raw,
          '%s sahnesi yok' % scene)
check('KURAL TESTI sahnesi var (yeni ates yayilmamali)',
      'NoReSpread' in lab_raw and 'one ring' in lab_raw,
      'tek halka kurali gozle dogrulanamiyor')
check('kaynak/hedef isaretleri hata ayiklamasi var',
      'ShowSourceTargetMarks' in lab_raw and 'ShowSourceTargetMarks' in view,
      'hangisi kaynak hangisi hedef gozle ayirt edilemiyor')
check('RESET yangin sahnesini de durduruyor',
      'StopAnimFire();' in method(lab, 'private void AnimResync()'),
      'resync sahneyi durdurmuyor')
check('oyun da ayni seam`den geciyor',
      'boardView.FireSpread.Play(boardView, spread.LastSpread)' in fb,
      'lab kendi kopyasini oynatiyor - retiming labda gorunmez')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('yangin: hepsi tamam')
