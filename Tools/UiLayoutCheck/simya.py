# -*- coding: utf-8 -*-
# SIMYA / IKI ELEMENTLI KART - statik kontrol.
#
# Korudugu UC sey:
# 1) KURAL CORE'DA. Iki elementli kart ayni anda yalnizca BIRI gibi davranir; Has() sadece aktif
#    secime evet der, secim kaydedilir, kopyalar secimi tasir. View kural uretmez, sadece
#    GameSession.ChooseCardElement'i cagirir.
# 2) SECIM KUCUK BIR PANEL. PC'de sag tik, mobilde basili tutma (parmak sol tus oldugu icin tutma
#    bir kaldirma olarak baslar, kipirdamazsa secime doner), pad'de bati tusu - hepsi AYNI panel.
#    Sag tikin eski isleri (cark dondurme, tilki, retro) panelde satir olarak durur.
# 3) KART AKTIF ELEMENTI GIYER. Kup karosu, bant etiketi ve rengi aktif elementten gelir; pasif
#    element karta boyanmaz. Animasyon yok: kart yeniden kurulur.
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


card = strip_comments(read('Assets', 'Scripts', 'Core', 'Cards', 'BlockCard.cs'))
ser = strip_comments(read('Assets', 'Scripts', 'Core', 'Save', 'CoreSerializers.cs'))
sess = strip_comments(read('Assets', 'Scripts', 'Core', 'Game', 'GameSession.cs'))
alch = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Alchemy.cs'))
drag = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.Drag.cs'))
pad = strip_comments(read('Assets', 'Scripts', 'View', 'GameUiController.PadPlay.cs'))
util = strip_comments(read('Assets', 'Scripts', 'View', 'ViewUtil.cs'))
visual = strip_comments(read('Assets', 'Scripts', 'View', 'CardVisual.cs'))
picker = strip_comments(read('Assets', 'Scripts', 'View', 'ChoicePickerView.cs'))

print('1) kural Core\'da')
check('Has() aktif secime bakar', 'ActiveElement' in card and 'element == active.Value' in card,
      'Has aktif elementi yok sayiyor')
check('secim kaydediliyor (".active")', '".active"' in ser, 'secim kayitta yok')
check('oturum secimi aciyor (ChooseCardElement)', 'public bool ChooseCardElement' in sess,
      'ChooseCardElement yok')
check('View secimi Core\'a birakiyor', 'session.ChooseCardElement(' in alch
      and 'ActiveChoice =' not in alch, 'View secimi kendisi yaziyor')

print('2) tek kucuk panel, uc giris')
check('kompakt panel var', 'public void ShowCompact' in picker, 'ShowCompact yok')
check('sag tik once simya panelini acar', 'OpenAlchemyPicker(round, rightHit.SlotIndex' in drag,
      'sag tik baglanmamis')
check('basili tutma kaldirmada kurulur ve suruklemede sayilir',
      'ArmAlchemyHold(' in drag and 'TickAlchemyHold(' in drag, 'basili tutma baglanmamis')
check('kipirdayan basis surukleme kalir', 'AlchemyHoldSlack' in alch, 'kipirdama esigi yok')
check('pad bati tusu ayni paneli acar', 'OpenAlchemyPicker(round, padHandSlot' in pad, 'pad baglanmamis')
check('dondurme / tilki panelde satir', 'AlchemyRowRotate' in alch and 'AlchemyRowFox' in alch,
      'sag tikin eski isleri kayboldu')
check('ters el (Sasirtmaca) secilemez', 'HandIsFaceDown' in alch, 'kapali kartin elementi gorunuyor')

print('3) kart aktif elementi giyer')
check('karo ve renk ShownElements\'ten', util.count('ShownElements(') >= 3, 'karo tum elementlere bakiyor')
check('bant etiketi ShownElements\'ten', 'ViewUtil.ShownElements(card)' in visual, 'bant iki elementi yaziyor')
check('animasyon yok, kart yeniden kurulur', 'cardLayer.ForgetCard(card.Id)' in alch, 'kart yenilenmiyor')

print()
if fail:
    print('SIMYA: %d HATA' % len(fail))
    for f in fail:
        print('  - ' + f)
    sys.exit(1)
print('SIMYA: hepsi tamam')
