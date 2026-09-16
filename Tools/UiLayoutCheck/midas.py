# -*- coding: utf-8 -*-
# MIDAS / ALTIN TEMETTUSU - statik kontrol.
#
# Bu dosyanin korudugu sey, efektin ne kadar guzel oldugu degil - HANGI KURALI anlattigi.
# Midas yalnizca ELDEKI altin kuplerden oder; tahtadaki altin kendi upkeep'ini zaten oder.
# Animasyon yanlis kaynagi gosterirse oyuncuya yanlis kurali ogretir ve bu, cirkin bir
# animasyondan cok daha pahaliya patlar.
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
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'MidasVisuals.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'MidasPayoutView.cs')
view = strip_comments(view_raw)
wire = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Midas.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
card = strip_comments(read('Assets', 'Scripts', 'View', 'CardVisual.cs'))

print('=== 1. KAYNAK: EL, TAHTA DEGIL ===')
score = method(joker, 'public override void ModifyScore(')
check('sayim yalniz Hand ve BonusHand uzerinden',
      'round.Hand' in score and 'round.BonusHand' in score,
      'elde olmayan bir yerden sayiliyor')
check('tahtadaki altin Midas kaynagi DEGIL',
      'Board' not in score and 'CountCubesOfKind' not in score,
      'tahtadaki altin ikinci kez sayiliyor - oyuncuya yanlis kural ogretiliyor')
check('rapor kart kart kaynak tutuyor (kart degil KUP anlatilsin diye)',
      'MidasGoldSource' in visuals and 'public int GoldCubes' in visuals,
      'rapor tek bir toplam - animasyonun ozne olarak kupu gosterme sansi yok')

print('=== 2. RAPOR GERCEGI TASIYOR ===')
check('element bastirilmasi raporu da susturuyor (CardHasElement round uzerinden)',
      'round.CardHasElement(card, BlockElement.Gold)' in joker,
      'Vanilya altinda bile efekt cikar')
check('EFFECTIVE shape kullaniliyor, basili sekil degil',
      'round.EffectiveShape(card)' in joker,
      'oyuncunun artik tutmadigi kupler icin odeme cizilir')
check('rapor [NotSaved] - kayitta yeri yok',
      '[field: NotSaved]' in joker_raw and 'MidasPayoutVisuals LastPayout' in joker,
      'per-turn rapor kayit dosyasina siziyor')
check('her odemede yeni SERIAL (tekrar cizim animasyonu yeniden baslatmasin)',
      'Serial = ++payoutSerial' in joker,
      'her repaint animasyonu bastan baslatir')
check('skor hesabi degismedi: tek AddFlat, kup x placeholder',
      'turn.Score.AddFlat(report.TotalScore, DefId)' in score
      and 'cubes * PointsPerGoldCubeHeld' in score,
      'raporlama skoru degistirmis')

print('=== 3. VIEW HICBIR SEY BILMIYOR ===')
check('tutar HARDCODE degil - rapordan geliyor',
      # Yorumlarda gecmesi serbest - burada aranan KODUN sayiyi bilmesi.
      'PointsPerGoldCube' in view and '"+2"' not in view and '+ 2)' not in view,
      'view +2 yaziyor - balans degistigi gun yalan soyler')
check('view kup saymiyor, kaynaklari disaridan aliyor',
      'SourceSpot' in view and 'CardHasElement' not in view,
      'view kendi basina altin kup sayiyor')
check('kup pozisyonu KARTIN kendi yerlesiminden',
      'CardVisual.MiniCubeLocal(' in wire and 'public static Vector2 MiniCubeLocal(' in card,
      'ayni formul ikinci kez yazilmis - kart yerlesimi degisince isaret kayar')
check('skor hedefi gercek HUD etiketinden, sabit koordinat degil',
      'totalText.rectTransform.position' in wire and 'ScreenToWorldPoint' in wire,
      'sabit ekran koordinati - telefonda bosluga ucar')

print('=== 4. HER TUR CALISACAK: OLCU ===')
total = (num(view, r'Wake = ([\d.]+)f') or 0) + (num(view, r'Hold = ([\d.]+)f') or 0) \
    + (num(view, r'Shrink = ([\d.]+)f') or 0) + (num(view, r'FlightFar = ([\d.]+)f') or 0)
check('cekirdek zincir ~1 saniyenin altinda (%.2f sn)' % total, total < 1.0,
      'her tur oynayacak bir efekt icin cok uzun')
check('EKRAN SARSINTISI / FLASH / KAMERA yok',
      'Shake' not in view and 'timeScale' not in view and 'camera' not in view.lower(),
      'Common bir joker icin legendary gosteri')
check('kalabalikta tutarlar kart basina toplaniyor',
      'SubtotalAbove' in view and 'VisibleValues' in view,
      'yirmi kup ekrani +2 copluguna cevirir')
check('kirinti ve kivilcim tavani var',
      'FleckCap' in view and 'SparkCap' in view,
      'konfeti / glitter yagmuru')
check('skor TEK cevap veriyor, her tohum icin bir zipla degil',
      'ScoreWarm = Mathf.Min(1f, ScoreWarm + ' in view,
      'on iki varista on iki zipla - skor titriyor')
check('efekt KARTLARIN USTUNDE ciziliyor',
      'GlintOrder = 80' in view and 'ValueOrder = 83' in view,
      'odeme elin arkasinda kaliyor - lab kendi elini 49-59`da seriyor, efekt onun altinda')
check('skor metnine VIEW dokunmuyor (tek sahip)',
      'totalText' not in view,
      'iki sistem ayni transformu cekistiriyor')

print('=== 5. LAB GERCEK ANIMASYONU SURUYOR ===')
check('lab kendi altin elini seriyor',
      'ShowLabHand(' in lab and 'BlockElement.Gold' in lab,
      'lab efekti gosteremiyor - elde altin olmadan bakilamiyor')
check('lab oyunun kullandigi seam`i cagiriyor',
      'PlayMidasDebug(' in lab and 'PlayMidasDebug(' in wire,
      'lab kendi kopyasini oynatiyor - retiming labda gorunmez')
check('lab tutari JOKERDEN okuyor',
      'PointsPerGoldCubeHeld' in method(lab, 'private int AnimMidasPerCube('),
      'lab kendi sayisini basiyor')
for scene in ('AnimMidas(1)', 'AnimMidas(5)', 'AnimMidas(20)', 'AnimMidas(40)'):
    check('lab sahnesi: %s' % scene, scene in lab_raw,
          '%s sahnesi yok - o yogunluk hic denenmiyor' % scene)
check('kaynak isaretleri hata ayiklamasi var (el mi tahta mi?)',
      'ShowSourceAnchors' in lab_raw and 'ShowSourceAnchors' in view,
      'odemenin elden mi geldigi gozle dogrulanamiyor')
check('RESET midas sahnesini de durduruyor',
      'StopAnimMidas();' in method(lab, 'private void AnimResync()'),
      'resync sahneyi durdurmuyor')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('midas: hepsi tamam')
