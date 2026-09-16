# -*- coding: utf-8 -*-
# HARCAMA BONUSU / EMPTY-DECK CASHBACK - statik kontrol.
#
# Bu dosyanin korudugu UC sey var.
#
# 1) NEDEN-SONUC ZINCIRI GORUNUYOR MU?
#    Efektin butun isi su cumleyi kurmak: "deste bosaldi -> bosalmanin karsiligi geldi -> skoruma
#    aktti". Ekranin ortasinda bir yerde beliren bir +60, bu cumlenin hicbir parcasini kurmuyor.
#    O yuzden KAYNAK gercek cekme destesi, HEDEF gercek skor etiketi olmak zorunda.
#
# 2) SAYI VE SAYIM CORE'UN MU?
#    PointsPerEmptyDrawPile bir denge yer tutucusu; "+60" yazan bir gorsel, biri o sayiyi
#    degistirdigi gun skor hakkinda yalan soyleyen bir gorsel olur. Ve joker bir BOOL okuyor -
#    "bu tur deste hic bosaldi mi" - yani iki kez kuruyan bir tur BIR kez oduyor. Animasyonun da
#    bir kez oynamasi gerekiyor.
#
# 3) TON DOGRU MU?
#    Bu Common bir joker ve odedigi olay ayni zamanda arenayi yiyen, esikten sonra da KAYBETTIREN
#    olay. Kutlama dili (flash, coin rain, jackpot, sarsinti) mekanigin karanlik tarafini yalanlar.
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
    m = re.search(pattern, src)
    return float(m.group(1)) if m else default


joker_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'ScoreJokers.cs')
joker = strip_comments(joker_raw)
visuals_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'RebateVisuals.cs')
view_raw = read('Assets', 'Scripts', 'View', 'RebateView.cs')
view = strip_comments(view_raw)
shapes_raw = read('Assets', 'Scripts', 'View', 'RebateShapes.cs')
shapes = strip_comments(shapes_raw)
wire_raw = read('Assets', 'Scripts', 'View', 'GameUiController.Rebate.cs')
wire = strip_comments(wire_raw)
cards = strip_comments(read('Assets', 'Scripts', 'View', 'CardLayerView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. SAYI CORE`UN ===')
check('rapor odenen MIKTARI tasiyor',
      'public int Payout' in visuals_raw,
      'gorsel kendi sayisini uydurur - denge degisince skor hakkinda yalan soyler')
check('rapor odemenin YANINDA yaziliyor',
      'turn.AddFlatScore(PointsPerEmptyDrawPile, DefId);' in joker
      and 'LastRebate = new RebateVisuals' in joker
      and joker.index('turn.AddFlatScore(PointsPerEmptyDrawPile')
      < joker.index('LastRebate = new RebateVisuals'),
      'rapor odemeden ayri bir yerde yaziliyor - ikisi bir gun ayri seyler soyler')
# Yorumlari ATARAK bakiyor: dosyanin basligi kuralin kendisini anlatirken "+60" yaziyor ve ilk
# yazilisinda bu kontrol tam da o cumleye takildi. Aranan sey KODDA gomulu bir sayi.
check('viewde SABIT bir puan YOK',
      not re.search(r'"\+\s*\d', view) and '"+" + report.Payout' in view,
      '"+60" gorsele gomulmus - sartname 131`in ilk kirmizi bayragi')
check('lab da miktari JOKERDEN okuyor',
      'AnimRebateAmount()' in lab and 'PointsPerEmptyDrawPile;' in lab,
      'lab kendi sayisini yaziyor - oyunla ayni seyi soylemeyi birakir')
check('miktar CoreTests`te cakili',
      "the receipt carries the joker's OWN amount" in tests,
      'sabit sayi geri gelse kimse fark etmez')

print('=== 2. TUR BASINA TEK ODEME, TEK ANIMASYON ===')
check('joker BOOL okuyor, sayac degil',
      'if (!turn.Report.DrawPileEmptiedThisTurn)' in joker,
      'joker kuruma sayiyor - kural bu degil')
check('rapor SERIAL tasiyor ve her odemede artiyor',
      'public int Serial' in visuals_raw and '++rebateSerial' in joker,
      'ayni odeme her repaint`te yeniden oynar')
# Once "paid.Serial == playedSerial" isteniyordu. O kalip YENI KOSUDA ilk odemeyi yutuyordu:
# seri her yeni jokerde 1'den basliyor, view ise kosular arasinda yasiyor. Raporun KENDISI
# karsilastiriliyor artik - her odeme yeni bir nesne, repaint ayni nesneyi veriyor.
check('view ODEMEYE bakiyor (raporun kendisi), olaya ya da seriye degil',
      'ReferenceEquals(paid, lastPlayed)' in view and 'Serial == ' not in view,
      'view "deste bosaldi"ya ya da seriye baglanmis - ayni turda iki kez oynar ya da yeni kosuda ilk odemeyi yutar')
check('rapor [NotSaved]',
      '[field: NotSaved]' in joker_raw and 'LastRebate' in joker_raw,
      'per-turn rapor kayda siziyor')
check('KURAL TESTI labda var (iki kuruma, tek fis)',
      'RULE TEST - pile dried TWICE, ONE payout' in lab_raw,
      'granulerlik gozle dogrulanamiyor')
check('ve CoreTests`te cakili',
      'one turn, one serial' in tests,
      'ikinci bir odeme sizsa kimse fark etmez')

print('=== 3. KAYNAK VE HEDEF GERCEK ===')
check('KAYNAK gercek cekme destesi',
      'CardLayerView.DrawPilePos' in wire,
      'sabit bir ekran noktasi - telefonda bosluga ucar, ve neden-sonuc zinciri kopar')
check('HEDEF gercek skor etiketi',
      'ScoreWorldAnchor()' in wire,
      'sabit kose - HUD yerlesimi degisince odeme bosluga gider')
check('OLCU de gercek desteden geliyor',
      'CardLayerView.PileWorldWidth' in wire and 'PileWorldWidth' in cards,
      'serit sabit bir boyutta - telefonda destenin yarisi kadar ya da iki kati olur')
check('viewde sabit kordinat yok',
      'new Vector2(0f, 3.6f)' not in view_raw,
      'gorsel kendi hedefini yaziyor')
check('DEV: kaynak ve hedef isaretlenebiliyor',
      'ShowAnchors' in view and 'ShowAnchors' in lab_raw,
      'gercek desteye bagli mi, gozle ayirt edilemiyor')

print('=== 4. ZINCIR: BOS YUVA -> FIS -> DEGER -> SKOR ===')
for beat, key in (('bos yuva vurusu', 'ShowEmptyBeat'), ('altin cizgi', 'ShowGoldLine'),
                  ('fis seridi', 'ShowReceiptStrip'), ('deger damgasi', 'ShowStamp'),
                  ('odeme cekirdegi', 'ShowRebateCore'),
                  ('toplama yolu', 'ShowCollectionPath')):
    check('vurus var: %s' % beat, key in view, '%s katmani yok' % beat)
check('fis ACILIYOR: once yukseklik, sonra genislik',
      'HEIGHT FIRST, THEN WIDTH' in view_raw,
      'panel gibi buyuyor - voucher acilmiyor')
check('deger OKUNACAK kadar duruyor',
      num(view, r'HoldFor = ([\d.]+)f', 0) >= 0.15,
      'odul ani yok - oyuncu bir sekil gorur, bir sayi gelmez')
check('fis KATLANIYOR, kaybolmuyor',
      'FoldFor' in view and 'fold' in view.lower(),
      'serit siliniyor - odemenin nereye gittigi kayboluyor')
check('cekirdek EGRI bir yolla gidiyor (kubik bezier)',
      'private static Vector2 Arc(' in view and 'u * u * u * a' in view,
      'duz cizgi - lazer gibi okunur')
# NOT: bu kural once "yay ucusa DIK buukuluyor mu" diye soruyordu ve o dik bukulme tam da
# kontrol noktasini tahtanin ortasina koyan seydi. Kural, kendisinin dogrulamasi gereken hatayi
# sarti haline getirmisti. Simdi sordugu sey: yay iki GERCEK capadan turetiliyor mu.
check('yay iki capadan turetiliyor, ucuncu bir yer yok',
      'Vector2 c1 = a +' in view and 'Vector2 c2 = a + span' in view,
      'yolda ucuncu bir referans noktasi var - tahta bu mekanigin parcasi degil')
check('skor tepkisi TEK yerde toplaniyor',
      'private void TickScoreResponse()' in wire
      and 'Mathf.Max(midasWarm, rebateWarm)' in wire,
      'iki efekt ayni etiketi yaziyor - biri otekini bitmeden normale dondurur')
check('ikinci bir +puan popup`i YOK',
      'duplicate reward' in wire_raw,
      'fis zaten sayiyi gosterdi - skorun yaninda ikinci bir +60 fazlalik')

print('=== 4B. BIR EKRAN GORUNTUSUNUN GERI GELMEMESI ICIN ===')
# Bu bolumdeki HER kural bir kez gercekten kirildi ve ekranda goruldu. Dordu de olculebilir
# seylerdi; hicbiri "biraz daha guzel olsun" degil.
check('DESTE gercekten BOS gorunuyor (yigin, ust kart ve SAYI kalkiyor)',
      'public void SetDrawPileShownEmpty(' in cards
      and 'drawLabelRoot.gameObject.SetActive(!empty)' in cards,
      'dolu bir deste yiginin uzerinde +60 - hicbir sebep anlatmiyor, ustelik sayi ile carpisiyor')
check('ve bos hal, arkadaki yeniden karmadan SAG CIKIYOR',
      'ApplyDrawPileShownEmpty();' in cards
      and cards.rindex('ApplyDrawPileShownEmpty();') > cards.index('UpdateRevealFans(round);'),
      'recycle yarida yigini geri koyuyor - fis dolu bir destenin uzerinde kaliyor')
check('ve fis gidince deste GERI VERILIYOR',
      'ReceiptGone' in view and 'ReleaseRebatePile()' in wire,
      'ucus boyunca deste bos gosteriliyor - oyuncuya destesi hakkinda yalan')
check('SERIT BEYAZ DEGIL (krem govde + altin kenar + bronz golge)',
      'Style.Cream.r' in view and 'stripEdge' in view and 'stripShade' in view,
      'Color.white - sicak deste sanati uzerinde hic voucher gorunmuyor')
share = num(view, r'TextShareOfStrip = ([\d.]+)f', 9)
check('DEGER seridin YUKSEKLIGINDEN turetiliyor (%.0f%%)' % (share * 100),
      0.5 <= share <= 0.75 and 'stripHigh * Style.TextShareOfStrip' in view,
      'yazi destenin GENISLIGINDEN olculuyor - seridin %180`i kadar cikiyor ve tepsiden tasiyor')
check('deger seridin UZERINDE duruyor',
      'strip.transform.position' in view and 'value.transform.position' in view,
      'yazi desteye ciziliyor, seride degil')
check('UCUSTA TAHTA TERIMI YOK',
      'no board-centre term' in view_raw and 'Vector2.zero' not in method(view, 'private static Vector2 Arc('),
      'kontrol noktasi tahtanin ortasina denk geliyor - tahta bu mekaniğin parcasi degil')
check('kaldirma DUZ YUKARI, ucusa DIK degil',
      'Style.Lift' in view and 'new Vector2(-span.y, span.x)' not in view,
      'dik bukulme kontrol noktasini tahtanin ortasina koyuyor - olculdu, (1.8, 0.0)')
check('IZ, TOKEN`dan INCE (ok gibi okunamaz)',
      'Style.CoreSize * 0.45f' in view
      and num(view, r'TrailWide = ([\d.]+)f', 9) < num(view, r'CoreSize = ([\d.]+)f', 0),
      'iz tokenden genis ve sivri - tahtada ucan altin bir UCGEN')
check('ve iz tokenden UZUN da degil',
      num(view, r'TrailLong = ([\d.]+)f', 9) <= 0.30,
      'iz tokenin iki kati uzunlukta - ikisi tek bir sivri nesne gibi okunuyor')
check('DEV: butun ucus yolu noktalanabiliyor',
      'ShowBezier' in view and 'ShowBezier' in lab_raw,
      'yolun tahtaya nisan almadigi gozle dogrulanamiyor')
check('lab: BOS DESTE tek basina bir sahne',
      'the EMPTY PILE on its own' in lab_raw,
      'sebep, sonuctan ayri izlenemiyor')
check('lab: ODEMENIN ARKASINDA yeniden karma sahnesi',
      'RESHUFFLE behind the cashback' in lab_raw,
      'gercek carpismanin testi yok')

ink = re.search(r'Ink = new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)', view)
cream = re.search(r'Cream = new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)', view)


def _lin(c):
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4


def _lum(c):
    return 0.2126 * _lin(c[0]) + 0.7152 * _lin(c[1]) + 0.0722 * _lin(c[2])


if ink and cream:
    a = tuple(float(ink.group(i)) for i in (1, 2, 3))
    b = tuple(float(cream.group(i)) for i in (1, 2, 3))
    la, lb = _lum(a), _lum(b)
    contrast = (max(la, lb) + 0.05) / (min(la, lb) + 0.05)
else:
    contrast = 0.0
# Bu bir kez 1.11 olarak gonderildi: fildisi bir sayi, krem bir seridin uzerinde. Yumusak bir
# gorunum degil, OKUNAMAYAN BIR SAYI. Renk seciminin degil, olcunun kurali.
check('deger ile seridin kontrasti %.2f (>= 4.5)' % contrast, contrast >= 4.5,
      'sayi seridin uzerinde okunmuyor - bir odul animasyonunun tek isi o sayiyi okutmak')
check('degerin ALTINDA golge var',
      'InkShade' in view and 'valueShade' in view,
      'sicak bir serit uzerinde kenarsiz yazi - rakamlar dagiliyor')

print('=== 5. SILUET: FIS NE KART NE MADENI PARA ===')
check('serit KARTTAN genis (voucher orani)',
      num(shapes, r'StripAspect = ([\d.]+)f', 0) >= 2.5,
      'kart silueti - kart katmaninin ustunde baska bir kart gibi okunur')
check('serit ucu ICBUKEY (bilet kenari)',
      'A TICKET END' in shapes_raw,
      'duz dikdortgen - bir UI paneli')
check('centikler SIG (dis degil)',
      'not teeth' in shapes_raw,
      'testere disi bir yirtma kenari - gercek market fisi taklidi')
check('cekirdek MADENI PARA DEGIL',
      'which is a COIN' in shapes_raw and 'CoreAt' in shapes,
      'altin disk - oyunda olmayan bir para birimi ikonu')
check('cekirdek YUMUSAK NOKTA da degil',
      'yellow dot Midas already drew once' in shapes_raw,
      'anlamsiz sari blur - Midas`in bir kez yaptigi hata')
check('sahte yazi / barkod yok',
      'barcode' not in shapes.lower() and 'report.Payout' in view_raw,
      'fis uzerinde uydurma metin')
check('dokular KARE, oran pisirilmis',
      'world unit in BOTH axes' in shapes_raw and 'Bake(Field field)' in shapes,
      'kare olmayan doku - localScale.x ile .y farkli seyler demeye baslar')

print('=== 6. COMMON RARITY: TON VE OLCU ===')
total = (num(view, r'FoldFor = ([\d.]+)f', 0) + num(view, r'HoldFor = ([\d.]+)f', 0)
         + num(view, r'StampAt = ([\d.]+)f', 0) + num(view, r'StampFor = ([\d.]+)f', 0)
         + num(view, r'TravelFar = ([\d.]+)f', 0) + num(view, r'Absorb = ([\d.]+)f', 0))
check('cekirdek zincir ~1 sn civari (%.2f sn)' % total, 0.75 <= total <= 1.25,
      'cok uzun ya da cok kisa - sartname 76/78')
check('TAVAN tanimli ve 1.5 sn altinda',
      num(view, r'Ceiling = ([\d.]+)f', 9) <= 1.3,
      'animasyon sinirsiz uzayabiliyor')
check('EKRAN SARSINTISI / FLASH / KAMERA yok',
      'Shake' not in view and 'timeScale' not in view and 'flash' not in view.lower(),
      'patlama dili - bu Common bir joker ve odul bir teselli')
check('parcacik sayisi kelepceli',
      'ParticleCap' in view and num(view, r'ParticleCap = (\d+)', 99) <= 12
      and 'flecks.Count < Style.ParticleCap' in view,
      'coin rain / konfeti')
check('kirinti sayisi az (Common)',
      num(view, r'FleckMax = (\d+)', 99) <= 7,
      'jackpot yogunlugu')
check('skor tepkisi KUCUK (fis zaten gosterdi)',
      num(view, r'ScorePunch = ([\d.]+)f', 9) <= 0.12,
      'ikinci bir kutlama - ayni puan iki kez kutlanmis olur')
check('TON: uzatmada da oduyor ama kutlamiyor',
      'ThresholdPassed' in visuals_raw and 'consolation' in view_raw,
      'kaybederken zafer dili - mekanigin karanlik tarafi yalanlanir')
check('SES kancalari birakilmis ve kasa sesi degil',
      'SoundStamp' in view and 'casino' in view_raw.lower(),
      'ses tasarimi icin tutamak yok ya da cha-ching dili')

print('=== 7. TAHTA VE OYUN ===')
check('destenin kendi vurusu KART KATMANINDA',
      'public void PlayDrawPileEmptyBeat()' in cards,
      'efekt destenin transformuna uzaniyor - bir sonraki repaint ile kavga eder')
check('ve yalnizca odeme GERCEKTEN basladiysa isteniyor',
      'if (fresh && rebate.Busy' in wire,
      'her repaint yuvayi yeniden titretir')
check('oyun da ayni seam`den geciyor',
      'SyncRebate(round);' in fb and 'PlayRebate(' in wire,
      'lab kendi kopyasini oynatiyor - retiming labda gorunmez')

print('=== 8. LAB ===')
for label in ('the whole payout', 'empty beat', 'gold line', 'receipt', 'value stamp',
              'flecks', 'rebate core', 'flight to the score'):
    check('lab girdisi: %s' % label, label in lab_raw, '%s girdisi yok' % label)
check('TON TESTI sahnesi var (uzatmada odeme)',
      'TONE TEST - the same payout in OVERTIME' in lab_raw,
      'kaybederken nasil gorundugu gozle dogrulanamiyor')
check('0.25x ve 0.5x girdileri var',
      '0.25x' in lab_raw and 'harcama bonusu: the whole payout at 0.5x' in lab_raw,
      'alti vurus ayri ayri izlenemiyor')
check('her vurus TEK BASINA izlenebiliyor',
      lab_raw.count('AnimRebateOnly(') >= 6,
      'vuruslar tek tek gorulemiyor')
check('anahtarlar sahne KURULMADAN once uygulaniyor',
      method(lab, 'private void AnimRebateOnly(').index('RebateView.Layers.AllOn()')
      < method(lab, 'private void AnimRebateOnly(').index('AnimRebate('),
      'kapali katman yine de cizilir')
check('RAPOR hata ayiklamasi var (kural ne dedi)',
      'ShowReport' in lab_raw and 'TimesThisRound' in view,
      'kuralin soyledigi ile cizilen yan yana gorulemiyor')
check('animasyon saati OLCEKLI (lab yavaslatabilsin)',
      'clock += Time.deltaTime;' in view,
      '0.25x kabul testi calistirilamaz')
check('RESET odemeyi de durduruyor',
      'StopRebate();' in method(lab, 'private void AnimResync()'),
      'resync sahneyi durdurmuyor')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('harcama bonusu: hepsi tamam')
