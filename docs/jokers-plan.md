# Joker Planı — sınıflandırma, merkezi kurallar, açık sorular

2026-07-18 tarihinde paylaşılan 31 jokerlik tasarım listesinin mevcut Core motoruna karşı analizi.
Her jokerin hangi mevcut kancaya oturduğu, motora ne eklenmesi gerektiği ve cevaplanması gereken
tasarım soruları aşağıdadır. Koda dair tüm iddialar dosyalara karşı doğrulanmıştır.

Sayılar (streak eşikleri, bonus miktarları, üst sınırlar) placeholder — hepsi joker sınıflarında
public alan, serbestçe ayarlanabilir.

## 0. Durum — ne yapıldı

**Framework kuruldu** (`Assets/Scripts/Core/Jokers/`) ve üç merkezi kural koda geçti:
envanter sırası, tek merkezi temizlik olayı, merkezi uzatma kapatması. Ayrıntı CLAUDE.md'de.

**1. dalga yazıldı ve test edildi (11 joker):** Renovasyon, İade, Insider, Çığ, Dondurma,
Siyam, Bereket, Harcama Bonusu, Domuz Kumbarası, Cimri Kumbara, Altın Kumbara.
Kumbaralar bugün değer biriktiriyor; satış `JokerInventory.Sell` ile çalışıyor, market
geldiğinde sadece oradan çağrılacak.

**Motora eklenenler:** `ITurnHooks` (AfterLineExplosion / AfterCleanSweep / ModifyScore /
AfterTurnScored), `ScoreBreakdown`, `TryResolveCleanSweep`, `ReplaceHandCard` (İade),
`DestroyCubes` + `GameBoard.DestroyCube` / `GetOccupiedCells` (2. dalga için hazır),
`CycleHandWithoutReshuffle` (Seri tetik için hazır), `TurnReport.Score` ve
`TurnReport.DrawPileEmptiedThisTurn`, `RoundRules.RevealTopDrawCard`.

**Doğrulama:** `Tools/CoreTests` — 72 assert geçiyor (60 fuzzlanmış koşu dahil), ve eski
motorla yeni motorun temel oyun izleri 24 senaryoda **birebir aynı** (davranış değişmedi).

**Sıradaki bloklayıcı kararlar:** aşağıdaki 2. bölümdeki hükümlerden 1. dalgayı
ilgilendirenler uygulandı; 2. dalga (Kayıt defteri, Buldozer, Robot süpürge, Kentsel
Dönüşüm, Batak, Seri tetik, Kazı çalışması) için 2.1–2.4 ve 2.11–2.13 maddelerinin
onaylanması gerekiyor.

### 0.1 enes dalıyla birleşme (2026-07-18)

Joker katmanı `origin/enes` üzerine taşındı. Alınan kararlar:

- **Uzatma kuralı enes'in sürümü.** Devam etmenin bedeli var: el desteye karışır, artan
  sayıda kart raunt dışına çıkar (`RoundRules.CardsRemovedPerContinue` +
  `ContinueCostEscalation`). Benim "her temizlikte N kart" sürümüm kaldırıldı.
- **Renovasyon uzatmada kapalı.** `RedrawHand` ıskartayı her zaman desteye karıştırıyor;
  uzatmada başka hiçbir şey karıştırmadığı için bu bedava deste yenilemesi olurdu ve
  deste-bitti kaybını tamamen delerdi. İade açık kaldı: normal çekme kuralından geçtiği
  için uzatmada boş deste yine kayıp. **Açık soru:** Renovasyon uzatmada bambaşka bir
  bedelle (ör. karıştırmadan, sadece destenin üstünden) çalışsın ister misin?
- **Domuz Kumbarası** joker olarak kaldı; blok elementi sürümünü enes kaldırıyor.
- **midas** artık yazılabilir: altın blok tahtada durdukça puan veriyor
  (`IScoreCalculator.ScoreGoldBonus`), midas bunu **elde** (bonus el dahil) tutmaya da
  genişletecek.
- **Temizlik koşulu biraz sıkılaştı:** "bir satır patladı" yerine "bu tur gerçekten küp
  yok edildi" şartı geldi. Sadece kırılmaz küplerden oluşan bir satır artık her turda
  yeniden temizlik tetiklemiyor — obsidyen/altın sahadayken oluşan farm açığı kapandı.

Doğrulama: enes'in motorunun oyun izi joker katmanı eklendikten sonra **birebir aynı**
(24 senaryo), 73 assert geçiyor, View+Core Unity assembly'lerine karşı temiz derleniyor.

## 1. Özet tablo

| Joker | Zorluk | Bekleyen alt sistem |
|---|---|---|
| Altın Kumbara | S | market, satış |
| Cimri Kumbara | S | market, satış |
| Domuz Kumbarası | S | market, satış |
| Insider | S | — |
| Renovasyon | S | — |
| bereket | S | — |
| Buldozer | M | — |
| Harcama bonusu | M | — |
| Kazı çalışması | M | — |
| Seri tetik | M | — |
| Siyam | M | — |
| dondurma | M | — |
| çığ | M | — |
| İade | M | — |
| Batak | L | bahis |
| Enfeksiyon | L | güçler |
| Kara delik | L | element küpleri |
| Kayıt defteri | L | — |
| Kentsel Dönüşüm | L | — |
| Robot süpürge | L | — |
| Buzluk | XL | element küpleri |
| Damlaya Damlaya Göl Olur | XL | market |
| Parazit | XL | market, karta takma |
| Powerbank | XL | güçler |
| Simya | XL | element küpleri, market |
| Taşkın | XL | element küpleri |
| Tutuştur | XL | element küpleri |
| Yangın | XL | element küpleri |
| elmas kazma | XL | element küpleri |
| ihale | XL | market, satış |
| midas | XL | element küpleri |

- **S** — mevcut kancalarla yazılır, motora dokunmaz
- **M** — küçük motor eklemesi (yeni TurnReport alanı, küçük primitif)
- **L** — yeni motor primitifi veya bir çekirdek kuralın genelleştirilmesi
- **XL** — önce bir alt sistem gerekiyor (element küpleri / market / güçler)

## 2. Merkezi kural kararları

Bu maddeler tek tek joker meselesi değil; birden çok jokerin aynı kuralı farklı yönlere çektiği yerler.
Framework yazılmadan önce karara bağlanmaları gerekiyor. Her birinde önerilen hüküm yazılı —
onaylaman ya da değiştirmen yeterli.

### 2.1 Robot süpürge, Kayıt defteri, Buldozer, Buzluk, Kara delik, Altın Kumbara, Batak, elmas kazma

**Çakışma:** The engine detects a clean sweep only inside RoundEngine.ResolvePlacement, immediately after a same-turn line explosion (step 3, Board.IsCleanForSweep). Robot süpürge (last-cube post-turn explosion) and Kayıt defteri (counter == board size) trigger a sweep OUTSIDE that window; Buldozer empties the board but explicitly must not count; and sweep-exempt cubes (Buzluk ice, later obsidian/gold) make IsCleanForSweep return true while cubes remain, so with only ice/obsidian on the board EVERY later line explosion re-detects a 'sweep' on an already-clean board — a farmable exploit the current explosion-required check does not actually prevent. At least five jokers plus the overtime reward/price consume this event, so its definition must be single-sourced.

**Önerilen hüküm:** Temizlik tek bir merkezi olay olsun ve iki koşula bağlansın: (a) alan bu tur 'temiz değil' durumundan 'temiz' durumuna geçmiş olmalı (yani en az 1 sayılan küp patlamış VE tur başında alan zaten temiz sayılmıyor olmalı — sadece buz/obsidyen kalınca her patlamada tekrar temizlik tetiklenmesin), (b) yerleştirme patlaması dışında yalnızca 'temizlik tetikler' diye tanımlanan efektler (Robot süpürgenin son küpü, Kayıt defteri sayacı) bu olayı yükseltebilsin; Buldozer asla yükseltemesin. Tur başına en fazla 1 temizlik. Kara delik, Altın Kumbara, Batak, elmas kazma ve uzatma temizlik ödül/bedeli (ıskarta karıştır + N kart çıkar + ilerleme teklifi) her zaman bu TEK olayı dinlesin — joker tetiklemeli temizlikler dahil, uzatmadaysa aynı ödül/bedel aynen işlesin.

### 2.2 Kayıt defteri

**Çakışma:** Kayıt defteri is 'unusable in overtime', but it has TWO effects: the counter-sweep trigger AND the passive redefinition ('leaving no cubes on the board no longer counts as temizlik'). If the redefinition stays active in overtime, clean sweeps become impossible — and per DrawWithRules the ONLY way to recycle the discard after the threshold is a clean sweep, so holding this joker would guarantee a DrawPileEmptyAfterThreshold loss. Also unspecified whether the counter freezes or resets in overtime.

**Önerilen hüküm:** Uzatmada Kayıt defteri TAMAMEN pasif olsun: sayaç donsun (sıfırlanmasın, eşiğe dönüş yok zaten raunt bitince sıfırlanır), 'alanı boşaltmak temizlik sayılmaz' kuralı da kalksın ve normal temizlik tanımı geri gelsin. Aksi halde uzatmada ıskarta asla desteye dönemez ve kayıp garantiye girer; joker bir 'ceza kartına' dönüşür.

### 2.3 Kayıt defteri, Kara delik, Altın Kumbara, Batak, Buzluk, elmas kazma

**Çakışma:** While Kayıt defteri is held (pre-threshold), emptying the board is not temizlik and only the counter triggers one. It is undefined whether other temizlik-consuming jokers follow this redefinition or keep their own: does Kara delik still get a void block when the board is emptied normally? Does Buzluk's 'only ice left counts as clean' still apply? Can Batak's bet only be won via the counter?

**Önerilen hüküm:** Temizlik tanımı herkes için ortak olsun: Kayıt defteri varken eşik öncesi TEK temizlik kaynağı sayaçtır ve Kara delik, Altın Kumbara, Batak, Buzluk, elmas kazma kendi temizlik tanımlarını tutmayıp merkezi olayı dinler. Sonuçlar: alanı normal yolla boşaltmak hiçbir jokeri tetiklemez; Batak beti yalnızca sayaç temizliğiyle kapanır (bunu Batak metnine açıkça yaz — bu kombinasyon bilinçli bir risk/sinerji kararı olur).

### 2.4 Kayıt defteri, Buldozer, Robot süpürge, bereket, Batak, Kara delik, Enfeksiyon, Tutuştur, elmas kazma

**Çakışma:** Multiple explosion sources exist but 'patlatılan küp' is undefined per source: line explosions score via ScoreLineExplosion; Buldozer is explicitly scoreless and non-sweep; Robot süpürge, Enfeksiyon, Kara delik's void-crush, Tutuştur chains and elmas kazma obsidian pops are unspecified for scoring, for the Kayıt defteri counter, and for sweep eligibility. Without one rule, every joker pair needs an ad-hoc decision (e.g. does Buldozer feed the Kayıt defteri counter to a free sweep?).

**Önerilen hüküm:** Her patlama olayına üç bayrak tanımla: (1) puan verir mi, (2) sayaçlara işler mi (Kayıt defteri), (3) temizlik tetikleyebilir mi. Standart tablo: sıra/sütun patlaması = evet/evet/evet; joker-güç patlamaları (Robot süpürge, Enfeksiyon, Kara delik boşluk ezmesi, Tutuştur zinciri) = puan YOK / sayaca işler / temizlik tetikleyebilir; elmas kazma obsidyenleri = puan verir / sayaca işler / ikinci bir temizlik tetiklemez; Buldozer = hayır/hayır/hayır (sayaca da işlemez, yoksa bedava sayaç temizliği olur). bereket'in '+ patlaması' = aynı patlama olayında en az 1 satır VE 1 sütunun birlikte patlaması olarak tanımlansın.

### 2.5 elmas kazma, Kara delik, Altın Kumbara, Batak, Domuz Kumbarası

**Çakışma:** Several effects fire on the same temizlik event, plus the overtime price (reshuffle, remove N cards, advance offer). Order changes outcomes: do elmas kazma's obsidian points count into Batak's bet-to-sweep sum and into the turn score before the advance offer? Does Kara delik's void card go to the discard before or after the overtime reshuffle (before = it gets shuffled into the draw pile immediately; after = it waits in the discard)?

**Önerilen hüküm:** Temizlik anında sabit çözüm sırası koy: 1) temizlik bonusu puanlanır, 2) elmas kazma obsidyenleri patlatır ve puanlar (sayaca işler, yeni temizlik tetiklemez), 3) Batak ödemesi hesaplanır (bu turun TÜM puanları, obsidyen dahil, hesaba girer), 4) diğer dinleyiciler: Kara delik boşluk kartını ıskartaya ekler, Altın Kumbara değer kazanır, 5) uzatmadaysa en son: ıskarta karıştırılır + N kart çıkar + ilerleme teklifi (yani boşluk kartı karıştırmadan ÖNCE ıskartada olduğu için desteye girer — bilinçli ödül). Aynı adımda birden çok joker varsa envanter sırası (soldan sağa) geçerli.

### 2.6 çığ, dondurma, Siyam

**Çakışma:** Same shape implies same cube count, so a Siyam-advancing turn can never advance çığ (bigger) or dondurma (smaller) — the three are per-turn mutually exclusive but not per-inventory exclusive. Undefined: whether an equal-size placement RESETS or merely pauses çığ/dondurma streaks, what 'size' means, and whether Siyam's 'same shape' treats rotations/mirrors as equal (BlockShape.CanonicalKey exists and does NOT rotate).

**Önerilen hüküm:** Üçü birlikte tutulabilsin, her joker KENDİ streak'ini bağımsız saysın (envanterde birbirini dışlamak yok, zaten aynı turda en fazla biri ilerler). Boyut = küp sayısı (Shape.Size). Eşit boyutlu yerleştirme çığ ve dondurma streak'lerini SIFIRLAR (duraklatmaz) — yoksa Siyam turları bedava koruma olur. Siyam için 'aynı şekil' = normalize şeklin birebir aynısı (CanonicalKey eşitliği); döndürülmüş/aynalanmış hali AYNI SAYILMAZ (temel oyunda rotasyon yok). Bonus elden oynanan kart da bir tur olduğu için üç streak'e de dahildir.

### 2.7 bereket, çığ, dondurma, Siyam, Damlaya Damlaya Göl Olur, Batak

**Çakışma:** No defined stacking/ordering for score bonuses: bereket permanently raises turn score, streak jokers add conditional bonuses, Damlaya adds a next-round bonus, and Batak pays a multiple of points earned between bet and sweep. Unresolved: do bonus points count toward the threshold (RoundScore) and TotalScore/market currency; does Batak's payout compound on the other bonuses; can Batak's payout feed itself.

**Önerilen hüküm:** Tek puan boru hattı tanımla: 1) taban skor (bereket'in kalıcı artışı doğrudan ScoringConfig değerlerine işlenir), 2) koşullu tur bonusları (çığ/dondurma/Siyam/Damlaya) TOPLAMA olarak eklenir, çarpan yok, 3) tur puanının tamamı hem RoundScore'a (eşiğe) hem TotalScore'a (market parasına) sayılır — bonus puan 'ikinci sınıf' değildir, 4) Batak ödemesi bet-temizlik arasında birikmiş NİHAİ puanlar (tüm joker bonusları dahil) üzerinden temizlik anında hesaplanır, üste eklenir; ödemenin kendisi yeni bir Batak hesabına girmez ama eşiğe ve TotalScore'a sayılır. Damlaya bonusu 'sonraki raundun ilk turunda' tek seferde verilsin.

### 2.8 Seri tetik

**Çakışma:** 'Not valid in overtime' is ambiguous against the engine's order: the threshold check runs AFTER the hand refill inside ResolvePlacement, so on the threshold-passing turn it is unclear whether Seri tetik's end-of-turn discard-and-redraw still fires; also undefined what happens to the +2 extra cards in hand, and whether overtime means ThresholdPassed or 'player declined to advance'. If the redraw ran in overtime it would burn the non-recycling draw pile straight into a DrawPileEmptyAfterThreshold loss.

**Önerilen hüküm:** Seri tetik ThresholdPassed olduğu ANDA kapansın: eşiği geçiren turun tur-sonu 'ıskartala + yeniden çek' adımı YAPILMAZ, el boyutu tabana (RoundRules.HandSize eski değeri) döner, eldeki fazla kartlar zorla atılmaz — el doğal yolla küçülür. Uzatma tanımı: ThresholdPassed = uzatma; ilerleme teklifini reddedip devam etmek jokeri geri açmaz, raunt boyunca kapalı kalır. (Ayrıca marketten alınırken deste < el+2 ise ilk dolumda anında kayıp riski var — market bu durumda satmasın.)

### 2.9 Renovasyon, İade

**Çakışma:** RoundEngine.RedrawHand — explicitly documented as the Renovasyon primitive — unconditionally calls Deck.ShuffleDiscardIntoDraw, but the overtime rule states the discard is NOT recycled anymore (only clean sweeps recycle it; DrawWithRules enforces this for draws). Using a redraw right in overtime would smuggle the discard back into the draw pile and gut the overtime pressure. İade has the same question plus: what happens when the draw pile has no replacement card.

**Önerilen hüküm:** Uzatmada yeniden çekme hakları (Renovasyon, İade) kullanılabilir ama ıskartayı GERİ KARIŞTIRMAZ: kartlar yalnızca mevcut çekme destesinden gelir; deste yetmezse normal uzatma kuralı işler (DrawPileEmptyAfterThreshold kaybı) — yani uzatmada bu hakları kullanmak bilinçli bir kumardır. Eşik öncesi bugünkü RedrawHand davranışı (önce ıskartayı karıştır) aynen kalsın. İade'de sadece değiştirilen kart ıskartaya gider, yenisi destenin üstünden gelir; RedrawHand bu iki joker için iki ayrı primitife bölünmeli (tam el / tek kart).

### 2.10 Kara delik

**Çakışma:** The void block is added 'to the discard for that turn', but RoundDeck is rebuilt from GameSession.OwnedCards every round and card ids come from GameSession.nextCardId. Undefined: whether void blocks persist across rounds (join OwnedCards) or are round-temporary; whether they count for the deck-exhaustion loss rules (HandCannotBeRefilled, DrawPileEmptyAfterThreshold) while in the piles; and what the 'maximum obtainable void blocks' cap measures (per round? lifetime? concurrent?).

**Önerilen hüküm:** Boşluk bloğu raunt-içi GEÇİCİ kart olsun: OwnedCards'a girmez, raunt bitince yok olur, id'sini GameSession'ın kart sayacından alır (determinizm için). Destede olduğu sürece tüm çekme/kayıp kurallarında normal kart sayılır — uzatmada desteyi 1 kart uzatması bu jokerin bilinçli ödülüdür. 'Maksimum' = aynı raunt içinde aynı anda var olabilecek boşluk bloğu sayısı (öneri: 2). Kalıcı versiyon istersen ayrı ve daha pahalı bir joker yap.

### 2.11 Kazı çalışması, Buldozer, Robot süpürge, Kara delik

**Çakışma:** When a block's cubes all explode 'in one go', its BlockCard is no longer on the board — it was discarded at play time and may since have been reshuffled into the draw pile (threshold pass, refill recycle) or moved to the removed zone (overtime sweep removal, expired bonus). Returning it to the bonus hand must pull it from a pile. Also unspecified: which explosion sources qualify (Buldozer wipes whole blocks 'in one go'; Robot süpürge can fully pop a 1x1; void-crush too), what the returned card's BonusPlayOutcome is, and note that playing it as a bonus burns a top draw card — in overtime an empty pile on that burn is a loss.

**Önerilen hüküm:** 'Tek seferde tümüyle patlama' = tek patlama olayında o kartın tahtadaki TÜM küplerinin yok olması (SourceCardId ile takip; kartın bazı küpleri daha önce patladıysa tetiklenmez). Tetiklenince kart nerede bulunursa oradan (ıskarta veya çekme destesi) bonus ele taşınır; 'removed' bölgesindeyse taşınmaz. Buldozer patlamaları TETİKLEMEZ (hiçbir şey saymaz kuralıyla tutarlı); Robot süpürge ve boşluk ezmesi tetikler. Bonus oynanınca sonuç ToDiscard olsun (kart normal deste kartı, kaybolmasın) ve bonus yakma kuralı aynen uygulanır — uzatmada boş destede yakma = kayıp riski bilerek korunur.

### 2.12 Batak, Buldozer, Kayıt defteri

**Çakışma:** Batak needs lifecycle rulings the engine cannot infer: losing the bet is a brand-new loss type (LossReason has only 3 members); the advance-offer flow (AwaitingAdvanceDecision, offer-outranks-loss) can end the round mid-bet; bonus-hand plays increment TurnNumber so bet-turn counting must be defined; Buldozer clears the board without temizlik; and whether bets are allowed in overtime at all.

**Önerilen hüküm:** Batak kuralları: 1) bet süresi dolarsa yeni bir LossReason (örn. BatakKaybedildi) ile raunt kaybedilir; süre dolduğu turda ilerleme teklifi de doğduysa mevcut 'teklif kayıptan üstündür' kuralı aynen uygulanır (oyuncu ilerleyerek kaçabilir), 2) oyuncu ilerlemeyi kabul ederse aktif bet İPTAL olur — ödül yok, ceza yok, 3) her tur (bonus el oynayışı dahil, TurnNumber artan her şey) bet sayacını ilerletir, 4) Buldozer temizliği bet'i KAPATMAZ (temizlik değildir), 5) uzatmada bet koyulabilir; Kayıt defteri varken bet yalnızca sayaç temizliğiyle kapanır (3. maddedeki merkezi tanım kararıyla tutarlı).

### 2.13 Robot süpürge, Buldozer, Enfeksiyon, Seri tetik

**Çakışma:** The engine has NO post-turn phase: ResolvePlacement finishes with the status decision and TurnResolved fires after everything, so 'after each turn' effects currently have nowhere to run before loss/advance is decided. With several such jokers held, their relative order changes outcomes (Robot süpürge popping a cube before vs after Enfeksiyon spreads or Buldozer's 4th-turn wipe; Seri tetik's redraw before vs after cubes are destroyed changes NoPlayableMove), and each can empty the board, so the sweep check must re-run after them — not only at step 3.

**Önerilen hüküm:** Tur çözümüne resmi bir 'tur sonu efekt fazı' ekle: kart düşümü/el dolumu sonrası, eşik ve durum/kayıp kontrollerinden ÖNCE çalışır. Sabit global sıra: 1) Enfeksiyon yayılımı ve patlatmaları, 2) Buldozer (4. turundaysa), 3) Robot süpürge, 4) Seri tetik ıskartala+yeniden çek. Her adımdan sonra merkezi temizlik kontrolü yapılır ('tur başına en fazla 1 temizlik' kuralı geçerli); aynı önceliktekiler envanter sırasıyla çözülür; tüm rastgelelik IRandomSource'tan gelir (determinizm bozulmasın). Durum/kayıp kontrolü ancak bu faz bittikten sonra yapılır.

## 3. Framework tasarımı

### Gereken yaşam döngüsü olayları

- TurnResolved (RoundEngine, Action<TurnReport>) — EXISTS. Keep it as the post-fact, immutable notification for UI and for the session's TotalScore accrual. Jokers do NOT subscribe to it directly (ordering/determinism); the JokerInventory is driven by explicit dispatch instead.
- StatusChanged (RoundEngine, Action<RoundStatus>) — EXISTS. GameSession already uses it to detect Advanced/Lost; it becomes the trigger for the new RoundEnded dispatch.
- PhaseChanged (GameSession, Action<GamePhase>) — EXISTS. Market enter/leave are derivable from it, but jokers get explicit OnMarketEntered/OnMarketLeft dispatches for clarity.
- RoundStarted — MUST ADD (GameSession dispatch, right after constructing the RoundEngine in StartRound). Consumers: charge reset (Renovasyon, İade, Taşkın, Yangın, Powerbank), ihale (picks its auction target via ctx.Rng), Batak (opens the bet window), Enfeksiyon/Buldozer/Robot süpürge counters reset.
- ModifyRoundConfig — MUST ADD (GameSession, called BEFORE constructing RoundEngine: roundConfig = jokers.FilterRoundConfig(progression.GetRound(n))). Consumer: Kentsel Dönüşüm (permanent extra board space). This is a filter hook, not an event — it must run before the board exists.
- RoundEnded(outcome: Advanced|Lost) — MUST ADD (GameSession dispatch from OnRoundStatusChanged). Consumers: Domuz Kumbarası, Altın Kumbara (value accrual), Kentsel Dönüşüm (arms its next-round bonus), Damlaya Damlaya (arms 'watch the market' state).
- AfterLineExplosion (in-turn hook, new step 2b of ResolvePlacement) — MUST ADD. Runs after Board.ResolveFullLines, before the sweep check; receives TurnContext and may destroy more cubes via ctx.ExplodeCubes. Consumers: Tutuştur (fire chain), Enfeksiyon (spread/detonate), Buldozer (every-4th-turn wipe, scoreless), Kayıt defteri (cube counter + forced sweep via ctx.RequestSweepCheck).
- AfterCleanSweep (in-turn hook, inside step 3 when the sweep fires) — MUST ADD. Consumers: elmas kazma (explode obsidians for points), Kara delik (inject void block into discard), Altın Kumbara (accrue), Batak (payout), Robot süpürge (sweep-credit bonus), Kayıt defteri ('natural sweep no longer counts' interacts here).
- ModifyScore (in-turn hook, step 5, via ScoreBreakdown) — MUST ADD. Consumers: çığ, dondurma, Siyam (streak bonuses; need TurnContext.PreviousReport), midas (gold-in-hand bonus), Damlaya Damlaya (next-round bonus), Batak partial payouts. bereket needs NO hook here — it permanently mutates ScoringConfig from AfterTurnScored (plus-shape detection = an exploded row AND column that intersect, readable from the in-progress report).
- AfterTurnScored (in-turn hook, after refill/threshold, before the final status update and TurnResolved) — MUST ADD. Consumers: Seri tetik (discard unused hand + redraw; overtime-disabled), Robot süpürge (random cube pop + cooldown), Cimri Kumbara (per-turn accrual), Harcama bonusu (reads report.DrawPileEmptiedThisTurn), Buldozer/Enfeksiyon turn counters, Batak (turn-limit check → ctx.DeclareLoss).
- OnCubesDestroyed(IReadOnlyList<DestroyedCube>) (in-turn hook, raised for EVERY batch of destroyed cubes: line clears, joker/power explosions, void blocks) — MUST ADD, and requires a GameBoard change: explosions must report the Cube VALUE (Kind + SourceCardId), not just GridPos. Consumers: Kazı çalışması (detect a card fully exploded in one shot → ctx.AddBonusCard), Parazit (host-cube death → queued joker removal), Buzluk (ice explosion bonus), Kayıt defteri counter.
- DrawPileEmptiedThisTurn — MUST ADD as a TurnReport flag set inside DrawWithRules (covers empty-then-reshuffled-same-turn). Consumer: Harcama bonusu.
- JokerAcquired / JokerSold / JokerRemoved — MUST ADD on JokerInventory (dispatch to other jokers + C# events for UI). Consumers: ihale ('no new auction until the auctioned joker sells'), Domuz Kumbarası (the future sell mechanic), Parazit cleanup.
- MarketEntered / ItemPurchased / MarketLeft(anythingPurchased) / MarketOffersGenerating — RESERVED NOW as virtual no-op hooks on Joker, dispatched when the real market replaces MarketStub. Consumers: Damlaya Damlaya (purchase tracking), Simya (offer generation filter), Parazit (attachment is a market-phase action).
- PowerUsed — RESERVED (empty virtual hook now, dispatched when powers ship). Consumer: Powerbank (refill one power use, 1 charge/round).
- NOT needed as events: threshold pass and overtime entry are readable from TurnReport.ThresholdJustPassed / Engine.ThresholdPassed inside existing hooks; Insider is a pure UI flag (RoundRules.RevealTopDrawCard) with no event at all.

### Joker taban tipi (taslak)

~~~csharp
// All files under Assets/Scripts/Core/Jokers/ — pure C# 9, no UnityEngine, LangVersion 9 compatible.

// ============================ Jokers/Joker.cs ============================
// PURPOSE: Base type of every joker. Virtual no-op hooks; JokerInventory calls them
// in inventory order (left-to-right = acquisition order = the ONE canonical order).

namespace ProjectBlock.Core
{
    /// <summary>How a round ended, for OnRoundEnded.</summary>
    public enum RoundOutcome { Advanced = 0, Lost = 1 }

    public abstract class Joker
    {
        protected Joker(string defId, int instanceId)
        {
            DefId = defId;
            InstanceId = instanceId;
        }

        /// <summary>Stable content id ("domuz_kumbarasi"); the save/replay key. Never rename.</summary>
        public string DefId { get; }

        /// <summary>Unique within the session; GameSession hands these out (like card ids).</summary>
        public int InstanceId { get; }

        // ---- sell value (usable BEFORE the market exists; market just reads SellValue) ----
        public int BaseSellValue { get; protected set; }
        public int AccruedValue { get; private set; }      // kumbara jokers grow this
        public int AuctionPremium { get; internal set; }   // ihale writes this from outside
        public int SellValue { get { return BaseSellValue + AccruedValue + AuctionPremium; } }
        protected void Accrue(int amount) { AccruedValue += amount; }

        // ---- per-round charges (Renovasyon, İade, Taşkın, Yangın, Powerbank; 0 = passive) ----
        public int ChargesPerRound { get; protected set; }
        public int ChargesLeft { get; private set; }
        internal void ResetCharges() { ChargesLeft = ChargesPerRound; }
        protected bool TrySpendCharge()
        {
            if (ChargesLeft <= 0) return false;
            ChargesLeft--;
            return true;
        }

        // ---- gating (checked centrally by JokerInventory dispatch, NOT by each joker) ----
        /// <summary>Kayıt defteri, Seri tetik: true → all hooks skipped while Engine.ThresholdPassed.</summary>
        public virtual bool DisabledInOvertime { get { return false; } }

        /// <summary>Parazit hosting: null = normal inventory joker. Set later by the market phase;
        /// dispatch skips the joker when the host cube is no longer alive (removal is then queued).</summary>
        public CubeAttachment? Attachment { get; internal set; }

        // ---- session lifecycle (all no-ops; override only what you need) ----
        public virtual void OnAcquired(SessionContext ctx) { }        // Seri tetik: Rules.HandSize += 2
        public virtual void OnSold(SessionContext ctx) { }            // Seri tetik: Rules.HandSize -= 2
        public virtual void OnJokerSold(SessionContext ctx, Joker sold) { }   // ihale
        public virtual void OnRoundStarted(RoundContext ctx) { }      // called AFTER ResetCharges
        public virtual void OnRoundEnded(RoundContext ctx, RoundOutcome outcome) { }
        public virtual void OnMarketEntered(SessionContext ctx) { }               // reserved
        public virtual void OnMarketLeft(SessionContext ctx, bool anythingPurchased) { } // Damlaya
        public virtual void OnPowerUsed(RoundContext ctx, string powerId) { }     // reserved: Powerbank

        // ---- in-turn hooks (see RoundEngine resolution order; TurnContext CAN mutate the turn) ----
        public virtual void AfterLineExplosion(TurnContext turn) { }  // Tutuştur, Buldozer, Enfeksiyon, Kayıt defteri
        public virtual void AfterCleanSweep(TurnContext turn) { }     // elmas kazma, Kara delik, Altın Kumbara, Batak
        public virtual void ModifyScore(TurnContext turn, ScoreBreakdown score) { } // çığ, dondurma, Siyam, midas
        public virtual void AfterTurnScored(TurnContext turn) { }     // Seri tetik, Robot süpürge, Cimri, Harcama bonusu
        public virtual void OnCubesDestroyed(TurnContext turn, System.Collections.Generic.IReadOnlyList<DestroyedCube> cubes) { } // Kazı çalışması, Parazit, Buzluk

        // ---- player-activated ability (Renovasyon, İade, Taşkın, Yangın, Powerbank) ----
        public virtual bool CanActivate(RoundContext ctx, ActivationTarget target) { return false; }
        public virtual void Activate(RoundContext ctx, ActivationTarget target) { }
    }

    /// <summary>Target of an activated joker: all fields optional, ability defines which it reads.</summary>
    public readonly struct ActivationTarget
    {
        public readonly int? HandIndex;   // İade: which card to swap
        public readonly GridPos? Cell;    // future: Enfeksiyon start cube, Taşkın center...
        public readonly string PowerId;   // Powerbank
        // ctor omitted
    }

    /// <summary>Parazit: which cube of which deck card hosts a joker.</summary>
    public readonly struct CubeAttachment
    {
        public readonly int CardId;     // matches Cube.SourceCardId (already stored on every cube)
        public readonly int CellIndex;  // index into BlockShape.Cells of that card
        // ctor omitted
    }
}

// ============================ Jokers/JokerContexts.cs ============================
// PURPOSE: What hooks are allowed to see/do. Contexts wrap live objects — never cache values.

namespace ProjectBlock.Core
{
    public sealed class SessionContext
    {
        public GameSession Session { get; }
        public JokerInventory Jokers { get; }
        public RoundRules Rules { get; }         // Seri tetik mutates HandSize here
        public ScoringConfig Scoring { get; }    // bereket mutates here (permanent)
        public IRandomSource Rng { get; }        // THE session rng — the only legal randomness
    }

    public sealed class RoundContext   // SessionContext + the current engine
    {
        public SessionContext SessionCtx { get; }
        public RoundEngine Engine { get; }       // RedrawHand (Renovasyon), ReplaceHandCard (İade), AddBonusCard...
        public IRandomSource Rng { get; }
    }

    /// <summary>Handed to in-turn hooks while ResolvePlacement is mid-flight.
    /// Mutations go through engine-owned methods so OccupiedCount, the report and
    /// re-entrant sweep checks stay consistent.</summary>
    public sealed class TurnContext
    {
        public RoundEngine Engine { get; }
        public GameBoard Board { get; }
        public TurnReport Report { get; }          // in-progress, partially filled
        public TurnReport PreviousReport { get; }  // last resolved turn, null on turn 1 (çığ/dondurma/Siyam)
        public IRandomSource Rng { get; }

        // engine-mediated mutations:
        public System.Collections.Generic.IReadOnlyList<DestroyedCube> ExplodeCubes(
            System.Collections.Generic.IEnumerable<GridPos> cells, ExplosionCause cause,
            bool grantsScore);                                    // Buldozer: grantsScore=false
        public void AddFlatScore(int amount, string sourceDefId); // routed into this turn's ScoreBreakdown
        public void AddCardToDiscard(BlockCard card);             // Kara delik void block
        public void AddBonusCard(BlockCard card, BonusPlayOutcome outcome); // Kazı çalışması
        public void RequestSweepCheck();                          // Kayıt defteri forced sweep
        public void DeclareLoss(LossReason reason);               // Batak (new LossReason.BetFailed)
    }

    public enum ExplosionCause { LineClear = 0, JokerEffect = 1, PowerEffect = 2, VoidBlock = 3 }

    /// <summary>One destroyed cube WITH its data — GameBoard must start reporting Cube
    /// values on explosion (today LineExplosionResult only carries positions).</summary>
    public readonly struct DestroyedCube
    {
        public readonly GridPos Pos;
        public readonly Cube Cube;            // Kind + SourceCardId (Kazı çalışması, Parazit)
        public readonly ExplosionCause Cause;
    }
}

// ============================ Jokers/JokerInventory.cs ============================
// PURPOSE: Session-scoped joker list + the ONLY dispatcher. Order = list order.
// Implements ITurnHooks so RoundEngine stays ignorant of the Joker type.

namespace ProjectBlock.Core
{
    /// <summary>What RoundEngine calls mid-turn. NullTurnHooks for jokerless tests.</summary>
    public interface ITurnHooks
    {
        void AfterLineExplosion(TurnContext turn);
        void AfterCleanSweep(TurnContext turn);
        void ModifyScore(TurnContext turn, ScoreBreakdown score);
        void AfterTurnScored(TurnContext turn);
        void OnCubesDestroyed(TurnContext turn,
            System.Collections.Generic.IReadOnlyList<DestroyedCube> cubes);
    }

    public sealed class JokerInventory : ITurnHooks
    {
        private readonly System.Collections.Generic.List<Joker> jokers = new();
        public System.Collections.Generic.IReadOnlyList<Joker> Jokers { get { return jokers; } }
        public int MaxSlots = 5;                       // balance placeholder

        /// <summary>ihale: only one auction at a time until that joker sells.</summary>
        public int? ActiveAuctionInstanceId { get; internal set; }

        // UI-only notifications (never game logic):
        public event System.Action<Joker> JokerAdded;
        public event System.Action<Joker> JokerRemoved;

        public bool Add(Joker joker, SessionContext ctx);      // → joker.OnAcquired, JokerAdded
        public long Sell(Joker joker, SessionContext ctx);     // returns SellValue → TotalScore;
                                                               // → joker.OnSold, others' OnJokerSold, clears auction
        internal void QueueRemoval(Joker joker);               // Parazit host died mid-dispatch

        // called by GameSession:
        internal RoundConfig FilterRoundConfig(RoundConfig cfg);          // Kentsel Dönüşüm
        internal void DispatchRoundStarted(RoundContext ctx);             // ResetCharges() first, then hooks
        internal void DispatchRoundEnded(RoundContext ctx, RoundOutcome o);
        internal void DispatchMarketEntered / MarketLeft / PowerUsed(...);

        // ITurnHooks impl: for each joker IN LIST ORDER, skipping
        //   (DisabledInOvertime && turn.Engine.ThresholdPassed) and dead-host attachments;
        // iterate over a snapshot, apply queued removals after the loop.
    }
}

// ============================ Jokers/JokerRegistry.cs ============================
// PURPOSE: DefId → factory, so saves/replays/market offers can create jokers by id.
public static class JokerRegistry
{
    // Register(string defId, Func<int /*instanceId*/, Joker> factory);
    // Joker Create(string defId, int instanceId);
    // AllDefIds — market offer pool later.
}

// ============================ Changes to EXISTING files ============================
// RoundEngine.cs:
//   public RoundEngine(RoundConfig config, RoundRules rules, IEnumerable<BlockCard> ownedCards,
//       IRandomSource rng, IScoreCalculator scorer, ITurnHooks hooks /* null → NullTurnHooks */)
//   NEW public void ReplaceHandCard(int handIndex)   // İade primitive: discard 1, draw 1 (RedrawHand already exists)
//   NEW resolution order (extends the documented 1-8):
//     1. place + base placement score into ScoreBreakdown
//     2. line explosions → DestroyedCube list → hooks.OnCubesDestroyed
//     2b. hooks.AfterLineExplosion (chained ctx.ExplodeCubes re-raises OnCubesDestroyed)
//     3. sweep check (now also honors Rules.NaturalSweepDisabled + RequestSweepCheck)
//        → on sweep: base sweep score, overtime handling, hooks.AfterCleanSweep
//     4. card disposition (honors TurnContext redirect-to-bonus from Kazı çalışması)
//     5. refill hand   6. threshold check
//     6b. finalize score: hooks.ModifyScore(breakdown) → single rounding → RoundScore
//     6c. hooks.AfterTurnScored (Seri tetik redraw, Robot süpürge...)
//     7./8. status update → TurnResolved (unchanged, notification only)
//   keeps a reference to the previous TurnReport for TurnContext.PreviousReport.
//
// GameSession.cs:
//   public JokerInventory Jokers { get; }   // per its own PURPOSE header
//   private int nextJokerInstanceId = 1;
//   StartRound():
//     RoundConfig cfg = Jokers.FilterRoundConfig(Config.Progression.GetRound(RoundNumber));
//     CurrentRound = new RoundEngine(cfg, Config.Rules, ownedCards, rng, scorer, Jokers);
//     ...existing wiring... then Jokers.DispatchRoundStarted(roundCtx);
//   OnRoundStatusChanged(): before switching phase → Jokers.DispatchRoundEnded(...).
//
// GameBoard.cs:  LineExplosionResult gains IReadOnlyList<DestroyedCube> (or parallel Cube list);
//   NEW public Cube? RemoveCube(GridPos pos) so engine-mediated explosions keep OccupiedCount right.
//
// RoundRules.cs additions:
//   public bool SweepRequiresLineExplosion = true;  // existing behavior, now bendable
//   public bool NaturalSweepDisabled = false;       // Kayıt defteri
//   public bool RevealTopDrawCard = false;          // Insider (UI reads it; zero rules impact)
//
// TurnReport.cs additions:
//   public bool DrawPileEmptiedThisTurn;            // Harcama bonusu
//   public IReadOnlyList<DestroyedCube> DestroyedCubes;  // replaces pos-only view for consumers
//
// LossReason: add BetFailed (Batak).
// BlockCard.cs (LATER, with market): int? AttachedJokerInstanceId — see attachment story.
~~~

### Skor boru hattı

Concrete shape — a mutable ScoreBreakdown assembled by RoundEngine during the turn, finalized once:

    public sealed class ScoreBreakdown
    {
        public int BasePlacement;   // scorer.ScorePlacement(...)  — DefaultScoreCalculator stays UNCHANGED
        public int BaseLines;       // scorer.ScoreLineExplosion(...)
        public int BaseSweep;       // scorer.ScoreCleanSweep()
        public int FlatBonus;       // stage 2
        public double Multiplier = 1.0;  // stage 3
        public List<ScoreContribution> Contributions;  // (sourceDefId, amount) — UI popups + debugging
        public void AddFlat(int amount, string sourceDefId);
        public void AddMultiplier(double factor, string sourceDefId);  // multiplies, never sets
        public int Total => (int)Math.Floor((BasePlacement + BaseLines + BaseSweep + FlatBonus) * Multiplier);
    }

IScoreCalculator keeps its three minimal methods and DefaultScoreCalculator keeps reading ScoringConfig live (so 'bereket' permanently buffing ScoringConfig needs zero pipeline code). The 'pipeline that sees the whole TurnReport-in-progress' from the header is: RoundEngine fills the three Base fields at their existing points in the turn, then at step 6b calls hooks.ModifyScore(turnContext, breakdown) exactly once, where turnContext exposes the in-progress TurnReport AND PreviousReport (çığ/dondurma/Siyam compare Card.Shape.Size / CanonicalKey across turns). RoundScore += breakdown.Total; report.ScoreGained = breakdown.Total. In-turn hooks that grant points outside ModifyScore (elmas kazma, Batak payout, Buzluk) call TurnContext.AddFlatScore, which routes into the same breakdown — one turn, one total, one rounding.

ORDERING RULE (canonical, deterministic): (1) base values from DefaultScoreCalculator; (2) all flat additions in JOKER INVENTORY ORDER (left-to-right, acquisition order); (3) all multipliers in inventory order; (4) floor once at the end. Flats compose by addition and multipliers by multiplication, so within each stage the order is commutative — the inventory order only becomes observable if a future joker caps or reads intermediate totals, which is why hooks receive the breakdown but the rule is 'add, never read Total mid-pipeline'. This is the Balatro-familiar 'chips then mult' rule and leaves room to later sell joker-reordering as a mechanic. RoundEngine's ScorePlacement call moves from step 1 to step 6b assembly-time only in the sense that the VALUE is computed at step 1 but banked into RoundScore only once at 6b (needed so sweep-time hooks can still see 'score so far this turn' via the breakdown).

### Envanter

The JokerInventory lives on GameSession (exactly what GameSession.cs's PURPOSE header promises: 'the joker/power inventories will live here (session-scoped state), subscribing to each new RoundEngine's events in StartRound'). It survives rounds; RoundEngine instances do not. Wiring per round, all inside GameSession.StartRound: (1) roundConfig = Jokers.FilterRoundConfig(Progression.GetRound(RoundNumber)) — Kentsel Dönüşüm hook, must run before the board exists; (2) the inventory is passed INTO the RoundEngine constructor as ITurnHooks (a new optional parameter) — this is stronger than event subscription because the in-turn hooks must run mid-ResolvePlacement and be able to mutate the turn, which a post-fact C# event cannot; (3) after construction (and the existing degenerate-loss check) GameSession calls Jokers.DispatchRoundStarted(roundCtx), which first resets every joker's charges and then calls OnRoundStarted in inventory order; (4) GameSession.OnRoundStatusChanged dispatches RoundEnded(Advanced|Lost) before flipping the phase. No joker ever subscribes to TurnResolved/StatusChanged directly — those stay notification-only for the UI and the session — so C# delegate subscription order can never influence game rules. Per-round reset story: automatic, because everything round-scoped (board, deck, engine) is rebuilt each round; joker-side round state is only ChargesLeft (reset centrally) plus whatever a joker resets itself in OnRoundStarted (Robot süpürge cooldown, Buldozer counter, Batak bet); run-scoped joker state (kumbara AccruedValue, bereket's ScoringConfig buffs, Kentsel Dönüşüm's extra space) simply persists in joker fields / config objects. Activated jokers: the View calls GameSession.ActivateJoker(inventoryIndex, ActivationTarget), which builds the RoundContext and forwards to the joker's CanActivate/Activate — Renovasyon calls the existing RoundEngine.RedrawHand, İade the new ReplaceHandCard(handIndex); both spend a charge via TrySpendCharge.

### Satış değeri

Sell value is fully represented on the Joker base type today, with the market only READING it later: SellValue = BaseSellValue + AccruedValue + AuctionPremium. AccruedValue is grown by the joker itself through its own hooks — Domuz Kumbarası in OnRoundEnded, Cimri Kumbara in AfterTurnScored (1 per placement-turn held), Altın Kumbara in AfterCleanSweep + OnRoundEnded — so all three kumbaras work from day one with zero market code; the debug HUD can already display SellValue. AuctionPremium is written from OUTSIDE by ihale: in OnRoundStarted it checks JokerInventory.ActiveAuctionInstanceId == null, picks a random joker (itself included) via ctx.Rng (deterministic), sets its AuctionPremium and records ActiveAuctionInstanceId; JokerInventory.Sell clears the auction id and dispatches OnJokerSold so ihale can re-auction next round. Selling itself is a 3-line inventory method (TotalScore += SellValue; remove; dispatch) that ships with the market phase, but nothing about accrual waits for it.

### Karta takma (Parazit)

Parazit ships last, but nothing in v1 blocks it because the two hard prerequisites are already in the framework: (1) Cube.SourceCardId exists (already implemented), and OnCubesDestroyed delivers full Cube values, so 'the host cube died' is detectable from the base framework; (2) Joker.Attachment (a nullable CubeAttachment { CardId, CellIndex }) sits on the base type from day one as an inert field — v1 dispatch just checks 'Attachment == null → always active'. When the market lands, Parazit's market-phase action sets victim.Attachment = (cardId, cellIndex) and BlockCard gains int? AttachedJokerInstanceId (its PURPOSE header already reserves this) with the 'max 1 joker per block' rule enforced there. Runtime semantics then: while the host card's cube at CellIndex is on the board OR the card is in a pile, the attached joker dispatches normally; when OnCubesDestroyed reports a cube with matching SourceCardId whose offset maps to CellIndex, the inventory queues removal of the attached joker (queued, not immediate — removal during dispatch iteration must be deferred to the end of the batch, which the dispatch loop already supports for exactly this reason). Parazit itself stays in the inventory as a normal passive joker. Open mapping detail for later: cube→CellIndex needs the placed cube to remember its shape-cell index (add a byte to Cube when Parazit ships) or Parazit v1 can simplify to 'any cube of the host card dies → joker dies'.

### Notlar ve riskler

- SIRALAMA KURALI (tasarım kararı): Joker etkileri her zaman envanter sırasıyla (soldan sağa, ediniliş sırası) uygulanır; skorda taban → flat bonus → çarpan → tek yuvarlama. Bu Balatro'daki 'chips sonra mult' kuralının karşılığı ve ileride 'jokerleri dizme' mekaniğini oyuncuya satılabilir bir derinlik olarak açık bırakıyor.
- Kayıt defteri iki temel varsayımı büker: 'temizlik sadece patlamayla olur' (RoundRules.SweepRequiresLineExplosion bayrağı eklendi) ve 'tahtayı boşaltmak temizliktir' (NaturalSweepDisabled bayrağı). Bu joker varken doğal temizliğin overtime deste ödülünü de mi kestiği (sadece puan mı, deste yenileme mi) tasarımcı kararı bekliyor.
- Kentsel Dönüşüm dikdörtgen tahta varsayımını kırıyor — GameBoard'un hücre maskesine (düzensiz şekle) genellenmesi gerekecek. RİSK: Bu, GetValidOrigins/AnyPlacementExists/satır-sütun tarama döngülerine dokunur. İlk sürümde 'her raunt +1 hücre yerine belirli rauntlarda +1 satır/sütun' olarak basitleştirilebilir; framework'teki FilterRoundConfig kancası her iki yorumu da taşır.
- Robot süpürge ve Buldozer tur SONRASI patlama yapar; bu patlama temizlik tetiklerse (süpürge → temizlik → Kara delik boşluk bloğu → Altın Kumbara...) zincirleme sıra AfterTurnScored içindeki ExplodeCubes'un yeniden süpürme kontrolü çalıştırmasıyla çözülür — ama 'tur skoru çoktan kesinleşti' durumundayız. KARAR GEREK: tur sonrası tetiklenen temizlik puanı bu tura mı, sonraki tura mı yazılır? (Öneri: aynı turun raporuna ek bir 'PostTurnEvents' bölümü.)
- Batak oyun kaybettirebilir → LossReason'a BetFailed eklenecek (enum'a üye eklemek kaydedilmiş replay'leri bozmaz ama UI metni ister). Batak'ın ödül eğrisi (1 tur = çok yüksek, 100 tur = sıfır, oransal erken bitirme) ayrı bir tunable eğri objesi olmalı — ScoringConfig gibi canlı okunan bir BatakConfig.
- DETERMİNİZM: Tüm joker rastgeleliği (ihale hedef seçimi, Robot süpürge küp seçimi) ctx.Rng üzerinden tek session rng'sine akar. Bunun bedeli: bir joker almak/satmak sonraki tüm rng akışını değiştirir. Replay = seed + oyuncu girdi kaydı olarak tanımlanmalı (sadece seed yetmez, girdiler şart). Ara kayıt (mid-run save) için JokerRegistry (DefId → fabrika) şimdiden var; joker durum anlık görüntüsü (SaveState/LoadState) v2'ye bırakıldı — alanlar ilkel tiplerde tutulursa sonradan eklemek kolay.
- Overtime kapatması merkezî: envanter dispatch'i DisabledInOvertime && ThresholdPassed olan jokeri atlar; joker kendi içinde kontrol yazmaz. DİKKAT: Seri tetik'in +2 el boyutu OnAcquired'da RoundRules.HandSize mutasyonu — overtime'da bu +2 geri alınmalı mı yoksa sadece 'tur sonu at-çek' mi durmalı? Tasarım metni ikisini ayırmıyor; öneri: overtime'da yalnızca at-çek durur, el boyutu kalır (aksi orta-raunt el küçültme kaosu yaratır).
- Aynı jokerden iki kopya taşınabilir mi? Framework InstanceId ile destekliyor; yasaklamak istenirse Market tarafında engellenir. Kumbara jokerlerinin satış mekaniği TotalScore = para tasarımıyla birleşince 'puan biriktiren joker' aslında faizli mevduat — sayılar dengelenirken bu birleşik ekonomi göz önünde tutulmalı.
- ihale'nin 'kendisi satılırsa' durumu: aktif ihale ihalenin kendisindeyse ve satılırsa premium zaten oyuncuya ödenmiş olur; başka jokerdeyken ihale satılırsa o premium kalır mı silinir mi? KARAR GEREK (öneri: kalır, oyuncu lehine ve kod basit).
- midas / Simya / Taşkın / Yangın / Buzluk / Tutuştur / elmas kazma elementli küplere bağımlı — framework kancaları hazır (AfterLineExplosion, OnCubesDestroyed, ModifyScore) ama CubeKind'e Fire/Water/Gold/Obsidian/Ice eklenmeden bu jokerler yazılamaz. Sıralama önerisi: önce element küpleri, sonra bu joker dalgası.
- Enfeksiyon listede joker ama davranışı güç gibi ('seçilen bir küp' hedefi var). ActivationTarget.Cell alanı bunun için kondu; joker mi güç mü kararı framework'ü etkilemiyor, ikisi de aynı Activate kancasını kullanır.
- TurnReport'a DestroyedCubes (küp verisiyle) ve DrawPileEmptiedThisTurn alanları ekleniyor; GameBoard patlamalarda artık pozisyonla birlikte Cube değerini de raporlamalı — mevcut LineExplosionResult.ExplodedCells yalnız pozisyon taşıyor, bu küçük ama kırıcı olmayan bir imza genişletmesi.
- İlk uygulama dilimi önerisi (framework'ü kanıtlamak için): Joker taban tipi + JokerInventory + ScoreBreakdown + RoundStarted/RoundEnded dispatch'i, ardından element/market beklemeyen 6 joker: Renovasyon (mevcut RedrawHand'i kullanır), İade (yeni ReplaceHandCard), Domuz/Cimri/Altın Kumbara (satış değeri birikimi), Harcama bonusu. Bunlar 5 farklı kanca tipini (charge'lı aktivasyon, tur/raunt/temizlik birikimi, rapor bayrağı) uçtan uca test eder.

## 4. Joker bazında detay

### Altın Kumbara — `S`

**Bekleyen alt sistem:** market, satış

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — the per-turn trigger; the same event carries the clean-sweep trigger via TurnReport.CleanSweep (TurnReport.cs), set in ResolvePlacement step 3
- Clean-sweep guard in RoundEngine.ResolvePlacement: 'explosion.LineCount > 0 && Board.IsCleanForSweep()' (RoundEngine.cs) — Altın automatically inherits the confirmed 'temizlik' definition (requires a same-turn line explosion), so future non-explosion board-emptiers (Buldozer, Robot süpürge) correctly do not pay it
- RoundEngine.StatusChanged → RoundStatus.Advanced (RoundEngine.cs) / GameSession.PhaseChanged → GamePhase.Market (GameSession.cs) — the per-round-finish trigger
- RoundEngine.ThresholdPassed (RoundEngine.cs) — overtime detection if the designer restricts overtime accrual
- GameSession.StartRound extension point (GameSession.cs)

**Motora eklenmesi gerekenler:**

- The joker subsystem: session-scoped inventory in GameSession + per-round wiring in StartRound
- Per-joker mutable sell value field (plus up to three tunable gain amounts if the trigger kinds pay differently)
- Market sell flow to cash the value out (MarketStub is empty)

**Durum (state):** One accrued-value counter. Scope: per-run, no resets. Tunable gain amounts are config, not state.

**Yaşam döngüsü:** Two trigger moments: (1) TurnResolved after every placement — +turn value, plus +clean-sweep value when report.CleanSweep is true (one event can grant both in the same turn); (2) RoundStatus.Advanced — +round-finish value. A single overtime turn can plausibly grant turn + sweep value and, if the player then takes the advance offer, the round-finish value too. No per-round reset. Overtime: not restricted by the designer — see question; same farming concern as Cimri.

### Cimri Kumbara — `S`

**Bekleyen alt sistem:** market, satış

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event, Action<TurnReport> (RoundEngine.cs) — fires exactly once per resolved placement, matching the designer's definition 'oyun alanına bir blok atmak = 1 tur'; TurnReport.TurnNumber (TurnReport.cs) is the authoritative turn counter
- TurnReport.PlayedFromBonusHand (TurnReport.cs) — distinguishes bonus-hand placements in case the designer excludes them
- RoundEngine.RedrawHand (RoundEngine.cs) — documented 'Does NOT consume a turn' and does not fire TurnResolved, so hand redraws correctly grant no value with zero extra work
- RoundEngine.ThresholdPassed (RoundEngine.cs) — overtime detection, needed only if the designer restricts overtime accrual
- GameSession.StartRound extension point (GameSession.cs) — where the inventory re-subscribes each round

**Motora eklenmesi gerekenler:**

- The joker subsystem: session-scoped inventory in GameSession + re-subscription to each new RoundEngine in StartRound
- Per-joker mutable sell value ('değer') field
- Market sell flow to cash the value out (MarketStub is empty)

**Durum (state):** One accrued-value counter. Scope: per-run — no per-round or per-turn resets.

**Yaşam döngüsü:** Triggers at the end of every resolved turn (after step 8 of the documented turn order, when TurnResolved fires), for hand AND bonus-hand placements alike — both go through ResolvePlacement and increment TurnNumber. RedrawHand grants nothing. No per-round reset. Overtime: the designer did not exclude it (unlike Kayıt defteri / Seri tetik where overtime is explicitly disabled), so by default overtime turns keep accruing value — flagged as a question because it rewards deliberately prolonging overtime.

### Domuz Kumbarası — `S`

**Bekleyen alt sistem:** market, satış

**Kullanacağı mevcut kancalar:**

- RoundEngine.StatusChanged event, Action<RoundStatus> (RoundEngine.cs) — RoundStatus.Advanced (TurnReport.cs) is the successful round-end signal, set only via DecideAdvance(true)
- GameSession.PhaseChanged event (GameSession.cs) — equivalent session-level signal: GamePhase.Market is entered exactly when the round status becomes Advanced (GameSession.OnRoundStatusChanged)
- GameSession.StartRound (GameSession.cs) — the documented EXTENSION POINT where the session-scoped joker inventory will re-subscribe to each new RoundEngine's events
- GameSession.TotalScore (GameSession.cs) + MarketStub.cs header ('held jokers/powers can be sold; the currency is TotalScore') — the confirmed currency the accrued value cashes out into

**Motora eklenmesi gerekenler:**

- The joker subsystem itself: session-scoped joker inventory in GameSession + per-round event wiring in StartRound (only the extension-point comment exists; no joker type/list anywhere in Core)
- Per-joker mutable sell value ('değer') field, readable by the UI and the future market
- Market sell flow (MarketStub is an empty class): sell a held joker for TotalScore, with price derived from the accrued value

**Durum (state):** One accrued-value counter on the joker instance. Scope: per-run — never resets between rounds, monotonically increasing.

**Yaşam döngüsü:** Triggers exactly once per round, at the moment RoundStatus becomes Advanced (player accepts the advance offer via DecideAdvance) / equivalently on GamePhase.Market entry. A Lost round grants nothing — under current rules a loss ends the run anyway. No per-turn or per-round resets. Overtime: no interaction — the trigger fires only after overtime ends in an advance, and the designer specified no overtime restriction.

### Insider — `S`

**Kullanacağı mevcut kancalar:**

- RoundDeck.DrawPile / RoundDeck.DrawCount (RoundDeck.cs) — full pile contents are already exposed with top = last element, and the file header explicitly says reveal jokers like Insider 'just change what the UI shows'
- RoundEngine.TurnResolved event (RoundEngine.cs) — the UI refresh moment for the revealed card
- TurnReport.BurnedCard (TurnReport.cs) — burns are already public information; Insider merely makes them predictable in advance

**Motora eklenmesi gerekenler:**

- Nothing in Core. Only the session joker inventory (GameSession EXTENSION POINT) so the View can ask 'is Insider held' and then render Deck.DrawPile[Deck.DrawCount - 1]

**Durum (state):** None. Purely passive while held (per-run ownership only); no per-round or per-turn counters.

**Yaşam döngüsü:** Continuous passive effect while held; the revealed card changes after every draw, every reshuffle (pre-threshold recycle, threshold pass, overtime clean sweep) and overtime card removal. Implementation constraint: the UI must only reveal the settled top card AFTER a turn fully resolves — TurnReport.CardsRemovedForRound is documented as information the player must NOT see, and because the overtime sweep shuffles before removing, showing only the post-resolution top leaks nothing about which cards were removed. No per-round reset; designer specified no overtime restriction and none is needed.

### Renovasyon — `S`

**Kullanacağı mevcut kancalar:**

- RoundEngine.RedrawHand() (RoundEngine.cs) — existing primitive explicitly documented as 'the primitive the future Renovasyon joker will use': discards the whole hand, shuffles discard into draw, refills, consumes no turn, enforces Status==InProgress via EnsurePlacingAllowed
- RoundEngine.ThresholdPassed (RoundEngine.cs) — the gate for whatever overtime rule the designer picks
- RoundEngine.Status / RoundEngine.Loss (RoundEngine.cs) — RedrawHand can end in Lost (LossReason.HandCannotBeRefilled), observers must handle that
- GameSession.PhaseChanged + GameSession.CurrentRound (GameSession.cs) — SetPhase deliberately re-fires for GamePhase.Round every round, usable as the per-round charge-reset signal; GameSession's header marks it as the future joker-inventory home

**Motora eklenmesi gerekenler:**

- GameSession: session-scoped joker inventory (the EXTENSION POINT in GameSession.cs) plus a player-facing activation path for triggered joker abilities — RedrawHand is currently only bound to a debug key in the View
- Charge accounting (2/round) exposed to the UI; lives entirely in the joker, no engine change needed
- Only if the designer restricts overtime: a RedrawHand variant/flag that skips Deck.ShuffleDiscardIntoDraw (today the reshuffle is unconditional)
- Optional: RedrawHand emits no event and produces no report — add a notification if other observers must animate or react to a redraw

**Durum (state):** Per-round: remaining redraw charges (starts at 2, decremented per use). Nothing per-turn; per-run only the fact of owning the joker.

**Yaşam döngüsü:** Player-activated between turns while Status==InProgress; runs entirely outside the 8-step turn resolution order and consumes no turn. Charges reset when a new round starts (new RoundEngine instance). Can end the round instantly: RedrawHand -> RefillHand loss (HandCannotBeRefilled) or the post-redraw NoPlayableMove check. Overtime behavior unspecified by the designer — note RedrawHand always reshuffles the discard into the draw pile, which contradicts the overtime rule that only clean sweeps recycle the discard.

### bereket — `S`

**Kullanacağı mevcut kancalar:**

- TurnReport.ExplodedRows + TurnReport.ExplodedColumns (TurnReport.cs) — on the rectangular board a same-turn full row and full column always intersect, so a '+' cross is detectable as 'both lists non-empty'; no board scan needed
- RoundEngine.TurnResolved event (RoundEngine.cs) — the consumption point for the trigger
- ScoringConfig mutability (ScoringConfig.cs) — its header explicitly names this joker: 'future jokers can buff values mid-game (e.g. bereket permanently raises gained score) by mutating this instance'; DefaultScoreCalculator reads it live and the instance is session-lived (GameSession passes Config.Scoring once), so mutations persist across rounds — 'kalıcı' comes for free
- GameSession.StartRound extension point (GameSession.cs header)

**Motora eklenmesi gerekenler:**

- Session joker inventory (as with all jokers)
- Only if the buff is a percentage of the whole turn's score: ScoringConfig has no global multiplier field — add one (e.g. TurnScoreMultiplier) applied in DefaultScoreCalculator or the score pipeline. If flat increments to the existing int fields (PointsPerCubePlaced / PointsPerLine / PointsPerCubeExploded) suffice, zero engine change is needed
- Only if the TRIGGERING turn must itself receive the increased score: the buff cannot come from TurnResolved (fires after RoundScore commit) — it needs the same in-pipeline seam as the streak jokers
- Optional: a TurnReport flag (e.g. bool CrossExplosion) for UI feedback, per the TurnReport.cs header rule

**Durum (state):** Per-run: the accumulated permanent increment (trigger count × step) — needed for UI display and for a possible revert if the future sell-mechanic must undo the buff. Per-round / per-turn: nothing. No randomness.

**Yaşam döngüsü:** Trigger detected from step-2 results (line explosion) via TurnResolved; the ScoringConfig mutation takes effect from the next scoring call onward. No per-round reset — the buff outlives every RoundEngine because ScoringConfig is session-scoped. Overtime: no designer exclusion, so it works identically during uzatma; overtime clean-sweep turns that clear a row+column cross also trigger it. Bonus-hand plays can trigger it like any placement.

### Buldozer — `M`

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — end-of-turn reaction point
- TurnReport.TurnNumber (TurnReport.cs) and RoundEngine.TurnNumber (RoundEngine.cs) — 4-turn cadence; per-round by construction because each round gets a fresh RoundEngine
- RoundEngine.Board (RoundEngine.cs) + GameBoard.OccupiedCount (GameBoard.cs) — what there is to clear
- CubeRules.IsDestructible (Cube.cs) — the central policy to consult when indestructible kinds (obsidian, map-placed gold) arrive

**Motora eklenmesi gerekenler:**

- GameBoard: a public non-scoring mass explosion of all cubes outside line resolution — ExplodeCell is private and only reachable through ResolveFullLines; must update OccupiedCount and return the cleared cells
- Loss re-evaluation after post-turn joker board mutations: Loss/Status are finalized in steps 7-8 before TurnResolved fires and GameSession flips to GameOver on Lost, so a Buldozer clear cannot currently rescue a same-turn NoPlayableMove loss (CheckForNoPlayableMove is private) — needs either an in-turn hook before step 8 or a public re-check primitive
- TurnReport field or a separate event for joker-caused explosions so the UI can animate the wipe (TurnReport is immutable after resolution)
- GameSession: joker inventory subsystem (shared prerequisite)

**Durum (state):** Per-round: turns-since-last-activation counter — or simply TurnNumber % 4, which auto-resets because TurnNumber restarts with each new RoundEngine. Nothing per-run.

**Yaşam döngüsü:** Fires at the end of every 4th resolved turn, after that turn's normal resolution — so it runs after the step-3 sweep check and can never satisfy or spoil the same turn's natural temizlik. Explodes every cube with no score, no temizlik: must NOT set TurnReport.CleanSweep and must NOT run the overtime sweep branch (no discard recycle, no RemoveRandomFromDraw, no advance offer). Per-round: cadence restarts with the new round (pending confirmation). Uzatma: the designer exempted other jokers (Kayıt defteri, Seri tetik) explicitly but not this one, so it keeps firing in overtime — a free periodic board wipe with zero sweep consequences.

### Harcama bonusu — `M`

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved + TurnReport.DiscardWasReshuffled (RoundEngine.cs / TurnReport.cs) — the closest existing signal, but insufficient: it also fires for threshold-pass and overtime-clean-sweep reshuffles and cannot isolate 'the draw pile ran out'
- RoundDeck.ShuffleCount / RoundDeck.DrawCount (RoundDeck.cs) — observable from outside, but carries the same cause-ambiguity
- RoundEngine.DrawWithRules() (RoundEngine.cs, private) — the single choke point every draw goes through and the only place exhaustion is actually detectable
- LossReason.DrawPileEmptyAfterThreshold (TurnReport.cs) — in overtime, pile exhaustion is currently an instant loss; the interplay must be decided
- IScoreCalculator EXTENSION POINT (IScoreCalculator.cs) — the planned 'pipeline that sees the whole TurnReport-in-progress' is where a threshold-counting payout would plug in

**Motora eklenmesi gerekenler:**

- A precise, observable draw-pile-exhaustion signal: an exhaustion counter set where DrawTop returns null / the pile hits zero (on RoundDeck or in RoundEngine.DrawWithRules), plus a TurnReport field for exhaustions inside a turn. It must also cover draws that happen OUTSIDE turn resolution (RoundEngine construction refill, RedrawHand, a future İade swap) — a TurnReport field alone misses those
- A way for a joker to add flat score: if the payout must count toward the threshold, RoundScore (private setter, today fed only by IScoreCalculator results inside ResolvePlacement) needs a joker score-contribution channel — the planned IScoreCalculator pipeline; that shared piece is L-sized and benefits many jokers. If the payout is TotalScore-only, a small GameSession primitive suffices
- GameSession: session joker inventory (shared prerequisite)

**Durum (state):** Per-round: number of payouts already made this round (needed if the trigger can repeat; resets with the new RoundDeck). Per-run: the bonus amount (balance tunable).

**Yaşam döngüsü:** Trigger = the draw pile running out, which can only happen at draw moments: hand refill (step 5 of the turn order), bonus-play burn (step 4), and out-of-turn draws (redraw actions, round-start refill). NOT exhaustions: the threshold-pass reshuffle (step 6) and the overtime clean-sweep reshuffle (step 3), both of which shuffle a possibly non-empty pile. Pre-threshold the pile can be exhausted repeatedly because the discard recycles. Payout is fully deterministic (no randomness, no IRandomSource use). Overtime: an empty draw pile on a draw attempt is currently an instant loss (DrawPileEmptyAfterThreshold) and the designer did not say whether the bonus still pays first.

### Kazı çalışması — `M`

**Kullanacağı mevcut kancalar:**

- Cube.SourceCardId (Cube.cs) — the header explicitly names this joker as its consumer
- RoundEngine.AddBonusCard(BlockCard, BonusPlayOutcome) (RoundEngine.cs) — the existing return-to-bonus-hand primitive; the RoundEngine header lists bonus-hand sources calling it
- BonusPlayOutcome.ExpireFromRound (BonusSlot.cs) — BonusSlot's header lists Kazı çalışması among the intended bonus-hand fillers; the default outcome fits
- BlockShape.Size via BlockCard.Shape (BlockShape.cs, BlockCard.cs) — the 'entirely exploded' comparison target
- RoundDeck.DiscardPile / RoundDeck.DrawPile (RoundDeck.cs) — locating the card instance to return
- RoundEngine.TurnResolved (RoundEngine.cs) — reaction point, with the timing caveat listed under missing

**Motora eklenmesi gerekenler:**

- TurnReport: exploded cells with their source card ids — the report only carries counts and row/column indexes; LineExplosionResult.ExplodedCells (positions) is not surfaced into it, and the Cube values are destroyed inside GameBoard.ExplodeCell before any subscriber runs. GameBoard must capture the removed Cube per exploded cell and TurnReport must expose (GridPos, CubeKind, SourceCardId) entries
- RoundDeck: remove a specific card instance from the discard pile (and possibly the draw pile) — the played card sits in the discard while its cubes are on the board, and a reshuffle (threshold pass, refill recycle) can even return it to the draw pile or hand; without a targeted-removal API the returned card would exist in a pile AND the bonus hand simultaneously
- In-turn timing: the bonus-hand re-add should happen before step 8's CheckForNoPlayableMove (which does consult the bonus hand) so a returned card can avert a same-turn NoPlayableMove loss; TurnResolved fires after status is final — needs the shared in-turn joker phase
- GameSession: joker inventory subsystem (shared prerequisite)

**Durum (state):** None — the joker is stateless; everything derives from the current turn's explosion data. The bonus-hand entries it creates are RoundEngine state and vanish with the round automatically.

**Yaşam döngüsü:** After step 2 (line explosions) of each turn: group exploded cells by SourceCardId; every card whose exploded-this-turn count equals its Shape.Size is returned to the bonus hand via AddBonusCard — including the just-played card if its placement immediately fully explodes. Per-round: nothing to reset; the bonus hand dies with the RoundEngine and unreturned cards were in normal piles anyway. Uzatma: no designer exemption; it inherits the confirmed bonus-play rules — playing the returned card burns the top of the draw pile, and in overtime an empty draw pile on that burn is an immediate loss (DrawWithRules), so the reward carries real risk late in a round.

### Seri tetik — `M`

**Kullanacağı mevcut kancalar:**

- RoundRules.HandSize (RoundRules.cs) — mutable and read live by RoundEngine.RefillHand on every refill; Hand.cs's header explicitly names 'Seri tetik' as a joker that changes hand size by mutating this shared instance
- RoundEngine.TurnResolved event + TurnReport.ThresholdJustPassed (RoundEngine.cs / TurnReport.cs) — detects the exact moment the joker must switch off
- RoundEngine.ThresholdPassed (RoundEngine.cs) — overtime gate ('uzatmalarda geçerli değil')
- GameSession.PhaseChanged (GameSession.cs) — reapply the +2 at each round start (ThresholdPassed is false again in the new RoundEngine)

**Motora eklenmesi gerekenler:**

- Engine: an end-of-turn full-hand cycle WITHOUT reshuffling the discard — e.g. a RoundRules flag (DiscardUnusedHandAfterTurn) honored at the card-disposition/refill step of ResolvePlacement in place of the normal top-up refill. RoundEngine.RedrawHand is NOT usable for this: it unconditionally calls Deck.ShuffleDiscardIntoDraw (changing the deck economy every turn) and throws outside Status==InProgress (e.g. when the same turn ended in AwaitingAdvanceDecision)
- TurnReport: a field listing the cards cycled out at end of turn (UI animation and other observers; TurnReport.cs says to add fields rather than let the UI re-derive)
- Joker framework: apply/revert bookkeeping for the +2 on RoundRules.HandSize (revert on threshold pass and on future joker removal/sale) — the engine never trims a hand that exceeds HandSize, so revert alone leaves extra cards in hand

**Durum (state):** Per-round: whether its +2 is currently applied to RoundRules.HandSize (applied at round start, revoked at threshold pass). No other counters.

**Yaşam döngüsü:** While active (pre-threshold): HandSize = base+2, and at the refill step of every turn (step 5 in the documented order: after card disposition, before the threshold check) the unused hand is discarded and a full new hand is drawn — pre-threshold DrawWithRules recycles the discard, so the cycle cannot self-mill as long as total circulating cards >= HandSize. Switches off the moment the threshold is passed (designer-explicit: invalid in overtime), which is coherent with the overtime economy where the discard must not recycle. The engine never trims an over-sized hand, so the fate of the extra cards needs a designer call. Reactivates at the next round start.

### Siyam — `M`

**Kullanacağı mevcut kancalar:**

- BlockShape.CanonicalKey (BlockShape.cs) — built exactly for this joker; the file header says shapes are normalized 'so that two equal shapes produce the same CanonicalKey (needed later by shape-comparing jokers like Siyam)'
- RoundEngine.TurnResolved event (RoundEngine.cs) and TurnReport.Card.Shape (TurnReport.cs) — the shape played this turn
- TurnReport.PlayedFromBonusHand (TurnReport.cs)
- BlockShape.RotatedClockwise() and BlockShape.MirroredHorizontally() (BlockShape.cs) — available if rotated/mirrored shapes must count as 'same'
- IScoreCalculator (IScoreCalculator.cs) — header names 'Siyam' as a decorator client
- GameSession.StartRound extension point (GameSession.cs header)

**Motora eklenmesi gerekenler:**

- Session joker inventory (as with all jokers)
- The score pipeline MUST carry shape identity: current IScoreCalculator signatures pass only cube counts (ScorePlacement(int cubesPlaced)), so a pure decorator can never see which shape was played — for Siyam the planned TurnReport-in-progress pipeline is a hard requirement, not a convenience as it is for çığ/dondurma. Bonus must still land in scoreGained before the RoundScore commit / step-6 threshold check in RoundEngine.ResolvePlacement
- If rotations count as 'same shape': a rotation-invariant canonical key (e.g. min CanonicalKey over the 4 rotations × mirror) — CanonicalKey today distinguishes rotated shapes
- TurnReport streak fields (shared with çığ/dondurma), per the TurnReport.cs header rule

**Durum (state):** Per-round: previous turn's shape key (string), current same-shape streak count, accumulated bonus. Cleared per round (new RoundEngine). Per-run: nothing. No randomness.

**Yaşam döngüsü:** Shape comparison + bonus at scoring step 1 (before the step-6 threshold check); state commit on TurnResolved; RedrawHand does not consume a turn so it must not affect the streak; reset at round start. Note the same physical card can legally recur within a round (discard reshuffled into draw before the threshold and on overtime clean sweeps), so same-shape repeats are common, and every 1x1 block is the same shape as every other 1x1. Overtime: designer silent — open question. Bonus-hand plays count as turns by default.

### dondurma — `M`

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (Action<TurnReport>) (RoundEngine.cs)
- TurnReport.PlacedCells.Count and TurnReport.Card.Shape.Size (TurnReport.cs, BlockShape.cs) — this turn's block size for the smaller-than-last-turn comparison
- TurnReport.TurnNumber and TurnReport.PlayedFromBonusHand (TurnReport.cs)
- IScoreCalculator / DefaultScoreCalculator (IScoreCalculator.cs) — header names 'dondurma' as a decorator client
- GameSession.StartRound extension point (GameSession.cs header)
- RoundEngine.ThresholdPassed (RoundEngine.cs)

**Motora eklenmesi gerekenler:**

- Exactly the same three additions as çığ: session joker inventory, the in-pipeline score seam (bonus into scoreGained before RoundScore commit and threshold check), and TurnReport streak fields
- Implementation note, not engine work: çığ and dondurma should share one parametrized size-comparison streak component (comparator = greater vs. smaller) so the streak rules stay identical by construction

**Durum (state):** Per-round: previous turn's placed cube count, current streak length, accumulated bonus level. Cleared per round (new RoundEngine). Per-run: nothing. No randomness.

**Yaşam döngüsü:** Mirror of çığ: comparison + bonus at scoring step 1 (before step-6 threshold check), state commit on TurnResolved, unaffected by RedrawHand (no TurnNumber increment), reset at round start. Overtime behavior unspecified by the designer ('streak mantığı aynen geçerli' inherits çığ's open questions). Bonus-hand plays count as turns by default.

### çığ — `M`

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (Action<TurnReport>) (RoundEngine.cs) — fires after every resolved placement; the documented joker hook for streak-state updates
- TurnReport.PlacedCells.Count and TurnReport.Card.Shape.Size (TurnReport.cs, BlockShape.cs) — this turn's block size for the bigger-than-last-turn comparison
- TurnReport.TurnNumber, TurnReport.PlayedFromBonusHand, TurnReport.StatusAfter (TurnReport.cs)
- IScoreCalculator / DefaultScoreCalculator (IScoreCalculator.cs) — header explicitly names 'çığ' as a decorator client; ScorePlacement(int cubesPlaced) already receives the placed cube count
- GameSession.StartRound extension point (GameSession.cs header) — where the session-scoped joker inventory will re-subscribe to each new RoundEngine
- RoundEngine.ThresholdPassed (RoundEngine.cs) — to gate the effect if the designer excludes overtime

**Motora eklenmesi gerekenler:**

- Session-level joker inventory on GameSession that subscribes jokers to each new RoundEngine in StartRound — only an extension-point comment exists today
- Score-modifier seam inside RoundEngine.ResolvePlacement: the streak bonus must be added to scoreGained BEFORE 'RoundScore += scoreGained' and the step-6 threshold check. A bonus paid from TurnResolved would not count toward the eşik and would desync GameSession.TotalScore (accrued from report.ScoreGained). Concretely: extend IScoreCalculator signatures / wrap DefaultScoreCalculator in the TurnReport-in-progress pipeline the IScoreCalculator.cs header already plans
- New TurnReport fields (e.g. int StreakLength, int StreakBonusScore) so UI/other jokers do not re-derive state, per the TurnReport.cs header rule

**Durum (state):** Per-round: previous turn's placed cube count, current streak length, accumulated bonus level ('birikebilir'). All cleared when a new RoundEngine is built (new round). Per-run: nothing. No randomness — IRandomSource not involved, determinism unaffected.

**Yaşam döngüsü:** Comparison + bonus at scoring step 1 of the turn order (must land in scoreGained before the step-6 threshold check); streak state commit on TurnResolved. RoundEngine.RedrawHand does not increment TurnNumber, so it must not touch the streak. Reset: per round start (fresh RoundEngine). Overtime: designer silent — unlike Seri tetik and Kayıt defteri, which explicitly exclude uzatma — so both 'active during uzatma?' and 'does the AwaitingAdvanceDecision pause / continue-into-overtime preserve the streak?' are open. Bonus-hand plays count as full turns in the engine (PlayFromBonus increments TurnNumber), so by default they would enter the comparison.

### İade — `M`

**Kullanacağı mevcut kancalar:**

- RoundEngine EXTENSION POINT header (RoundEngine.cs): abilities acting on round state are new public methods on RoundEngine — RedrawHand is the direct template for this one
- Hand.RemoveAt(int) (Hand.cs) — internal on purpose, only RoundEngine may mutate the hand, so the swap must be an engine method
- RoundDeck.Discard(BlockCard) (RoundDeck.cs) — 'sadece değiştirilen blok ıskartaya' maps directly onto it
- RoundEngine.DrawWithRules() (RoundEngine.cs, private) — the ONE draw path carrying the pre-threshold-recycle and overtime-loss rules; the replacement draw must route through it
- RoundEngine.CheckForNoPlayableMove() (RoundEngine.cs, private) — must re-run after the swap
- GameSession.PhaseChanged (GameSession.cs) — per-round charge-reset signal

**Motora eklenmesi gerekenler:**

- RoundEngine: a public single-card swap method (e.g. SwapHandCard(int handIndex)): discard exactly that card, draw one replacement via DrawWithRules, handle the null/loss outcomes (overtime: DrawPileEmptyAfterThreshold), then re-run the no-playable-move check — today only whole-hand RedrawHand exists
- GameSession: session joker inventory + player-facing activation path (shared prerequisite for all jokers)
- Charge accounting (2/round) exposed for the UI; joker-side state, no engine change

**Durum (state):** Per-round: remaining swap charges (starts at 2). Nothing per-turn or per-run beyond ownership.

**Yaşam döngüsü:** Player-activated while Status==InProgress; outside the turn order and consumes no turn (parallel to the documented RedrawHand behavior). Charges reset each round. The replacement draw obeys DrawWithRules as-is: before the threshold an empty draw pile recycles the discard (so the just-returned card can immediately be drawn back); in overtime an empty draw pile on the replacement draw is LossReason.DrawPileEmptyAfterThreshold. The designer gave no explicit overtime exception.

### Batak — `L`

**Bekleyen alt sistem:** bahis

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — per-turn check of sweep/deadline
- TurnReport.CleanSweep, TurnReport.TurnNumber, TurnReport.ScoreGained, TurnReport.RoundScoreAfter, TurnReport.StatusAfter (TurnReport.cs)
- RoundEngine.TurnNumber, RoundEngine.RoundScore, RoundEngine.ThresholdPassed public properties (RoundEngine.cs) — bet snapshot + overtime awareness
- GameSession.PhaseChanged event (GameSession.cs) — detect round start to open the first bet window
- IScoreCalculator decoration seam (IScoreCalculator.cs — its PURPOSE header explicitly names 'Batak payouts' as a decorator use-case)
- GameSession.StartRound extension point (GameSession.cs header: session-scoped joker inventory re-subscribes to each new RoundEngine) — comment-only seam, no inventory code exists yet

**Motora eklenmesi gerekenler:**

- RoundEngine: public primitive for an external effect to force a round loss (e.g. ForceLoss(LossReason)) plus a new LossReason member (BetMissed) — Loss setter and SetStatus are private, and LossReason has only 3 members
- RoundEngine: public score-injection primitive for non-placement payouts that updates RoundScore AND reaches GameSession.TotalScore — TotalScore currently accrues ONLY via TurnReport.ScoreGained in GameSession.OnTurnResolved, so an out-of-band payout would silently miss the market currency
- Bet-placement interaction API gated to the two legal windows (round start / immediately after a clean sweep) — the engine knows no player actions besides PlayFromHand/PlayFromBonus/DecideAdvance/RedrawHand
- Joker inventory on GameSession (session-scoped, survives RoundEngine replacement) — extension-point comment only
- Payout tunables: bet→multiplier curve (1 turn = huge, 100 turns = 0) and min/max bet, in a mutable config object per the RoundRules/ScoringConfig pattern
- TurnReport fields for bet events (bet active/deadline, payout granted, bet missed) so the UI does not re-derive state
- Decision on precedence when the bet deadline expires in the same turn a pending advance offer is created — ResolvePlacement step 7 currently lets a pending offer outrank a same-turn loss

**Durum (state):** Per-round: the active bet {target turn count, TurnNumber at placement, RoundScore snapshot at placement} and whether a bet window is currently open (round start / post-sweep). Nothing survives the round — the bet dies with the RoundEngine. Per-run: nothing beyond generic joker-inventory bookkeeping.

**Yaşam döngüsü:** Bet window opens at round construction and re-opens in turn-resolution step 3 whenever a clean sweep fires; the bet itself is optional. On each TurnResolved: if CleanSweep and a bet is active → payout = curve(betTurns) × (elapsedTurns/betTurns), pro-rated per the designer's 5/10 and 3/7 examples, bet cleared, new window opens; else if elapsedTurns reaches betTurns without a sweep → forced game loss (needs the new loss primitive). Bonus-hand plays increment TurnNumber in ResolvePlacement, so they count as turns for the deadline. Reset: implicit at round end (new RoundEngine). Overtime: designer is silent — but an overtime sweep simultaneously triggers discard reshuffle + N-card removal + an advance offer, and a threshold-pass turn can coincide with the bet deadline, so precedence and availability in overtime must be answered (see questions).

### Enfeksiyon — `L`

**Bekleyen alt sistem:** güçler

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — the per-turn spread tick
- GameBoard.GetCube(GridPos) and GameBoard.IsInside(GridPos) (GameBoard.cs) — reading neighbour cells for spread
- Cube.SourceCardId (Cube.cs) — whole-block membership; its doc comment names exactly this kind of block-wide effect as the reason it exists
- TurnReport.PlacedCells (TurnReport.cs) — detecting blocks newly placed touching the infection to start their 3-turn timers
- IRandomSource + RandomSourceExtensions.Shuffle (IRandomSource.cs) — any random neighbour/spread choice must go through the shared rng for determinism

**Motora eklenmesi gerekenler:**

- Per-cell mutable status layer (infected flag + countdown timer): Cube is a readonly struct holding only Kind and SourceCardId, and GameBoard has no SetCube/replace-in-place API — infection status cannot be stored on the board today
- GameBoard: explode/destroy a single arbitrary cell outside line resolution, with a scoring decision — ExplodeCell is private and coupled to ResolveFullLines' seen/exploded bookkeeping
- An official effect phase inside RoundEngine.ResolvePlacement: board mutations from a TurnResolved handler run AFTER the clean-sweep check (step 3) and status evaluation (steps 7/8), so infection explosions would leave stale NoPlayableMove verdicts and undetectable sweeps — spread/timers need a sanctioned step in the resolution order
- Cube-targeting activation API ('seçilen bir küp'): no interaction layer exists for a player to aim an effect at a cell
- TurnReport fields for infection events (cells infected this turn, cells exploded by infection) for the UI
- A defined deterministic spread order (cell iteration order or rng-driven) so replays from one seed stay identical

**Durum (state):** Per-round, board-scoped (dies with the round — GameBoard and RoundEngine are rebuilt every round): set of infected cells, per-cell 3-turn countdown timers for blocks placed onto/against the infection, and the spread frontier. Per-round: remaining activations (count unspecified). Per-run: nothing.

**Yaşam döngüsü:** Activation: player targets a cube at some moment (frequency and timing unspecified — see questions). Tick: infection spreads to neighbouring cells/blocks once per resolved turn; a block placed in contact with the infection starts a 3-turn timer and its cubes explode when it expires. Both the spread tick and the timed explosion must run INSIDE the turn resolution (ideally between line explosion and the clean-sweep check) rather than in a TurnResolved subscriber, or sweeps and loss checks will not see the post-infection board. All infection state resets implicitly at round end. Overtime: designer silent; note an infection explosion in overtime interacts with the sweep-only discard-recycle rule if it is allowed to trigger a temizlik.

### Kara delik — `L`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event + TurnReport.CleanSweep (RoundEngine.cs / TurnReport.cs) — sweep detection, fires for normal and overtime sweeps alike
- RoundEngine.AddBonusCard(BlockCard, BonusPlayOutcome) (RoundEngine.cs) — the file header explicitly lists 'Kara delik' among the intended AddBonusCard callers
- RoundDeck.Discard(BlockCard) public method (RoundDeck.cs) — the alternative 'ıskartaya ekle' reading from the design note
- BonusPlayOutcome enum (BonusSlot.cs) — controls the void card's post-play fate (ExpireFromRound vs ToDiscard)
- BlockShape.FromCells (BlockShape.cs) — building the fixed 1x1 shape
- CubeKind enum + CubeRules (Cube.cs) — the extension point; 'Void' is already named in the future-kinds comment
- TurnReport.CardsRemovedForRound and TurnReport.DiscardWasReshuffled (TurnReport.cs) — observing whether an overtime sweep swallowed or recycled the void card

**Motora eklenmesi gerekenler:**

- Runtime card minting: GameSession.nextCardId is private and there is no public factory — a joker cannot create a BlockCard with a unique id today; need a session-level factory for round-temporary cards that never join OwnedCards
- CubeKind.Void member + CubeRules answers for it (CountsForCleanSweep? IsDestructible?) — currently only a comment
- GameBoard.CanPlace generalization: allow a placement to overlap a Void cube — today ANY occupied cell vetoes placement, so 'üstüne blok koyulmasına izin verir' is impossible
- GameBoard/RoundEngine: instant destruction of cubes landing on a void cell, outside ResolveFullLines (ExplodeCell is private and tied to line resolution), with an explicit scoring decision and a new TurnReport field for the destroyed cells
- Cap accounting for the maximum obtainable void blocks (scope per-round vs per-run undecided) as a tunable
- A marker distinguishing the void card from normal cards — BlockCard is intentionally slim (Id + Shape only), so a card-type flag or subtype is a new capability (BlockCard.cs names per-card traits as the extension point)

**Durum (state):** Per-round or per-run (designer ambiguous): count of void blocks granted vs. the cap. Per-round: identity of the live temporary void card(s) so they can be excluded from persistence — they vanish naturally at round end because every round rebuilds RoundDeck from GameSession.OwnedCards, provided the minted card is never added to OwnedCards.

**Yaşam döngüsü:** Trigger: turn-resolution step 3, on every clean sweep (until the cap). Ordering trap: TurnResolved fires at the END of ResolvePlacement, i.e. AFTER an overtime sweep has already done ShuffleDiscardIntoDraw + RemoveRandomFromDraw — a void card injected into the discard from the handler lands after the reshuffle, and in overtime the discard is only recycled by the NEXT sweep (before the threshold, DrawWithRules' refill recycle will pick it up). If instead it goes to the bonus hand (per the code comment), it is playable immediately but a bonus play burns the top draw card, which in overtime can itself trigger the DrawPileEmptyAfterThreshold loss. Round end: temp card disappears with the RoundDeck; cap-reset scope unknown. Overtime: no designer exception — the joker keeps triggering on overtime sweeps.

### Kayıt defteri — `L`

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — per-turn reaction point
- TurnReport.CubesExploded (TurnReport.cs) — per-turn exploded-cube count to accumulate
- TurnReport.CleanSweep (TurnReport.cs) — detects natural sweeps that must stop counting while held
- RoundEngine.ThresholdPassed (RoundEngine.cs) — overtime detection for the 'unusable in uzatma' rule
- RoundEngine.Board + GameBoard.Width / GameBoard.Height (GameBoard.cs) — the cell count the counter is compared against
- RoundRules (RoundRules.cs) — the documented live-read mutation point where a sweep-disabling flag would live
- GameSession.StartRound EXTENSION POINT (GameSession.cs) — session-scoped joker inventory subscribes to each new RoundEngine

**Motora eklenmesi gerekenler:**

- RoundEngine step 3: the natural clean-sweep check (explosion.LineCount > 0 && Board.IsCleanForSweep()) is hardcoded; needs a live-read RoundRules flag (e.g. NaturalCleanSweepDisabled) so holding the joker suppresses natural temizlik
- GameBoard: clear/explode ALL cubes outside line resolution — ExplodeCell is private and only reachable via ResolveFullLines; must update OccupiedCount and return the cleared cells
- RoundEngine: a joker-triggered temizlik path that awards the sweep bonus into RoundScore in the SAME turn — RoundScore has a private setter and TotalScore only accumulates via TurnReport.ScoreGained, so no external score-grant primitive exists today
- An in-turn joker phase between line explosion (step 2) and threshold/status (steps 6-8) — TurnResolved fires after status is final, too late for a trigger whose score must count toward that turn and whose board clear must precede CheckForNoPlayableMove
- TurnReport: a field for a joker-triggered sweep and its cleared cells (UI animation, interop with other sweep-reactive jokers)
- GameSession: the joker inventory subsystem itself (shared prerequisite for every joker)

**Durum (state):** Per-round: cumulative counter of exploded cubes, compared against the current Board.Width*Height (board size changes per round via DefaultRoundProgression). Resets at round start; whether it resets to 0 or carries the overshoot remainder after triggering is an open designer question. No per-run state.

**Yaşam döngüsü:** Accumulates after step 2 (line explosions) of every turn; when it reaches the board's cell count it triggers a board clear + temizlik within the same turn, before the step-6 threshold check so the score counts. While held, natural clean sweeps no longer count as temizlik (step-3 suppression). Per-round reset: counter to 0, board size re-read. Uzatma: explicitly unusable once ThresholdPassed is true — the counter can no longer trigger. Danger flagged: if the natural-sweep suppression also persists in overtime, no temizlik is possible at all, and since overtime only recycles the discard via clean sweeps (DrawWithRules loses on an empty draw pile), overtime becomes a guaranteed eventual loss.

### Kentsel Dönüşüm — `L`

**Kullanacağı mevcut kancalar:**

- RoundEngine.StatusChanged with RoundStatus.Advanced (RoundEngine.cs) / GameSession.PhaseChanged to GamePhase.Market (GameSession.cs) — the 'her round sonu' trigger
- GameSession.StartRound / GameSession.LeaveMarket (GameSession.cs) — the only place a next-round board is created; the persistent modifier must be applied there
- IRoundProgression.GetRound (IRoundProgression.cs) + RoundConfig.BoardWidth/BoardHeight (RoundConfig.cs) — the base geometry the modifier stacks on
- GameBoard resizing EXTENSION POINT (GameBoard.cs header) — explicitly anticipates board-resizing effects: 'create a new board and copy cubes, or generalize this class; do NOT assume Width/Height are round-constant'

**Motora eklenmesi gerekenler:**

- A run-scoped board-shape modifier owned by GameSession and injected into RoundEngine construction — the RoundEngine constructor hardcodes new GameBoard(config.BoardWidth, config.BoardHeight) with no seam for extra cells or altered dimensions
- If 'bir blokluk yer' means cell(s) outside the rectangle: non-rectangular board support (an occupancy/validity mask) touching IsInside, CanPlace, ResolveFullLines full-line detection (what a 'full' row/column means with extra cells), IsCleanForSweep, AnyPlacementExists / GetValidOrigins loop bounds, and ToAscii — the board is rectangular in every algorithm today
- A round-end decision step/API for choosing where the space opens (if player-chosen), fitting into the Market phase flow
- Reconciliation with DefaultRoundProgression's own board growth (6x6 up to 10x10): a rule for what happens to previously opened cells when the base rectangle later expands over or past them
- GameSession: joker inventory subsystem (shared prerequisite)

**Durum (state):** Per-run: the accumulated permanently opened extra cells (positions or a size delta), growing by one per completed round while held. Explicitly 'kalıcı', so it must survive RoundEngine replacement every round — and possibly even the joker being sold (open question).

**Yaşam döngüsü:** Triggers once per round end (RoundStatus.Advanced / entry into Market), outside any turn. The accumulated modifier is applied at every subsequent StartRound when the new GameBoard is built. No per-round reset — the effect is cumulative and permanent by design. No overtime interaction (it fires between rounds). Losing the round means no trigger (the run ends).

### Robot süpürge — `L`

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — the 'her turdan sonra' trigger
- RoundEngine.Board (RoundEngine.cs) + GameBoard.OccupiedCount / GameBoard.GetCube (GameBoard.cs)
- GameBoard.IsCleanForSweep() (GameBoard.cs) — CubeRules-aware check of whether its explosion emptied the board
- IRandomSource + RandomSourceExtensions (IRandomSource.cs) — the run's seeded rng; GameSession holds it privately, so the joker layer must be handed the same instance for determinism
- IScoreCalculator.ScoreCleanSweep (IScoreCalculator.cs) — if its triggered temizlik pays the bonus
- RoundEngine.ThresholdPassed (RoundEngine.cs) and RoundRules.OvertimeCardsRemovedPerCleanSweep (RoundRules.cs) — if overtime sweep consequences apply to its temizlik

**Motora eklenmesi gerekenler:**

- GameBoard: deterministic enumeration of occupied cells — the cells array is private and no occupied-cell listing exists; needed to pick a random cube reproducibly through IRandomSource (fixed x/y iteration order)
- GameBoard: explode a single arbitrary cell outside line resolution, without scoring, updating OccupiedCount
- RoundEngine: a generalized 'temizlik happened' path callable outside ResolvePlacement — the sweep guard, sweep scoring, overtime consequences (Deck.ShuffleDiscardIntoDraw + RemoveRandomFromDraw + advance offer) and SetStatus are all private and inlined in step 3; a vacuum-triggered sweep must bypass the 'same-turn line explosion' guard by design, which generalizes a core rule
- RoundEngine: an external score-grant primitive into RoundScore (private setter; TotalScore only flows through TurnReport.ScoreGained) if its explosions or its temizlik award points
- Post-turn ordering: the vacuum must run before (or force a re-run of) CheckForNoPlayableMove — it frees at least one cell every turn and can change the loss verdict, but status is final before TurnResolved fires
- TurnReport field or event channel reporting which cube(s) it exploded (UI animation + Kayıt defteri counter interop)
- GameSession: joker inventory subsystem plus rng plumbing to jokers (shared prerequisite)

**Durum (state):** Per-round: cubesPerActivation (starts at 1, +1 for each vacuum-triggered temizlik, resets at round start) and cooldownTurnsRemaining (set to 2 when a temizlik is triggered, decremented each turn, resets at round start). Nothing per-run.

**Yaşam döngüsü:** After every resolved turn, if not on cooldown: pick cubesPerActivation random occupied cubes via IRandomSource and explode them. If that empties the board (IsCleanForSweep) it counts as temizlik: cubesPerActivation += 1 for the rest of the round and cooldown = 2 turns. Per-round reset: amount back to 1, cooldown cleared, both structural via the fresh RoundEngine subscription. Uzatma: no designer exemption, so it presumably keeps running; whether its temizlik fires the overtime sweep branch (discard recycle, card removal, advance offer) is open — if yes, the vacuum can autonomously generate advance offers and card removals in overtime without player intent.

### Buzluk — `XL`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- CubeRules.CountsForCleanSweep (Cube.cs) — exactly the seam for 'buz temizliği engellemez'; GameBoard.IsCleanForSweep already consults it per cube (GameBoard.cs)
- CubeRules.IsDestructible (Cube.cs) — ice stays line-explodable; GameBoard.ExplodeCell consults it
- GameBoard.Width / Height / GetCube (GameBoard.cs) — wall adjacency test (x==0, x==Width-1, y==0, y==Height-1); note the board-resize extension point warns Width/Height may change mid-round later
- IScoreCalculator.ScoreLineExplosion decoration (IScoreCalculator.cs) — the header explicitly invites extending the signatures, which ice's extra points require
- RoundEngine.TurnResolved event (RoundEngine.cs) — observation only; freezing must happen earlier, see missing

**Motora eklenmesi gerekenler:**

- Water + Ice CubeKinds and their CubeRules rows (ice: CountsForCleanSweep=false, IsDestructible=true) — elemental subsystem
- GameBoard: in-place kind transformation (freeze water→ice preserving SourceCardId) — same primitive as Taşkın/Yangın need
- A defined freeze step INSIDE turn resolution before the clean-sweep check (step 3): the resolution order has no elemental post-placement step yet (the GameBoard header calls it 'a future post-placement resolution step'), and a turn whose last counting water cubes freeze must be evaluated consistently against the sweep guard
- Kind-aware explosion results: LineExplosionResult.ExplodedCells stores positions only and ExplodeCell nulls the cube before the caller sees it (GameBoard.cs), so 'an ice cube exploded → extra points' cannot be computed today; explosion results must record each exploded cube's kind and the scorer must see them
- TurnReport field for freeze events (UI animation)

**Durum (state):** None — continuous passive rule with no charges; the frozen cubes themselves are board state.

**Yaşam döngüsü:** Continuous: whenever a water cube occupies a wall cell (placed there, or converted there e.g. by Taşkın) it freezes; the check runs inside every turn's resolution before the sweep check (step 3). No per-round reset. Overtime: designer silent — assume always active. Adversarial edge: freezing alone can leave ONLY ice on the board — the sweep guard (requires a same-turn line explosion, RoundEngine step 3) means that turn is NOT a temizlik, but every later turn with any line explosion re-qualifies as temizlik while only ice remains, enabling repeated sweeps (and in overtime, repeated card-removal/offer cycles).

### Damlaya Damlaya Göl Olur — `XL`

**Bekleyen alt sistem:** market

**Kullanacağı mevcut kancalar:**

- GameSession.PhaseChanged event (GameSession.cs) — observes Market↔Round transitions
- GameSession.LeaveMarket() (GameSession.cs) — the exact moment the market closes and the next round is created (RoundNumber++ then StartRound)
- GameSession extension-point header (GameSession.cs) — joker inventories are session-scoped; this joker's armed flag spans the market→round boundary, so session scope is mandatory
- RoundEngine.StatusChanged event (RoundEngine.cs) — round-end detection (Advanced/Lost) for disarming a round-long bonus
- ScoringConfig / IScoreCalculator (ScoringConfig.cs, IScoreCalculator.cs) — if the bonus is a next-round multiplier: mutate for one round, revert at round end

**Motora eklenmesi gerekenler:**

- The market itself: MarketStub (MarketStub.cs) is an empty class — no purchase API, no per-visit transaction record, nothing observable. The joker's core condition ('marketten bir şey almazsa') is currently untestable and would be trivially always-true. Needs at minimum a per-visit purchase counter or a Purchased event on the market
- If the flat-score interpretation wins: RoundEngine has no way to grant score outside a placement — RoundScore's setter is private and score only changes inside ResolvePlacement. Needs e.g. RoundEngine.AddExternalScore(int) that also runs the step-6 threshold logic; otherwise a start-of-round bonus that meets the eşik would not produce the advance offer until after the first placement
- Session joker inventory (as with all jokers)

**Durum (state):** Per-market-visit: a 'purchase made' flag, reset each time the market opens. Per-round: the armed bonus (value or multiplier) for exactly the one round following a purchase-free market, consumed/cleared at that round's end. Both live at session level because they cross the round/market boundary. No randomness.

**Yaşam döngüsü:** Observe purchases during GamePhase.Market; on LeaveMarket with zero purchases, arm the bonus for the round about to start; apply it either as a one-shot grant at round start or live throughout the round (depends on the designer's answer); disarm on RoundStatus Advanced/Lost or when the next market opens. Round 1 has no preceding market, so the first possible trigger is round 2. Overtime: if it is a round-long multiplier it presumably stays active through that round's uzatma (designer silent). GameOver makes the armed state moot.

### Parazit — `XL`

**Bekleyen alt sistem:** market, karta takma

**Kullanacağı mevcut kancalar:**

- BlockCard (BlockCard.cs) — header EXTENSION POINT explicitly reserves 'an attached joker cube ("Parazit")' as future per-card state
- Cube.SourceCardId (Cube.cs) — maps every board cube back to its owning card; its doc comment already names whole-block effects as the use case
- GameBoard.Place (GameBoard.cs) — stamps 'new Cube(CubeKind.Normal, card.Id)' per cell, iterating card.Shape.Cells in order; the attached cell is identified here at placement time
- GameBoard.ResolveFullLines + LineExplosionResult.ExplodedCells (GameBoard.cs) — the only current cube-destruction path, i.e. where 'küp kırılırsa' can happen today
- RoundEngine.TurnResolved event (RoundEngine.cs) — where the joker layer detects the break, once exploded-cube identity is reported (see missing)
- GameSession.Phase / GamePhase.Market and GameSession.OwnedCards (GameSession.cs) — the phase gate for the attach action and the deck ('oyun destesi') the target block lives in

**Motora eklenmesi gerekenler:**

- Card-attachment subsystem: a BlockCard field holding (attached joker, cell offset within Shape.Cells) + a market-phase attach API that enforces 'Parazit is held', 'max 1 joker per block', and the one-placement allowance
- Cube identity within its card: Cube stores only Kind + SourceCardId, not WHICH cell of the shape it is — add a cell index to Cube, or derive the offset from board position minus origin at explosion time
- GameBoard.ExplodeCell nulls the cell and discards the Cube value; LineExplosionResult / a new TurnReport field must report destroyed cube identities (SourceCardId + cell offset), not just GridPos positions, so 'the attached cube broke' is observable — and every FUTURE destruction path (Buldozer, Robot süpürge, Boşluk bloğu, Enfeksiyon) must feed the same report
- A permanent joker-deletion primitive (remove a joker from the run mid-turn) plus the joker pipeline supporting jokers that are active while hosted on a card OUTSIDE the inventory, deactivated the moment the cube breaks
- Market phase itself (MarketStub is empty) — the attach action is a market-phase interaction

**Durum (state):** Attachment registry: (attached joker, target card Id, cell offset). Scope: per-run — survives round transitions and board resets, ends only when that cube is broken or (pending designer answer) when Parazit leaves the inventory. Possibly a per-market-phase 'placement used' flag depending on the answer to the limit question.

**Yaşam döngüsü:** Attach: market phase only. Effect of the attached joker: continuous from attach until break (per designer text). Break detection: turn-resolution step 2 (full-line explosions inside Board.ResolveFullLines); deletion lands mid-turn, which creates a trigger-ordering question for that turn (see questions). Round end recreates board and piles without 'breaking' any cube, so the attachment persists across rounds; overtime clean-sweep removals (RoundDeck.RemoveRandomFromDraw) take the card out of the round but do not break the cube, so the joker also survives those. No overtime-specific rule was given by the designer.

### Powerbank — `XL`

**Bekleyen alt sistem:** güçler

**Kullanacağı mevcut kancalar:**

- GameSession.PhaseChanged event (GameSession.cs) — reset the once-per-round charge when the phase returns to Round
- RoundEngine.StatusChanged event (RoundEngine.cs) — round-end detection
- RoundEngine extension-point header (RoundEngine.cs: 'Powers (güçler) = new public methods here') — comment only; zero power code exists anywhere in Core

**Motora eklenmesi gerekenler:**

- The entire powers subsystem: power definitions, a session/round-scoped power inventory, a per-power use/charge model (current vs max uses), and activation methods on RoundEngine — today this is only an extension-point comment
- A refill API on that subsystem: enumerate held powers, query charges, restore one use (Powerbank is a one-line consumer of it)
- Player interaction to choose which power gets refilled and when (unless the designer wants it automatic)
- Per-round usage flag for the joker itself, plus the generic joker inventory on GameSession that survives RoundEngine replacement (extension-point comment only)

**Durum (state):** Per-round: a single used-this-round flag (reset every round). Per-run: nothing.

**Yaşam döngüsü:** Reset: at every round start (new RoundEngine via GameSession.StartRound / PhaseChanged → Round). Trigger: at most once per round, either on explicit player activation or automatically when a power's uses hit zero — undecided. It does not touch the turn resolution order at all; it only mutates future power-charge state, so it can act between turns. Overtime: the designer excluded overtime explicitly for Kayıt defteri and Seri tetik but said nothing here, so presumably it remains usable during overtime if the round's charge is unspent — worth confirming.

### Simya — `XL`

**Bekleyen alt sistem:** element küpleri, market

**Kullanacağı mevcut kancalar:**

- GameSession.Market property (GameSession.cs) → MarketStub (MarketStub.cs) — today an empty placeholder whose header confirms blocks will be sold
- GameSession.PhaseChanged event + GamePhase.Market (GameSession.cs) — detect market-phase entry
- BlockCard (BlockCard.cs) — header reserves per-card elemental types and shop pricing exactly where Simya's dual-element data would live
- IRandomSource / SeededRandom (IRandomSource.cs) — element-pair rolls must flow through the session rng for determinism

**Motora eklenmesi gerekenler:**

- The entire market offer-generation system (MarketStub has no members) — including the base 'elemental blocks appear in the market' feature Simya modifies
- Elemental data model on cards AND dual-element support: Cube.Kind is a single enum value (Cube.cs) and GameBoard.Place stamps CubeKind.Normal per cube (GameBoard.cs) — two simultaneous elements need a flags/set representation plus CubeRules able to answer for combinations
- A behavior matrix for element combinations (hem ateş hem su etc.): every elemental interaction (explosion chains, spreading, freezing) must define what happens when a cube has two elements — this is rules design, not just data
- An offer-generation pipeline seam where a joker can transform generated offers before they are shown (Simya rewrites elemental offers into dual-element ones)

**Durum (state):** None — passive modifier of market offer generation; the dual-element cards it produces live on in GameSession.OwnedCards.

**Yaşam döngüsü:** Active only during the market phase, at offer-generation time; it never touches turn resolution, has no per-round reset, and overtime is irrelevant to it. Its effect is permanent on bought cards for the rest of the run.

### Taşkın — `XL`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- GameBoard.GetCube / IsInside / Width / Height (GameBoard.cs) — locate water cubes and their neighbor cells
- Cube.SourceCardId (Cube.cs) — conversion should preserve provenance (whole-block effects like 'Kazı çalışması' depend on it)
- RoundEngine.TurnResolved event (RoundEngine.cs) — the trigger point if the effect turns out to be automatic
- GameSession.PhaseChanged event (GameSession.cs) — per-round charge reset; SetPhase always fires for GamePhase.Round on every StartRound

**Motora eklenmesi gerekenler:**

- Water CubeKind + CubeRules behavior — elemental subsystem
- GameBoard: a public in-place cube-transformation primitive, e.g. ReplaceCube(pos, kind) preserving SourceCardId — the cells array is private and Cube is an immutable struct, so no outside code can currently change a cube's kind
- An activation surface: if the player triggers it, jokers need an activate-ability API (the RoundEngine header plans jokers as passive event subscribers only; powers are planned as public RoundEngine methods); if automatic, a defined step in the resolution order
- Joker inventory + per-round joker state plumbing in GameSession (only an extension-point comment exists)
- TurnReport field for kind conversions so the UI can animate the flood
- A shared 'spread element X from all X-cubes to neighbors, once per round' primitive parameterized by CubeKind would serve both Taşkın and Yangın

**Durum (state):** Per-round: 1 charge (used/unused flag). Nothing else.

**Yaşam döngüsü:** Once per round; the charge resets when a new round starts (new RoundEngine). The trigger moment is unresolved (see questions) — if automatic, it must run as a post-placement resolution step so the same turn's sweep check (step 3) and line detection see the converted cubes. Overtime: uzatma is the same round continuing, so the charge does NOT refresh at eşik or on overtime sweeps unless the designer says otherwise. Conversion of 'all neighbors' is deterministic — no rng needed; any added randomness must go through IRandomSource.

### Tutuştur — `XL`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- GameBoard.ResolveFullLines + private ExplodeCell (GameBoard.cs) — the file header explicitly assigns 'fire chain' behavior to this seam / a future post-placement resolution step
- LineExplosionResult.ExplodedCells / LineCount (GameBoard.cs) — the current explosion result the chain must extend
- CubeRules.IsDestructible (Cube.cs) — chain must still respect indestructible cubes
- TurnReport.CubesExploded / ScoreGained / CleanSweep (TurnReport.cs) — where the chain's results must be reported
- RoundEngine.TurnResolved event (RoundEngine.cs) — observation only; the chain must resolve in-turn, before the sweep (step 3) and eşik (step 6) checks

**Motora eklenmesi gerekenler:**

- Fire CubeKind — elemental subsystem
- Kind-aware explosion reporting: ExplodeCell nulls the cell immediately, so nobody can currently know a fire cube was among the exploded — explosion results must record cube kinds
- A secondary-explosion primitive on GameBoard: explode an arbitrary cell set as a chain wave within the SAME turn resolution, feeding its cubes into scoring and the step-3 clean-sweep check (generalizing step 2 into an iterative loop)
- A unified 'cube exploded' seam so the chain triggers on ANY source of fire-cube explosions (future Buldozer, Robot süpürge, elmas kazma...), not only full lines
- Scoring definition for chain-exploded cubes (new IScoreCalculator method, or inclusion in ScoreLineExplosion's cubesExploded count)
- TurnReport field distinguishing chain-exploded cells from line-exploded cells (UI + other counters)

**Durum (state):** None — passive, always on, unlimited triggers; no per-round data.

**Yaşam döngüsü:** Triggers inside resolution step 2 the moment at least one fire cube explodes; the chain (all fire cubes on the board) resolves in the same turn, its cleared cells feed the step-3 clean-sweep check (the same-turn line-explosion guard is satisfied when the trigger was a line) and its points land before the step-6 eşik check. No per-round reset. Overtime: designer silent — assume always active; a chain-triggered sweep in overtime drives the normal reshuffle/card-removal/offer cycle.

### Yangın — `XL`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- GameBoard.GetCube / IsInside / Width / Height (GameBoard.cs) — locate fire cubes and their neighbor cells
- Cube.SourceCardId (Cube.cs) — conversion should preserve provenance
- RoundEngine.TurnResolved event (RoundEngine.cs) — the trigger point if the effect is automatic
- GameSession.PhaseChanged event (GameSession.cs) — per-round charge reset on round start

**Motora eklenmesi gerekenler:**

- Fire CubeKind + CubeRules behavior — elemental subsystem
- GameBoard: public in-place cube-transformation primitive preserving SourceCardId (same primitive Taşkın needs; cells is private, Cube immutable)
- Activation surface (player-triggered vs automatic) and joker inventory plumbing in GameSession — same gaps as Taşkın
- TurnReport field for kind conversions (UI animation)
- The shared element-spread primitive parameterized by CubeKind (one implementation covers Yangın and Taşkın)

**Durum (state):** Per-round: 1 charge (used/unused flag). Nothing else.

**Yaşam döngüsü:** Mirror of Taşkın with fire: once per round, charge resets on new round (new RoundEngine); trigger moment unresolved; if automatic it must run inside resolution before the sweep/line checks of the same turn. Overtime: same round, so no recharge by default. Extra interaction to plan for: cubes Yangın converts to fire become fuel for Tutuştur's chain in later explosions.

### elmas kazma — `XL`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- TurnReport.CleanSweep + RoundEngine.TurnResolved event (TurnReport.cs, RoundEngine.cs) — sweep detection (but fires too late for same-turn scoring, see missing)
- CubeRules.CountsForCleanSweep + CubeRules.IsDestructible (Cube.cs) — the exact seams that will make obsidian sweep-exempt and explosion-proof; GameBoard.IsCleanForSweep and GameBoard.ExplodeCell already consult them per cube (GameBoard.cs)
- GameBoard.GetCube / Width / Height (GameBoard.cs) — scan the board for obsidian cells
- ScoringConfig (ScoringConfig.cs) — mutable, read live; a natural home for an obsidian-points tunable

**Motora eklenmesi gerekenler:**

- Obsidian itself: a CubeKind member plus CubeRules rows (CountsForCleanSweep=false, IsDestructible=false) — elemental subsystem
- GameBoard: a public primitive to explode a specific arbitrary cell set OUTSIDE ResolveFullLines that can bypass IsDestructible (ExplodeCell is private, rule-respecting, and would refuse obsidian; OccupiedCount must stay consistent)
- An in-resolution hook at the clean-sweep step (step 3): points added from a TurnResolved subscriber would miss the same-turn eşik check (step 6) and RoundScoreAfter, and RoundScore's setter is private anyway
- Scoring: no IScoreCalculator method covers 'obsidian exploded on sweep' — needs a new method or config value
- TurnReport: field(s) recording the joker-triggered obsidian explosion (cells + score) so the UI can animate it and other jokers can react

**Durum (state):** None — stateless trigger; obsidian cubes on the board are the only relevant state and they belong to the board.

**Yaşam döngüsü:** Fires exactly when a clean sweep is confirmed (resolution step 3), after the sweep bonus and before the threshold check (step 6) so its points count toward eşik the same turn. Nothing to reset per round. Overtime: designer silent — assume it fires on overtime sweeps too (the sweep's reshuffle/card-removal/offer machinery in step 3 is unaffected). Note: exploding obsidian can never re-trigger a sweep, since obsidian never counted for sweep in the first place.

### ihale — `XL`

**Bekleyen alt sistem:** market, satış

**Kullanacağı mevcut kancalar:**

- GameSession.PhaseChanged event (GameSession.cs) — SetPhase deliberately re-fires for GamePhase.Round ('Phase == newPhase && newPhase != GamePhase.Round' guard), so PhaseChanged(Round) fires at EVERY round start including round 1: a clean 'her raunt başında' signal outside turn resolution
- GameSession.RoundNumber (GameSession.cs) — available if the extra price scales with round
- IRandomSource.NextInt (IRandomSource.cs) — the mandated deterministic primitive for the random joker pick
- MarketStub.cs header — confirms the design ihale plugs into: 'held jokers/powers can be sold; the currency is TotalScore'
- GameSession header extension point (GameSession.cs) — the joker inventory ihale picks its target from will live here

**Motora eklenmesi gerekenler:**

- Joker inventory with a stable, deterministic iteration order (the uniform random pick over it must be replayable from the run seed)
- Per-joker base sell price + an 'extra price' (ek fiyat) modifier the market reads when listing a held joker for sale
- Market sell action + a 'joker sold' notification/event so ihale can clear its lock — MarketStub is an empty class, no sell flow exists at all
- Deterministic rng access for the joker layer: GameSession.rng is private; the joker system must be handed the run's IRandomSource (hard rule: no un-seeded randomness in Core)
- A joker-removed (destroyed-without-sale) notification if the lock must clear when the target dies by other means, e.g. a Parazit-attached target's cube breaking

**Durum (state):** Current auction target (joker reference/id) + the extra price amount. Scope: per-run — explicitly persists across rounds until that exact joker is sold ('o joker satılana kadar başka bir jokere ihale çıkmaz'). No per-round or per-turn state.

**Yaşam döngüsü:** Triggers at every round start (PhaseChanged → Round, before any turn is played): if no unsold target is pending, pick one joker uniformly from the inventory (self included) via IRandomSource and attach the extra price; if a target is still pending, do nothing that round. The lock clears only in a market phase when the target is sold; selling ihale itself while it is the target ends the effect naturally. No per-round reset of the pending target. Overtime: no interaction — the trigger sits entirely outside turn resolution.

### midas — `XL`

**Bekleyen alt sistem:** element küpleri

**Kullanacağı mevcut kancalar:**

- RoundEngine.TurnResolved event (RoundEngine.cs) — per-turn reaction point for a session-held joker
- RoundEngine.Hand property + Hand.Cards / Hand.Count (RoundEngine.cs, Hand.cs) — read the held cards to find gold blocks
- RoundEngine.BonusHand property + BonusSlot.Card (RoundEngine.cs, BonusSlot.cs) — 'bonus el dahil' requires scanning bonus slots too
- IScoreCalculator decoration point (IScoreCalculator.cs — file header names decoration the main joker hook) — if the gold bonus turns out to be score
- GameSession.PhaseChanged event + GameSession.CurrentRound (GameSession.cs) — re-subscribe the joker to each new RoundEngine (PhaseChanged fires GamePhase.Round on every StartRound)
- BlockCard (BlockCard.cs) — file header explicitly reserves the spot where per-card elemental type will live

**Motora eklenmesi gerekenler:**

- Elemental card data: BlockCard has no element field and CubeKind (Cube.cs) has only Normal — the concept 'gold block' does not exist
- The gold block's BASE bonus mechanic itself is defined nowhere in Core; midas only changes its trigger condition, so the bonus must exist first
- If the bonus is score: a joker score-injection path into the resolving turn — RoundEngine.RoundScore has a private setter and TurnResolved fires only after ScoreGained, the threshold check (step 6) and status update are finalized
- An engine-defined hand snapshot moment: RefillHand runs inside ResolvePlacement (step 5) before TurnResolved fires, so 'held during this turn' is ambiguous without a pre-refill snapshot (e.g. a TurnReport field or a pre-scoring hook)

**Durum (state):** None of its own — stateless scan of Hand + BonusHand at the evaluation moment (per-turn). Gold identity lives on the cards.

**Yaşam döngüsü:** Evaluates every turn at whatever moment the gold-block bonus normally triggers (that moment is itself still undefined); the hand must be read BEFORE resolution step 5 (refill), because the hand contents change mid-resolution. No per-round reset. Overtime: designer silent — assume always active; bonus-hand plays explicitly included by the designer note.

## 5. Cevap bekleyen sorular

Toplam 106 soru. Hepsini birden cevaplaman gerekmez — sadece sıradaki dalgada
yazılacak jokerlerin soruları bloklayıcıdır.

### Altın Kumbara

- Tur / temizlik / raunt bitirme tetiklerinin değer artışları aynı miktar mı, yoksa üçü ayrı ayrı mı ayarlanacak?
- Uzatmadaki turlar ve temizlikler de değer kazandırıyor mu, yoksa Kayıt defteri'ndeki gibi bu joker uzatmada devre dışı mı?
- "Raunt bitirme" sadece Advance kararı mı — kaybedilen raunt değer vermez varsayıyorum, doğru mu?

### Cimri Kumbara

- Bonus elden oynanan blok da tur sayılıyor (kod bonus oynayışını normal tur gibi çözüyor, TurnNumber artıyor) — Cimri bundan da değer kazansın mı?
- Uzatma turlarında değer birikimi devam ediyor mu? Kayıt defteri ve Seri tetik'te uzatmayı açıkça kapatmışsın ama bunda belirtmemişsin; oyuncu uzatmayı bilerek uzatıp değer farmlayabilir.

### Domuz Kumbarası

- "Raunt sonu" sadece Advance edilen rauntlar mı? Uzatmada oynamaya devam edip sonra kaybeden oyuncu o rauntun değer artışını hiç alamayacak — istediğin davranış bu mu?
- Satış fiyatı = jokerin baz fiyatı + biriken değer mi, yoksa biriken değer fiyatın tamamı mı? (Bu soru üç kumbara jokeri için de geçerli.)

### Renovasyon

- Uzatmada Renovasyon kullanılabilsin mi? Mevcut RedrawHand ıskartayı desteye karıştırıyor; uzatmada ıskarta normalde sadece temizlikle geri dönüyor. Uzatmada hak tamamen yasak mı olsun, karıştırmasız mı çalışsın, yoksa aynen mi kalsın?
- El önce ıskartaya atılıp ıskarta desteye karıştırıldıktan SONRA yeni el çekiliyor; yani aynı kartlar hemen geri gelebilir. Bu kabul mü, yoksa yeni el eski el hariç mi çekilsin (önce çek, sonra ıskartaya at)?
- Desteler eli doldurmaya yetmeyecek kadar azaldıysa hakkı kullanmak anında kaybettirir (el doldurulamadı kuralı). Bu durumda kullanım engellensin mi, yoksa oyuncu bile bile bu riske girebilsin mi?

### bereket

- '+ şeklinde patlama' tanımın nedir: aynı turda en az bir satır VE en az bir sütunun birlikte patlaması yeterli mi (dikdörtgen tahtada bunlar her zaman kesişip artı oluşturur), yoksa başka bir şey mi kastediyorsun?
- Aynı turda birden fazla kesişim olursa (örn. 2 satır + 1 sütun = 2 kesişim) artış bir kez mi, kesişim başına mı uygulanır?
- Kalıcı artış tetiklendiği turun puanına da işler mi, yoksa sonraki turlardan itibaren mi? (İlkiyse motor içi kancaya ihtiyacım var, ikincisi bedava.)
- Artış sabit sayı mı yoksa yüzde/çarpan mı? ('Biraz arttırır' iki şekilde de okunabiliyor; çarpansa ScoringConfig'e yeni bir alan gerekir.)
- Joker satılırsa o ana kadar birikmiş kalıcı artış kalır mı, geri alınır mı?

### Buldozer

- 4 turluk sayaç raunt geçişinde sıfırlanıyor mu, yoksa rauntlar arasında taşınıyor mu?
- 4. turda oyuncunun hamlesi sonrası 'oynanacak hamle yok' kaybı oluşacakken Buldozer aynı turun sonunda alanı temizliyorsa kayıp iptal olur mu?
- İleride gelecek patlatılamaz küpler (obsidyen, haritaya yerleştirilmiş altın) Buldozer'in 'tüm küpleri patlatır' etkisine dahil mi?

### Harcama bonusu

- 'Çekme destesi bitti' tam olarak ne zaman sayılır: son kart çekilip deste 0'a düştüğü anda mı, yoksa boş desteden kart çekilmeye çalışıldığı (eşik öncesi ıskartanın desteye geri karıştığı) anda mı?
- Raunt içinde birden çok kez tetiklenebilir mi (eşik öncesi ıskarta geri karıştığı için deste defalarca bitebilir), yoksa raunt başına 1 kez mi?
- Kazanılan puan raunt puanına mı yazılsın (yani eşiğe sayılır), yoksa sadece toplam puana / market parasına mı?
- Uzatmada deste bitmesi zaten kayıp sebebi: o anda bonus kaybetmeden önce yine de ödensin mi, yoksa uzatmada bu joker tamamen etkisiz mi?

### Kazı çalışması

- 'Tümüyle patlarsa' derken: bloğun yerleştirilen TÜM küpleri aynı turda mı patlamalı, yoksa daha önce kısmen kırılmış bir bloğun KALAN küplerinin hepsi aynı anda patlarsa da sayılır mı?
- Oynanan kart ıskartadayken deste karışabiliyor ve kart, küpleri hâlâ tahtadayken çekme destesine hatta ele geri dönebiliyor. Blok tam patladığı anda kart ıskartada değilse (destede/eldeyse) ne olacak — bulunduğu yerden alınıp bonus ele mi taşınır, yoksa iade iptal mi?
- Bonus ele iade edilen kart tekrar oynanıp yine tümüyle patlarsa tekrar iade edilir mi (her bonus oynayışta üstten kart yakma bedeli işlemeye devam ederek)?

### Seri tetik

- Bonus elden oynanan turda normal el hiç ellenmemiş oluyor; o turun sonunda da el tamamen değişsin mi, yoksa döngü sadece elden oynanan turlarda mı çalışsın?
- Eşiğin geçildiği turda tur-sonu döngüsü hâlâ çalışır mı, yoksa joker tam o anda mı kapanır?
- Joker kapandığında (eşik geçilince) eldeki +2 fazla kart ne olacak: elde kalıp oynanabilir mi, yoksa ıskartaya mı atılsın? Motor şu an eli asla kırpmıyor, bu yüzden bir karar gerekiyor.

### Siyam

- Döndürülmüş veya aynalanmış blok 'aynı şekil' sayılır mı? (Şu an CanonicalKey rotasyonları FARKLI şekil sayıyor; sayılacaksa rotasyon-bağımsız bir anahtar eklemem gerekir.)
- 'Streak mantığı olabilir olmayabilir' demiştin — karar: minimum streak şartı olacak mı, yoksa art arda ikinci eş şekilde bonus hemen mi ödenecek?
- Siyam uzatmada geçerli mi; 'devam et' kararı streak'i sıfırlar mı?
- Bonus elden oynanan blok karşılaştırmaya dahil mi? (Örn. ileride Kara delik'in 1x1 boşluk blokları art arda oynanırsa Siyam tetiklenir mi?)

### dondurma

- Aynı boyutta blok streak'i bozar mı, yoksa sadece ilerletmez mi?
- Çığ için vereceğin uzatma / bonus el / birikme cevapları dondurma için birebir geçerli mi, yoksa farklılaşan bir kural var mı?
- En küçük blok 1 küp olduğundan kesin azalan dizi de en fazla 5 tur sürer (5→4→3→2→1) — streak eşiği bunu hesaba katıyor mu?

### çığ

- Önceki turla AYNI boyutta blok gelirse streak bozulur mu, yoksa korunup sadece artmaz mı?
- Çığ uzatmada geçerli mi? Eşik geçilip 'devam et' dediğinde veya uzatma temizliğindeki teklif duraklamasında streak korunur mu?
- Bonus elden oynanan blok da streak karşılaştırmasına giriyor mu?
- Varsayılan üreteç 1-5 küplük blok veriyor; kesin artan bir dizi en fazla 5 tur sürebilir (1→2→3→4→5). Streak eşiğini bu tavana göre mi seçeceksin, yoksa 'büyük veya eşit' gibi bir gevşetme mi istiyorsun?
- 'Birikebilir' tam olarak ne demek: streak uzadıkça tur başı bonus mu büyüyor, yoksa sabit bonus her uygun turda tekrar mı ödeniyor?

### İade

- Uzatmada çekme destesi boşken İade kullanmak mevcut çekme kuralına göre anında kayıp demek. Uzatmada joker tamamen yasak mı olsun, deste boşken kullanım mı engellensin, yoksa risk oyuncuya mı kalsın?
- Eşik öncesi deste boşsa ıskarta desteye karıştırılıyor; az önce iade ettiğin blok hemen geri çekilebilir. Bu kabul mü, yoksa iade edilen kart o çekilişten hariç mi tutulsun?

### Batak

- Uzatmada (eşik geçildikten sonra) da bet koyulabilsin istiyor musun? Uzatma temizliği zaten desteyi karıştırıp rastgele kart siliyor ve devam teklifi getiriyor — bet ödülü bununla aynı turda birlikte mi işlesin?
- Bet aktifken oyuncu devam teklifini kabul edip markete geçerse bet ne olur: sessizce iptal mi, yoksa süresi dolmamış beti bırakıp raundu bitirmek bir kayıp/ceza mı?
- Betin son turu temizliksiz biterken aynı turda eşik geçilip devam teklifi doğarsa hangisi kazanır? Mevcut motor kuralı bekleyen teklifin aynı-tur kaybı gölgelemesi — bet kaybı için de öyle mi olsun?
- Bet ödülü raunt skoruna mı eklensin (eşiğe sayılır) yoksa sadece toplam skora/market parasına mı?
- 'Beti koyduğun tur ile temizlik turu arasında kazanılan puan' hesabına temizlik turunun kendi puanı (temizlik bonusu dahil) giriyor mu? Ödül eğrisinin tam formülünü (1 tur → kaç kat) sen belirleyeceksin, şu an tanımsız.

### Enfeksiyon

- Enfeksiyonu kim ve ne zaman başlatıyor: oyuncu bir küp seçip güç gibi mi tetikliyor, yoksa otomatik/rastgele mi? Raunt başına kaç kez kullanılabiliyor?
- 'Üstüne konan bloklar' tam olarak ne demek? Dolu hücreye blok konulamıyor — enfekte küplere komşu/temas ederek yerleştirilen yeni blokları mı kastediyorsun?
- Yayılma küp bazında mı ilerliyor, yoksa temas edilen bloğun tamamına mı bulaşıyor (küplerde kaynak kart id'si tutuluyor, blok bütünlüğü takip edilebilir)? Her turda kaç hücre, çapraz komşular dahil mi, hedef seçimi rastgele mi?
- 3 turluk sayaç bloğun yerleştirildiği turda mı başlıyor, ve blok patlayınca o hücrelerdeki enfeksiyon da temizleniyor mu yoksa yayılmaya devam mı ediyor?
- Enfeksiyon patlaması puan versin mi, ve bu patlama tahtayı boşaltırsa temizlik sayılsın mı (şu anki kural temizlik için aynı turda satır/sütun patlaması istiyor)?

### Kara delik

- Boşluk bloğu ıskartaya mı ekleniyor (notun öyle diyor) yoksa bonus ele mi (RoundEngine'deki yorum Kara delik'i AddBonusCard çağıranı olarak listeliyor)? Hangisi güncel kararın?
- Boşluk küpünün üstüne blok konunca tam olarak ne oluyor: sadece üstüne denk gelen küp mü patlıyor, boşluk küpü de yok oluyor mu, yoksa yerinde kalıp tekrar tekrar mı kullanılıyor? Bu anında patlamalar puan veriyor mu?
- Boşluk patlaması tahtayı boşaltırsa temizlik sayılsın mı? Şu anki kural temizlik için aynı turda satır/sütun patlaması şart koşuyor. Ayrıca tahtada SADECE boşluk küpleri kaldıysa temizlik sayılır mı (Buzluk'taki buz istisnası gibi)?
- Maksimum boşluk bloğu sınırı raunt başına mı, run başına mı, ve kaç?
- Uzatma temizliğinde ıskarta desteye karışıp rastgele N kart siliniyor; aynı turda kazanılan boşluk bloğu bu karıştırmadan önce mi sonra mı ıskartaya girsin, ve rastgele silinen kartlardan biri olabilsin mi?

### Kayıt defteri

- Sayaç oyun alanı büyüklüğüne 'eşit ise' diyorsun — bir turda çok küp patlayıp sayaç eşitliği atlayarak aşarsa yine tetiklenir mi? Tetiklenince sayaç sıfırlanır mı, yoksa aşan miktar bir sonraki döngüye devreder mi?
- Jokerin tetiklediği temizlikte tahtadaki küpler gerçekten patlatılıyor mu, normal temizlik bonusu puanı veriliyor mu, ve bu patlayan küpler puan/sayaç sayıyor mu?
- Buldozer ve Robot süpürge gibi puan vermeyen patlatmalar da sayaca ekleniyor mu?
- Uzatmada joker devre dışıyken 'küp bırakmamak temizlik sayılmaz' kuralı da devam ediyor mu? Ediyorsa uzatmada hiç temizlik yapılamaz; mevcut kurallarda uzatmada ıskarta sadece temizlikle desteye karıştığı için çekme destesi bitince oyun garantili kaybedilir — bunu bilinçli mi istiyorsun?

### Kentsel Dönüşüm

- 'Bir blokluk ekstra yer' tam olarak ne: tek bir hücre mi (1 küplük), yoksa bir blok şekli kadar bir alan mı?
- Açılan yer dikdörtgen tahtanın dışına mı ekleniyor (tahta düzensiz şekle mi giriyor), yoksa tahtanın eni/boyu mu büyüyor? Konumu oyuncu mu seçiyor, yoksa otomatik mi?
- Tahta düzensizleşiyorsa satır/sütun patlaması ekstra hücreleri nasıl sayacak — ekstra hücreli bir satırın 'dolu' sayılması için o ekstra hücre de mi dolu olmalı?
- Sonraki rauntlarda temel tahta boyutu zaten büyüdüğünde (örn. 6x6'dan 7x7'ye) daha önce açılmış ekstra hücreler ne olacak — büyüyen alanın içinde eriyip kayıp mı olur, dışarı mı taşınır?
- Joker ileride satılırsa açılmış alanlar 'kalıcı' olarak kalmaya devam edecek mi?

### Robot süpürge

- Süpürgenin her tur patlattığı küp(ler) puan kazandırıyor mu?
- Süpürge temizliği tetiklediğinde normal temizlik bonusu veriliyor mu; uzatmadaysa uzatma temizlik sonuçları (ıskartanın desteye karışması, N kartın çıkarılması, devam teklifi) otomatik uygulanıyor mu?
- Silme miktarı 2+ olduğunda son 2-3 küpü tek seferde silip alanı boşaltmak da temizlik sayılır mı ('son küpü' tekil yazmışsın)? Tahtada silme miktarından az küp varsa hepsini mi siler?
- Süpürge, tur sonundaki 'oynanacak hamle yok' kayıp kontrolünden önce mi çalışır — yani açtığı hücre oyuncuyu aynı turda kayıptan kurtarabilir mi?

### Buzluk

- Donma anı: su küpü duvar hücresine geldiği anda mı donar, tur çözümünün belirli bir adımında mı? Taşkın'ın aynı tur suya çevirdiği duvar kenarı küpler de hemen donar mı?
- Buz, dolu satır/sütun patlamasında normal küpler gibi patlıyor (satırın dolu sayılmasına dahil) ve sadece ekstra puan veriyor — doğru mu? Ekstra puanın miktarı/formülü ne?
- Tahtada sadece buz kaldıktan sonra oyuncu satır patlattığı HER tur yeniden temizlik tetiklenebilir (uzatmada her biri kart sildirip yeni teklif açar) — bu art arda temizlik zinciri istediğin bir davranış mı?

### Damlaya Damlaya Göl Olur

- Bonus tam olarak ne: sonraki raunt başında yatan sabit puan mı, yoksa o raunt boyunca kazanılan puanlara uygulanan bir çarpan mı? Eşiğe (ScoreThreshold) sayılıyor mu?
- Sabit puansa ve raunt başında veriliyorsa, düşük eşikli bir rauntta tek başına eşiği geçebilir — bu durumda ilerleme teklifi daha ilk blok konmadan mı gelmeli?
- Art arda birden çok markette hiçbir şey almazsan bonus birikir/katlanır mı, yoksa sabit mi kalır?
- Reroll'a puan harcamak ya da joker/blok SATMAK 'bir şey almak' sayılır mı, yoksa bonusu sadece satın alma mı bozar?

### Parazit

- Bloğa bağlanan joker envanter slotunu işgal etmeye devam ediyor mu, yoksa Parazit'in asıl faydası slot boşaltmak mı?
- "En fazla bir joker" sınırı tam olarak ne: aynı anda toplam tek bağlı joker mi, yoksa her market fazında bir yerleştirme hakkı mı (zamanla birden fazla blokta bağlı joker birikebilir mi)?
- Parazit'in kendisi satılır ya da yok olursa bağlı joker ne olur — envantere mi döner, silinir mi?
- Blok tahtada değilken (destede/elde/ıskartadayken) bağlı jokerin etkisi aktif mi, yoksa sadece küpü tahtada dururken mi?
- Küpün kırıldığı turda bağlı joker o turun tetiklerini hâlâ alır mı? Kırılma adım 2'de (patlama) oluyor ama çoğu joker tetiği tur sonunda (TurnResolved) işliyor.
- "Küp kırılırsa" tanımına puan vermeyen patlatmalar da (planlanan Buldozer, Robot süpürge, Boşluk bloğu) dahil mi?

### Powerbank

- Powerbank otomatik mi çalışsın (bir gücün hakkı bitince kendiliğinden dolsun) yoksa oyuncu hangi gücü ne zaman dolduracağını kendisi mi seçsin?
- 'Kullanımını doldurur' +1 kullanım mı demek, yoksa gücü maksimum hakkına kadar tamamlamak mı?
- Uzatmada da (o rauntta henüz kullanılmadıysa) kullanılabilsin mi? Kayıt defteri ve Seri tetik'te uzatmayı açıkça yasakladın, burada belirtmedin.

### Simya

- Çift element küp bazında mı (her küp iki elemente birden sahip) yoksa blok bazında mı (bloğun bazı küpleri bir element, kalanları diğeri)?
- Çelişen ikililerde (ör. hem ateş hem su) iki davranış da aynı anda mı geçerli olacak, yoksa bir öncelik/iptal kuralı mı koyacaksın?
- Simya markette elementli blok ÇIKMA olasılığını da arttırıyor mu, yoksa sadece zaten elementli gelen teklifleri mi çift elemente çeviriyor?

### Taşkın

- Taşkını oyuncu mu tetikliyor (istediği turda, bir güç gibi) yoksa otomatik mi? Otomatikse tam olarak hangi anda tetikleniyor?
- 'Etrafındaki' komşuluk 4 yönlü mü, çapraz dahil 8 yönlü mü? Dönüşüm tek halka mı, yoksa yeni su olan küplerden zincirleme yayılır mı?
- Taşkın tahtadaki TÜM su bloklarının etrafına aynı anda mı uygulanır, yoksa tek bir su bloğu/bölge mi hedeflenir?
- Uzatma aynı rauntun devamı olduğu için hak uzatmada yenilenmiyor — doğru mu?

### Tutuştur

- Zincirle patlayan ateş küpleri nasıl puanlanır — satır patlaması küpleri gibi mi (PointsPerCubeExploded), ayrı bir değer mi, yoksa puansız mı?
- Zincir yalnızca satır/sütun patlamalarıyla mı tetiklenir? Buldozer gibi 'puan vermeyen, temizlikten sayılmayan' patlatmalar da bir ateş küpünü patlatırsa zincir tetiklenir mi; tetiklenirse zincirin puanı ve temizlik durumu neye tabi olur?

### Yangın

- Taşkın için sorduğum tetikleme (oyuncu mu/otomatik mi), komşuluk (4/8 yön), zincirleme yayılma ve uzatmada yenilenmeme soruları Yangın için de geçerli — iki jokerin cevapları birebir aynı mı, yoksa farklılaşan bir yön var mı?
- Yangının ateşe çevirdiği küpler aynı turda patlarsa (satır dolarsa) bu istenen bir kombo mu? Tutuştur ile birlikte tahtanın büyük kısmını tek turda silebilir, bunu sınırlamak ister misin?

### elmas kazma

- Patlayan obsidyenler küp başına normal patlama puanı mı verir (PointsPerCubeExploded) yoksa obsidyene özel daha yüksek bir değer mi?
- Bu puanlar temizliğin olduğu turun puanına eklenip aynı turdaki eşik kontrolüne sayılır mı?
- Elmas kazmanın patlattığı obsidyenler, patlatılan küp sayısını sayan diğer mekaniklere (ör. Kayıt defteri) dahil edilir mi?

### ihale

- Ek fiyat miktarı neye göre belirlenecek — sabit mi, jokerin baz satış fiyatının bir katı mı, raunt numarasıyla ölçekli mi?
- İhaledeki joker satılmadan başka bir yolla yok olursa (örn. Parazit ile bir küpe bağlıyken küp kırılıp joker silinirse) ihale kilidi açılır mı, yoksa süresiz kilitli mi kalır?
- Parazit ile bir bloğa bağlı joker ihale hedefi seçilebilir mi? Bağlı joker muhtemelen satılamaz durumda — hedef olursa kilit hiç açılmayabilir.

### midas

- Altın bloğun bonusu tam olarak nedir ve normalde ne zaman tetiklenir (yerleştirilince mi, küpleri patlayınca mı, elde tutulan her tur mu)? Kodda henüz hiç tanımlı değil, Midas'ı yazabilmek için önce bunu netleştirmen gerekiyor.
- Elde aynı anda birden fazla altın blok varsa bonus her biri için ayrı ayrı mı işler, yoksa tur başına tek sefer mi?
- El aynı turun çözümü içinde yeniden dolduruluyor: o tur ele yeni çekilen altın blok aynı turun bonusunu sayar mı, yoksa tura elde başlamış olması mı gerekir?
