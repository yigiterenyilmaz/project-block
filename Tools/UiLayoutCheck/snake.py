# Yilan (SnakeView): cizilmis 6 parca, topoloji, omurga hareketi, isirma, kesilme, yenilgi.
#
#   1. SANAT: alti parca projede, her birinin pivotu hucre merkezinde; kodla yilan cizilmiyor.
#   2. TOPOLOJI: bas/kuyruk yonu ve dort kose donusu temel topolojiden TURETILIYOR (ayna yok).
#   3. CORE: YALNIZ RAPOR - kesikler sirayla, kaymanin HER HUCRESI, yenen blok kendi yuzuyle;
#      kurallar degismedi; testler var.
#   4. VIEW HICBIR SEYE KARAR VERMIYOR: yon, mesafe, yeme, kesik sayisi hepsi rapordan.
#   5. HAREKET: tek omurga, bir hucre araliklarla segmentler, mesafeye gore sure, easing.
#   6. ISIRMA: kendi yok olma dili - genel kaldirma efekti yok, blok agzin ARKASINDA kayboluyor.
#   7. KESILME: hat yilani patlatmiyor; sinyal kuyruga iniyor; segment yumusak kopuyor.
#   8. YASAKLAR: parcacik/iz/sarsinti/rastgele/yeni materyal yok; havuzlu renderer.
#   9. LAB: 22 sahne, 7 anahtar, oyunun kapisindan; kapanista sifirlama.
#  10. Olculer brief araliklarinda; uzunluga gore LOD.
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
fail = []


def read(*parts):
    return open(os.path.join(ROOT, *parts), encoding='utf-8').read()


def strip_comments(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return '\n'.join(re.sub(r'//.*$', '', line) for line in s.split('\n'))


def method(src, signature):
    i = src.index(signature)
    rest = src[i + len(signature):]
    m = re.search(r'\n        (private|public|internal|protected|static) ', rest)
    return src[i:i + len(signature) + (m.start() if m else len(rest))]


def check(label, ok, why):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeView.cs'))
vfx = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeVfxController.cs'))
seg_fn = method(view,
    'private void PaintSegment(int index, int count, float arc, float slide, float dt)')
skin = read('Assets', 'Resources', 'Shaders', 'SnakeSkin.shader')
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
vu = strip_comments(read('Assets', 'Scripts', 'View', 'ViewUtil.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
boss = strip_comments(read('Assets', 'Scripts', 'Core', 'Bosses', 'Definitions', 'SnakeBoss.cs'))
visuals = strip_comments(read('Assets', 'Scripts', 'Core', 'Bosses', 'SnakeVisuals.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. SANAT: CIZILMIS ALTI PARCA ===')
FILES = ['snake_head', 'snake_head_open', 'snake_body', 'snake_bend', 'snake_tail',
         'snake_body_swollen']
art = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Blocks')
missing = [f for f in FILES if not os.path.exists(os.path.join(art, f + '.png'))]
check('alti parca projede', not missing, 'eksik sprite: %s' % missing)
pivots = {}
for f in FILES:
    meta = open(os.path.join(art, f + '.png.meta'), encoding='utf-8').read()
    ppu = re.search(r'spritePixelsToUnits: (\d+)', meta)
    align = re.search(r'alignment: (\d+)', meta)
    pv = re.search(r'spritePivot: \{x: ([\d.]+), y: ([\d.]+)\}', meta)
    pivots[f] = (int(ppu.group(1)), int(align.group(1)), float(pv.group(1)), float(pv.group(2)))
check('her parca bir birim = BIR HUCRE (ppu ayni)',
      len(set(p[0] for p in pivots.values())) == 1, 'parcalar farkli olcekte: %s' % pivots)
check('her parcanin pivotu OZEL (alignment 9) - hucre merkezine oturuyor',
      all(p[1] == 9 for p in pivots.values()), 'pivot merkezde degil: %s' % pivots)
check('pivotlar tuvalin ortasinda degil (bas/kuyruk kaydirilmis)',
      abs(pivots['snake_head'][2] - 0.5) > 0.01 or abs(pivots['snake_bend'][2] - 0.5) > 0.05,
      'pivotlar islenmemis')
check('sprite referanslari ISIMLE ve acik (sheet runtime kesilmiyor)',
      'public static Sprite SnakeTile(SnakePiece piece)' in vu
      and all(('"%s"' % f) in vu for f in FILES)
      and 'SnakeTile(' in view, 'sprite referanslari acik degil')
generated = set(re.findall(r'private static Sprite (\w+)Sprite\(\)', view))
check('uretilen sprite yalniz golge/kirinti/nokta/halka - yilan parcasi kodla cizilmiyor',
      generated and generated <= {'Shadow', 'Fleck', 'Dot', 'Ring'},
      'kod yilan ciziyor: %s' % sorted(generated))

print()
print('=== 2. TOPOLOJI ===')
check('bas ve kuyruk temel yonu SAG, ceyrek donusle isaretleniyor',
      'private static int TurnForFacing(Vector2 dir)' in view, 'bas yonu yok')
check('kose temeli SOL + ASAGI ve dort donus TURETILIYOR (ayna yok)',
      'private static bool TryTurnForBend(Vector2 a, Vector2 b, out int turn)' in view
      and 'new Vector2(-1f, 0f), turn' in view.replace('Rotate(', '')
      and 'flipX' not in view and 'Scale(-1' not in view, 'kose haritasi elle yazilmis')
# Dirsegin yonu KOSENIN KENDI iki uzvundan geliyor, yaninda alinmis teğet orneklerinden degil:
# segment koseden 0.45-0.5 hucre uzaktayken iki ornek de ayni uzva dusuyor, hicbir donus
# eslesmiyor ve eski kod sessizce 0 donduruyordu - yani govdenin gitmedigi yone bakan bir dirsek.
check('dirsek KOSENIN uzuvlarindan yonleniyor, sessiz varsayilan yok',
      'out Vector2 into, out Vector2 outOf' in view
      and 'TryTurnForBend(Snap(-into), Snap(outOf), out elbow)' in seg_fn
      and 'Snap(spine.Toward(arc - cellSize * 0.45f))' not in view, 'dirsek tahminle yonleniyor')
check('donus Quaternion ile, ceyrek adimlarla',
      'Quaternion.Euler(0f, 0f, 90f * (turn & 3))' in view, 'donus yok')
check('bas GOVDENIN USTUNDE, yenen blok basin ARKASINDA',
      re.search(r'ProxyOrder = (\d+)', view) and re.search(r'BodyOrder = (\d+)', view)
      and int(re.search(r'ProxyOrder = (\d+)', view).group(1))
      < int(re.search(r'BodyOrder = (\d+)', view).group(1))
      < int(re.search(r'HeadOrder = (\d+)', view).group(1)), 'siralama')

print()
print('=== 3. CORE: YALNIZ RAPOR ===')
for field in ['BodyBeforeCuts', 'CutCount', 'CutRows', 'CutColumns', 'RemovedTailCells',
              'BodyAfterEachCut', 'Defeated', 'WasStuck', 'MoveStep', 'StepSnapshots',
              'EatenCell', 'EatenCube', 'GrowthOccurred', 'BodyAfter']:
    if field not in visuals:
        fail.append('rapor alani eksik: %s' % field)
check('rapor 14 alanin tamamini tasiyor', not [f for f in fail if 'rapor alani' in f],
      'rapor eksik')
check('boss raporu yaziyor ve View icin aciyor',
      'public SnakeTurnVisuals LastTurn { get; private set; }' in boss, 'LastTurn yok')
turn = method(boss, 'public override void AfterTurnScored(TurnContext turn)')
check('kesikler once, sonra olum ya da kayma - kurallarin sirasi degismedi',
      turn.index('CountCutsFrom(') < turn.index('CutTail(') < turn.index('DeclareRoundWon()')
      < turn.index('Slide('), 'tur sirasi degisti')
slide = method(boss, 'private void Slide(RoundEngine round, IRandomSource rng, SnakeTurnVisuals visuals)')
check('kaymanin HER hucresi raporlaniyor',
      'visuals.NoteStep(body);' in slide and slide.index('Advance(board, next, food);')
      < slide.index('visuals.NoteStep(body);'), 'adimlar raporlanmiyor')
check('yenen blok kurallar silmeden ONCE aliniyor',
      slide.index('visuals.NoteEaten(next, standing.Value);')
      < slide.index('round.DestroyCubes('), 'blok yuzu kayboluyor')
check('sikisma ve yon raporlaniyor',
      'visuals.NoteStuck();' in slide and 'visuals.NoteDirection(direction);' in slide,
      'sikisma/yon yok')
check('tek zar atisi (yon secimi degismedi)', slide.count('rng.NextInt(') == 1, 'zar degisti')
check('kuyruk kesigi ve olum raporlaniyor',
      'visuals.NoteTailCut(tail, body);' in boss and 'visuals.NoteDefeated();' in turn,
      'kesik/olum yok')
check('Core testleri raporu dogruluyor',
      'Snake_EveryTurnIsReportedForTheView' in tests and 'SnakeTurnVisuals' in tests
      and 'every snapshot is a snake' in tests, 'test yok')

print()
print('=== 4. VIEW KARAR VERMIYOR ===')
play = method(fb, 'private bool PlaySnake(RoundEngine round)')
check('sahne tamamen rapordan kuruluyor',
      all(w in play for w in ('turn.BodyBeforeCuts', 'turn.RemovedTailCells',
                              'turn.BodyAfterEachCut', 'turn.StepSnapshots', 'turn.EatenCell',
                              'turn.EatenCube', 'turn.WasStuck', 'turn.Defeated', 'turn.BodyAfter')),
      'view tahmin ediyor')
check('ayni rapor iki kez oynatilmiyor', 'turn.Turn == snakeTurnPlayed' in play, 'tekrar oynar')
check('view tahtayi okumuyor / degistirmiyor',
      'SetCubeAt' not in view and 'GetCube' not in view and 'DestroyCube' not in view,
      'view tahtaya dokunuyor')
check('view yon/mesafe/yeme hesaplamiyor',
      'IsInside' not in view and 'rng' not in view.lower().replace('string', ''),
      'view kural hesapliyor')
check('tahta yeniden cizildigi karede oynuyor',
      'PlaySnake(round);' in method(fb, 'private void FinalizePlacement('), 'akis kopuk')

print()
print('=== 5. HAREKET: TEK OMURGA ===')
frame = method(view, 'private void PaintFrame()')
check('tek polyline: onde basin yolu, arkada govde',
      'spine.Build(pathBuffer, behindBuffer)' in frame and 'act.Path[i]' in frame, 'omurga yok')
check('her segment bir hucre araliginda',
      'float arc = headArc + i * cellSize;' in frame, 'segmentler bagimsiz')
turn_fn = method(view, 'public void PlayTurn(TurnScene turn)')
check('basin yolu Core adimlarindan (view kendi yol uretmiyor)',
      'view.CellToWorld(turn.Steps[i][0])' in turn_fn
      and 'float cells = turn.Steps.Count;' in turn_fn, 'yol uydurma')
check('sure mesafeye gore, ustten sinirli',
      'Style.MoveDurationPerExtraCell * Mathf.Max(cells - 1f, 0f)' in turn_fn
      and 'Style.MoveMaxDuration' in turn_fn, 'sure sabit')
# Isirmada kayma, yolun basin park ettigi mesafe kadar EKSIGI: komsu bloku yerken kayacak yol
# neredeyse yok, tam hucre gibi zamanlanirsa yilan vurmadan once oyle durur.
check('isirmada kayma suresi park mesafesi kadar kisaliyor',
      'cells = Mathf.Max(cells - Style.BiteLungeDistance' in turn_fn, 'yilan vurmadan once duruyor')
check('hizlanma/yavaslamali egri', 'Eased(u, Style.MoveEaseIn, Style.MoveEaseOut)' in view,
      'dogrusal lerp')
check('kose YAPISTIRILMIYOR (govde kopmasin)',
      'at = corner;' not in view, 'kose yapistirilmis - govde kopar')
check('topoloji degisiminde carpraz gecis, sert takas yok',
      'seg.FadeClock = seg.FadeLength;' in view and 'Style.TopologyCrossfadeDuration' in view,
      'sprite pop')
check('bas ve kuyruk ceyrek donusu ZAMAN icinde yapiyor',
      'Mathf.MoveTowardsAngle(seg.Angle, target, speed)' in seg_fn
      and 'bool turns = index == 0 || index == count - 1;' in seg_fn, 'uclar aninda donuyor')
# Aradaki DOSENEN parcalar ceyrek izgarada kalmali: pivot hucre merkezinde + ceyrek donus, agizlar
# hucre sinirina oturuyor. 30 derece egilen bir gövde karosu her ekte centik acar. Duz parca zaten
# ceyrek suzmez - once dirsek olur, sonra oteki yone duz olur; ona gereken temiz SPRITE degisimi.
check('govde karolari ceyrek izgarada kaliyor (ek yerleri acilmasin)',
      'seg.HasAngle = false;' in seg_fn and 'Style.TopologyCrossfadeDuration' in view,
      'karolar izgaradan cikiyor')

print()
print('=== 6. ISIRMA ===')
proxy = method(view, 'private void PaintProxy(float headArc)')
eat = strip_comments(read('Assets', 'Scripts', 'View', 'SnakeEatView.cs'))
check('yenen blok KENDI yuzuyle, kendi cikarma shaderiyla ciziliyor',
      'act.EatenLook' in proxy and 'bite.Begin(view, act.EatenLook, cell' in proxy
      and 'proxy.sprite = food.Tile;' in eat, 'blok kendi yuzunu kaybediyor')
# ESKI OKUMA BURADAN GITTI: blok kucultulup sikistirilip agza kaydiriliyordu, yani "tween'li
# silinen bir tile". Artik maddesi soküluyor - kucultme ve kaydirma SnakeView'da kalmadi.
check('blok kucultulup agza KAYDIRILMIYOR (maddesi sokuluyor)',
      'EatenBlockFinalScale' not in view and 'EatenBlockCompression' not in view
      and 'Vector2.Lerp(cell, jaw' not in proxy, 'blok sadece kuculup kayiyor')
# Ilk gecis burada basarisiz oldu: bas bloga ceyrek hucre kala duruyordu, yani isirma aninda
# blogun ucte ucunu kapatiyordu. Gorulecek blok, temas cizgisi ve yutacak yer kalmiyordu.
check('bas blogun ON YUZUNDE duruyor (uzerinde degil)',
      float(re.search(r'BiteLungeDistance = ([\d.]+)f', view).group(1)) >= 0.6
      and 'SnakeEatView.HeadOffset(t, parked)' in view, 'bas blogun ustune biniyor')
check('isirmanin her isareti BASTAN ve blogun YUZUNDEN olculuyor',
      'Vector2 head = spine.At(headArc)' in proxy and 'Vector2 face = cell - axis' in proxy
      and 'Hook.BiteContact, face' in proxy, 'hucre merkezinden olculuyor')
check('isirmanin TEK saati var (zaman cizelgesi SnakeEatView.Style)',
      'private float BiteClock(Act a)' in view
      and 'SnakeEatView.Style.Total' in view
      and 'public static float BiteAnticipationDuration' not in view
      and 'public static float MouthCloseDuration' not in view
      and 'public static float GulpDuration' not in view,
      'isirmanin suresi iki yerde')
check('cene inince basin KENDI yuzeyi cevap veriyor',
      'Style.BiteContactSurfaceResponse' in view and 'Style.BiteContactAccentBoost' in view
      and 'jawImpact = 1f;' in proxy, 'isirma sadece tween')
check('genel kaldirma efektlerinden hicbiri kullanilmiyor',
      not any(w in view for w in ('PlayRemoval', 'ColdSink', 'PhaseFold', 'Cryo', 'CellFlashFx',
                                  'ClusterBurst.Play', 'MomentumPeel')), 'kaldirma efekti')
check('acik agiz sprite\'i gercek isirmada',
      'MouthIsOpen(act)' in view and 'Piece.HeadOpen' in view, 'agiz acilmiyor')
check('yeme hareketin devami (ayri klip degil)',
      'BiteHeadOffset(act)' in frame and 'Beat.Move' in method(view, 'public void PlayTurn(TurnScene turn)'),
      'yeme ayri olay')
check('buyumenin ekledigi segment YUTARKEN, govdenin bittigi yerde doguyor',
      'GrowthShare(act)' in frame and 'arc = (count - 1) * cellSize;' in frame
      and 'Style.GrowthSegmentStartScale' in view, 'fazladan segment bastan var / pat diye biter')
check('buyume CORE sonucundan (view segment eklemiyor)',
      'CopyInto(body, a.To);' in method(view, 'private void OnActFinished(Act a)')
      and 'swollenIndex = 1;' in view, 'view kendi uzatiyor')

print()
print('=== 7. KESILME ===')
squeeze = method(view, 'private float CutSqueezeAt(int index, int count)')
check('sinyal hattin dokundugu segmentten kuyruga iniyor',
      'Style.CutIntersectionResponse' in squeeze and 'Style.CutSignalTravelDuration' in squeeze
      and 'count - 1, travel' in squeeze, 'sinyal yok')
check('kopan segment yumusak gidiyor (geri tepme, kuculme, solma)',
      'private void Release(' in view and 'Style.TailDetachDistance' in view
      and 'Style.TailDetachRotation' in view and 'Style.TailDetachDuration' in view,
      'sert silme')
check('omurga kuyrugun arkasinda bir hucre yedek track tasiyor',
      'ExtendBehind(pathBuffer, behindBuffer);' in frame
      and 'private static void ExtendBehind(' in view, 'geride kalan govde omurgadan tasar')
check('kuyruk yonunu komsusuna giden tegenden aliyor',
      'spine.Toward(arc - 0.5f * cellSize)' in seg_fn, 'kuyruk kopukluk/bosluk yapiyor')
check('yeni kuyruk kendi suresinde donusuyor',
      'Style.NewTailMorphDuration' in view and 'Style.NewTailSettleAmount' in view,
      'yeni kuyruk pop')
check('cok kesikte sirayla', 'Style.MultiCutDelay' in view, 'kesikler ust uste')
check('kesik sayisi Core\'dan', 'turn.RemovedTailCells.Count' in play, 'view sayiyor')
check('son segment gidince farkli bir final',
      'Beat.Defeat' in view and 'Style.DefeatCollapseScale' in view
      and 'Style.DefeatMoteCount' in vfx and 'Style.DefeatBoardPulseStrength' in vfx,
      'yenilgi yok')

print()
print('=== 8. YASAKLAR VE PERFORMANS ===')
check('parcacik sistemi / iz / sarsinti / kamera / zaman / rastgele yok',
      not any(w in view for w in ('ParticleSystem', 'TrailRenderer', 'LineRenderer', 'ShakeCamera',
                                  'Camera', 'timeScale', 'Random.')), 'yasak var')
check('yeni materyal uretilmiyor, renderer havuzlu',
      'new Material(' not in view and 'private SpriteRenderer Rent(' in view
      and 'spare.Push(r)' in view, 'havuz yok')
check('animasyon dongusunde LINQ yok', 'System.Linq' not in view, 'LINQ')
check('patlama / parlama dili yok',
      'Explode' not in view and 'Flash' not in view, 'patlama dili')
check('gorsel varyasyon deterministik (hucre hash\'i)',
      'private static uint Hash(int x, int y)' in view and 'Dice(' in view, 'rastgele')

print()
print('=== 9. TAHTA VE YASAM DONGUSU ===')
refresh = method(boardview, 'public void Refresh()')
check('tahta yilan hucrelerine kup CIZMIYOR (yilan kendini ciziyor)',
      'cube.Value.Kind == CubeKind.Snake' in refresh and 'sawSnake = true;' in refresh,
      'tahta yesil kare ciziyor')
check('BoardView sahibi, yeniden kurulumda yasiyor, her cizimde senkron',
      'public SnakeView Snake' in boardview and 'child == keepSnake' in boardview
      and 'Snake.Sync(this);' in refresh, 'sahiplik')
check('govde her yenilemede Core\'dan',
      'boardView.Snake.SetBody(boss.Body' in fb, 'govde view hafizasindan')
check('ilk cizimde uyanma dalgasi',
      'SetBody(boss.Body, !snakeSpawned)' in fb and 'Beat.Spawn' in view, 'uyanma yok')
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('kosu bitince duruyor', 'boardView.StopSnake()' in menus
      and 'public void StopSnake()' in boardview, 'kosu sonu')
check('csproj iki dosyayi da iceriyor',
      'SnakeView.cs' in read('ProjectBlock.View.csproj')
      and 'SnakeVisuals.cs' in read('ProjectBlock.Core.csproj'), 'csproj eksik')

print()
print('=== 10. OLCULER ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in
             re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
names = list(style.keys())
dead = [n for n in names if not re.search(r'\bStyle\.%s\b' % n, view[end:] + fb + lab)]
print('   %d ayar, %d okunuyor' % (len(names), len(names) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['SpawnDuration', 'SpawnSegmentDelay', 'SpawnStartScale', 'SpawnBrightnessStart',
          'SpawnHeadSettleAmount', 'IdleWavePeriodMin', 'IdleWavePeriodMax', 'IdleWaveSegmentDelay',
          'IdleCrossScaleAmount', 'IdleLongScaleAmount', 'IdleHeadPressureAmount',
          'IdleTailSwayAmount', 'MoveDurationOneCell', 'MoveDurationPerExtraCell',
          'MoveMaxDuration', 'MoveEaseIn', 'MoveEaseOut', 'MoveBodyWaveStrength',
          'MoveBodyWaveSpeed', 'MoveHeadLeadAmount', 'MoveSettleDuration', 'MoveSettleAmount',
          'HeadTurnDuration', 'TopologyCrossfadeDuration', 'StuckDuration', 'StuckHeadPressure',
          'StuckBodyWaveLength', 'StuckBodyWaveStrength', 'BiteLungeDistance',
          'GulpStrength', 'SwollenAppearDuration',
          'SwollenOvershoot', 'SwollenHoldDuration', 'SwollenFadeDuration',
          'CutIntersectionResponse', 'CutSignalTravelDuration', 'CutSignalCompression',
          'TailConstrictDuration', 'TailDetachDistance', 'TailDetachDuration', 'TailDetachRotation',
          'NewTailMorphDuration', 'NewTailSettleAmount', 'MultiCutDelay',
          'DefeatCollapseDuration', 'DefeatCollapseScale']
absent = [w for w in wanted if w not in style]
check("brief'in %d ayari Style'da" % len(wanted), not absent, 'eksik ayar: %s' % absent)
check('bir hucre %.0f ms (180-280)' % (style['MoveDurationOneCell'] * 1000),
      0.18 <= style['MoveDurationOneCell'] <= 0.28, 'bir hucre suresi')
three = style['MoveDurationOneCell'] + 2 * style['MoveDurationPerExtraCell']
check('uc hucre %.0f ms (280-450)' % (three * 1000), 0.28 <= three <= 0.45, 'uc hucre')
check('uzun kayma en fazla %.0f ms (550-800)' % (style['MoveMaxDuration'] * 1000),
      0.55 <= style['MoveMaxDuration'] <= 0.80, 'uzun kayma')
check('uyanma %.0f ms (450-750)' % (style['SpawnDuration'] * 1000),
      0.45 <= style['SpawnDuration'] <= 0.75, 'uyanma suresi')
check('bekleme dalgasi %.1f-%.1f s (1.8-3.5)'
      % (style['IdleWavePeriodMin'], style['IdleWavePeriodMax']),
      1.8 <= style['IdleWavePeriodMin'] and style['IdleWavePeriodMax'] <= 3.5, 'bekleme periyodu')
# TAVAN VE TABAN. Sadece tavan koymak "olcülu" ile "hic yok"u ayirt etmiyor: %2 genlik ~58px'lik
# bir hucrede tek piksel, yani ekranin cozunurlugunun altinda. Bekleme GORUNMEK zorunda, ama her
# eylemden belirgin sekilde zayif kalmali.
check('bekleme genligi %%%.1f capraz / %%%.1f boyuna (8-12 / daha az)'
      % (style['IdleCrossScaleAmount'] * 100, style['IdleLongScaleAmount'] * 100),
      0.08 <= style['IdleCrossScaleAmount'] <= 0.12
      and 0.03 <= style['IdleLongScaleAmount'] < style['IdleCrossScaleAmount'], 'bekleme gorunmez')
check('bekleme dalgasi hic sifirlanmiyor (govde donup kalmiyor)',
      'float pulse = 0.5f + 0.5f * Mathf.Sin(phase * 6.2831853f);' in seg_fn,
      'dalga yarim periyot boyunca duruyor')
# SIRALAMA, sadece her basamagin tavani degil. Brief'in yogunluk hiyerarsisi: bekleme en altta,
# eylemler uzerine. Her biri ayri ayri "olcülu" olup hepsi birlikte gorunmez kalabilir - ilk halde
# sikisma beklemeden DAHA ZAYIFTI, yani duvara dayanan yaratik nefes alandan az kimildiyordu.
check('yogunluk merdiveni: bekleme < hareket dalgasi degil ama < sikisma < kesme',
      style['IdleCrossScaleAmount'] < style['StuckBodyWaveStrength']
      < style['CutSignalCompression'], 'yogunluk hiyerarsisi ters')
check('hicbir genlik piksel alti degil (>= %3, ~58px hucrede 2px)',
      min(style['IdleCrossScaleAmount'], style['StuckHeadPressure'],
          style['StuckBodyWaveStrength'], style['MoveBodyWaveStrength']) >= 0.03,
      'ekranda gorunmeyecek kadar kucuk')
check('hareket govde dalgasi %%%.1f (3-7)' % (style['MoveBodyWaveStrength'] * 100),
      0.03 <= style['MoveBodyWaveStrength'] <= 0.07, 'govde dalgasi')
check('sikisma %.0f ms (150-250)' % (style['StuckDuration'] * 1000),
      0.15 <= style['StuckDuration'] <= 0.25, 'sikisma suresi')
estyle = dict((n, float(v)) for n, v in
              re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', eat))
bite = max(estyle['SwollenStart'] + estyle['SwollenRelaxDuration'],
           max(estyle['GulpStart'] + estyle['GulpDuration'],
               estyle['MouthCloseStart'] + estyle['MouthCloseDuration']))
check('isirma %.0f ms (700-950)' % (bite * 1000), 0.70 <= bite <= 0.95, 'isirma suresi')
check('bas bloga %.2f hucre kala duruyor (0.60-1.00)' % style['BiteLungeDistance'],
      0.60 <= style['BiteLungeDistance'] <= 1.00, 'hamle mesafesi')
# Bas bloga kala duran mesafe, kaymanin en kisa halinden (bir hucre) uzun olamaz: olursa
# govdenin son segmenti omurganin ucunu gecer ve `arc > limit` onu hic cizmez - isirma boyunca
# yilan bir segment eksik gorunur, buyumenin ekledigi segment son karede pat diye biter.
check('bas + geri cekilme bir hucreyi gecmiyor (%.2f)'
      % (style['BiteLungeDistance'] + estyle['CoilDistance']),
      style['BiteLungeDistance'] + estyle['CoilDistance'] <= 1.0,
      'govde omurgadan tasar')
check('kesme sinyali %.0f ms (120-300)' % (style['CutSignalTravelDuration'] * 1000),
      0.12 <= style['CutSignalTravelDuration'] <= 0.30, 'sinyal suresi')
check('sinyal sikismasi %%%.1f (12-20)' % (style['CutSignalCompression'] * 100),
      0.12 <= style['CutSignalCompression'] <= 0.20, 'sinyal genligi')

check('cok kesik arasi %.0f ms (70-120)' % (style['MultiCutDelay'] * 1000),
      0.07 <= style['MultiCutDelay'] <= 0.12, 'kesik araligi')
check('uzun yilanda genlik kisiliyor (%d+ segment)' % style['LodLongLength'],
      12 <= style['LodLongLength'] <= 18 and style['LodLongAmplitude'] < 1.0
      and 'private float Lod' in view, 'LOD yok')

print()
print('=== 11. LAB ===')
check('yilan sahnesi sonucu EKRANDA BIRAKIYOR (kendi kendine resync etmiyor)',
      'AnimResync();' not in method(lab, 'private IEnumerator SnakeRoutine(AnimSnakeScene scene)'),
      'sahne bir an sonra silinip turun tahtasi geri geliyor')
scenes = ['yılan: uyanıyor, 8 segment', 'yılan: uyanıyor, 12 segment', 'yılan: uyanıyor, 20 segment',
          'yılan: bekleme', 'yılan: bir hücre kayar', 'yılan: üç hücre kayar', 'yılan: uzun kayma',
          'yılan: yön değiştirerek kayar', 'yılan: sıkıştı', 'yılan: sade blok yer',
          'yılan: obsidyen yer', 'yılan: altın yer', 'yılan: yer ve uzar',
          'yılan: bir kuyruk kesilir', 'yılan: aynı turda iki kesik', 'yılan: aynı turda üç kesik',
          'yılan: son segment', 'yılan: tam tur dizisi', 'yılan: hat dizisi',
          'yılan testi: dört yön', 'yılan testi: gövde şekilleri', 'yılan testi: dört yönde ısırma']
absent = [s for s in scenes if s not in lab_raw]
check('%d sahne girisi' % len(scenes), not absent, 'labda eksik: %s' % absent)
start = view.index('public static class Layers')
end = view.index('\n        }', start)
switches = re.findall(r'public static bool (\w+)', view[start:end])
unread = [s for s in switches if not re.search(r'Layers\.%s\b' % s, view[end:])]
check('%d hata ayiklama anahtari, hepsi okunuyor' % len(switches),
      len(switches) == 8 and not unread, 'okunmayan anahtar: %s' % unread)
check('lab anahtarlarinin tamami girisli',
      all(('SnakeView.Layers.%s' % s) in lab_raw for s in switches), 'anahtar girisi eksik')
check('lab kendi tahtasinda ve oyunun kapisindan',
      'PlaySnakeBody(body, waking)' in lab and 'PlaySnakeScene(turn)' in lab
      and 'boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter)' in lab, 'lab ayri yol')
check('lab kaymayi bossun kuraliyla yuruyor (duvar / govde / blok)',
      'live.Contains(next)' in lab and 'board.GetCube(next)' in lab
      and 'turn.Steps.Add(' in lab, 'lab kaymayi uyduruyor')
close = method(lab, 'private void CloseAnimationLab(')
check('lab kapaninca sahne biter, anahtarlar varsayilana',
      'StopAnimSnake();' in close and 'SnakeView.Layers.Defaults();' in close, 'lab kapanisi')
check('eski "ham yilan" girisi kalmadi', 'ham - Yılan' not in lab_raw, 'eski giris duruyor')


print()
print('=== 12. VFX: YUZEY / TEMAS / AKSIYON ===')
check('ayri bir VFX katmani var ve hareket katmaniyla olay kancalariyla bagli',
      'public sealed class SnakeVfxController' in vfx and 'public enum Hook' in vfx
      and 'Vfx.Signal(' in view, 'VFX katmani yok')
hooks = re.findall(r'^\s{12}(\w+),?$', vfx[vfx.index('public enum Hook'):vfx.index('public enum Hook') + 900],
                   re.M)
wanted_hooks = ['WakeSegment', 'WakeHead', 'MoveStart', 'MoveTurn', 'MoveSettle', 'StuckPressure',
                'MouthOpen', 'BiteContact', 'GulpPass',
                'GrowthSettle', 'LineSnakeImpact', 'CutSignalPass', 'TailPinch', 'TailDetach',
                'NewTailSettled', 'DefeatStart', 'DefeatCollapse', 'DefeatComplete']
missing_hooks = [h for h in wanted_hooks if h not in hooks]
check("brief'in %d olay kancasi tanimli" % len(wanted_hooks), not missing_hooks,
      'eksik kanca: %s' % missing_hooks)
signalled = set(re.findall(r'Hook\.(\w+)', view))
check('%d kanca hareket tarafindan gercekten tetikleniyor' % len(signalled),
      len(signalled) >= 15, 'tetiklenmeyen kancalar: %s' % sorted(set(wanted_hooks) - signalled))
check('deri shaderi: per-renderer, 2B isik etiketi, geri dusus',
      '"LightMode" = "Universal2D"' in skin and 'Fallback "Sprites/Default"' in skin
      and '[PerRendererData] _MainTex' in skin, 'deri shaderi eksik')
check('yuzey tepkisi: sheen, on kenar, aksan, band, sikisma, renk cekilmesi',
      all(('_%s' % k) in skin for k in ('Sheen', 'Lead', 'Accent', 'Band', 'Compression', 'Drain')),
      'yuzey tepkisi eksik')
check('sheen DUNYA uzayinda - ceyrek donus isigi dondurmuyor',
      '(world.xy - _Centre.xy)' in skin and 'float2(-0.42, 0.78)' in skin, 'isik donuyor')
check('tam sprite tint YOK (her etki pikselin kendi rengiyle lerp)',
      'lerp(c, c *' in skin and 'c = _' not in skin.replace('c = _BandColour', ''), 'sprite boyaniyor')
check('parlama / kontur / additive yok',
      not any(w in strip_comments(skin).lower()
              for w in ('bloom', 'blend one one', 'outline', 'glow')),
      'parlama var')
check('TEK materyal, segment basina instance yok (MaterialPropertyBlock)',
      vfx.count('new Material(') == 1 and 'public static Material SkinMaterial' in vfx
      and 'SetPropertyBlock(block)' in vfx and 'new Material(' not in view, 'materyal cogaltiliyor')
check('segmentler deriyi giyiyor', 'SnakeVfxController.SkinMaterial' in view, 'deri takilmamis')
check('golge segmentin yonune ve parcasina gore (kopyala-yapistir elips degil)',
      'Part.Tail ? 0.74f' in vfx and 'Part.Bend' in vfx and 'horizontal ? along : across' in vfx,
      'golgeler ayni')
check('tahta temasi notr golge renginde (yesil boya izi yok)',
      'ContactColour = new Color(0.05f, 0.07f, 0.09f)' in vfx
      and 'kind == Mark.Arc || kind == Mark.Streak ? Cream : ContactColour' in vfx,
      'tahtaya boya suruluyor')
check('dort parcacik sekli (kare konfeti yok)',
      "enum Grain" in vfx and all(g in vfx for g in ('Soft', 'Short', 'Round', 'Sliver')),
      'tek tip parcacik')
check('parcacik butcesi var ve yilan uzunlugu ile buyumuyor',
      'Style.FleckBudget - flecks.Count' in vfx and 'Style.LodLongContact' in vfx,
      'butce yok')
check('bekleme ve normal hareket parcacik uretmiyor',
      'Hook.MoveStart' in vfx and 'smearing = true' in vfx
      and 'case Hook.CutSignalPass:' in vfx and 'Throw(' not in
      vfx[vfx.index('case Hook.MoveStart:'):vfx.index('case Hook.MoveTurn:')], 'hareket parcacik saciyor')
# Yenen blogun rengi artik SnakeEatView'in isi (snake_eat.py), ve tint'ten degil blogun
# MADDESINDEN geliyor: kendi boyasini tasiyan bir tile'in tinti beyazdir, korumali kupte magenta.
check('yenen blogun rengi bu katmanda BOYANMIYOR (bite kendi paletini turetiyor)',
      'BiteIngestionStreak' not in vfx and 'Paint = ViewUtil.CubeMaterialColor' in fb,
      'blok rengi kayboluyor')
check('yerel isik ve tahta nabzi kucuk ve yerel',
      'Style.LocalLightRadius' in vfx and 'Style.DefeatBoardPulseRadius' in vfx
      and 'Mark.Pulse' in vfx, 'isik/nabiz yok')
check('yasaklar: parcacik sistemi, iz, sarsinti, kamera, rastgele yok',
      not any(w in vfx for w in ('ParticleSystem', 'TrailRenderer', 'ShakeCamera', 'Camera',
                                 'timeScale', 'Random.')), 'yasak var')
check('VFX havuzlu', 'private SpriteRenderer Rent(' in vfx and 'spare.Push(r)' in vfx, 'havuz yok')
check('VFX varyasyonu deterministik', 'private struct Dice' in vfx and 'Random' not in vfx,
      'rastgele')
start = vfx.index('public static class Style')
end = vfx.index('\n        }', start)
vstyle = dict((n, float(v)) for n, v in
              re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', vfx[start:end]))
vdead = [n for n in vstyle if not re.search(r'\bStyle\.%s\b' % n, vfx[end:] + view)]
print('   %d VFX ayari, %d okunuyor' % (len(vstyle), len(vstyle) - len(vdead)))
if vdead:
    fail.append('okunmayan VFX Style: ' + ', '.join(vdead))
vwanted = ['VfxGlobalIntensity', 'SurfaceSheenStrength', 'SurfaceSheenSoftness',
           'LeadingEdgeHighlightStrength', 'ShadowOpacity', 'ShadowSoftness',
           'ShadowMoveSoftening', 'BoardContactStrength', 'BoardContactDuration',
           'LocalLightStrength', 'LocalLightRadius', 'WakeSheenStrength', 'WakeSheenDuration',
           'WakeColorRecovery', 'WakeShadowTightening', 'WakeBoardPressure', 'WakeMoteCount',
           'MoveSurfaceFlowStrength', 'MoveLeadingHighlight', 'MoveContactSmearStrength',
           'MoveContactSmearDuration', 'MoveTurnHighlightStrength', 'MoveSettlePressureStrength',
           'StuckContactPressure', 'StuckContactDuration', 'StuckDustCount',
           'StuckSurfaceCompressionResponse', 'BiteContactArcStrength', 'BiteContactArcDuration',
           'BiteFleckCount', 'BiteFleckSpeed', 'BiteFleckLifetime', 'BiteMouthShadowStrength',
           'GulpSurfaceResponse', 'GulpAccentBoost', 'SwollenSurfaceTension',
           'GrowthContactPressure', 'CutImpactSurfaceResponse', 'CutImpactFleckCount',
           'CutSignalMaterialStrength', 'CutSignalAccentBoost', 'TailPinchShadowStrength',
           'TailDetachFleckCount', 'TailDetachFleckDistance', 'TailDetachAfterimageStrength',
           'NewTailSurfaceResponse', 'NewTailContactPressure', 'DefeatColorDrain',
           'DefeatSurfaceDim', 'DefeatMoteCount', 'DefeatMoteDistance', 'DefeatMoteLifetime',
           'DefeatResidueStrength', 'DefeatResidueDuration', 'DefeatBoardPulseStrength',
           'DefeatBoardPulseRadius', 'DefeatBoardPulseDuration', 'DefeatShadowDeathDuration']
vabsent = [w for w in vwanted if w not in vstyle]
check("brief'in %d VFX ayari tanimli" % len(vwanted), not vabsent, 'eksik VFX ayari: %s' % vabsent)
check('bite parcacigi %d (2-6), cut %d (3-6), defeat %d (8-12)'
      % (vstyle['BiteFleckCount'], vstyle['TailDetachFleckCount'], vstyle['DefeatMoteCount']),
      2 <= vstyle['BiteFleckCount'] <= 6 and 3 <= vstyle['TailDetachFleckCount'] <= 6
      and 8 <= vstyle['DefeatMoteCount'] <= 12, 'parcacik butcesi')
check('temas izi %.0f ms (50-100)' % (vstyle['MoveContactSmearDuration'] * 1000),
      0.05 <= vstyle['MoveContactSmearDuration'] <= 0.10, 'iz suresi')
check('isirma yayi %.0f ms (80-120)' % (vstyle['BiteContactArcDuration'] * 1000),
      0.08 <= vstyle['BiteContactArcDuration'] <= 0.12, 'yay suresi')
# Yutma izleri BURADAN GITTI: uc duz lekeydi ve blogun TINT'iyle boyaniyordu, yani beyaz ya da
# korumali kupte magenta - ekran goruntulerindeki pembe duz cizgiler. Blogun maddesi artik
# SnakeEatView'in kivrimli, hacimli, blok renginde hüzmeleriyle gidiyor (bkz. snake_eat.py).
check('yenilgi nabzi %.1f hucre yaricap (1-2) ve %.0f ms'
      % (vstyle['DefeatBoardPulseRadius'], vstyle['DefeatBoardPulseDuration'] * 1000),
      1.0 <= vstyle['DefeatBoardPulseRadius'] <= 2.0
      and vstyle['DefeatBoardPulseDuration'] <= 0.4, 'nabiz buyuk')
check('golge %%%.0f opaklik (<= 35) ve harekette yumusuyor' % (vstyle['ShadowOpacity'] * 100),
      vstyle['ShadowOpacity'] <= 0.35 and vstyle['ShadowMoveSoftening'] > 0, 'golge')
check('tek global yogunluk knob\'u', vstyle['VfxGlobalIntensity'] == 1.0
      and 'Style.VfxGlobalIntensity' in vfx, 'global knob yok')

print()
print('=== 13. LAB: VFX KATMANLARI ===')
vfx_labels = ['yılan vfx: hareket aç/kapa', 'yılan vfx: temas gölgeleri aç/kapa',
              'yılan vfx: materyal tepkisi aç/kapa', 'yılan vfx: parçacıklar aç/kapa',
              'yılan vfx: tahta teması aç/kapa', 'yılan vfx: bütün katmanları geri aç',
              'yılan: ısırma hero, yavaş çekim']
vabsent = [v for v in vfx_labels if v not in lab_raw]
check('VFX katman satiri ve yavas cekim girisi', not vabsent, 'labda eksik: %s' % vabsent)
vstart = vfx.index('public static class Layers')
vend = vfx.index('\n        }', vstart)
vswitches = re.findall(r'public static bool (\w+)', vfx[vstart:vend])
vunread = [x for x in vswitches if not re.search(r'Layers\.%s\b' % x, vfx[vend:])]
check('%d VFX anahtari, hepsi okunuyor' % len(vswitches),
      len(vswitches) == 4 and not vunread, 'okunmayan VFX anahtari: %s' % vunread)
check('lab kapaninca VFX katmanlari geri aciliyor',
      'SnakeVfxController.Layers.AllOn();' in method(lab, 'private void CloseAnimationLab('),
      'lab kapanisi')
check('csproj VFX dosyasini iceriyor',
      'SnakeVfxController.cs' in read('ProjectBlock.View.csproj'), 'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
