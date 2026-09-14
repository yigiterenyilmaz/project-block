# MAYIN DANSI: ADALET, KUP HAREKETSIZLIGI VE YOL SADAKATI
#
# Bu mekanikte gorsel bir ayrinti mayinin yerini ele veriyorsa oyun bozulur - ve tam olarak
# bu iki kez oldu (isaretcinin kapaklarin ustunde kalmasi, ve mayin kapaginin hep YUKARI,
# digerinin hep ASAGI yay cizmesi). Uc seyi kaynak uzerinden dogrular:
#   1. Kapak cizen hicbir yordam mayini bilmiyor
#   2. View tahtanin kendi kuplerine hic dokunmuyor
#   3. Core'un yolu birebir, sirasiyla, eksiksiz oynatiliyor
import re
import sys

SRC = r'C:\Users\yigit\project-block\Assets\Scripts\View\MineShuffleView.cs'
s = open(SRC, encoding='utf-8').read()
step = s[s.index('private IEnumerator ShuffleStep'):s.index('private static float SlideEase')]
fails = []
NL = chr(10)


def check(name, ok, why=''):
    print('  %s %s%s' % ('OK  ' if ok else 'KIRIK', name, '' if ok else '   -> ' + why))
    if not ok:
        fails.append(name)


def code_only(text):
    """Yorumlari atar. Sorulan sey KODUN mayina erisip erismedigi - bir yorumun
    mayindan bahsetmesi zaten gerekli, cunku adaletin NEDEN boyle kuruldugunu o anlatiyor."""
    out = []
    for line in text.split(NL):
        stripped = line.strip()
        if stripped.startswith('//') or stripped.startswith('///'):
            continue
        i = line.find('//')
        out.append(line[:i] if i >= 0 else line)
    return NL.join(out)


def method(signature):
    """Bir yordamin govdesi: imzasindan bir sonraki yordamin imzasina kadar."""
    i = s.index(signature)
    marks = [s.find(NL + '        private ', i + 1),
             s.find(NL + '        public ', i + 1),
             len(s)]
    end = min(m for m in marks if m > i)
    return s[i:end]


print('=== 1. ADALET: kapak cizen yordamlar mayini biliyor mu ===')
check('iki kapak da ayni sayida Place() aliyor',
      step.count('Place(a,') == step.count('Place(b,') and step.count('Place(a,') >= 2)
check('iki kapak da Smear() aliyor', 'Smear(a,' in step and 'Smear(b,' in step)
check('egim buyuklugu ayni, sadece isareti farkli', 'tilt);' in step and '-tilt);' in step)
check('sure ADIM indeksinden geliyor', 'step <= 3' in step and 'Frac(step *' in step)
check('on/arka YONDEN karar veriliyor', 'aInFront' in step and 'pb.x > pa.x' in step)

# BuildMine ve PulseMine mayini bilir ve bilmelidir - ikisi de kapak kapanmadan once biter.
# Kapak cizen yordamlarin hicbiri bilmemeli.
for sig in ['private IEnumerator ShuffleStep', 'private void Place(', 'private void Smear(',
            'private void SetOrder(', 'private IEnumerator Settle(',
            'private IEnumerator LiftAway(']:
    body = code_only(method(sig))
    name = sig.split('(')[0].split()[-1]
    check('%s mayini hic bilmiyor' % name,
          'Mine' not in body and 'mine' not in body,
          'kapak cizen yordam mayin kimligine erisiyor')

print()
print('=== 2. KUPLER: view tahtaya dokunuyor mu ===')
for bad in ['cellRenderers', 'SetCube', 'Rebuild(', 'boardView.Refresh']:
    check('"%s" cagrilmiyor' % bad, bad not in s, 'tahtanin kendi gorselini degistiriyor')
reads = sorted(set(re.findall(r'board\.(\w+)', s)))
check('tahtadan yalnizca okuma (%s)' % ', '.join(reads),
      set(reads) <= {'CellToWorld', 'CellWorldSize'})

print()
print('=== 3. YOL: Core adimlari birebir mi ===')
dance = s[s.index('for (int step = 1; step < path.Count'):]
dance = dance[:dance.index('yield return new WaitForSeconds(Style.FinalHold)')]
check('dongu path.Count kadar donuyor', 'step < path.Count' in dance)
check('hedef dogrudan path[step]', 'GridPos to = path[step];' in dance)
check('ara nokta eklenmiyor', 'Insert' not in dance and '.Add(' not in dance)
check('gorsel katmanda rastgelelik yok', 'Random' not in s)

print()
if fails:
    print('SONUC: %d SORUN -> %s' % (len(fails), ', '.join(fails)))
    sys.exit(1)
print("SONUC: adil, kupler sabit, yol Core'un yolu")
