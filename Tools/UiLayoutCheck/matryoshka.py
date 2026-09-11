# Matruşka gorsel sistemi: yapisal kontroller.
#
# Brief'in "kesinlikle" dediklerini koddan dogrular - hepsi "derleniyor + testler geciyor"un
# goremeyecegi seyler:
#   1. Style'da okunmayan ayar yok (PullSeconds vakasi).
#   2. View hedef SECMIYOR: cocuklarin ve suyla tasinmanin hedefi yalnizca DollEvent'ten geliyor,
#      hareket kodunda Random yok.
#   3. Placeholder yok: bebek kodla cizilmiyor, hazir sprite'lar yukleniyor ve dosyalari var.
#   4. Core yalnizca RAPOR ekledi: boss'a yeni alan eklenmedi (kayit formati degismedi), her olay
#      turu yaziliyor, kural akisi ayni.
#   5. Senkron: tur olaylari refresh'te TUTULUYOR, patlama aninda BIRAKILIYOR.
#   6. Lab'da istenen durumlarin hepsi var.
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
V = os.path.join(ROOT, 'Assets', 'Scripts', 'View')
fail = []


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8').read()


def strip_comments(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return '\n'.join(re.sub(r'//.*$', '', line) for line in s.split('\n'))


def method(src, signature):
    i = src.index(signature)
    rest = src[i + len(signature):]
    m = re.search(r'\n        (private|public|internal|protected) ', rest)
    return src[i:i + len(signature) + (m.start() if m else len(rest))]


def check(label, ok, why):
    print('   %-50s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'MatryoshkaView.cs'))

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
names = re.findall(r'public static [\w\[\]]+ (\w+) =', view[start:end])
body = view[end:]
dead = [n for n in names if not re.search(r'\bStyle\.%s\b' % n, body)]
print('   %d ayar, %d okunuyor' % (len(names), len(names) - len(dead)))
for n in dead:
    print('   OLU: Style.%s' % n)
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))

print()
print('=== 2. HEDEFLER CORE\'DAN ===')
stage = method(view, 'private void Stage(')
check('cocuk hedefleri e.Children\'dan', 'e.Children[c]' in stage, 'Stage cocuk hedefini olaydan almiyor')
check('suyla tasima hedefi e.To\'dan', 'Carry(e.Cell, e.To' in stage, 'Stage tasima hedefini olaydan almiyor')
for sig in ('private void Stage(', 'private IEnumerator Emerge(', 'private IEnumerator Carry(',
            'private IEnumerator Open('):
    check('%s icinde Random YOK' % sig.split()[-1].rstrip('('), 'Random' not in method(view, sig),
          '%s rastgelelik kullaniyor' % sig)

print()
print('=== 3. PLACEHOLDER YOK / ASSET VAR ===')
check('bebek kodla cizilmiyor (MakeRect/MakeCell yok)',
      'MakeRect' not in view and 'MakeCell' not in view, 'MatryoshkaView kare/hucre ciziyor')
check('hazir sprite yukleniyor', 'Resources.Load<Sprite>' in view and 'matryoshka_g' in view,
      'MatryoshkaView sprite yuklemiyor')
art = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Matryoshka')
missing = []
for g in range(1, 5):
    for suffix in ('', '_top', '_bottom'):
        base = os.path.join(art, 'matryoshka_g%d%s.png' % (g, suffix))
        for p in (base, base + '.meta'):
            if not os.path.exists(p):
                missing.append(os.path.basename(p))
check('12 sprite + meta dosyasi mevcut', not missing, 'eksik: %s' % missing)
bv = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
check('eski renkli kare bebek kaldirildi', 'DollColor' not in bv and '"Doll_"' not in bv,
      'BoardView hala eski kare bebegi ciziyor')

print()
print('=== 4. CORE YALNIZCA RAPOR ===')
boss = read('Assets', 'Scripts', 'Core', 'Bosses', 'Definitions', 'MatryoshkaBoss.cs')
code = strip_comments(boss)
fields = sorted(re.findall(r'^\s+(?:public|private|internal)\s+(?:readonly\s+)?[\w<>\[\],]+\s+(\w+)\s*(?:=|;)',
                           code, flags=re.M))
try:
    head = subprocess.run(['git', 'show', 'HEAD:Assets/Scripts/Core/Bosses/Definitions/MatryoshkaBoss.cs'],
                          cwd=ROOT, capture_output=True, text=True, encoding='utf-8').stdout
    head_fields = sorted(re.findall(r'^\s+(?:public|private|internal)\s+(?:readonly\s+)?[\w<>\[\],]+\s+(\w+)\s*(?:=|;)',
                                    strip_comments(head), flags=re.M))
except Exception:
    head_fields = None
check('boss\'a yeni alan eklenmedi (kayit formati ayni)', head_fields is not None and fields == head_fields,
      'MatruskaBoss alanlari degisti: %s -> %s' % (head_fields, fields))
for kind in ('Arrived', 'Split', 'Emptied', 'Moved', 'AllCracked'):
    check('DollEvent.%s yaziliyor' % kind, 'DollEvent.%s(' % kind in code, '%s olayi raporlanmiyor' % kind)
check('rastgele cagri sayisi ayni (NextInt 1 yerde)', code.count('NextInt(') == strip_comments(head).count('NextInt('),
      'boss rastgele cagrilarini degistirmis')

print()
print('=== 5. SENKRON ===')
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
marks = method(fb, 'private void RefreshBossBoardMarks(')
check('tur olaylari refresh\'te TUTULUYOR', 'HoldDolls(' in marks and 'DollEvents' in marks,
      'RefreshBossBoardMarks olaylari tutmuyor')
check('patlama aninda BIRAKILIYOR', 'ReleaseDolls()' in method(fb, 'private void PlayExplosionFeedback('),
      'PlayExplosionFeedback bebekleri birakmiyor')
check('split sirasinda ekran sarsintisi YOK', 'ShakeCamera' not in view, 'MatryoshkaView sarsinti kullaniyor')
misuse = re.findall(r'Mathf\.SmoothStep\((?!0f, 1f,)[^)]*\)', view)
check('Mathf.SmoothStep kenar gibi KULLANILMAMIS', not misuse, 'SmoothStep yanlis: %s' % misuse[:2])

print()
print('=== 6. LAB ===')
lab = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
wanted = ['ilk bebek', 'tüm nesiller', 'büyük -> 2 orta', 'orta -> 2 küçük', 'küçük -> 2 minik',
          'minik boş', 'aynı anda', 'su bebeği', 'son bebek']
absent = [w for w in wanted if w not in lab]
check('9 Matruşka durumu labda', not absent, 'labda eksik: %s' % absent)

print()
print('=== 7. BEKLEME MALZEMESI ===')
shader_path = os.path.join(ROOT, 'Assets', 'Resources', 'Shaders', 'MatryoshkaIdle.shader')
check('idle shader Resources altinda (build\'e girer)', os.path.exists(shader_path),
      'MatryoshkaIdle.shader Resources/Shaders altinda degil')
shader = open(shader_path, encoding='utf-8').read() if os.path.exists(shader_path) else ''
check('shader 2D renderer\'a etiketli', '"LightMode" = "Universal2D"' in shader, 'Universal2D pass etiketi yok')
check('yuz hicbir katmana girmiyor (keep = 1 - face)', 'float keep = 1.0 - mask.b;' in shader
      and shader.count('keep') >= 8, 'yuz maskesi katmanlara uygulanmiyor')
check('kenar isigi TEK tarafta (isik yonu)', 'toLight' in shader and 'saturate(texel.a - outside)' in shader,
      'rim tum siluete esit uygulanıyor olabilir')
check('parlama supurmesi YOK (sheen sabit merkezli)', 'float2(0.27, 0.46)' in shader and '_Time.y * _SheenSpeed' not in shader.replace('t * _SheenSpeed', ''),
      'sheen merkezi zamanla kayiyor')
masks_missing = []
for g in range(1, 5):
    p = os.path.join(art, 'matryoshka_g%d_mask.png' % g)
    if not os.path.exists(p) or not os.path.exists(p + '.meta'):
        masks_missing.append(os.path.basename(p))
    else:
        meta = open(p + '.meta', encoding='utf-8').read()
        if 'sRGBTexture: 0' not in meta or 'alphaIsTransparency: 0' not in meta:
            masks_missing.append(os.path.basename(p) + ' (meta: sRGB/alpha)')
check('4 malzeme maskesi, dogrusal ve alfa verisi korunur', not masks_missing, 'maske sorunu: %s' % masks_missing)
check('doll basina material YOK (tek new Material)', view.count('new Material(') == 1,
      'MatryoshkaView birden fazla yerde material uretiyor')
check('property block YOK (batching korunur)', 'MaterialPropertyBlock' not in view, 'property block kullaniliyor')
pose = method(view, 'private void PoseResting(')
check('bekleme tum sprite rengini DEGISTIRMIYOR', 'Body.color' not in pose, 'PoseResting govde rengini degistiriyor')
check('hareket yardimci katman (MotionSecondaryStrength)', 'Style.MotionSecondaryStrength' in pose,
      'hareket gucu bekleme hareketine uygulanmiyor')
light_labels = ['karışık nesiller', '8 minik', 'gövde sıcaklığı', 'altın yanıtı', 'cila parlaması',
                'zemin ışığı', 'kenar ışığı', 'hareket aç/kapa']
missing_labels = [w for w in light_labels if w not in lab]
check('lab isik testleri ve 6 katman anahtari', not missing_labels, 'labda eksik: %s' % missing_labels)
check('lab kapaninca katmanlar geri aciliyor', 'MatryoshkaView.Layers.AllOn()' in lab,
      'lab katmanlari kapali birakabiliyor')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
