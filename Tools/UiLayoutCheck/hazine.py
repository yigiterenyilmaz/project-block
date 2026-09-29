# -*- coding: utf-8 -*-
# HAZINE + DINAMIT - statik kontrol.
#
# Bu dosyanin korudugu DORT sey var.
#
# 1) VIEW HICBIR SEY HESAPLAMIYOR. Hazinenin dort odulunden yalnizca BIRI skor (patlamanin yarisi,
#    o da satir patladiysa); gerisi indirim, guc, bonus kart. Dinamitin uc cezasinin HICBIRI skor
#    degil. "+N / -N" gosteren bir View ancak Core'un olctugu sayiyi gosterebilir.
#
# 2) GIZLI BILGI SIZMAZ. Birini bulmak digerini kaldirir; digerinin NEREDE oldugu oyuncunun hak
#    etmedigi bilgi. Rapor onu tasimiyor, View de bulunan hucreler disinda hicbir yere cizmiyor.
#
# 3) CIZIMLER KAHRAMAN, TEK BASINA YETMEZ. Sprite sheet kare kare, kendi ms tablosuyla oynar; isik,
#    parcacik, hukum ve oz belirli CIZILMIS karelere baglanir. Dinamit KAMERAYI degil arenayi iter,
#    ve bunu kendi terimiyle yapar (deprem sarsintisiyla kavga etmez).
#
# 4) IKISI BIRDEN IPTAL. Odul yok, ceza yok, skor yok; ortada notr bir carpisma.
from __future__ import print_function
import os
import re
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

fail = []


def check(label, ok, why):
    print('   %-72s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8-sig').read()


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


def floats(src, name):
    m = re.search(name + r'\s*=\s*(?:\{|AtTempo\()([^})]*)[})]', src)
    if not m:
        return []
    return [float(x.strip().rstrip('f')) for x in m.group(1).split(',') if x.strip()]


def num(src, pattern, default):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else default


def png_size(path):
    with open(path, 'rb') as f:
        head = f.read(24)
    return struct.unpack('>II', head[16:24])


def luminance(c):
    def ch(v):
        return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = c
    return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)


def contrast(a, b):
    la, lb = luminance(a), luminance(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def colour(src, name):
    m = re.search(name + r'\s*=\s*new Color\(([^)]*)\)', src)
    return tuple(float(x.strip().rstrip('f')) for x in m.group(1).split(',')[:3])


joker_raw = read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'HazineJoker.cs')
joker = strip_comments(joker_raw)
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'HazineVisuals.cs'))
view_raw = read('Assets', 'Scripts', 'View', 'HazineRevealView.cs')
view = strip_comments(view_raw)
player = strip_comments(read('Assets', 'Scripts', 'View', 'FrameSequenceFx.cs'))
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'HazineShapes.cs'))
ctrl = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Hazine.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
rebate = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Rebate.cs'))
board = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
tests = read('Tools', 'CoreTests', 'JokerTests.cs') + read('Tools', 'CoreTests', 'HazineRuleTests.cs')
fx = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Fx')

print('=== 1. RAPOR CORE`UN, VIEW HESAPLAMIYOR ===')
check('LastFind [NotSaved] ve kimlikle eslenir',
      re.search(r'\[field: NotSaved\]\s*public HazineVisuals LastFind', joker) is not None,
      'rapor kayda girer ya da hic yok')
check('her bulus YENI bir rapor nesnesi',
      'var find = new HazineVisuals' in joker,
      'ayni nesne yeniden yazilirsa View ikinciyi oynamaz')
check('YENI raunt raporu unutur; uzatma (esigi gecen tur) UNUTMAZ',
      'LastFind = null;' in method(joker, 'public override void OnRoundStarted(')
      and 'LastFind = null;' not in method(joker, 'private void Arm(')
      and 'LastFind = null;' not in method(joker, 'public override void OnOvertimeStarted('),
      'esigi gecen turdaki bulus odenip gosterilmez')
check('isaret HER yikimla acilir: motorun yikim akisi imlecle okunur (tur disi guc, sagdaki joker, boss, Deprem)',
      'public override void OnDestructionSettled(' in joker
      and 'ctx.Round.DestructionFeed' in joker
      and 'feedCursor = ctx.Round.DestructionFeed.Count;' in method(joker, 'private void Arm(')
      and 'public override void AfterTurnScored(' not in joker,
      'bazi yikimlar hazineyi acmiyor')
check('patlama bonusu hattin BANKALADIGININ yarisi (uzatma vergisi dahil)',
      'score.RegularScoreFactor' in joker,
      'uzatmada bonus hattin bes kati')
check('etki, UYGULANDIGI satirin yaninda yaziliyor (7 etki + etkisiz)',
      all(('HazineEffect.' + e) in joker for e in
          ['ExplosionBonus', 'MarketDiscount', 'PowerRefilled', 'BonusCard',
           'PowerDrained', 'CardFrozen', 'HandDiscarded', 'Fizzled', 'None']),
      'bir etki raporlanmiyor')
check('skor OLCULUYOR (RoundScore once/sonra), kopyalanmiyor',
      'int before = ctx.Round.RoundScore;' in joker
      and 'find.ScoreDelta = ctx.Round.RoundScore - before;' in joker,
      'Terslik / olcek / taban sayiya girmez')
check('rastgelelik: rapor tohumu hucrelerden, tur rng`sinden DEGIL',
      'Rng' not in method(joker, 'private static uint SeedOf(')
      and 'Random' not in method(joker, 'private static uint SeedOf('),
      'odul cekilisinin sirasi kayar - baseline bozulur')
check('View etkinin DEGERINI hesaplamiyor (TreasureScoreBonus/MinDiscount okumaz)',
      'TreasureScoreBonus' not in view and 'MinDiscount' not in view and 'FreezeTurns' not in view
      and 'PointsPerLine' not in view,
      'View kuralin sayisini kendi uretiyor')
check('View`in yazdigi skor raporun ScoreDelta`si',
      '"+" + report.ScoreDelta' in view and 'report.ScoreDelta < 0' in view,
      'skor sayisi Core`dan gelmiyor ya da eksi hali yok')
check('odul olmayan etkiler skor animasyonu calmaz',
      'report.Effect != HazineEffect.ExplosionBonus' in method(view, 'private void PaintScore('),
      'indirim/guc/kart skoru zipkinliyor - yalan')
check('ceza etiketi skor degil (GUC/KART/EL), etkisiz de yaziliyor',
      'NO EFFECT' in view and 'ETKİSİZ' in view and 'TUR DONDU' in view,
      'dinamit hep "-N skor" gibi okunur')
check('testler: rapor gercek durum degisikligiyle karsilastiriliyor',
      'Hazine_ReportsWhatTheFindReallyDid' in tests
      and 'a penalty never reports score' in tests
      and 'the seeds reached the score reward' in tests,
      'raporun dogrulugu kanitlanmamis')

print()
print('=== 2. GIZLI BILGI ===')
check('rapor yalnizca vurulan isaretleri tasiyor',
      'if (treasureHit)\n            {\n                find.Discoveries.Add' in joker
      and 'if (dynamiteHit)\n            {\n                find.Discoveries.Add' in joker,
      'bulunmayan isaretin yeri rapora girebilir')
check('rapor tipinde "diger isaret" alani YOK',
      re.search(r'Counterpart|Other(Cell|Mark)|Removed(Cell|Mark)', visuals) is None,
      'View`e gizli konum verilebilir')
for f, src in (('HazineRevealView', view), ('GameUiController.Hazine', ctrl)):
    check('%s TreasureCell / DynamiteCell okumuyor' % f,
          'TreasureCell' not in src and 'DynamiteCell' not in src,
          '%s canli isareti okuyor - bulunmadan once cizebilir' % f)
check('View yalnizca report.Discoveries hucrelerine ciziyor',
      len(re.findall(r'cellWorld\(', view)) == len(re.findall(r'cellWorld\((?:d\.Cell|report\.Discoveries\[[^\]]+\]\.Cell)\)', view)),
      'View baska bir hucreye ciziyor olabilir')
check('diger isaret SEMBOLIK: bulunan patlamanin ICINDE bir kor/zerre',
      'AddBit(4,' in view and 'at + new Vector2(rng.Range(-0.1f, 0.1f)' in view,
      'diger isaret tahta uzerinde bir yere gidiyor/geliyor')
check('laboratuvarda gizli bilgi testi',
      'AnimHazineScene.HiddenInfo' in lab and 'hidden info: report names' in lab,
      'sizinti gozle dogrulanamiyor')
check('bulus sahnesinden once hic bir sey cizilmiyor (sheet saati negatif baslar)',
      'burst.Clock = -revealAt;' in view and 'if (t < 0f)\n                {\n                    return -1;' in player,
      'kupler kirilmadan hazine gorunur')

print()
print('=== 3. CIZIMLER + KOD DESTEGI ===')
for name in ('treasure', 'dynamite'):
    path = os.path.join(fx, 'hazine_%s_sheet.png' % name)
    ok = os.path.exists(path) and os.path.exists(path + '.meta')
    check('%s sheet + meta var' % name, ok, '%s sheet eksik' % name)
    if ok:
        w, h = png_size(path)
        check('%s sheet 4x2 kare hucre (%dx%d)' % (name, w, h),
              w == 2 * h and w % 4 == 0, 'kareler kayik kesilir')
        meta = open(path + '.meta', encoding='utf-8').read()
        check('%s meta: mipmap yok, alfa seffaflik, clamp' % name,
              'enableMipMap: 0' in meta and 'alphaIsTransparency: 1' in meta and 'wrapU: 1' in meta,
              'kenar kanamasi / bulaniklik')
tms = floats(view_raw, 'TreasureMs')
dms = floats(view_raw, 'DynamiteMs')
check('hazine ms tablosu = 70/80/90/100/120/100/90/90',
      tms == [70, 80, 90, 100, 120, 100, 90, 90], 'animatorun zamanlamasi degisti: %s' % tms)
check('dinamit ms tablosu = 70/70/65/80/90/100/100/90',
      dms == [70, 70, 65, 80, 90, 100, 100, 90], 'animatorun zamanlamasi degisti: %s' % dms)
tl = floats(view_raw, 'TreasureLight')
check('hazine isik egrisi = 0.05,0.12,0.25,0.45,1,0.75,0.35,0.10',
      tl == [0.05, 0.12, 0.25, 0.45, 1, 0.75, 0.35, 0.10], 'isik cizime uymuyor')
check('isik egrisinin tepesi cizimin tepe karesinde (hazine F4, dinamit F3)',
      tl.index(max(tl)) == 4 and floats(view_raw, 'DynamiteLight').index(1.0) == 3
      and 'TreasurePeakFrame = 4' in view and 'DynamitePeakFrame = 3' in view,
      'isik ve cizim ayri zamanda doruga cikiyor')
tp = num(view, r'TreasureLightPeak = ([\d.]+)f', 0)
dp = num(view, r'DynamiteLightPeak = ([\d.]+)f', 0)
check('isik tepe opakligi hazine 0.12-0.22, dinamit 0.15-0.30 (%.2f / %.2f)' % (tp, dp),
      0.12 <= tp <= 0.22 and 0.15 <= dp <= 0.30, 'isik ya gorunmez ya ekran flasi')
ts = num(view, r'TreasureSize = ([\d.]+)f', 0)
ds = num(view, r'DynamiteSize = ([\d.]+)f', 0)
check('cizim boyu 1.2-1.9 hucre (%.2f / %.2f)' % (ts, ds),
      1.2 <= ts <= 1.9 and 1.2 <= ds <= 1.9, 'patlama komsulari yutar ya da kaybolur')
tempo = num(view, r'const float Tempo = ([\d.]+)f', 0)
check('tempo tablolarin SEKLINI korur, yalniz hizi degistirir (1.0-1.6: %.2f)' % tempo,
      1.0 <= tempo <= 1.6 and 'ms[i] /= Tempo;' in view
      and 'CancelMs = AtTempo(' in view,
      'kare sureleri tek tek oynanmis ya da iptal kuyrugu farkli hizda')
check('isik kendi kenarinda SIFIRA iniyor (kare/disk plaka degil)',
      't * t * (3f - 2f * t) * t' in method(shapes, 'private static float LightAt('),
      'isik bir cikartma gibi okunur')
check('oynatici kare basina sure kullaniyor, tek fps degil',
      'FrameMs' in player and 'fps' not in player.lower(),
      'tepe karesi kaybolur')
check('oynatici havuzlu',
      'Stack<SpriteRenderer> pool' in player and 'Return(' in player,
      'her bulus yeni GameObject uretir')
check('olcekli saat (lab zaman olcegi cizim + kod birlikte yavaslar)',
      'Time.deltaTime' in method(view, 'private void Update(') and 'unscaled' not in view.lower(),
      'cizim ve kod 0.25x`te birbirinden kayar')
check('eksik sheet: bir uyari, istisna yok',
      'LogWarning' in player and 'throw' not in player,
      'strip edilmis build her karede patlar')
check('parcacik sinirlari: zerre<=8, parca<=7, duman<=4',
      num(view, r'MotesMax = (\d+)', 99) <= 8 and num(view, r'FragmentsMax = (\d+)', 99) <= 7
      and num(view, r'PuffsMax = (\d+)', 99) <= 4,
      'parcacik yagmuru')
check('is izi hucre icinde kaliyor (<= 1 hucre)',
      num(view, r'ScorchSize = ([\d.]+)f', 9) <= 1.0, 'is izi komsu kuplerin ustune tasiyor')
check('dinamit KAMERAYI sallamiyor',
      'ShakeCamera' not in view and 'ShakeCamera' not in ctrl and 'Camera.main' not in view,
      'kamera sarsintisi - satir patlamasinin dili')
check('itis BoardView`da AYRI bir terim (deprem sarsintisiyla birlesir)',
      'public void SetImpulse(Vector2 offset)' in board
      and 'impulseOffset.x' in method(board, 'private void ApplyArenaTransform('),
      'iki yazar ayni transformu ezer')
check('itis tepe karesinde, kucuk (<= 0.03 dunya birimi)',
      num(view, r'float Impulse = ([\d.]+)f', 9) <= 0.03 and 'StartOf(peak)' in view,
      'itis cizimle eszamanli degil ya da cok buyuk')
check('itis bitince sifirlaniyor (Stop + omur sonu)',
      'Impulse(Vector2.zero)' in method(view, 'public void Stop(')
      and 'Impulse(Vector2.zero)' in method(view, 'private void PaintImpulse('),
      'tahta kaymis kalir')
check('hazine tahtayi itmiyor',
      'if (!first.IsTreasure)\n                {\n                    impulseAt' in view,
      'hazine de sarsiyor')
check('iz, tasinan ozden KUCUK (ok ucu degil)',
      'float ts = s * (0.7f - 0.15f * t);' in view,
      'iz tokeni gecer - ucan bir ucgen')
check('ucus yalnizca iki capadan (tahtaya nisan almaz)',
      'board' not in method(view, 'private Vector2 Bezier(').lower(),
      'ucus tahtanin ortasina egilir')
check('hedef gercek: skor etiketi / guc paneli / kart / iskarta',
      'ScoreWorldAnchor()' in ctrl and 'PowerPanelWorld(find.PowerId)' in ctrl
      and 'cardLayer.Held(find.CardId)' in ctrl and 'CardLayerView.DiscardPilePos' in ctrl,
      'oz uydurma bir koseye ucuyor')
check('gidecek gercek bir yer yoksa oz ucmuyor',
      'if (!destination.Has' in method(view, 'private bool EssenceWillFly('),
      'oz bosluga ucuyor')
check('skor etiketi TEK yazardan (TickScoreResponse) yaziliyor',
      'hazine.ScoreScale' in rebate and 'totalText' not in view,
      'iki efekt etiketi ayni anda ezer')
check('bulus, KUPLER KIRILDIGINDA (su dustukten sonra) oynuyor',
      'SyncHazine(round, report);' in method(fb, 'private void PlayExplosionFeedback(')
      and 'SyncHazine' not in method(fb, 'private void RefreshAll('),
      'hazine onu bulan satirdan once acilir')
check('kirilma gecikmesi satir supurmesinden olculuyor',
      'LineSweepView.Style.PropagationSeconds' in ctrl and 'ClusterBurstView.Style' in ctrl,
      'aciliş kirilmayla eszamanli degil')
ink = colour(view_raw, 'Gold')
check('hukum rengi tahtada okunur (altin vs arduvaz %.1f >= 4.5)' % contrast(ink, (0.105, 0.115, 0.14)),
      contrast(ink, (0.105, 0.115, 0.14)) >= 4.5, 'sayi okunmuyor')
burnt = colour(view_raw, 'Burnt')
check('ceza rengi tahtada okunur (yanik amber %.1f >= 4.5)' % contrast(burnt, (0.105, 0.115, 0.14)),
      contrast(burnt, (0.105, 0.115, 0.14)) >= 4.5, 'ceza etiketi okunmuyor')
check('hukum cizimin parlak tepesinin USTUNDE (NumberLift >= yaricap)',
      num(view, r'NumberLift = ([\d.]+)f', 0) >= max(ts, ds) / 2,
      'altin karenin ustunde altin sayi')
check('sesler ve dokunsal kancalar',
      'public static System.Action<string> Sounded;' in view
      and 'public static System.Action<string> Haptic;' in view
      and view.count('Say(Sounded,') >= 6,
      'ses/titresim baglanamaz')
check('rapor KIMLIKLE eslenir, seri numarasiyla degil',
      'ReferenceEquals(find, lastPlayed)' in view and 'Serial' not in view,
      'yeni kosunun ilk bulusu atlanir')

print()
print('=== 4. IPTAL ===')
check('iptal: cizimler kesiliyor',
      'burst.StopAt = Style.CancelCutFrame;' in view, 'iki tam patlama + iptal - celiski')
check('iptal: ortada notr carpisma (hazinenin kuyrugu, fildisi)',
      'new FrameSequenceFx.Sheet(\n            "Art/Fx/hazine_treasure_sheet", 4, 2, 5,' in view
      and 'cancelBurst.Tint = Style.CancelTint;' in view,
      'iptalin kendi dili yok')
check('iptal: skor, oz ve ceza yok',
      'report.Result == HazineResult.BothCancelled' in method(view, 'private bool EssenceWillFly(')
      and 'find.Effect = HazineEffect.None;' in joker,
      'iptal bir sey oduyor gibi gorunur')
check('iptal testi rapora bakiyor',
      'one settle took both, so they cancel' in tests,
      'iptal raporu dogrulanmamis')

print()
print('=== 5. LABORATUVAR ===')
entries = len(re.findall(r'AddAnim\("hazine', lab))
check('en az 25 giris (%d)' % entries, entries >= 25, 'lab eksik')
check('her etki icin sahne',
      all(('AnimHazineScene.' + s) in lab for s in
          ['TreasureScore', 'TreasureDiscount', 'TreasurePower', 'TreasureCard', 'TreasureInverted',
           'DynamitePower', 'DynamiteFrozen', 'DynamiteHand', 'DynamiteFizzle',
           'CancelNear', 'CancelFar', 'CancelSameLine']),
      'bir etki labda oynatilamiyor')
check('lab oyunun yolundan oynatiyor (PlayHazine)',
      'PlayHazine(report, board, null, jokerId, rows, null);' in lab,
      'lab animasyonun kopyasini oynatiyor')
check('lab sayilari joker/kurallardan okuyor',
      'joker.FreezeTurns' in lab and 'joker.TreasureScoreBonus' in lab
      and 'session.Config.Scoring.PointsPerLine' in lab,
      'lab kendi sayisini yaziyor')
check('deprem ALTINDA dinamit sahnesi (iki terim birlikte)',
      'AnimHazineScene.DynamiteQuake' in lab and 'boardView.Quake.Play(boardView, quake);' in lab,
      'itis + sarsinti birlikte gorulemiyor')
check('kare no + ms hata ayiklama ve 0.25x',
      'ShowFrameDebug' in lab and 'hazine: treasure at 0.25x' in lab,
      'cizim zamanlamasi gozle dogrulanamiyor')
check('cizimsiz / yalniz cizim sahneleri, anahtarlar ONCE',
      method(lab, 'private void AnimHazineOnly(').index('Layers.AllOn()')
      < method(lab, 'private void AnimHazineOnly(').index('AnimHazine(treasure'),
      'kapali katman yine cizilir')
check('RESET hazineyi durdurup katmanlari geri aciyor',
      'StopHazine();' in method(lab, 'private void AnimResync()')
      and 'HazineRevealView.Layers.AllOn();' in method(lab, 'private void AnimResync()'),
      'RESET sonrasi katman kapali kalir')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('hazine: hepsi tamam')
