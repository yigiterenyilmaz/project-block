# -*- coding: utf-8 -*-
# TUTUSTUR / COMBUSTION SWEEP - statik kontrol.
#
# Korudugu UC sey:
# 1) VIEW HICBIR SEY SECMIYOR. Zincirin yaktigi hucreler, kupleri, tetikleyen ates ve puan Core'un
#    raporu (DestroyCubes'in DONDURDUGU liste, yakilmadan once alinan kupler, turun kaydi, dokumden
#    olculen puan). View tahtada ates aramaz.
# 2) YANMA, PATLAMA DEGIL. Kaynak ates kendi patlamasini oynar; digerleri tukenir: isinma -> alttan
#    duman -> kenardan ice komurlesme -> ice cokus -> kor/kul -> duman kalkinca bos hucre.
# 3) DALGA ALTTAN USTE. Satir basina gecikme, satir icinde kucuk jitter (soldan saga tarama degil),
#    toplam tirmanis sinirli; kalabalikta butceler kuculur, tahta gri sise donmez.
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


joker = method(strip_comments(read('Assets', 'Scripts', 'Core', 'Jokers', 'Definitions', 'ElementJokers.cs')),
               'public sealed class TutusturJoker')
view = strip_comments(read('Assets', 'Scripts', 'View', 'IgnitionBurnView.cs'))
shapes = strip_comments(read('Assets', 'Scripts', 'View', 'IgnitionShapes.cs'))
shader = read('Assets', 'Resources', 'Shaders', 'IgnitionBurn.shader')
ctrl = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Ignition.cs'))
fb = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Feedback.cs'))
rebate = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Rebate.cs'))
lab = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.AnimationLab.cs'))
tests = read('Tools', 'CoreTests', 'JokerTests.cs')

print('=== 1. CORE SECIYOR ===')
check('LastIgnition [NotSaved]', re.search(r'\[field: NotSaved\]\s*public IgnitionVisuals LastIgnition', joker) is not None,
      'rapor kayda giriyor')
check('kupler YAKILMADAN once alinir, hucreler DestroyCubes donusu',
      joker.index('before[fire[i]] = cube.Value;') < joker.index('DestroyCubes(fire, true)')
      and 'report.Cells.Add(burned[i]);' in joker, 'yanlis hucre / yuz yok')
check('tetikleyen ates turun kaydindan', 'report.SourceCells.Add(log[i].Pos);' in joker, 'kaynak uydurma')
check('puan dokumden olculur (Genel temizlik yoksa 0)',
      '(turn.Score.FlatBonus + turn.Score.LateFlat - paidBefore) * turn.Score.ScoreScale' in joker, 'puan kopya')
check('View tahtada ates aramaz / puan hesaplamaz', 'CellsOfKind' not in view + ctrl and 'PointsPerChainedCube' not in view
      and 'SourceCardId' not in view, 'View zinciri yeniden hesapliyor')
check('test: rapor gercek zincirle dogrulaniyor', 'the report names exactly the fire the chain took' in tests
      and 'with the points measured off the breakdown' in tests, 'rapor dogrulanmamis')

print()
print('=== 2. YANMA, PATLAMA DEGIL ===')
check('patlama dili YOK (FlashCells / ShakeCamera / flas)',
      'FlashCells' not in view + ctrl and 'ShakeCamera' not in view + ctrl and 'flash' not in view.lower(),
      'normal patlama oynuyor')
check('vekiller yenilemede tutulur, zincir patlama tepesinde baslar',
      'SyncIgnition();' in method(fb, 'private void RefreshAll(')
      and 'PlayIgnition();' in method(fb, 'private void PlayExplosionFeedback(')
      and 'LineSweepView.Style.PropagationSeconds + LineSweepView.Style.PeakHold' in ctrl,
      'ates patlamadan once / sonra kayboluyor')
between('aktivasyon halkasi yaricapi (hucre)', num(view, 'ActivationRadius'), 1, 2, 'aktivasyon cok buyuk')
between('isinma (sn)', num(view, 'Heat'), 0.06, 0.09, 'isinma')
between('isinma olcegi', num(view, 'HeatScale'), 1.02, 1.03, 'bounce')
check('yanma MALZEME (shader: isi + maske esigi + kor cephesi), alfa degil',
      '_Char' in shader and '_BurnMask' in shader and 'front' in shader and 'charred' in shader,
      'kup sadece soluyor')
check('maske nesne uzayinda (paketli karoda da dogru)', 'output.local = input.positionOS.xy;' in shader,
      'maske sprite UV ile kayar')
check('maske kenardan ice (merkez en son), duz radyal degil',
      'n * 0.55f + edge * 0.45f' in shapes, 'kusursuz radyal maske')
check('shader yoksa kup yine komure doner', 'Color.Lerp(tint, Style.Charcoal, charShown)' in view, 'fallback yok')
check('cokus: X 0.55-0.70, Y 0.40-0.50', 0.55 <= num(view, 'CollapseX') <= 0.70 and 0.40 <= num(view, 'CollapseY') <= 0.50,
      'cokus olcekleri')
check('cokus alfa 1 -> 0.8 -> 0.35 -> 0', 'Piece(collapse, 1f, 0.8f, 0.35f, 0f)' in view, 'alfa egrisi')
check('duman kupun ALTINDAN dogar (-0.42 kup)', 'float y = -0.42f * cube' in view, 'duman merkezde daire')
check('duman dar dogar, tirmandikca genisler', 'Mathf.Lerp(0.65f, 1f, eased)' in view, 'huni yok')
between('duman baslangic opaklik', num(view, 'SmokeAlphaStart'), 0.30, 0.45, 'duman')
between('duman tepe opaklik', num(view, 'SmokeAlphaPeak'), 0.55, 0.70, 'duman')
between('duman yukselme (sn)', num(view, 'SmokeRise'), 0.12, 0.18, 'duman')
between('duman yukselme (hucre)', num(view, 'SmokeRiseCells'), 0.5, 0.8, 'duman')
check('duman sicak-komur renk (saf gri degil)', 'SmokeWarm = new Color(0.58f, 0.32f, 0.16f)' in view, 'gri duman')
check('duman kupten SONRA kalir (tutma), sonra kalkar', num(view, 'SmokeHold') >= 0.06
      and 'float clearAt = Style.CollapseFrom + Style.Collapse + Style.SmokeHold;' in view, 'bos hucre dumansiz gorunur')
between('duman kalkis (sn)', num(view, 'SmokeClear'), 0.12, 0.22, 'kalkis')
check('kor yukari + yana, kul daha yavas', 'rng.Range(0.8f, 1.4f)' in view and 'rng.Range(0.35f, 0.6f)' in view, 'parcacik')
check('kor/kul cokusun son %40`inda', 'collapse >= 0.6f' in view, 'zamanlama')
check('alev dili kucuk ve kenarda (alta degil)', 'never under it' in read('Assets', 'Scripts', 'View', 'IgnitionBurnView.cs'),
      'dev alev')

print()
print('=== 3. ALTTAN USTE DALGA ===')
between('satir gecikmesi (sn)', num(view, 'RowDelay'), 0.035, 0.065, 'dalga hizi')
between('satir ici jitter (sn)', num(view, 'SameRowJitter'), 0.010, 0.025, 'jitter')
check('toplam tirmanis <= 0.35 sn', num(view, 'MaxPropagation') <= 0.35
      and 'Mathf.Min(Style.RowDelay, Style.MaxPropagation / span)' in view, 'dalga uzun')
check('baslangic SATIRA gore (sutuna gore degil)', 't.Start = Style.WaveLead + t.Row * rowDelay' in view,
      'soldan saga tarama')
check('butceler: duman <= 40, kor <= 30, kul <= 24',
      num(view, 'MaxSmoke') <= 40 and num(view, 'MaxEmbers') <= 30 and num(view, 'MaxAsh') <= 24, 'limit')
check('kalabalikta puf sayisi duser (4 / 3 / 2)', 'int puffs = n <= 4 ? 4 : n <= 12 ? 3 : 2;' in view, 'gri sis')
check('sesler satir basina, kup basina degil', 'SoundWaveRow + target.Row' in view and 'target.Row % 2 == 0' in view,
      'ses spami')
check('tek dokunsal (dalga tepesi)', 'Say(Haptic, HapticPeak)' in view, 'dokunsal spam')
check('sicak bant opsiyonel ve varsayilan KAPALI', 'public static bool ShowGlobalHazeBand;' in view, 'bant zorunlu')
check('havuz: yerel konum sifirlanir, materyal geri verilir',
      'r.transform.localPosition = Vector3.zero;' in method(view, 'private SpriteRenderer Rent(')
      and 'r.sharedMaterial = DefaultMaterial;' in view, 'havuz sizintisi')
check('1-3 hedef kendi puani, 4+ tek toplam (%70 yandiginda)', 'if (n <= 3)' in view and 'n * 0.7f' in view,
      'puan spami')
check('skor tek yazardan', 'ignition.ScoreScale' in rebate, 'skor ezilir')
check('debug yalnizca editor / debug build', 'Application.isEditor || Debug.isDebugBuild' in view, 'debug')

print()
print('=== 4. LAB ===')
n = len(re.findall(r'AddAnim\("tutuştur: ', lab))
check('en az 19 giris (%d)' % n, n >= 19, 'lab eksik')
check('9 debug anahtari', all(('IgnitionBurnView.Layers.' + f) in lab for f in [
    'ShowIgnitionTargets', 'ShowRowBuckets', 'ShowWaveStartTimes', 'ShowSmokeBounds', 'ShowBurnMask',
    'ShowAsh', 'ShowEmberDebug', 'ShowProxyDebug', 'ShowGlobalHazeBand']), 'debug eksik')
sc = method(lab, 'private System.Collections.IEnumerator AnimIgnitionScenarioRoutine(')
order = [sc.find('board.SetCubeAt(gap'), sc.find('ignition.Prepare(report);'), sc.find('FlashLine(board'),
         sc.find('ignition.Begin(report, IgnitionPeak());')]
check('TAM SENARYO oyunun sirasiyla (blok -> hazirla -> satir patlamasi -> tepede zincir)',
      min(order) >= 0 and order == sorted(order), 'senaryo sirasi')
check('RESET durdurur ve katmanlari acar', 'StopIgnition();' in method(lab, 'private void AnimResync()')
      and 'IgnitionBurnView.Layers.AllOn();' in method(lab, 'private void AnimResync()'), 'RESET')

print()
if fail:
    print('HATALAR:')
    for f in fail:
        print('  -', f)
    sys.exit(1)
print('tutustur: hepsi tamam')
