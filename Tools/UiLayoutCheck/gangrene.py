# Kangren (NECROTIC TAKEOVER / KURU CURUME): GangreneView + Gangrene / GangreneStreak shaderlari.
#
#   1. Style: brief'in 32 ayari eksiksiz, okunmayan ayar yok.
#   2. Core: YALNIZ RAPOR - yayilma olayi (hucre, hangi komsudan, oradaki kup, bagisik komsular) ve
#      olen hatlar (satir/sutun, kenar hatti, donusen kupler, oncekileri, kacinci tur); eski
#      cagrilar ayni; RoundEngine rapora iletiyor; Core testleri dogruluyor.
#   3. Akis: tahta yeniden cizildigi karede (suyun arkasina ertelenmeden); donusen hucreler artik
#      eski soguk ize gitmiyor; view hangi hucre/yon/hat/kenar oldugunu KENDI hesaplamiyor.
#   4. Teslim: hucreler inene kadar tahtada tutulur, olu hat yikamasi supurmeyle gelir, bitiste
#      devir teslim gorunmez (ayni pisirilmis dokuyu hem shader hem duran katman kullanir).
#   5. Yasaklar: kaldirma efektlerinin tekrari, parcacik / iz / sarsinti / kamera / rastgele yok;
#      palet kul - kurum - olu zeytin, zehirli yesil / lime / neon degil.
#   6. Olculer ve zamanlama brief'in araliklarinda.
#   7. Lab: 9 sahne, gercek kurallarla (SpreadGangrene / InfectFullLines) ve oyunun kapisindan;
#      8 hata ayiklama anahtari; kapanista sifirlama.
#   8. Yasam dongusu: BoardView sahibi, yeniden kurulumda yasar, menude duruyor, csproj.
import colorsys
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
    m = re.search(r'\n        (private|public|internal|protected) ', rest)
    return src[i:i + len(signature) + (m.start() if m else len(rest))]


def check(label, ok, why):
    print('   %-72s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
    if not ok:
        fail.append(why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'GangreneView.cs'))
boardview = strip_comments(read('Assets', 'Scripts', 'View', 'BoardView.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
vu = strip_comments(read('Assets', 'Scripts', 'View', 'ViewUtil.cs'))
lab_raw = read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs')
lab = strip_comments(lab_raw)
report = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'TurnReport.cs'))
gang = strip_comments(read('Assets', 'Scripts', 'Core', 'Board', 'GameBoard.Gangrene.cs'))
scoring = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'RoundEngine.Scoring.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')
cube_shader = read('Assets', 'Resources', 'Shaders', 'Gangrene.shader')
streak_shader = read('Assets', 'Resources', 'Shaders', 'GangreneStreak.shader')

print('=== 1. STYLE ===')
start = view.index('public static class Style')
end = view.index('\n        }', start)
style = dict((n, float(v)) for n, v in
             re.findall(r'public static (?:float|int) (\w+) = (-?[\d.]+)f?;', view[start:end]))
names = re.findall(r'public static (?:float|int|SweepMode) (\w+) =', view[start:end])
dead = [n for n in names if not re.search(r'\bStyle\.%s\b' % n, view[end:] + fb)]
print('   %d ayar, %d okunuyor' % (len(names), len(names) - len(dead)))
if dead:
    fail.append('okunmayan Style: ' + ', '.join(dead))
wanted = ['SpreadDurationEmpty', 'SpreadDurationOccupied', 'SourceCreepStrength',
          'FloorContaminationAmount', 'VeinVisibility', 'VeinCount', 'VeinSpeed',
          'ColorDrainStrength', 'SurfaceMatteAmount', 'CrackVisibility', 'SurfaceCollapseAmount',
          'SpawnRiseAmount', 'SpawnSettleDuration', 'IdlePulseStrength', 'IdlePulsePeriodMin',
          'IdlePulsePeriodMax', 'TurnDamagePulseStrength', 'TurnDamagePulseDuration',
          'DeadLineSweepDuration', 'DeadLineSweepMode', 'DeadLineUnderlayOpacity',
          'DeadLineUnderlayRadius', 'DeadLineTextureStrength', 'DeadLineShadowStrength',
          'EdgeTransferDuration', 'EdgeTransferVisibility', 'EdgeTransferVeinStrength',
          'EdgeConversionDuration', 'EdgeConversionStagger', 'ChainPauseDuration',
          'VisualSeedVariation', 'LargeClusterCompensation']
missing = [w for w in wanted if w not in names]
check("brief'in %d ayari Style'da" % len(wanted), not missing, 'eksik ayar: %s' % missing)
check('supurme yonu secilebilir (CentreOut / EndToEnd)',
      'enum SweepMode' in view and 'CentreOut' in view and 'EndToEnd' in view, 'supurme yonu yok')

print()
print('=== 2. CORE: YALNIZ RAPOR ===')
check('rapor: yayilma olayi (hucre, kaynak, onceki kup, bagisik komsular)',
      'public sealed class GangreneSpread' in report
      and all(('public %s %s { get; }' % t) in report for t in
              (('GridPos', 'Cell'), ('GridPos?', 'Source'), ('Cube?', 'Before')))
      and 'public IReadOnlyList<GridPos> Immune { get; }' in report, 'yayilma olayi eksik')
check('rapor: olen hat (satir/sutun, hat, kenar hatti, donusenler, oncekileri, tur)',
      'public sealed class GangreneLineDeath' in report
      and all(('public %s %s { get; }' % t) in report for t in
              (('bool', 'IsRow'), ('int', 'Line'), ('int', 'EdgeLine'), ('int', 'Pass')))
      and 'public IReadOnlyList<GridPos> Converted { get; }' in report
      and 'public IReadOnlyList<Cube> Before { get; }' in report, 'hat olumu eksik')
check('rapor tasiyor (GangreneSpread + GangreneLineDeaths)',
      'public GangreneSpread GangreneSpread { get; internal set; }' in report
      and 'public IReadOnlyList<GangreneLineDeath> GangreneLineDeaths' in report, 'rapor tasimiyor')
spread = method(gang, 'public GridPos? SpreadGangrene(IRandomSource rng, out GangreneSpread spread)')
check('yayilma: tek zar atisi, once ne vardi, hangi curumus komsudan',
      spread.count('rng.NextInt(') == 1 and 'Cube? before = cells[tx, ty];' in spread
      and 'RottenNeighbour(tx, ty)' in spread and 'ImmuneAround(tx, ty)' in spread,
      'yayilma raporu kurali degistiriyor ya da eksik')
check('kaynak komsu SABIT sirayla bulunur (zar harcamaz)',
      'private GridPos? RottenNeighbour(int x, int y)' in gang
      and 'rng' not in method(gang, 'private GridPos? RottenNeighbour(int x, int y)'),
      'kaynak secimi rastgele')
lines = method(gang, 'public List<GridPos> InfectFullLines(List<GangreneLineDeath> deaths)')
check('olen her hat sirayla, kendi kenar hatti ve donusturdugu kuplerle',
      lines.count('deaths.Add(new GangreneLineDeath(') == 2
      and 'converted.GetRange(first, converted.Count - first)' in lines
      and 'pass));' in lines, 'hat olumleri yazilmiyor')
check('donusen kupun ONCEKI hali de yaziliyor',
      'before.Add(cells[x, y].Value);' in gang, 'onceki kup yok')
check('eski cagrilar ayni',
      'return SpreadGangrene(rng, out ignored);' in gang and 'return InfectFullLines(null);' in gang,
      'eski cagri degisti')
sp = method(scoring, 'internal GridPos? SpreadGangrene()')
check('RoundEngine ikisini de rapora iletiyor',
      'MainBoard.SpreadGangrene(rng, out spreadEvent)' in sp
      and 'MainBoard.InfectFullLines(deaths)' in sp
      and 'currentReport.GangreneSpread = spreadEvent;' in sp
      and 'currentReport.AddGangreneLineDeaths(deaths);' in sp, 'rapora iletilmiyor')
check('Core testleri raporu dogruluyor',
      'Kangren_TheReportSaysWhatTheRotTook' in tests and 'report.GangreneSpread' in tests
      and 'InfectFullLines(deaths)' in tests
      and 'reporting the spread changes neither the draw nor where it went' in tests, 'test yok')

print()
print('=== 3. AKIS ===')
fin = method(fb, 'private void FinalizePlacement(')
check('tahta yeniden cizildigi karede, suyun arkasina ertelenmeden',
      'PlayGangrene(round, report);' in fin
      and fin.index('RefreshAll(report);') < fin.index('PlayGangrene(round, report);')
      < fin.index('WaterFallFrames'), 'rot ertelenebilir')
play = method(fb, 'private bool PlayGangrene(RoundEngine round, TurnReport report)')
check('her sey rapordan: hucre, kaynak, onceki kup, bagisik, hat, kenar, donusenler',
      all(w in play for w in ('spread.Cell', 'spread.Source', 'spread.Before', 'spread.Immune',
                              'death.IsRow', 'death.Line', 'death.EdgeLine', 'death.Converted[i]',
                              'death.Before[i]')), 'view tahtadan tahmin ediyor')
check('tur faturasi bossun kendi yazdigi dokumden',
      'round.Boss as KangrenBoss' in play and 'report.Score.Contributions' in play
      and 'c.Source == boss.DefId' in play, 'fatura varsayiliyor')
check('view kendi hat / kenar hesabi yapmiyor',
      'RowIsInfectionDead' not in play and 'InfectionDeadRows' not in play
      and 'Width - 1' not in play, 'view hesapliyor')
emit = method(fb, 'private void EmitBlastParticles(')
check('DONUSEN hucreler artik eski soguk ize gitmiyor',
      'LiftKind.Transformed' in emit
      and emit.index('LiftKind.Transformed') < emit.index('LiftCells('),
      'donusen hucre iki kez oynatiliyor')
turn = method(view, 'public void PlayTurn(TurnScene turn)')
check('zincir sirali: supurme -> aktarim -> donusum -> duraklama',
      turn.index('Style.DeadLineSweepDuration') < turn.index('Style.EdgeTransferDuration')
      < turn.index('Style.EdgeConversionDuration') < turn.index('Style.ChainPauseDuration'),
      'zincir sirasi yok')
check('kenardaki BOS hucreye kup dogmuyor (yalniz rapordaki donusenler)',
      'death.Converted.Count' in turn and 'SetCubeAt' not in view, 'view kup uretiyor')

print()
print('=== 4. TESLIM ===')
check('hucreler oynarken tahtada tutulur, inince birakilir',
      'view.HoldCells(scene.Held)' in turn and 'view.ReleaseCells(cellBuffer)' in view
      and 'view.ReleaseCells(done.Held)' in view, 'teslim yok')
check('olu hat yikamasi geri sarilir, supurmeyle gelir',
      'view.SetRotWash(cell, 0f);' in view and 'view.SetRotWash(e.Cells[i]' in view
      and 'public void SetRotWash(GridPos cell, float amount)' in boardview
      and 'RotWashAt(gp) * (cube.HasValue ? 0.4f : 0.66f)' in boardview, 'yikama animasyonsuz')
check('yikama tek hucrede yeniden boyanabiliyor (her karede tam refresh yok)',
      'preWashCache[x, y] = color;' in boardview
      and 'private void RepaintWash(GridPos cell)' in boardview, 'her karede tam refresh')
check('duran doku tahtanin gercekten cizdigi kupe bagli (RotLight)',
      'public float RotLight(GridPos cell)' in boardview and 'view.RotLight(' in view,
      'doku hayalet kupe biniyor')
check('curumus kup SADE karoda (kartin yuzunu tasimiyor)',
      'if (kind == CubeKind.Gangrene)' in vu and 'ViewUtil.CubeTile(CubeKind.Gangrene, null)' in view,
      'curumus kup kartin yuzunu tasiyor')
look = method(view, 'private static Texture2D LookAtlas()')
check('devir teslim gorunmez: ayni pisirilmis doku hem shaderde hem duran katmanda',
      'block.SetTexture(LookTexId, LookAtlas())' in view and 'LookSprite(p.Variant)' in view
      and '1f - (1f - a1) * (1f - a2) * (1f - a3)' in look
      and 'lerp(c, look.rgb, saturate(look.a * _LookVis))' in cube_shader, 'iki katman ayrisir')
orders = dict((n, int(v)) for n, v in re.findall(r'private const int (\w+) = (-?\d+);', view))
check('band hucrelerin ALTINDA (%d < 1), duran doku ustunde (%d > 1)'
      % (orders['BandOrder'], orders['SurfaceOrder']),
      orders['BandOrder'] < 1 < orders['SurfaceOrder'] < 3, 'siralama')
check('olu katman canli katmanin altinda (%d < %d)' % (orders['RotOrder'], orders['BodyOrder']),
      orders['ShadowOrder'] < orders['RotOrder'] < orders['BodyOrder'] < orders['PathOrder'],
      'olu kup ustte kaliyor')

print()
print('=== 5. YASAKLAR VE PALET ===')
check('kaldirma efektlerinden hicbiri yeniden kullanilmiyor',
      not any(w in view for w in ('ColdSink', 'PhaseFold', 'Cryo', 'CellFlashFx'))
      and 'CellFlashFx' not in play and 'PlayRemoval' not in play, 'kaldirma efekti tekrari')
check('parcacik / iz / sarsinti / kamera / zaman / rastgele yok',
      not any(w in view for w in ('ParticleSystem', 'TrailRenderer', 'LineRenderer', 'ShakeCamera',
                                  'Camera', 'timeScale', 'Random.')), 'yasak var')
check('patlama dili yok (parlama / patlama cagrisi)',
      'Explode' not in view and 'Flash' not in view, 'patlama dili')
cols = {}
for n, v in re.findall(r'private static readonly Color (\w+) = new Color\(([^)]*)\)', view):
    cols[n] = tuple(float(x) for x in re.findall(r'-?\d*\.?\d+', v)[:3])
toxic = []
for n, c in cols.items():
    # The lab's own marker colour is not part of the palette: it is never on a player's screen.
    if 'Debug' in n:
        continue
    h, s, v = colorsys.rgb_to_hsv(*c)
    if s > 0.35 or (60 <= h * 360 <= 160 and s > 0.30):
        toxic.append('%s %s (doygunluk %.2f, ton %.0f)' % (n, c, s, h * 360))
check('hicbir renk zehirli / lime / neon degil', not toxic, 'zehirli renk: %s' % toxic)
h, s, v = colorsys.rgb_to_hsv(*cols['RotColour'])
check('olu doku olu zeytin (ton %.0f, doygunluk %.2f < 0.30)' % (h * 360, s),
      50 <= h * 360 <= 110 and s < 0.30, 'olu doku rengi')
band_h, band_s, band_v = colorsys.rgb_to_hsv(*cols['BandColour'])
check('olu hat bandi tas / kul grisi (doygunluk %.2f < 0.15)' % band_s, band_s < 0.15, 'band yesil')
m = re.search(r'case CubeKind\.Gangrene: return new Color\(([^)]*)\);', vu)
vu_rot = tuple(float(x) for x in re.findall(r'-?\d*\.?\d+', m.group(1))[:3])
check('tahtanin curumus kup rengi ile efektin rengi AYNI %s' % (vu_rot,),
      max(abs(a - b) for a, b in zip(vu_rot, cols['RotColour'])) < 1e-6, 'iki renk ayrisik')
for name, sh in (('Gangrene', cube_shader), ('GangreneStreak', streak_shader)):
    check('%s shaderi 2B isik etiketi + geri dusus' % name,
          '"LightMode" = "Universal2D"' in sh and 'Fallback "Sprites/Default"' in sh,
          '%s shader' % name)
check('shader yoksa efekt yine calisiyor (geri dusus yollari)',
      view.count('gangreneMaterial == null') >= 2 and 'streakMaterial == null' in view,
      'shader yoksa bos ekran')

print()
print('=== 6. OLCULER ===')
check('bos hucreye yayilma %.0f ms (220-400)' % (style['SpreadDurationEmpty'] * 1000),
      0.22 <= style['SpreadDurationEmpty'] <= 0.40, 'bos hucre suresi')
check('kup donusumu %.0f ms (250-450)' % (style['SpreadDurationOccupied'] * 1000),
      0.25 <= style['SpreadDurationOccupied'] <= 0.45, 'donusum suresi')
check('yeni kutle %.1f px yukselir (1-3)' % (style['SpawnRiseAmount'] * 64),
      1.0 <= style['SpawnRiseAmount'] * 64 <= 3.0, 'yukselme')
check('yuzey cokmesi %.1f px (<= 3) ve sonunda tahtanin olcusune oturuyor'
      % (style['SurfaceCollapseAmount'] * 64),
      style['SurfaceCollapseAmount'] * 64 <= 3.0
      and 'Mathf.Sin(Mathf.PI * Mathf.Pow(p, 0.7f))' in view, 'cokme')
check('bekleme nabzi %.1f-%.1f s (1.5-3.0)'
      % (style['IdlePulsePeriodMin'], style['IdlePulsePeriodMax']),
      1.5 <= style['IdlePulsePeriodMin'] and style['IdlePulsePeriodMax'] <= 3.0
      and style['IdlePulsePeriodMin'] < style['IdlePulsePeriodMax'], 'nabiz periyodu')
check('bekleme nabzi hafif (%%%.0f <= 20) ve desenkron' % (style['IdlePulseStrength'] * 100),
      style['IdlePulseStrength'] <= 0.2 and 'p.Phase' in view, 'nabiz gurultulu')
check('tur sonu nabzi %.0f ms (80-160)' % (style['TurnDamagePulseDuration'] * 1000),
      0.08 <= style['TurnDamagePulseDuration'] <= 0.16, 'tur nabzi')
check('hat supurmesi %.0f ms (280-500)' % (style['DeadLineSweepDuration'] * 1000),
      0.28 <= style['DeadLineSweepDuration'] <= 0.50, 'supurme suresi')
check('zincir duraklamasi %.0f ms (60-140)' % (style['ChainPauseDuration'] * 1000),
      0.06 <= style['ChainPauseDuration'] <= 0.14, 'duraklama')
check('kenar kuplerinde gecikme %.0f ms (dalga, <= 60)' % (style['EdgeConversionStagger'] * 1000),
      0 < style['EdgeConversionStagger'] <= 0.06, 'kenar dalgasi')
check('bagisik tepkisi 40-100 ms', 'Mathf.Lerp(0.04f, 0.10f' in view, 'bagisik tepkisi')
one_cell = style['SpreadDurationOccupied'] + style['TurnStartDelay']
check('bir turun rot suresi %.2f s (<= 0.80)' % one_cell, one_cell <= 0.8, 'tek hucre uzun')
check('buyuk lekede varlik kisiliyor',
      'LargeClusterCompensation' in view and 'IdleScale(presence.Count)' in view,
      'buyuk leke gurultusu')

print()
print('=== 7. LAB ===')
labels = ['kangren: boş hücreye yayılma', 'kangren: dolu küpü dönüştürme', 'kangren: bağışık hedef',
          'kangren: varlık / bekleme', 'kangren: tur sonu hasar nabzı', 'kangren: tam satır ölümü',
          'kangren: tam sütun ölümü', 'kangren: satır ölür, kangren kenara atlar',
          'kangren: zincirleme atlama']
check('9 sahne girisi', all(w in lab_raw for w in labels),
      'labda eksik: %s' % [w for w in labels if w not in lab_raw])
toggles = ['kaynak yönü aç/kapa', 'damar katmanı aç/kapa', 'çatlak katmanı aç/kapa',
           'hücre zemini bulaşması aç/kapa', 'ölü hat bandı aç/kapa', 'kenara aktarım yolu aç/kapa',
           'kenar küplerinin dönüşümü aç/kapa', 'zincir adım işaretleri aç/kapa']
check('8 hata ayiklama anahtari', all(w in lab_raw for w in toggles),
      'anahtar eksik: %s' % [w for w in toggles if w not in lab_raw])
start = view.index('public static class Layers')
end = view.index('\n        }', start)
switches = re.findall(r'public static bool (\w+)', view[start:end])
unread = [s for s in switches if not re.search(r'Layers\.%s\b' % s, view[end:])]
check('%d anahtar, hepsi okunuyor' % len(switches), len(switches) == 8 and not unread,
      'okunmayan anahtar: %s' % unread)
rot = method(lab,
             'private void AnimRotTurn(GameBoard board, AnimRotScene scene, GangreneView.TurnScene turn)')
check('lab GERCEK kurallari kosuyor (yayilma + hat olumu)',
      'board.SpreadGangrene(new SeededRandom' in rot and 'board.InfectFullLines(deaths)' in rot
      and 'board.InfectFullLines(null)' in rot, 'lab kurallari sahneliyor')
check('lab olaylari rapordan kuruyor (kendi uydurmuyor)',
      'spread.Cell' in rot and 'spread.Source' in rot and 'spread.Immune' in rot
      and 'death.EdgeLine' in rot and 'death.Converted[i]' in rot, 'lab olay uyduruyor')
check('lab kendi tahtasinda ve oyunun kapisindan',
      'boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter)' in lab
      and 'PlayGangreneScene(turn);' in lab
      and 'private bool PlayGangreneScene(GangreneView.TurnScene scene)' in fb, 'lab ayri yol')
check('eski sahnelenmis kangren sahnesi kalmadi',
      'AnimBossScene.Gangrene' not in lab and 'AnimRotEdge' not in lab, 'eski sahne duruyor')
close = method(lab, 'private void CloseAnimationLab(')
check('lab kapaninca sahne biter, anahtarlar varsayilana',
      'StopAnimRot();' in close and 'GangreneView.Layers.Defaults();' in close
      and close.index('StopAnimRot();') < close.index('AnimResync();'), 'lab kapanisi')

print()
print('=== 8. YASAM DONGUSU ===')
check('BoardView sahibi, yeniden kurulumda yasiyor, her cizimde senkron',
      'public GangreneView Gangrene' in boardview and 'child == keepRot' in boardview
      and 'Gangrene.Sync(this);' in method(boardview, 'public void Refresh()'), 'sahiplik')
check('yeni tahtada her sey sifirlanir',
      'Stop();' in method(view, 'public void Sync(BoardView owner)'), 'eski tahta izi kalir')
menus = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Menus.cs'))
check('kosu bitince duruyor', 'boardView.StopGangrene()' in menus
      and 'public void StopGangrene()' in boardview, 'kosu sonu')
check('csproj dosyayi iceriyor', 'GangreneView.cs' in read('ProjectBlock.View.csproj'),
      'csproj eksik')

print()
if fail:
    print('SONUC: %d SORUN' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SONUC: temiz')
