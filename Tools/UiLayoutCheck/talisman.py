# -*- coding: utf-8 -*-
# TILSIM / HAYALET ALANI - statik kontrol.
#
# Bu dosyanin korudugu tasarim kararlari, kural degil: hangi sey Core'da kaliyor, hangi tuzaklara
# geri dusulmuyor, ve bir raunt sinirini asan bu gucun sunumu nerede kopmuyor.
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


def method(src, signature, after=None):
    """The body of a method. `after` scopes the search to a class - SpecialPowers.cs holds a
    dozen powers and every one of them has a Run and an OnRoundStarted, so an unscoped index()
    silently returns somebody else's method. cold_sink.py was bitten by exactly this once."""
    start = src.index(after) if after else 0
    i = src.index(signature, start)
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


# Where TilsimPower's own body starts, so a method lookup cannot wander into another power.
TILSIM = 'class TilsimPower'

fail = []


def check(label, ok, why):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


power = strip_comments(read('Assets', 'Scripts', 'Core', 'Powers', 'Definitions',
                            'SpecialPowers.cs'))
power_raw = read('Assets', 'Scripts', 'Core', 'Powers', 'Definitions', 'SpecialPowers.cs')
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Powers', 'TalismanVisuals.cs'))
view = strip_comments(read('Assets', 'Scripts', 'View', 'TalismanView.cs'))
board = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'TalismanView.cs')
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'TalismanShapes.cs'))
shapes_raw = read('Assets', 'Scripts', 'View', 'TalismanShapes.cs')
spirit = read('Assets', 'Resources', 'Shaders', 'TalismanSpirit.shader')
stain = strip_comments(read('Assets', 'Scripts', 'View', 'TalismanStain.cs'))
stain_raw = read('Assets', 'Scripts', 'View', 'TalismanStain.cs')
tendril = strip_comments(read('Assets', 'Scripts', 'View', 'TalismanTendril.cs'))
tendril_raw = read('Assets', 'Scripts', 'View', 'TalismanTendril.cs')
stain_shader = read('Assets', 'Resources', 'Shaders', 'TalismanStain.shader')
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
boardview_raw = read('Assets', 'Scripts', 'View', 'BoardView.cs')
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. HAYALET IZ KENDI SANATIYLA CIZILIYOR ===')
check('disari tasan hayalet, hayalet blogun KENDI tile`ini kullaniyor',
      'ViewUtil.ApplyTile(trace' in boardview,
      'hayalet iz duz bir kare - blogun sanati ve materyali zaten vardi, sorulmamisti')
check('tinti notr birakildi ki materyal gorunsun',
      'new Color(1f, 1f, 1f, 0.35f)' in boardview, 'tint materyali eziyor')
check('hasat, blogun KENDI yuzunden basliyor (karttan, kind`dan degil)',
      'view.FaceOf(' in view and 'public Sprite FaceOf(' in boardview,
      'hayaletin yuzu varsayilan blok - hayalet sanati KART elementinde')

print('=== 2. CORE = GERCEK, VIEW = SUNUM ===')
check('guc kendi raporlarini yaziyor',
      'class TalismanActivationVisuals' in visuals
      and 'class TalismanGroundVisuals' in visuals, 'rapor yok')
check('ikisi de KAYDEDILMIYOR ([NotSaved])',
      power_raw.count('[field: NotSaved]') >= 2
      and 'LastActivation' in power_raw and 'LastGround' in power_raw,
      'per-event sunum verisi save`e giriyor')
check('hayaletler SILINMEDEN once fotograflaniyor',
      'foreach (KeyValuePair<GridPos, Cube> ghost in ctx.Round.Board.OutsideCubes)'
      in method(power, 'public override bool Run(', TILSIM),
      'silinen kubun rengiyle animasyon baslatilamaz')
check('"geri kazanilabilir mi" kurali TEK yerde (CanReclaim)',
      power.count('>= 0 && cell.Y >= 0') == 1
      and 'public static bool CanReclaim(' in power
      and power.count('CanReclaim(') >= 3,
      'kosul birden fazla yerde - biri degisirse digeri sessizce yanlis kalir')
check('LAB da o kurali kendi yazmiyor, soruyor',
      'TilsimPower.CanReclaim(' in lab and '.X >= 0' not in lab.split('AnimTalisman')[1][:4000],
      'lab koordinat testini tekrar yaziyor - bir lab bir kurali asla kopyalamamali')
check('View o kurali TEKRAR YAZMIYOR',
      '.X >= 0' not in view and '.Y >= 0' not in view,
      'View hangi hucrenin geri kazanilacagini kendi hesapliyor')
check('View hangi hucrelerin bonus zemin oldugunu SECMIYOR',
      'report.Cells' in view and 'IsOptional' not in view,
      'View bonus zemini tahtadan cikariyor')
check('sonraki rauntun zemini, o tahta KURULURKEN raporlaniyor',
      'LastGround.Cells.AddRange(convertedCells)' in method(
          power, 'public override RoundConfig FilterRoundConfig(', TILSIM),
      'zemin raporu tahta kurulduktan sonra tahmin ediliyor')
check('OnRoundStarted calisma listesini temizliyor ama RAPORU temizlemiyor',
      'convertedCells.Clear();' in method(power, 'public override void OnRoundStarted(',
                                              TILSIM)
      and 'LastGround' not in method(power, 'public override void OnRoundStarted(', TILSIM),
      'raunt baslayinca View`un acacagi hediye kayboluyor')

print('=== 3. BONUS ZEMIN: ACIK CERCEVE ===')
check('dort KOSE RUNU var',
      'Corner(' in shapes and 'g.Corners' in view, 'bonus zemin hala tek duz renk')
check('koseler BIRLESMIYOR - kural bu bosluktan okunuyor',
      'never join' in view_raw.lower() or 'NEVER join' in view_raw,
      'kapali cerceve - normal hucreden ayirt edilemez')
check('kose runu KENDI kutusunu dolduruyor (hucre boyutunda sprite degil)',
      'THE STROKE FILLS ITS OWN BOX' in shapes_raw,
      'kose sprite`i hucre kadar - her kose devasa cikar')
check('zemin plakasi da degisti (eski zeytuni gitti)',
      (num(boardview, r'BonusGroundColor = new Color\(([\d.]+)f') or 1) < 0.2
      and 'jade' in boardview_raw.lower(), 'bonus zemin hala eski duz zeytuni')
check('uzerindeki kup BOYANMIYOR',
      'SetCubeKind' not in view and 'cube' not in view.lower().split('harvest')[0][-200:],
      'View kupu boyuyor')
check('idle cok seyrek ve hucre CAPINDA degil (tek kose, sonra karsisi)',
      (num(view, r'IdleMinInterval = ([\d.]+)f') or 0) >= 4
      and 'IdleCorner' in view, 'butun hucre nabiz atiyor')

print('=== 4. HASAT: MADDESEL DEGIL RUHSAL ===')
check('normal kup patlamasi KULLANILMIYOR',
      'ClusterBurst' not in view and 'FlashCells' not in view,
      'hayalet hasadi siradan patlama')
check('parca ATES/TAS degil: yumusak yonga',
      'TalismanShapes.Flake' in view and 'Debris' not in view, 'tas kirigi')
check('parca once BLOGUN KENDI rengini tasiyor',
      'ShardKeepsColour' in view and 'h.Colour' in view,
      'ilk kareden itibaren spectral - o blogun olumu olmaktan cikiyor')
check('geri kazanilamayan hayalet TOHUM birakmiyor',
      'h.Reclaimable ?' in view, 'her hayalet zemin talep ediyor gibi')
check('cok hayaletde animasyon UZAMIYOR, dalga sikisiyor',
      'HarvestCap' in view and 'stagger = Mathf.Max' in view,
      '20 hayalet 20 kat uzun suruyor')

print('=== 5. TALEP: ZEMIN DEGIL, SOZ ===')
check('talep havada duruyor, tahtaya oturmuyor',
      'ClaimLift' in view, 'talep zemine oturmus gibi')
check('hucre basina AYRI CELENK yok - tek sarmasik sistemi',
      'LoopWeave' not in view and 'PaintLoop' not in view and 'BuildClaims(' in view,
      'her hucrenin cevresinde bagimsiz halka - noel celengi / koleksiyon rozeti gibi okunuyor')
check('sarmasik TAHTANIN KENARINDAN cikiyor',
      'c.Origin' in view and 'EdgeRune' in view and 'board.MinX + board.Width - 1' in view,
      'sarmasik havada basliyor - tahtayla organik bagi yok')
check('bilesenler 4-komsuluktan cikariliyor (daginik talep daginik sistem olmasin)',
      'dx + dy == 1' in view, 'her hucre kendi adasi')

# ---- the cover is a SPRITE SHEET now, not a drawn root system ----
check('sarmasik CIZILMIYOR - sprite sheet',
      'talisman_vine_sheet' in view and 'Sprite.Create' in view,
      'kodlu sarmasik geri gelmis')
check('cizili kok makinesinden hicbir sey kalmadi',
      not any(k in view for k in ['class Runner', 'BuildRunner', 'PaintRunner', 'RootBody',
                                  'RootCrown', 'BuildRunner']),
      'kaldirilmasi istenen kodlu sarmasik hala duruyor')
check('9 kare, 5x2 gride okuma sirasinda dilimleniyor',
      "FrameCount = 9" in view and 'FrameColumns = 5' in view and 'FrameRows = 2' in view
      and '(FrameRows - 1 - row) * cellH' in view,
      'sheet yanlis dilimleniyor - kareler karisir')
check('sheet YOKSA uyari verip cikiyor (her karede exception degil)',
      'sheetMissing = true' in view and 'Debug.LogWarning' in view,
      'sheet yoksa her sarmasik her karede patlar')
check('KARELER AYNI AYAKTAN cizilmis (pivot sapin kesik ucunda)',
      'FootPivot' in view and '11f / 224f' in view and '54f / 216f' in view,
      'kareler ortalanmis - bitki buyurken kayar, dokuz ayri sarmasik gibi okunur')

# ---- placement: the one thing that decides whether it covers ----
check('yerlesim AYAGA gore degil KUTLEYE gore (cizimin agirligi ayagin cok otesinde)',
      'MassAt' in view and 'Style.MassAt * size' in view,
      'sarmasik hucreye degil hucrenin bir bucuk otesine dusuyor')
check('AYNALANAN kopya lean`i de aynaliyor',
      'flip ? -Style.ArtRise : Style.ArtRise' in view,
      'aynalanan sarmasiklar hucrenin yanlis tarafinda asili kalir')
check('SAYI YOK: kaplama hedefi var',
      'TargetCoverage' in view and 'TargetCellCoverage' in view
      and 'private static bool Covered(' in view and 'private static bool Barest(' in view,
      'kac hucre kac sprite tablosuna geri donulmus')
check('hucre sayisina bagli SABIT aktor tablosu KALMADI',
      not any(k in view for k in ['ClaimVinesSingleCell', 'ClaimVinesSmall', 'ClaimVinesMedium',
                                  'VinesPerCell', 'VineSpacing', 'RankDepth']),
      'sabit sayi tablosu geri gelmis - her boyutta yanlis olur')
check('kaplama SPRITE`IN KENDI SILUETINDEN olculuyor (bounding box degil)',
      'FrameMask' in view and 'private static bool Hits(Vine v' in view
      and 'MaskGrid' in view,
      'kaplama kutu ile olculuyor - bos kivrimlar dolu sayilir')
check('ulasilamayan ornek DOLU sayilmiyor',
      'public readonly List<bool> Given' in view and 'c.Given[best] = true;' in view,
      'vazgecilen ornekler kaplama diye sayiliyor - olcum kendi olctugu seyi gizler')
check('her KUSAK daha kucuk VE daha ERKEN karede duruyor',
      'GenerationScale' in view and 'GenerationFrame' in view
      and 'GenerationFrame = { 8, 7, 6, 5, 4 }' in view,
      'her segment son karede - tek asset tek uzunluk, PNG yigini')
check('uzak hedefte ZINCIR devam ediyor (her cocuk bir kucuk degil)',
      'bool far = Vector2.Distance(Tip(parent), want)' in view
      and 'far\n                ? parent.Generation' in view.replace('\r', ''),
      'ag menzili bitiyor - buyume girisin yaninda yigiliyor, uzak taraf bos kaliyor')
check('ADAY PUANLAMA var: kazanc - gomme - disari',
      'Style.OverlapPenalty' in view and 'Style.OutsidePenalty' in view
      and 'private Vine BestChild(' in view,
      'dallar rastgele yerlesiyor - kaplama ile ust uste binme ayni sey saniliyor')
check('cocuk EBEVEYNIN GOVDESINDEN dogiyor',
      'Vector2.Lerp(parent.Foot, parent.Mass, along)' in view and 'BranchAtMin' in view,
      'segmentler havada bagimsiz duruyor')
check('zincir ebeveynin UCUNDAN devam ediyor',
      'private static Vector2 Tip(Vine v)' in view and 'Tip(parent)' in view,
      'uzun mesafe tek sprite gerilerek gecilmis')
check('cocuk ebeveyn HENUZ BUYURKEN basliyor (paralel cephe)',
      'ChildSpawnAt' in view and 'parent.Start + parent.Duration * Style.ChildSpawnAt' in view,
      'dallar sirayla bitiyor - buyuk alan saniyelerce suruyor')
check('BUYUME TOPLAMI sinirli (alan buyudukce yavaslamiyor)',
      (num(view, r'ClaimCap = ([\d.]+)f') or 9) <= 1.6,
      '3x3, 1x1`den dokuz kat uzun suruyor')
check('KENAR biraz daha yogun (ortasi dolu kenari bos olmasin)',
      'EdgeBias' in view, 'ortada yumak, kenarlar bos')
check('guvenlik tavani var ama once kaplama geliyor',
      'MaxSegments' in view and 'c.Vines.Count < Style.MaxSegments && !Covered(c)' in view,
      'sonsuz buyume ya da sayiyla sinirlanmis buyume')
check('sarmasik ARTIK MASKELENMIYOR ve nedeni yazili',
      'VisibleInsideMask' not in view and 'c.Mask' not in view
      and 'NOTHING IS CLIPPED TO THE CLAIM' in view_raw,
      'leke hucreleri takip ediyor - eski stencil hero sanati govdesinden keser')

# ---- THE CURSE STAIN: assembled from the exact cells, drawn as one field ----
check('karanlik PARCALARDAN kuruluyor: yama + kopru + kose kaynagi + kenar dili',
      'private static void Patch(' in stain and 'private static void Bridge(' in stain
      and 'private static void Merge(' in stain and 'private static void Bleed(' in stain,
      'tek buyuk sekil - bounding box uzerine cizilmis bir panel')
check('parcalar MAX ile birlesiyor, ust uste CIZILMIYOR',
      'if (f > c.Field[i])' in stain and 'PARTS COMBINE WITH MAX' in stain_raw,
      'yari saydam quad`lar ust uste biniyor - hucre sinirlarinda bantlanma')
check('alan GERCEK geri kazanilan hucrelerden pisiyor (kutudan degil)',
      'List<GridPos> cells' in stain and 'IndexOf(cells,' in stain,
      'karanlik bounding box`i takip ediyor - L bile dikdortgen cikar')
check('kopru YALNIZ 4-komsu cift icin kuruluyor',
      '(dx == 1 && dy == 0) || (dx == 0 && dy == 1)' in stain,
      'seyrek talep tek kutleye yapisiyor')
check('kose kaynagi YALNIZ tam 2x2 icin kuruluyor',
      'if (right < 0 || up < 0 || far < 0)' in stain,
      'ortasi delik 3x3`un ortasi doluyor')
check('kenar dili YALNIZ geri kazanilmamis komsuya bakan kenarda',
      'if (IndexOf(cells, cells[i].X + nx, cells[i].Y + ny) >= 0)' in stain,
      'ic kenarlardan da tas cikiyor - dikis goze batiyor')
check('diller GOVDENIN ICINDE kok saliyor (boncuk degil)',
      'BleedRoot' in stain and 'rooted INSIDE the body' in stain_raw,
      'kenarda bir dizi yuvarlak boncuk - bulut silueti')
check('diller ESIT DEGIL: kimi dil kimi yumru',
      'float taper = 0.15f + 0.7f * Random01' in stain,
      'ayni boyda yuvarlak loblar - kenar yuvarlatilmis dikdortgen')
check('DERINLIK birlesimin BULANIGINDAN geliyor, cizen parcadan degil',
      'private static float[] Blur(' in stain and 'DEPTH COMES FROM A BLUR' in stain_raw,
      'ic taraf yumru yumru - hangi ovalin kopru hangi dairenin hucre oldugu goruluyor')
check('leke TAHTADAN KOYU, asla soluk ve asla gri',
      'Stain = new Color(0.02f, 0.052f, 0.05f)' in stain,
      'soluk leke zemin der, gri leke sis der')
check('RENK CEKILMESI gercek bir CARPMA - alpha tek basina lineer uzayda yetmiyor',
      'BlendMode.DstColor' in view and '_Mode > 0.5' in stain_shader
      and 'LINEAR' in stain_raw,
      'yine yalniz alpha overlay - ekranda arka planin 0.87`si, yani gorunmuyor')
check('ton OLCULDU: govde 0.65-0.72, kenar ~0.80 hedefinde',
      0.35 <= (num(stain, r'Centre = ([\d.]+)f') or 0) <= 0.5
      and 0.1 <= (num(stain, r'Edge = ([\d.]+)f') or 0) <= 0.25,
      'karanligi gormek icin dikkatli bakmak gerekiyor - kabul testi bunu FAIL sayiyor')
check('BUYUME BIR KANAL, ayri bir zaman cizelgesi degil',
      '_Front' in stain_shader and 'THE WHOLE ANIMATION IS A CHANNEL' in stain_raw,
      'karanligin kendi zamanlayicisi var - sarmasiktan bagimsiz kayiyor')
check('hucre varisi SARMASIGIN kendisinden olculuyor',
      'private void MeasureArrival(' in view and 'Hits(vine, c.Field[k])' in view,
      'karanlik mesafeye ya da indekse gore geliyor - bitki gecmeden yer kararıyor')
check('yama MERKEZDEN aciliyor',
      'IT OPENS FROM ITS MIDDLE' in stain_raw, 'yama hazir bir kare olarak beliriyor')
check('kopru IKI UCTAN birden kapaniyor',
      'Mathf.Min(whenA + Style.BridgeClose * u' in stain,
      'kopru tek parca pop ediyor - alan hucre hucre kapanmiyor')
check('RENK karanliktan ONCE cekiliyor',
      'DrainLead' in stain and 'TalismanStain.Style.DrainLead / scale' in view,
      'karanlik sarmasiktan once geliyor')
check('ACILIS ayni haritanin TERSI - ayrica yazilmis degil',
      'Mathf.Lerp(1.1f, -0.15f, Ease(retract))' in view,
      'ortu tek parca soluyor - genel dissolve')

check('TEMAS GOLGESI silueti kadar - dev daire yok',
      0 < (num(view, r'ShadowStrength = ([\d.]+)f') or 9) <= 0.2
      and (num(view, r'ShadowSpread = ([\d.]+)f') or 9) <= 1.05,
      'sarmasiklarin arkasinda dev gri daireler - spot gibi')

check('KOK AGZI var: tahtanin kenarinda cukur + altin catlak',
      'OriginSize' in view and 'c.EdgeRecess' in view and 'OriginRecess' in view
      and (num(view, r'OriginSize = ([\d.]+)f') or 0) >= 0.2,
      'nereden ciktigi okunmuyor - sarmasik tahtanin yaninda havada duruyor')
check('cukur ONCE aciliyor, altin catlak SONRA yaniyor',
      'Style.EdgeWake * 0.45f' in view,
      'run tahtaya cizilmis gibi beliriyor, tahta acilmis gibi degil')
check('TOHUM UI noktasi degil',
      (num(view, r'SeedSize = ([\d.]+)f') or 0) >= 0.15,
      'tohum bir piksellik altin nokta')
check('lab hasattan sonra hayaletleri GERCEKTEN aliyor',
      'TakeOutsideCellsForConversion' in lab,
      'hasat oynatiliyor ama izler duruyor - talebin altinda acik gri kareler kaliyor')
check('BONUS ZEMIN ortu kalkana kadar TUTULUYOR (tahtada hazir duruyor olsa da)',
      'owner.ReleaseCells(releasing)' in view and 'view.HoldCells(held)' in view,
      'hediye ilk kareden beri ortada - ustunden bir karanlik soluyor sadece')
check('zemin ve run, LEKENIN KENDI cephesine gore aciliyor - ayri zamanlayici yok',
      'private float Uncover(' in view and 'float appear = Ease(Uncover(g.Cell));' in view,
      'run hala kendi gecikmesiyle geliyor - iki zamanlayici kayar')
check('tutulan hucre HER durumda geri veriliyor (yarida kesilse bile)',
      'ReleaseFloor();' in method(view, 'private void ClearClaims()'),
      'raunt boyunca tahtada bos hucre kaliyor')
check('KARA AKINTILAR: acilista karanlik sarmasiga GIDIYOR',
      'private void BuildStreams(' in view and 'StreamLength' in tendril,
      'karanlik durdugu yerde soluyor - hicbir zaman gercekten orada degilmis gibi')
check('her kopya CEVRILIYOR / AYNALANIYOR / BOYUTLANIYOR',
      'VineTiltSpread' in view and 'VineSizeSpread' in view
      and 'v.Flip' in view and 'flipY' in view,
      'dokuz kare tek boyutta tekrar ediyor - dosemeli doku')
check('havuzdan gelen renderer`in eski aynasi siliniyor',
      'r.flipX = false;' in view and 'r.flipY = false;' in view,
      'havuzdan gelen sarmasik tabaninin yanlis tarafindan buyur')
check('TEMAS GOLGESI var - yuzeyin uzerinde duruyor',
      'ShadowStrength' in view and 'v.Shadow' in view,
      'sarmasik yuzeye cizilmis, uzerinde yatmiyor')
check('golge DUNYA yonunde dusuyor (sarmasikla birlikte donmuyor)',
      'a shadow does not turn with the thing casting it' in view_raw,
      'golge sarmasikla birlikte donuyor - isik her parcada baska yerden')

# ---- choreography ----
check('kare basina sure gozle takip edilebilir (govde en yavas)',
      (num(view, r'GenerationFrameTime =\s*\{ ([\d.]+)f') or 0) >= 0.055,
      'sarmasik 150ms`de olusuyor - buyumemis, acilmis')
check('dallar EBEVEYN BUYURKEN cikiyor (hepsi ayni anda degil)',
      0 < (num(view, r'ChildSpawnAt = ([\d.]+)f') or 9) < 1,
      'alti sarmasik ayni karede basliyor - once kok sonra dal okunmuyor')
check('BASLAMADAN once hicbir sey cizilmiyor',
      'if (grow <= 0f)' in view,
      'sarmasiklar ilk karesini bastan cizer - her hucreye dagilmis noktalar')
check('geri cekilme AYNI ANIMASYONUN TERSI (fade degil)',
      'span - (v.Start + v.Duration)' in view and 'grow *= 1f - Ease' in view,
      'ortu solup gidiyor - sarmasiklar hic orada olmamis gibi')
check('acilirken ALTINDAN yeni alan cikiyor (tohum ortunun ALTINDA)',
      (num(view, r'SeedOrder = (\d+)') or 99) < (num(view, r'VineOrder = (\d+)') or 0),
      'tohum ortunun ustunde duruyor - ortu bir seyi gizlemiyor')

# ---- the two bugs the player actually hit ----
check('TAHTA PLAKASI arenadan olculuyor, buyuyen depodan DEGIL',
      'int arenaWide = 1;' in board and 'Surface.Build(center, arenaWide, arenaHigh' in board
      and 'board.IsInside(gp) && !board.IsOptional(gp)' in board,
      '2x2 talep tahtaya iki TAM sutun ekliyor - dort hucre icin bir sutun zemin')
check('hucre boyu da arenadan (talep tahtayi kucultmesin)',
      'maxWorldSize / arenaWide' in board,
      'talep geldiginde butun tahta kuculuyor')
check('geri acilma suresi ORTUNUN kendi suresini takip ediyor',
      'longest * Style.RetractRate' in view and 'RetractRate' in view,
      'geri sarma sabit sureye sikismis - orgu bir iki karede yok oluyor, animasyon yok gibi')
check('geri sarmanin SAATI ile SURESI ayni yerden geliyor',
      'Ease(Span(revealClock, 0f, RevealSpan()))' in view,
      'iki ayri sayi - biri biterken oteki devam ediyor')

# ---- the soft layers: what covers the claim where the ART did not ----
check('GOLGE KOKLERI var - sarmasiktan cikip talebin geri kalanina suruluyor',
      'private void BuildTendrils(' in view and 'public void Ribbon(' in tendril,
      'sarmasiklar arasi bosluklardan dis alan goruniyor - bolge sarilmis gibi durmuyor')
check('kokler kaplama alanindan suruluyor (bos yere degil, EN BOS yere)',
      'if (!Barest(c, out target))' in method(view, 'private void BuildTendrils('),
      'kokler rastgele sacilmis - kapanmayan yer kapanmadan kaliyor')
check('kok yesil sanati TAKLIT ETMIYOR (yaprak yok, isik yok, yesil yok)',
      'MUST NEVER TRY TO LOOK LIKE THE ART' in tendril_raw
      and 'no green, no highlight' in tendril_raw,
      'kok ikinci bir bitki olmus - hero sanatla yarisiyor')
check('bir kok EN FAZLA bir kez catalliyor, catal catallamiyor',
      'TendrilForkMax' in tendril and 'forks >= TalismanTendril.Style.TendrilForkMax' in view,
      'catal ustune catal - kok degil egrelti otu')
check('ZAR CEPLERI var ve UC noktaya asiliyor',
      'private void BuildPockets(' in view and 'public void Pocket(' in tendril
      and 'p.Count < 3' in view,
      'alan uzerinde bir seyler duran acik bir alan - ustu kapanmamis')
check('cep ICBUKEY sarkiyor (disbukey = kabarcik)',
      'VeilSag' in tendril and 'THE SAG' in tendril_raw,
      'cep bir kabarcik gibi boslukta duruyor')
check('cep boyutu IKI UCTAN da kelepceli - dev levha yok',
      'VeilMin' in tendril and 'VeilMax' in tendril
      and 'TalismanTendril.Style.VeilMax * 0.5f' in view,
      'her bosluk kapatilmis - sarmasik agi kayboldu, kati bir levha kaldi')
check('cep HER bosluga konmuyor',
      'PocketsCap' in view and 'c.Pockets.Count < want' in view,
      'her bosluga bir cep - kati levha')
check('cep ilgili hucre KARARDIKTAN sonra geriliyor',
      'ArrivalNear(c, middle)' in view,
      'perde daha buyumemis bir dala asili duruyor')
check('SARMASIGIN CEVRESINDE lanet derinligi var ve TEMAS GOLGESINDEN ayri',
      'v.Depth' in view and 'DepthSpread' in view and 'DepthStrength' in view
      and 'ShadowStrength' in view,
      'sarmasik ya havada duruyor ya arkasinda tek bir lekesi var')
check('derinlik bitkinin KUTLESINE gore olceklenıyor, ayagina gore degil',
      '(1f - Style.DepthSpread) * (v.Mass - v.Foot)' in view,
      'karartma bitkinin altindan kayiyor')
check('ALTIN MUHUR var ve KUSAK KUSAK yayiliyor',
      'private float Seal(Claim c, Vine v)' in view and 'v.Generation * Style.SealPulse' in view,
      'talep bitisinde hicbir sey olmuyor - kilit kapanmiyor')
check('mühür KAPANIS, parlama DEGIL (bitis daha KARANLIK)',
      'SealDarken' in view and '1f - Style.SealDarken * Ease' in view,
      'talep sonunda bolge parliyor - havai fisek, kilit degil')
check('altin sarmasigin KENDI renginde tasiniyor, uzerine cizilmiyor',
      'Color.Lerp(body, Style.SealGold' in view,
      'sarmasigin uzerine ayri bir isik konmus - sticker gibi')
check('MUHUR ZEMINE de isliyor: gectigi yer bir an KOYULASIYOR',
      'SealSweep' in view and '_Sweep' in stain_shader and 'sweep = Span(c.Clock, c.Span' in view,
      'muhur yalniz sarmasiga dokunuyor - zemin hic tepki vermiyor')
check('muhur KAPANISI bir SIKISMA, cember ya da parlama degil',
      'SealTighten' in view and '_Contract' in stain_shader,
      'kapanista bir halka ya da flas var - kilit degil havai fisek')
check('BOSTA: ic ice gecen tek bir kara damar, alan geneli nabiz DEGIL',
      'CrawlGain' in view and 'private void Crawl(' in view
      and 'k * 1.25f' in view,
      'bolge tek parca nefes aliyor - UI ogesi gibi')

print('=== 6. MALZEME ===')
check('isik DUNYA uzayinda (bir sarmasik onlarca rotasyondan olusuyor)',
      'worldRight' in spirit and 'TransformObjectToWorldDir' in spirit,
      'her segment kendi yonunden isikli')
check('isigin TABANI var (yoksa yari karanlik tahtaya gomulur)',
      '0.34 + 0.66 * saturate(facing)' in spirit, 'yalniz yonlu isik')
check('maddesizlesme duz alpha fade DEGIL (ortadan inceliyor)',
      '_Spectral' in spirit and 'core = 1.0 - smoothstep' in spirit,
      'sprite kapatiliyor gibi')
check('neon yok: sicaklik ic gradyan, disari isik degil',
      '_Heat' in spirit and 'never an outward glow' in spirit,
      'ruhsal seyler isik sacıyor - neon')
check('_FaceHalf tuzagi var', '_FaceHalf' in spirit, 'sprite 1 birim varsayimi')

print('=== 7. SILUET PRIMITIFI ===')
check('kaynasan daire kullaniliyor (buyuyen/organik sey)',
      'private static float Fuse(' in shapes and 'Blob(' in shapes,
      'organik sey poligonla cizilmis')
check('yanlis primitif dersi yorumda yaziyor',
      'flower' in shapes_raw.lower() and 'rotor' in shapes_raw.lower(),
      'sonraki pas ayni primitif hatasina duser')
check('dugum GLIF DEGIL (acik halka + baglama = e harfi)',
      'A LASHING, not a ring' in shapes_raw and 'letter e' in shapes_raw,
      'dugum bir harfe benziyor')
check('cizili sarmasik sekilleri de temizlenmis',
      not any(k in shapes for k in ['RootBody', 'RootBulb', 'RootCrown', 'BakeStem']),
      'sheet geldi ama cizili kok sekilleri dosyada duruyor')

print('=== 8. LAB ===')
scenes = re.search(r'private enum AnimTalismanScene\s*\{(.*?)\n        \}', lab, re.S)
names = [x.strip().rstrip(',') for x in scenes.group(1).split('\n') if x.strip()] if scenes else []
check('tılsım sahneleri tam (>= 11)', len(names) >= 11, 'sahne sayisi %d' % len(names))
check('sahne tahtayi SOLA kaydiriyor (dis alan panelin altinda kalmasin)',
      'AnimTalismanShift' in lab and 'MainBoardCenter + new Vector2(-AnimTalismanShift' in lab,
      'Tılsım`in butun olayi tahtanin sag disinda oluyor - lab paneli de orada, yani sahne '
      'kendi konusunu panelin altina cizmis olur')
check('cozum LAB PANELINI degil TAHTAYI oynatiyor',
      'public const int VisibleRows = 12;' in
      open(os.path.join(ROOT, 'Assets', 'Scripts', 'View', 'AnimationLabView.cs'),
           encoding='utf-8').read()
      and 'PageRows' not in open(os.path.join(ROOT, 'Assets', 'Scripts', 'View',
                                              'AnimationLabView.cs'), encoding='utf-8').read(),
      'lab paneli bozulmus - panel her animasyonun izlenebilmesi icin tek kolon kalmali')
check('lab kendi tahtasini kuruyor (round Core state`ine dokunmuyor)',
      'new GameBoard(w, h, cells, cells)' in lab,
      'lab gucun gercek Run`unu cagiriyor - puan ve tahta mutasyonu')
check('bonus zemin sahnesi tahtayi GERCEK bonus hucrelerle kuruyor',
      'new GameBoard(w, h, cells, cells)' in lab,
      'runlerin altindaki plaka taklit')
check('dikdortgen olmayan talep sahnesi var',
      'GroundShape' in lab and 'AN L.' in lab_raw, 'her talep dikdortgen varsayiliyor')
check('geri kazanilamayan hasat sahnesi var',
      'HarvestNoClaim' in lab, 'yalniz basarili hasat test ediliyor')
for toggle in ['ShowVines', 'ShowKnots', 'ShowContactShadows', 'ShowCellPatches',
               'ShowNeighborBridges', 'ShowCornerMerges', 'ShowEdgeBleed',
               'ShowLocalColorDrain', 'ShowShadowTendrils', 'ShowVeilPockets',
               'ShowCurseDepth', 'ShowActualReclaimCells', 'DebugParts', 'ShowCorners',
               'ShowSeeds']:
    check('lab anahtari: %s' % toggle, 'TalismanView.Layers.' + toggle in lab_raw,
          '%s anahtari yok' % toggle)
check('resync tılsım sahnesini durduruyor',
      'StopAnimTalisman();' in method(lab, 'private void AnimResync()'),
      'resync sahneyi durdurmuyor')
check('ham bolumde artik yalniz Tılsım zemini yok - Mapus cikti, bu da cikmali',
      'Tılsım bonus zemini (yalnız renk)' not in lab_raw,
      'ham bolumde hala eski Tılsım girisi var')
# ---- the acceptance set: the shapes the darkness has to survive ----
for scene in ['StainSingle', 'StainTwo', 'StainSquare', 'StainL', 'StainHole', 'StainSparse',
              'UnsealSquare', 'UnsealHole', 'UnsealSparse']:
    check('lab sahnesi: %s' % scene, 'AnimTalismanScene.' + scene in lab_raw,
          '%s sahnesi yok - o geometri hic denenmiyor' % scene)
check('SARMASIKLAR KAPALI sahnesi var - asil kabul testi bu',
      'AnimTalismanCurse(AnimTalismanScene.StainSquare, false)' in lab_raw,
      'bitki olmadan alanin hala lanetli okunup okunmadigi hic bakilmiyor')
check('tek tek katman sahneleri BAKE`e gidiyor, renderer gizlemeye degil',
      'private void AnimTalismanOnly(' in lab
      and 'TalismanView.Layers.CurseOff();' in lab,
      'katman kapatmak icin renderer gizleniyor - parcalar tek bir alan, gizlenecek renderer yok')
check('DIKDORTGEN TESTI: geri kazanilan hucreler ve kutulari cizilebiliyor',
      'ShowActualReclaimCells' in lab_raw and 'private void BuildMarks(' in view,
      'karanligin hucreleri mi kutuyu mu takip ettigi tartisma konusu kaliyor')
check('parca renkleri ile hata ayiklama var (yama/kopru/kaynak/dil)',
      'DebugParts' in lab_raw and '_Debug > 0.5' in stain_shader,
      'hangi parcanin nereyi cizdigi goruntuden anlasilmiyor')

print('=== 9. KURAL TESTI ===')
check('bonus zemin kayip kosuluna dahil - testi var',
      'Tilsim_BonusGroundIsStillSomewhereToPlay' in tests, 'test yok')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('tılsım / hayalet alani: hepsi tamam')
