# Enfeksiyon patlamasi: yapisal kontroller.
#
# Dort soru soruyor, hepsi daha once gercekten yasanmis hatalar:
#   1. Style'da okunmayan alan var mi? (PullSeconds/PullStretch vakasi: ayar yazilir,
#      animasyon onu hic okumaz, "derleniyor + testler geciyor" bunu goremez.)
#   2. Bulasma hedeflerini VIEW mi uyduruyor? Oyun yolunda arti hesabi yapan tek satir
#      bile olmamali - hedefler kurallarin LastSpreadCells'inden gelmeli.
#   3. Brief'in acikca yasakladiklari geri sizmis mi: ekran sarsintisi, ortak
#      FlashCells yesili, tam ekran flash.
#   4. Senkron: blok kuplerin silindigi karede mi cikiyor, yeni cekirdekler patlamayi
#      bekliyor mu, lab ayni cagriyi mi kullaniyor, Mathf.SmoothStep yanlis mi kullanilmis.
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
V = os.path.join(ROOT, 'Assets', 'Scripts', 'View')
VIEW = os.path.join(V, 'InfectionBurstView.cs')
ACT = os.path.join(V, 'GameUiController.Activation.cs')
LAB = os.path.join(V, 'GameUiController.AnimationLab.cs')
BOARD = os.path.join(V, 'BoardView.cs')
CORES = os.path.join(V, 'InfectionCoreView.cs')

fail = []


def read(p):
    return open(p, encoding='utf-8').read()


def strip_comments(s):
    """Yorumlari atar - bir alanin adi sadece aciklamada geciyorsa 'okunuyor' sayilmaz.
    (Mayin pasinda checker'in kendisi tam bu yuzden yanlis alarm vermisti.)"""
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return '\n'.join(re.sub(r'//.*$', '', line) for line in s.split('\n'))


def method(src, signature):
    """Imzadan bir sonraki uye tanimina kadar olan govde."""
    i = src.index(signature)
    rest = src[i + len(signature):]
    m = re.search(r'\n        (private|public|internal|protected) ', rest)
    return src[i:i + len(signature) + (m.start() if m else len(rest))]


def check(label, ok, why):
    print('   %-44s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


code = strip_comments(read(VIEW))
act = strip_comments(read(ACT))
lab = strip_comments(read(LAB))
board = strip_comments(read(BOARD))
cores = strip_comments(read(CORES))

# ------------------------------------------------------------------ 1. olu ayar var mi
start = code.index('public static class Style')
end = code.index('\n        }', start)
style = code[start:end]
names = re.findall(r'public static \w+ (\w+) =', style)
body = code[end:]
dead = [n for n in names if not re.search(r'\bStyle\.%s\b' % n, body)]
print('=== 1. STYLE ALANLARI ===')
print('   %d ayar, %d tanesi okunuyor' % (len(names), len(names) - len(dead)))
for n in dead:
    print('   OLU: Style.%s' % n)
if dead:
    fail.append('okunmayan Style alanlari: %s' % ', '.join(dead))

# ------------------------------------------------------------------ 2. arti hesabi
print()
print('=== 2. BULASMA HEDEFLERI ===')
plus = r'new GridPos\([^)]*[XY]\s*[+-]\s*1'
check('view kendi komsusunu URETMIYOR', not re.findall(plus, code),
      'InfectionBurstView komsu hucre uretiyor')
check('oyun yolu LastSpreadCells + Centre okuyor',
      'enf.LastSpreadCells' in act and 'enf.LastSpreadCentre' in act,
      'Activation kurallarin bulasma listesini okumuyor')
check('oyun yolu arti UYDURMUYOR', not re.findall(plus, act),
      'Activation kendi artisini kuruyor')
print('   lab (uydurmasi serbest)                      : %d komsu ifadesi'
      % len(re.findall(plus, lab)))

# ------------------------------------------------------------------ 3. yasaklar
print()
print('=== 3. BRIEF YASAKLARI ===')
blast = act[act.index('private void TriggerInfectionBlast'):]
blast = blast[:blast.index('private void PlayPowerBlast')]
for label, needle in [('kamera sarsintisi YOK', 'ShakeCamera'),
                      ('ortak FlashCells YOK', 'FlashCells('),
                      ('tam ekran flash YOK', 'FlashBoard')]:
    check(label, needle not in blast, 'enfeksiyon yolunda %s' % needle)

# ------------------------------------------------------------------ 4. senkron
print()
print('=== 4. SENKRON ===')
check('blok yikim kaydindan ciziliyor', 'report.DestroyedCubes' in act,
      'blok gercek yikilan kuplerden cizilmiyor')

blk = method(act, 'private void PlayInfectionBlock(')
check('hayalet blok AYNI karede cikiyor',
      'boardView.PlayInfectionBurst(' in blk and 'yield' not in blk
      and 'WaitForSeconds' not in blk,
      'hayalet blok gecikmeli - kupler once kaybolup sonra geri gelir')

pib = method(board, 'public float PlayInfectionBurst(')
check('yeni cekirdekler tasiyicilar INENE kadar bekliyor',
      'infectionCores.HoldBirth(spreadTo, rupture + InfectionBurstView.Style.CarrierSeconds)' in pib,
      'HoldBirth tasiyicilarin inisine bagli degil - bulasma sporlardan once iniyor')
hb = method(cores, 'public void HoldBirth(')
check('tutulan cekirdek duz tendril CIZMIYOR', 'c.SpreadTimer = Style.SpreadBloomSeconds;' in hb,
      'tutulan cekirdek tendrille geliyor - bulasma arti isareti gibi dort duz cizgi')

reset = method(cores, 'private static void Reset(')
check('havuzdan donen cekirdek bekletmeyi SIFIRLIYOR',
      'c.BirthHold = 0f;' in reset and 'localScale = Vector3.one' in reset,
      'Reset BirthHold/olcek sifirlamiyor - geri donen cekirdek gorunmez kalir')

tick = method(cores, 'private void Tick(')
held_first = ('if (c.BirthHold > 0f)' in tick and 'c.SpreadTimer -= dt' in tick
              and tick.index('if (c.BirthHold > 0f)') < tick.index('c.SpreadTimer -= dt'))
check('bekleyen cekirdegin saati ISLEMIYOR', held_first,
      'Tick bekletmeden once bulasma saatini isletiyor')

check('tasiyicilar bekletmeyle AYNI sureyi kullaniyor',
      'Style.CarrierSeconds' in method(code, 'private IEnumerator Carry('),
      'tasiyici suresi bekletmeden farkli - cekirdek sporlardan once/sonra acilir')

orders = {k: int(re.search(r'const int %s = (\d+);' % k, code).group(1))
          for k in ('BodyOrder', 'RotOrder', 'BodyMaskBack', 'BodyMaskFront',
                    'RotMaskBack', 'RotMaskFront')}
apart = (orders['BodyMaskBack'] < orders['BodyOrder'] <= orders['BodyMaskFront']
         < orders['RotMaskBack'] < orders['RotOrder'] <= orders['RotMaskFront'])
check('govde/curume maskeleri AYRI (aralik ucu ne olursa)', apart,
      'maske araliklari cakisiyor: %s' % orders)
check('curume ici BOS (yalniz cephe bandi gorunur)',
      'p.Rot.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask'
      in method(code, 'private Piece Build('),
      'curume tum yenen alani dolduruyor - yesil kare geri gelir')

check('lab oyunun cagrisini KULLANIYOR',
      'PlayInfectionBlock(' in method(lab, 'private void AnimInfectionBurst('),
      'lab kendi kopyasini oynatiyor')

check('maske ozel aralikla SINIRLI', 'isCustomRangeActive = true' in code,
      'maske aralik sinirsiz - baska maskeli rendererlari da yer')

misuse = re.findall(r'Mathf\.SmoothStep\((?!0f, 1f,)[^)]*\)', code)
check('Mathf.SmoothStep kenar gibi KULLANILMAMIS', not misuse,
      'Mathf.SmoothStep deger arasi karisim, kenar degil: %s' % misuse[:2])

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - %s' % f)
    sys.exit(1)
print('SONUC: temiz')
