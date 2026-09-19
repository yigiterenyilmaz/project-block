// PURPOSE: The jokers that bend the elemental block rules: midas, elmas kazma, Tutuştur,
// Yangın, Taşkın, Buzluk, Simya. They only work because the element system exists - every
// one of them reads or rewrites cube kinds through GameBoard/CubeRules, never by hand.
//
// CONFIRMED RULES:
//  - midas: a gold block normally pays only while it sits ON THE BOARD. Midas extends that
//    to gold held in HAND, bonus hand included - holding it is enough.
//  - Element conversions (Taşkın, Yangın) keep the cube's source card, so fire chains and
//    "whole block exploded" checks still see the original block.
//  - Buzluk freezes wall-touching water into ice. Ice does not block a clean sweep (a board
//    holding only ice counts as swept) but it CAN be exploded, and pays extra when it is.
//  - Effects that destroy or retype cubes go through RoundEngine, so the destruction log
//    and the sweep pre-condition stay correct.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"midas" - holding a gold block pays like having it on the board.</summary>
    public sealed class MidasJoker : Joker
    {
        /// <summary>Points per gold CUBE held, per turn. Mirrors the board-side gold bonus.</summary>
        public int PointsPerGoldCubeHeld = 2;

        /// <summary>Gold cubes counted in hand last turn, for the UI.</summary>
        public int GoldCubesHeld { get; private set; }

        /// <summary>
        /// WHAT IT PAID AND WHICH CUBES PAID IT - for the View, and reporting only.
        ///
        /// [NotSaved] like every other per-turn report: it is rebuilt by the next turn and means
        /// nothing across a load. The payout animation's subject is the CUBE, so this carries the
        /// sources rather than one number - see MidasVisuals.
        /// </summary>
        [field: NotSaved]
        public MidasPayoutVisuals LastPayout { get; private set; }

        [NotSaved]
        private int payoutSerial;

        public MidasJoker()
            : base("midas", "Midas")
        {
            SetDescription(
                "Gold blocks pay their bonus while they are in your hand, bonus hand included. "
                    + "Gold already on the board is unaffected.",
                "Altın bloklar elindeyken bonus verir, bonus el de dahil. Tahtada duran altına "
                    + "bir etkisi olmaz.");
        }

        public override string StatusText
        {
            get { return Loc.Pick(GoldCubesHeld + " gold cubes", GoldCubesHeld + " altın küp"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            GoldCubesHeld = 0;
            LastPayout = null;
        }

        public override void ModifyScore(TurnContext turn)
        {
            // THE SCORE IS UNCHANGED: the same cubes counted the same way, and the same one
            // AddFlat at the end. What is new is that the counting WRITES DOWN where each cube
            // came from, because the payout animation is per cube and the View may not recount.
            var report = new MidasPayoutVisuals
            {
                Serial = ++payoutSerial,
                PointsPerGoldCube = PointsPerGoldCubeHeld
            };
            int cubes = 0;
            RoundEngine round = turn.Round;
            for (int i = 0; i < round.Hand.Count; i++)
            {
                cubes += Count(round, round.Hand[i], false, i, report);
            }
            for (int i = 0; i < round.BonusHand.Count; i++)
            {
                cubes += Count(round, round.BonusHand[i].Card, true, i, report);
            }
            GoldCubesHeld = cubes;
            report.TotalScore = cubes * PointsPerGoldCubeHeld;
            LastPayout = report;
            if (cubes > 0)
            {
                turn.Score.AddFlat(report.TotalScore, DefId);
            }
        }

        /// <summary>Counts one held card and, when it pays, records it as a source.</summary>
        private int Count(RoundEngine round, BlockCard card, bool bonus, int slot,
            MidasPayoutVisuals report)
        {
            int cubes = GoldCubesOf(round, card);
            if (cubes <= 0)
            {
                return 0;
            }
            report.Sources.Add(new MidasGoldSource
            {
                CardId = card.Id,
                BonusHand = bonus,
                Slot = slot,
                // THE EFFECTIVE SHAPE, the one the card is being drawn in - see GoldCubesOf.
                Shape = round.EffectiveShape(card),
                GoldCubes = cubes,
                Subtotal = cubes * PointsPerGoldCubeHeld
            });
            return cubes;
        }

        /// <summary>How many gold cubes this held card is worth right now. Both questions go
        /// through the ROUND, never the card: a boss can suppress every element ("Vanilya"), and
        /// a card's shape is not fixed for the round - "Kıtlık" fattens what comes back from the
        /// discard and the fox reshape rewrites it, both into the same store. Counting the
        /// PRINTED shape paid for a block the player is no longer holding.</summary>
        private static int GoldCubesOf(RoundEngine round, BlockCard card)
        {
            if (card == null || !round.CardHasElement(card, BlockElement.Gold))
            {
                return 0;
            }
            return round.EffectiveShape(card).Size;
        }
    }

    /// <summary>"elmas kazma" - a clean sweep cracks the obsidian too, and pays for it.
    /// Obsidian is indestructible by the normal rules, so this uses the engine's forced
    /// destruction. The cracked cubes do NOT trigger a second sweep (one per turn).
    /// What it broke and what it paid go to the View as LastQuarry (QuarryVisuals).</summary>
    public sealed class ElmasKazmaJoker : Joker
    {
        public int PointsPerObsidian = 25;

        /// <summary>This sweep's break, for the View: a new object per sweep, never saved.
        /// </summary>
        [field: NotSaved]
        public QuarryVisuals LastQuarry { get; private set; }

        public ElmasKazmaJoker()
            : base("elmas_kazma", "Elmas Kazma")
        {
            SetDescription(
                "A clean sweep also shatters obsidian, which pays points.",
                "Temizlik yapınca obsidyenler de patlar ve puan verir.");
        }

        /// <summary>Statistics: one proc per sweep that broke stone, worth what it paid.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override void AfterCleanSweep(TurnContext turn)
        {
            List<GridPos> obsidian = turn.Round.Board.CellsOfKind(CubeKind.Obsidian);
            if (obsidian.Count == 0)
            {
                return;
            }
            // countsForSweep: the sweep already fired this turn, so this cannot re-trigger it,
            // but the cubes must still show up in the destruction log and counters.
            // The faces are taken BEFORE the destroy - by the time anything is drawn the cells
            // are empty, and what stood there is the whole subject of the animation.
            var before = new Dictionary<GridPos, Cube>();
            for (int i = 0; i < obsidian.Count; i++)
            {
                Cube? cube = turn.Round.Board.GetCube(obsidian[i]);
                if (cube.HasValue)
                {
                    before[obsidian[i]] = cube.Value;
                }
            }
            IReadOnlyList<GridPos> cracked = turn.Round.DestroyCubes(obsidian, true, true);
            if (cracked.Count > 0)
            {
                ScoreBreakdown score = turn.Score;
                int paidBefore = score.FlatBonus + score.LateFlat;
                turn.AddFlatScore(cracked.Count * PointsPerObsidian, DefId);
                NoteProc(score.FlatBonus + score.LateFlat - paidBefore, turn);
                var report = new QuarryVisuals
                {
                    Points = (score.FlatBonus + score.LateFlat - paidBefore) * score.ScoreScale
                };
                uint seed = 2166136261u;
                for (int i = 0; i < cracked.Count; i++)
                {
                    Cube cube;
                    report.Cells.Add(cracked[i]);
                    report.Cubes.Add(before.TryGetValue(cracked[i], out cube)
                        ? cube : new Cube(CubeKind.Obsidian, 0));
                    seed = (seed ^ unchecked((uint)(cracked[i].X * 73856093))) * 16777619u;
                    seed = (seed ^ unchecked((uint)(cracked[i].Y * 19349663))) * 16777619u;
                }
                report.Seed = seed;
                LastQuarry = report;
            }
        }
    }

    /// <summary>"Tutuştur" - when a fire cube goes up, every fire cube on the board goes with
    /// it. The engine's own fire rule only chains within one block; this chains the board.</summary>
    public sealed class TutusturJoker : Joker
    {
        /// <summary>Points per cube taken by the chain.</summary>
        public int PointsPerChainedCube = 4;

        /// <summary>This turn's chain, for the View: a new object per chain, never saved.
        /// </summary>
        [field: NotSaved]
        public IgnitionVisuals LastIgnition { get; private set; }

        public TutusturJoker()
            : base("tutustur", "Tutuştur")
        {
            SetDescription(
                "When one fire block explodes, ALL fire blocks on the board explode.",
                "Bir ateş bloğu patlayınca alandaki TÜM ateş blokları patlar.");
        }

        /// <summary>Statistics: one proc per chain, worth what it paid (nothing unless "Genel
        /// temizlik" makes a joker's destruction score).</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        /// <summary>Fire cubes the chain has taken over the run.</summary>
        private int cubesBurned;

        public override string StatusText
        {
            get { return Loc.Pick(cubesBurned + " burned", cubesBurned + " küp yandı"); }
        }

        public override void AfterLineExplosion(TurnContext turn)
        {
            if (!AnyFireDestroyed(turn.Report.DestroyedCubes))
            {
                return;
            }
            List<GridPos> fire = turn.Round.Board.CellsOfKind(CubeKind.Fire);
            if (fire.Count == 0)
            {
                return;
            }
            // For the View, taken BEFORE the chain runs: what lit it, and what stood where it burns.
            var report = new IgnitionVisuals();
            IReadOnlyList<DestroyedCube> log = turn.Report.DestroyedCubes;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Cube.Kind == CubeKind.Fire)
                {
                    report.SourceCells.Add(log[i].Pos);
                }
            }
            var before = new Dictionary<GridPos, Cube>();
            for (int i = 0; i < fire.Count; i++)
            {
                Cube? cube = turn.Round.Board.GetCube(fire[i]);
                if (cube.HasValue)
                {
                    before[fire[i]] = cube.Value;
                }
            }
            IReadOnlyList<GridPos> burned = turn.Round.DestroyCubes(fire, true);
            // The chain itself is the effect; it pays only under "Genel temizlik".
            int paidBefore = turn.Score.FlatBonus + turn.Score.LateFlat;
            if (burned.Count > 0 && turn.Round.ExternalDestructionScores)
            {
                int paid = burned.Count * PointsPerChainedCube;
                turn.Score.AddFlat(paid, DefId);
                // Only "Genel temizlik" makes the chain pay at all, so the points are part of
                // what that joker has been worth - see RoundEngine.ExternalScoreCredited.
                turn.Round.CreditExternalScore(paid);
            }
            if (burned.Count > 0)
            {
                cubesBurned += burned.Count;
                NoteProc(turn.Score.FlatBonus + turn.Score.LateFlat - paidBefore, turn);
                report.Points = (turn.Score.FlatBonus + turn.Score.LateFlat - paidBefore) * turn.Score.ScoreScale;
                uint seed = 2166136261u;
                for (int i = 0; i < burned.Count; i++)
                {
                    Cube cube;
                    report.Cells.Add(burned[i]);
                    report.Cubes.Add(before.TryGetValue(burned[i], out cube) ? cube : new Cube(CubeKind.Fire, 0));
                    seed = (seed ^ unchecked((uint)(burned[i].X * 73856093))) * 16777619u;
                    seed = (seed ^ unchecked((uint)(burned[i].Y * 19349663))) * 16777619u;
                }
                report.Seed = seed;
                LastIgnition = report;
            }
        }

        private static bool AnyFireDestroyed(IReadOnlyList<DestroyedCube> destroyed)
        {
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Cube.Kind == CubeKind.Fire)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Shared body of "Yangın" and "Taşkın": once per TURN, every cube next to a
    /// cube of the source kind becomes that kind too. One ring only - no chain reaction,
    /// which would trivially convert the whole board.</summary>
    public abstract class SpreadJoker : Joker
    {
        protected SpreadJoker(string defId, string displayName, CubeKind kind)
            : base(defId, displayName)
        {
            SpreadKind = kind;
            ChargesPerRound = 1;
        }

        /// <summary>The kind that spreads.</summary>
        public CubeKind SpreadKind { get; }

        /// <summary>
        /// WHAT THE LAST USE DID - for the View, and reporting only. [NotSaved] like every other
        /// per-turn report: rebuilt by the next use and meaningless across a load.
        ///
        /// It carries the SOURCES, the TARGETS, which SIDE each target caught from and what each
        /// target used to be. All four are things the View would otherwise have to guess at, and
        /// the guess it would make - "everything that is fire now was lit by something" - is the
        /// one that draws a second ring of spreading that the rules do not have.
        /// </summary>
        [field: NotSaved]
        public SpreadVisuals LastSpread { get; private set; }

        [NotSaved]
        private int spreadSerial;

        /// <summary>Cubes this joker has turned over the whole run.</summary>
        private int cubesConverted;

        public override void OnRoundStarted(RoundContext ctx)
        {
            base.OnRoundStarted(ctx);
            LastSpread = null;
        }

        /// <summary>ONCE PER TURN, not per round: the charge comes back as every turn ends. It
        /// is still one charge, so it cannot be spent twice before a block is placed.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            GrantCharge();
        }

        /// <summary>Statistics: every use is a proc, and the card says how many cubes it has
        /// turned in total - which is what the spread has actually been worth.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override string StatusText
        {
            get
            {
                string ready = ChargesLeft > 0 ? Loc.Pick("ready", "hazır") : Loc.Pick("used", "kullanıldı");
                return cubesConverted > 0
                    ? ready + Loc.Pick("  ·  " + cubesConverted + " turned", "  ·  " + cubesConverted + " küp döndü")
                    : ready;
            }
        }

        public override bool CanActivate(RoundContext ctx)
        {
            return ChargesLeft > 0
                && ctx.Round.Status == RoundStatus.InProgress
                && ctx.Round.Board.CellsOfKind(SpreadKind).Count > 0;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            if (!CanActivate(ctx) || !TrySpendCharge())
            {
                return false;
            }
            LastSpread = SpreadOn(ctx.Round.Board, SpreadKind, ++spreadSerial);
            cubesConverted += LastSpread.Targets.Count;
            NoteProc(0);
            return true;
        }

        /// <summary>
        /// THE SPREAD ITSELF, on any board - and the ONE place the rule lives.
        ///
        /// Public and static so the ANIMATION LAB can run it on a board of its own and get
        /// exactly what a round would get, reported the same way: the same bargain
        /// MapusBoss.RetargetOn makes with the boss's targeting. A lab that reimplements the
        /// rule is a lab that agrees with itself and with nothing else.
        /// </summary>
        public static SpreadVisuals SpreadOn(GameBoard board, CubeKind kind, int serial)
        {
            var report = new SpreadVisuals { Serial = serial, Kind = kind };
            if (board == null)
            {
                return report;
            }
            List<GridPos> sources = board.CellsOfKind(kind);
            report.Sources.AddRange(sources);

            // Collect first, convert after: converting as we walk would let the new cubes
            // seed further conversions and turn the whole board in one use.
            var targets = new List<GridPos>();
            foreach (GridPos source in sources)
            {
                foreach (GridPos neighbour in board.Neighbours(source))
                {
                    Cube? cube = board.GetCube(neighbour);
                    if (!cube.HasValue || cube.Value.Kind == kind)
                    {
                        continue;
                    }
                    if (!targets.Contains(neighbour))
                    {
                        targets.Add(neighbour);
                        // The cube as it stands NOW: once the loop below has run, what it used
                        // to be is gone, and that is where the animation starts from.
                        report.Targets.Add(new SpreadIgnition
                        {
                            Cell = neighbour,
                            Was = cube.Value
                        });
                    }
                    // WHICH SIDE it caught from, including the second and third - two fires
                    // meeting on one cube is a thing the picture should be able to say.
                    report.Targets[targets.IndexOf(neighbour)].From.Add(source);
                }
            }
            foreach (GridPos pos in targets)
            {
                board.SetCubeKind(pos, kind);
            }
            return report;
        }
    }

    /// <summary>"Yangın" - once per turn, fire spreads to its neighbours.</summary>
    public sealed class YanginJoker : SpreadJoker
    {
        public YanginJoker()
            : base("yangin", "Yangın", CubeKind.Fire)
        {
            SetDescription(
                "Once per turn: the blocks around fire blocks turn to fire too.",
                "Tur başına 1 kez: ateş bloklarının etrafındaki bloklar da ateş olur.");
        }
    }

    /// <summary>"Taşkın" - once per turn, water spreads to its neighbours.</summary>
    public sealed class TaskinJoker : SpreadJoker
    {
        public TaskinJoker()
            : base("taskin", "Taşkın", CubeKind.Water)
        {
            SetDescription(
                "Once per turn: the blocks around water blocks turn to water too.",
                "Tur başına 1 kez: su bloklarının etrafındaki bloklar da su olur.");
        }
    }

    /// <summary>"Buzluk" - water that reaches a wall freezes. Ice is sweep-exempt (a board
    /// holding only ice still counts as clean) and pays a bonus when it finally explodes.</summary>
    public sealed class BuzlukJoker : Joker
    {
        public int PointsPerIceExploded = 12;

        /// <summary>Cubes frozen this round, for the UI.</summary>
        public int FrozenThisRound { get; private set; }

        /// <summary>
        /// What froze THIS TURN, for the animation. Per-turn and meaningless across a load, so
        /// it never goes in the save file - and because the View reads it on every repaint, it
        /// is keyed on a SERIAL rather than on being non-null.
        /// </summary>
        [field: NotSaved]
        public FreezeVisuals LastFreeze { get; private set; }

        [NotSaved]
        private int freezeSerial;

        public BuzlukJoker()
            : base("buzluk", "Buzluk")
        {
            SetDescription(
                "Water blocks touching a wall freeze. Ice never blocks a clean sweep "
                    + "and pays extra when exploded.",
                "Duvara değen su blokları donar. Buz temizliği engellemez ve "
                    + "patlayınca ek puan verir.");
        }

        public override string StatusText
        {
            get { return Loc.Pick(FrozenThisRound + " ice", FrozenThisRound + " buz"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            FrozenThisRound = 0;
        }

        /// <summary>Freezing happens after the board has settled for the turn, so water that
        /// only touches a wall in passing is not caught mid-fall.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            LastFreeze = FreezeOn(turn.Round.Board, ++freezeSerial);
            FrozenThisRound += LastFreeze.Cells.Count;
        }

        /// <summary>
        /// THE FREEZE ITSELF, on any board - and the ONE place the rule lives.
        ///
        /// Public and static so the ANIMATION LAB can run it on a board of its own and get
        /// exactly what a turn would get, reported the same way: the same bargain
        /// SpreadJoker.SpreadOn and MapusBoss.RetargetOn both make. A lab that reimplements the
        /// rule is a lab that agrees with itself and with nothing else - and here the thing most
        /// worth agreeing about is WHICH SIDE is the wall, because that is what the picture draws.
        /// </summary>
        public static FreezeVisuals FreezeOn(GameBoard board, int serial)
        {
            var report = new FreezeVisuals { Serial = serial };
            if (board == null)
            {
                return report;
            }
            List<GridPos> water = board.CellsOfKind(CubeKind.Water);
            for (int i = 0; i < water.Count; i++)
            {
                BoardSides sides = board.EdgeSidesOf(water[i]);
                if (sides == BoardSides.None)
                {
                    continue;
                }
                // The cube as it stands NOW: one line below it is ice, and "it was water" is
                // where the animation starts from.
                Cube? was = board.GetCube(water[i]);
                if (!board.SetCubeKind(water[i], CubeKind.Ice))
                {
                    continue;
                }
                report.Cells.Add(new FrozenCell
                {
                    Cell = water[i],
                    Was = was.Value,
                    Sides = sides
                });
            }
            return report;
        }

        public override void ModifyScore(TurnContext turn)
        {
            int ice = 0;
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Cube.Kind == CubeKind.Ice)
                {
                    ice++;
                }
            }
            if (ice > 0)
            {
                turn.Score.AddFlat(ice * PointsPerIceExploded, DefId);
            }
        }
    }

    /// <summary>"Simya" - elemental blocks in the market come with a second element.
    /// Only touches offers that already have exactly one; a plain block stays plain.</summary>
    public sealed class SimyaJoker : Joker
    {
        /// <summary>Elements the second slot may be drawn from. Kept to the ones whose
        /// behaviour is implemented, so a doubled block never gets a dead element.</summary>
        public readonly List<BlockElement> SecondElementPool = new List<BlockElement>
        {
            BlockElement.Fire,
            BlockElement.Water,
            BlockElement.Gold,
            BlockElement.Dynamite,
            BlockElement.Mechanical,
            BlockElement.Ghost
        };

        public SimyaJoker()
            : base("simya", "Simya")
        {
            SetDescription(
                "Elemental blocks in the market arrive with 2 elements at once.",
                "Marketteki elementli bloklar aynı anda 2 elemente sahip gelir.");
        }

        public override BlockCard FilterMarketOffer(SessionContext ctx, BlockCard card)
        {
            if (card.Elements.Count != 1)
            {
                return card;
            }
            var candidates = new List<BlockElement>();
            for (int i = 0; i < SecondElementPool.Count; i++)
            {
                if (!card.Has(SecondElementPool[i]))
                {
                    candidates.Add(SecondElementPool[i]);
                }
            }
            if (candidates.Count == 0)
            {
                return card;
            }
            var elements = new List<BlockElement>(card.Elements);
            elements.Add(candidates[ctx.Rng.NextInt(0, candidates.Count)]);
            // Same Id on purpose: the offer is the same card, only richer.
            return new BlockCard(card.Id, card.Shape, elements);
        }
    }

    /// <summary>
    /// "Metamorfoz" - a plain cube that sits still long enough changes into something else. Any
    /// ELEMENTLESS cube that survives TurnsToRipen turns on the board turns to GOLD.
    ///
    /// Only plain cubes qualify: something that already has an element has nothing left to become.
    ///
    /// Read the trade before taking this joker. Gold scores every turn it stands there, but gold
    /// also NEVER breaks and BLOCKS a clean sweep, so every cube that ripens is a permanent
    /// fixture on your board. Left alone long enough this joker slowly bricks the arena it is
    /// paying you for.
    ///
    /// The clock is tracked per CELL, and a cell whose occupant changes starts over. Cubes hardly
    /// ever move, but the ones that do - a retro row collapse, an inflation squeeze, a line swap,
    /// the escalator boss - reset their cube's clock, which is the honest reading anyway: that
    /// cube stopped sitting still.
    ///
    /// All numbers are BALANCE PLACEHOLDERS.
    /// </summary>
    public sealed class MetamorfozJoker : Joker
    {
        /// <summary>Turns a plain cube must survive, in one spot, before it turns to gold.</summary>
        public int TurnsToRipen = 7;

        /// <summary>Cell -> turns its current cube has held it.</summary>
        private readonly Dictionary<GridPos, int> age = new Dictionary<GridPos, int>();

        /// <summary>Cell -> the card the tracked cube came from, so a cell that changed hands
        /// is not credited with the previous cube's time.</summary>
        private readonly Dictionary<GridPos, int> ageCard = new Dictionary<GridPos, int>();

        private readonly List<GridPos> ripened = new List<GridPos>();
        private readonly List<GridPos> stale = new List<GridPos>();

        private int goldThisRound;

        /// <summary>What is ripening and what just turned, for the View. A NEW object per turn,
        /// matched by identity; rebuilt every turn and meaningless across a load.</summary>
        [NotSaved]
        public MetamorphosisVisuals LastChange;

        public MetamorfozJoker()
            : base("metamorfoz", "Metamorfoz")
        {
            // The count is read off the field rather than written out twice: a balance number
            // that also lives in a sentence is a balance number that will be changed in one
            // place and not the other.
            SetDescription(
                "A plain block that survives " + TurnsToRipen + " turns on the board turns to "
                    + "GOLD - but gold never breaks and it blocks a clean sweep, so every block "
                    + "that changes is there for good.",
                "Oyun alanında " + TurnsToRipen + " tur patlamadan duran elementsiz bloklar "
                    + "ALTINA dönüşür - ama altın asla kırılmaz ve temizliği engeller, yani "
                    + "dönüşen her blok kalıcıdır.");
        }

        /// <summary>Cubes turned to gold so far this round, for the UI.</summary>
        public int GoldThisRound
        {
            get { return goldThisRound; }
        }

        /// <summary>Turns the longest-standing cube still has to wait, or 0 when none is
        /// tracked. Lets the UI say how close the next change is.</summary>
        public int TurnsToNextGold
        {
            get
            {
                int best = 0;
                foreach (KeyValuePair<GridPos, int> entry in age)
                {
                    if (entry.Value > best)
                    {
                        best = entry.Value;
                    }
                }
                return best > 0 ? TurnsToRipen - best : 0;
            }
        }

        public override string StatusText
        {
            get
            {
                if (goldThisRound > 0)
                {
                    return goldThisRound + Loc.Pick(" gold", " altın");
                }
                int wait = TurnsToNextGold;
                return wait > 0
                    ? wait + Loc.Pick("t to gold", "t sonra altın")
                    : Loc.Pick("nothing settled", "duran blok yok");
            }
        }

        /// <summary>It keeps proc statistics, so the tooltip prints its count even at zero. What
        /// it counts is CUBES TURNED - it pays no points, so the count is the whole story.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // A new round is a new board, so no clock carries over.
            age.Clear();
            ageCard.Clear();
            goldThisRound = 0;
            LastChange = null;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            GameBoard board = turn.Round.Board;
            ripened.Clear();
            stale.Clear();

            // Age every plain cube that is still where it was, and start the clock on new ones.
            List<GridPos> occupied = board.GetOccupiedCells();
            var seen = new HashSet<GridPos>();
            for (int i = 0; i < occupied.Count; i++)
            {
                GridPos cell = occupied[i];
                Cube? cube = board.GetCube(cell);
                if (!cube.HasValue || cube.Value.Kind != CubeKind.Normal)
                {
                    continue; // only a PLAIN cube has something left to become
                }
                seen.Add(cell);
                int cardId = cube.Value.SourceCardId;
                int heldBy;
                int turns;
                if (ageCard.TryGetValue(cell, out heldBy) && heldBy == cardId
                    && age.TryGetValue(cell, out turns))
                {
                    turns++;
                }
                else
                {
                    // A fresh cube, or the cell changed hands: the clock starts over.
                    turns = 1;
                    ageCard[cell] = cardId;
                }
                age[cell] = turns;
                if (turns >= TurnsToRipen)
                {
                    ripened.Add(cell);
                }
            }

            // Drop the cells that no longer hold a plain cube - exploded, lifted, or retyped.
            foreach (KeyValuePair<GridPos, int> entry in age)
            {
                if (!seen.Contains(entry.Key))
                {
                    stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                age.Remove(stale[i]);
                ageCard.Remove(stale[i]);
            }

            // The change itself. A ripened cube leaves the clock because it stops being plain.
            var report = new MetamorphosisVisuals();
            for (int i = 0; i < ripened.Count; i++)
            {
                if (board.SetCubeKind(ripened[i], CubeKind.Gold))
                {
                    goldThisRound++;
                    report.AddTurned(ripened[i]);
                }
                age.Remove(ripened[i]);
                ageCard.Remove(ripened[i]);
            }
            // Everything STILL on the clock, after the ripened ones have left it - so a cube that
            // turned this turn is reported as turned and never also as nearly there.
            foreach (KeyValuePair<GridPos, int> entry in age)
            {
                report.AddRipening(entry.Key, entry.Value, TurnsToRipen);
            }
            LastChange = report;
            // ONE proc per TURN in which anything changed, not one per cube: what the player is
            // being told is "the metamorphosis happened", and it happened once. No points - this
            // joker pays in gold cubes, and a statistic that invented a score would be a lie.
            if (report.Turned.Count > 0)
            {
                NoteProc(0, turn);
            }
        }
    }
}
