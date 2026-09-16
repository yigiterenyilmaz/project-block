# -*- coding: utf-8 -*-
# TASKIN / OVERFLOW FRONT - statik kontrol.
#
# Korudugu UC sey:
# 1) VIEW HICBIR SEY SECMIYOR. Kaynaklar, hedefler, her hedefin eski kupu ve hangi kaynaklardan su
#    geldigi SpreadVisuals'in (Core, SpreadOn). View komsuluk kuralini yeniden yazmaz, ikinci halka
#    uydurmaz, eski yuzu tahtadan okumaz (tahta coktan su olarak boyandi).
# 2) BOYAMA DEGIL, BOGMA. Kaynak basinc toplar, HEDEFE BAKAN kenarda kabarir, su dili sinirdan tasar,
#    hedef o kenardan islanir, film gelis yonunden yayilir, alttaki kup kirilir / renk ve kontrast
#    kaybeder / akar, gercek su altinda belirir, kirik halkalarla oturur.
# 3) TEK HEDEF, TEK DONUSUM. Birden cok kaynaktan gelen su tek maskede birden cok film; iki ayri
#    donusum ust uste binmez.
from __future__ import print_function
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
fail = []


def check(label, ok, why):
    print('   %-74s : %s' % (label, 'evet' if ok else 'HAYIR - HATA'))
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


def num(src, name, default=-999.0):
    m = re.search(r'\b' + name + r'\s*=\s*(-?[\d.]+)f?;', src)
    return float(m.group(1)) if m else default


def between(label, v, lo, hi, why):
    check('%s %.3f in [%.3f, %.3f]' % (label, v, lo, hi), lo <= v <= hi, why)


view = strip_comments(read('Assets', 'Scripts', 'View', 'FloodView.cs'))
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'FloodShapes.cs'))
shader = read('Assets', 'Resources', 'Shaders', 'FloodFilm.shader')
ctrl = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Flood.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
fire = strip_comments(read('Assets', 'Scripts', 'View', 'FireSpreadView.cs'))
lab = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs'))

print('=== 1. CORE SECIYOR ===')
check('rapor Core`un SpreadVisuals`i; yalniz SU oynanir, ates ayri gorunumde',
      "r.Kind != CubeKind.Water" in view and 'report.Kind != CubeKind.Fire' in fire, 'yanlis rapor')
check('View komsuluk / tahta taramasi yapmiyor', 'Neighbours' not in view and 'CellsOfKind' not in view
      and 'SpreadOn' not in view, 'kural yeniden yazilmis')
check('yalniz RAPORDAKI kaynaklar tasar', 'if (!byCell.TryGetValue(from, out s))' in view, 'uydurma kaynak')
check('gelis yonu raporun From listesinden', 'foreach (GridPos from in entry.From)' in view, 'yon tahmini')
check('eski yuz RAPORDAN (tahtadan degil - tahta zaten su)', 'boardView.CubeFace(cube, out tile, out colour);' in ctrl
      and 'TryCubeLook' not in ctrl, 'eski yuz yerine su gorunur')
check('kimlikle eslenir; RESET oynatilmis sayar', 'ReferenceEquals(r, lastPlayed)' in view and 'flood.MarkPlayed(' in ctrl,
      'tekrar / atlama')
check('tahta bekletilir (HoldCells) ve birakilir', 'Hold(held)' in view and 'Release(held)' in view, 'su ustune cizim')
check('puan / flas / sarsinti yok', 'ScoreScale' not in view and 'ShakeCamera' not in view + ctrl and 'flash' not in view.lower(),
      'odul dili')

print()
print('=== 2. BOYAMA DEGIL, BOGMA ===')
between('kaynak basinci (sn)', num(view, 'Pressure'), 0.09, 0.14, 'basinc')
between('kaynak parlakligi', num(view, 'PressureBright'), 0.06, 0.12, 'parlaklik')
check('kenar vurgusu YALNIZ hedefe bakan kenarda', 'sp.Highlight.transform.position = src + sp.Dir * (cube * 0.47f);' in view,
      'tum kenar parliyor')
between('kabarma (px)', num(view, 'SwellPx'), 3, 8, 'kabarma')
between('kabarma (sn)', num(view, 'Swell'), 0.07, 0.11, 'kabarma')
check('kabarma sprite`in tamami degil, kenarda yerel', 'sp.Swell.transform.position = src + sp.Dir' in view,
      'tum kup buyuyor')
between('su dili genisligi (hucre)', num(view, 'TongueWidth'), 0.14, 0.28, 'dil')
between('su dili (sn)', num(view, 'Tongue'), 0.09, 0.15, 'dil')
check('su dili: genis kok, daralan govde, damla uc (isin degil)',
      'Mathf.Lerp(0.48f, 0.26f' in method(shapes, 'public static Sprite Tongue') and 'tipR' in shapes,
      'lazer / dikdortgen')
check('dil ease-out + kisa oturma', '1f - (1f - grow) * (1f - grow)' in view and 'settle' in method(view, 'private void PaintSpill('),
      'dil hareketi')
check('temasta 2-3 damla, fiskiye yok', num(view, 'Contact') <= 0.09 and 'int contactDrops = n <= 4 ? 3' in view,
      'sicrama festivali')
between('film suresi (sn)', num(view, 'Film'), 0.16, 0.24, 'film')
check('film KENAR BASINA ilerleme (L R B T), tek maske, max ile birlesir',
      'Side(_Film.x' in shader and 'Side(_Film.w' in shader and 'cov = max(cov, c);' in shader,
      'filmler ust uste')
check('film cephesi dalgali (duz silme degil)', 'sin(along * 9.0' in shader, 'duz silme')
check('cephe cizgisi, baska yondeki su zaten ortunce soner', 'front *= 1.0 - inside;' in shader, 'cepheler cakisir')
check('film altinda kup ONCE renk, SONRA kontrast, SONRA su rengi (duz mavi tint degil)',
      0 <= shader.find('lerp(tex.rgb, lum.xxx') < shader.find('(col - 0.45)') < shader.find('lerp(col, _Water.rgb, filmA)'),
      'mavi boya')
check('film opakligi 0.22 -> 0.55 (alt kup gorunur)', 'lerp(0.22, 0.55, _Submerge)' in shader, 'opak mavi silme')
check('kirilma hafif (~1-1.5 px) ve yalniz film altinda', 'cov * _Px * 1.2' in shader, 'jel dalgalanmasi')
check('sivilasma birkac px asagi/yana, yalniz film altinda', '_Liquefy * cov * _Px * 4.0' in shader, 'mum erimesi')
check('maske nesne uzayinda (paketli karo)', 'output.local = input.positionOS.xy;' in shader, 'UV kaymasi')
check('gercek SU altta belirir (su karosu + kendi warp materyali), eski yuz ustte soner',
      'ViewUtil.TileMaterial(waterTile)' in view and 'WaterOrder = 5' in view and 'ProxyOrder = 6' in view,
      'duz crossfade / sahte su')
between('su belirmesi (sn)', num(view, 'Identity'), 0.10, 0.16, 'gecis')
check('cozulurken kup 1 -> 0.985 -> 1 (squash yok)', '1f - 0.015f * Mathf.Sin(liq * Mathf.PI)' in view, 'squash')
between('halka (sn)', num(view, 'Ripple'), 0.12, 0.19, 'halka')
between('halka opakligi', num(view, 'RippleAlpha'), 0.12, 0.2, 'halka')
check('halka KIRIK oval (parlak hedef halkasi degil)', 'broken' in method(shapes, 'public static Sprite Ripple('), 'mukemmel halka')
check('halka yaricapi hucrenin %20 -> %55', 'Mathf.Lerp(0.20f, debugRipple ? 0.9f : 0.55f' in view, 'halka boyu')
check('kaynak geri toplanir (rebound)', num(view, 'Rebound') >= 0.10 and '(1f - rebound)' in view, 'kabarma kalir')
check('shader yoksa eski kup yine suyun altinda soner', 'FloodFilm shader missing' in read('Assets', 'Scripts', 'View', 'FloodView.cs'),
      'fallback yok')

print()
print('=== 3. KALABALIK, KANCALAR, SIRA ===')
between('kaynak gecikmesi (sn)', num(view, 'SourceStagger'), 0.02, 0.035, 'gecikme')
check('damlacik tavani <= 24', num(view, 'MaxDroplets') <= 24, 'damla yagmuru')
check('LOD: 1-4 tam, 5-10 azaltilmis, 11+ yalniz kahraman damla', 'int settleDrops = n <= 4 ? 3 : n <= 10 ? 2 : 1;' in view,
      'LOD yok')
check('ses: basinc, tasma, temas, donusum, oturma; ayni sesten en cok 4 ses', all(('Sound' + k) in view for k in
      ['Trigger', 'SourcePressure', 'Spill', 'Contact', 'Convert', 'Settle']) and 'MaxVoices = 4' in view, 'ses spami')
check('havuz: yerel konum sifirlanir, materyal geri verilir',
      'r.transform.localPosition = Vector3.zero;' in method(view, 'private SpriteRenderer Rent(')
      and 'r.sharedMaterial = DefaultMaterial;' in view, 'havuz sizintisi')
check('tahta yenilemesinde, yangin yayilmasinin yaninda', 'SyncFlood();' in method(fb, 'private void RefreshAll('),
      'tetiklenmiyor')
check('debug yalnizca editor / debug build', 'Application.isEditor || Debug.isDebugBuild' in view, 'debug')

print()
print('=== 4. LAB ===')
n = len(re.findall(r'AddAnim\("taşkın: ', lab))
check('en az 27 giris (%d)' % n, n >= 27, 'lab eksik')
check('11 debug anahtari', all(('FloodView.Layers.' + f) in lab for f in [
    'ShowFloodSources', 'ShowFloodTargets', 'ShowIncomingDirections', 'ShowLiquidTongues', 'ShowWaterFilmMask',
    'ShowRefraction', 'ShowOldCubeProxy', 'ShowWaterRenderer', 'ShowRipple', 'ShowDroplets', 'ShowSourceStagger']),
      'debug eksik')
check('lab GERCEK kurali calistirir (SpreadOn, su)', 'SpreadJoker.SpreadOn(board, CubeKind.Water' in lab, 'lab kopya')
check('su dusmesi -> sonra taskin sirasi sahnesi', 'board.SettleWaterAndReact(frames);' in lab
      and 'boardView.PlayWaterAnimation(frames' in lab, 'sira testi yok')
check('RESET durdurur ve katmanlari acar', 'StopFlood();' in method(lab, 'private void AnimResync()')
      and 'FloodView.Layers.AllOn();' in method(lab, 'private void AnimResync()'), 'RESET')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('taskin: hepsi tamam')
