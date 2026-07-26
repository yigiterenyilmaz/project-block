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

        /// <summary>Added to the on-board per-cube gold bonus while Midas is owned.</summary>
        public int GoldBonusBoost = 1;

        /// <summary>Gold cubes counted in hand last turn, for the UI.</summary>
        public int GoldCubesHeld { get; private set; }

        public MidasJoker()
            : base("midas", "Midas")
        {
            SetDescription(
                "Gold blocks pay their bonus in your hand too (bonus hand included), "
                    + "and every gold cube is worth more.",
                "Altın bloklar elindeyken de bonus verir (bonus el dahil) "
                    + "ve her altın küp daha çok puan kazandırır.");
        }

        /// <summary>Permanently raises the board-side gold bonus (a live ScoringConfig buff).</summary>
        public override void OnAcquired(SessionContext ctx)
        {
            ctx.Scoring.GoldPointsPerCubePerTurn += GoldBonusBoost;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Scoring.GoldPointsPerCubePerTurn -= GoldBonusBoost;
        }

        public override string StatusText
        {
            get { return Loc.Pick(GoldCubesHeld + " gold cubes", GoldCubesHeld + " altın küp"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            GoldCubesHeld = 0;
        }

        public override void ModifyScore(TurnContext turn)
        {
            int cubes = 0;
            RoundEngine round = turn.Round;
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Has(BlockElement.Gold))
                {
                    cubes += round.Hand[i].Shape.Size;
                }
            }
            foreach (BonusSlot slot in round.BonusHand)
            {
                if (slot.Card.Has(BlockElement.Gold))
                {
                    cubes += slot.Card.Shape.Size;
                }
            }
            GoldCubesHeld = cubes;
            if (cubes > 0)
            {
                turn.Score.AddFlat(cubes * PointsPerGoldCubeHeld, DefId);
            }
        }
    }

    /// <summary>"elmas kazma" - a clean sweep cracks the obsidian too, and pays for it.
    /// Obsidian is indestructible by the normal rules, so this uses the engine's forced
    /// destruction. The cracked cubes do NOT trigger a second sweep (one per turn).</summary>
    public sealed class ElmasKazmaJoker : Joker
    {
        public int PointsPerObsidian = 25;

        public ElmasKazmaJoker()
            : base("elmas_kazma", "Elmas Kazma")
        {
            SetDescription(
                "A clean sweep also shatters obsidian, which pays points.",
                "Temizlik yapınca obsidyenler de patlar ve puan verir.");
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
            IReadOnlyList<GridPos> cracked = turn.Round.DestroyCubes(obsidian, true, true);
            if (cracked.Count > 0)
            {
                turn.AddFlatScore(cracked.Count * PointsPerObsidian, DefId);
            }
        }
    }

    /// <summary>"Tutuştur" - when a fire cube goes up, every fire cube on the board goes with
    /// it. The engine's own fire rule only chains within one block; this chains the board.</summary>
    public sealed class TutusturJoker : Joker
    {
        /// <summary>Points per cube taken by the chain.</summary>
        public int PointsPerChainedCube = 4;

        public TutusturJoker()
            : base("tutustur", "Tutuştur")
        {
            SetDescription(
                "When one fire block explodes, ALL fire blocks on the board explode.",
                "Bir ateş bloğu patlayınca alandaki TÜM ateş blokları patlar.");
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
            IReadOnlyList<GridPos> burned = turn.Round.DestroyCubes(fire, true);
            if (burned.Count > 0)
            {
                turn.Score.AddFlat(burned.Count * PointsPerChainedCube, DefId);
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

    /// <summary>Shared body of "Yangın" and "Taşkın": once per round, every cube next to a
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
            GameBoard board = ctx.Round.Board;
            List<GridPos> sources = board.CellsOfKind(SpreadKind);

            // Collect first, convert after: converting as we walk would let the new cubes
            // seed further conversions and turn the whole board in one use.
            var targets = new List<GridPos>();
            foreach (GridPos source in sources)
            {
                foreach (GridPos neighbour in board.Neighbours(source))
                {
                    Cube? cube = board.GetCube(neighbour);
                    if (cube.HasValue && cube.Value.Kind != SpreadKind && !targets.Contains(neighbour))
                    {
                        targets.Add(neighbour);
                    }
                }
            }
            foreach (GridPos pos in targets)
            {
                board.SetCubeKind(pos, SpreadKind);
            }
            return true;
        }
    }

    /// <summary>"Yangın" - once per round, fire spreads to its neighbours.</summary>
    public sealed class YanginJoker : SpreadJoker
    {
        public YanginJoker()
            : base("yangin", "Yangın", CubeKind.Fire)
        {
            SetDescription(
                "Once per round: the blocks around fire blocks turn to fire too.",
                "Raunt başına 1 kez: ateş bloklarının etrafındaki bloklar da ateş olur.");
        }
    }

    /// <summary>"Taşkın" - once per round, water spreads to its neighbours.</summary>
    public sealed class TaskinJoker : SpreadJoker
    {
        public TaskinJoker()
            : base("taskin", "Taşkın", CubeKind.Water)
        {
            SetDescription(
                "Once per round: the blocks around water blocks turn to water too.",
                "Raunt başına 1 kez: su bloklarının etrafındaki bloklar da su olur.");
        }
    }

    /// <summary>"Buzluk" - water that reaches a wall freezes. Ice is sweep-exempt (a board
    /// holding only ice still counts as clean) and pays a bonus when it finally explodes.</summary>
    public sealed class BuzlukJoker : Joker
    {
        public int PointsPerIceExploded = 12;

        /// <summary>Cubes frozen this round, for the UI.</summary>
        public int FrozenThisRound { get; private set; }

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
            GameBoard board = turn.Round.Board;
            List<GridPos> water = board.CellsOfKind(CubeKind.Water);
            for (int i = 0; i < water.Count; i++)
            {
                if (board.IsOnEdge(water[i]) && board.SetCubeKind(water[i], CubeKind.Ice))
                {
                    FrozenThisRound++;
                }
            }
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
    /// "Kiracı" - a plain cube that sits still long enough starts paying rent. Any ELEMENTLESS
    /// cube that survives TurnsToRipen turns on the board turns to GOLD.
    ///
    /// Only plain cubes qualify: something that already has an element is not a tenant.
    ///
    /// Read the trade before taking this joker. Gold pays every turn it stands there, but gold
    /// also NEVER breaks and BLOCKS a clean sweep, so every cube that ripens is a permanent
    /// fixture on your board. Left alone long enough this joker slowly bricks the arena it is
    /// paying you for.
    ///
    /// Tenancy is tracked per CELL, and a cell whose occupant changes starts over. Cubes hardly
    /// ever move, but the ones that do - a retro row collapse, an inflation squeeze, a line swap,
    /// the escalator boss - reset their tenant's clock, which is the honest reading anyway: that
    /// cube stopped sitting still.
    ///
    /// All numbers are BALANCE PLACEHOLDERS.
    /// </summary>
    public sealed class KiraciJoker : Joker
    {
        /// <summary>Turns a plain cube must survive, in one spot, before it turns to gold.</summary>
        public int TurnsToRipen = 5;

        /// <summary>Cell -> turns its current tenant has held it.</summary>
        private readonly Dictionary<GridPos, int> tenancy = new Dictionary<GridPos, int>();

        /// <summary>Cell -> the card the tracked tenant came from, so a cell that changed hands
        /// is not credited with the previous cube's time.</summary>
        private readonly Dictionary<GridPos, int> tenantCard = new Dictionary<GridPos, int>();

        private readonly List<GridPos> ripened = new List<GridPos>();
        private readonly List<GridPos> stale = new List<GridPos>();

        private int goldThisRound;

        public KiraciJoker()
            : base("kiraci", "Kiracı")
        {
            SetDescription(
                "A plain block that survives 5 turns on the board turns to GOLD. Gold pays rent "
                    + "every turn - but it never breaks and it blocks a clean sweep, so every "
                    + "tenant you let settle is there for good.",
                "Oyun alanında 5 tur patlamadan duran elementsiz küpler ALTINA dönüşür. Altın "
                    + "durduğu her tur kira öder - ama asla kırılmaz ve temizliği engeller, yani "
                    + "yerleşmesine izin verdiğin her kiracı kalıcıdır.");
        }

        /// <summary>Cubes turned to gold so far this round, for the UI.</summary>
        public int GoldThisRound
        {
            get { return goldThisRound; }
        }

        /// <summary>Turns the longest-standing tenant still has to wait, or 0 when none is
        /// tracked. Lets the UI say how close the next payday is.</summary>
        public int TurnsToNextGold
        {
            get
            {
                int best = 0;
                foreach (KeyValuePair<GridPos, int> entry in tenancy)
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
                    : Loc.Pick("no tenants", "kiracı yok");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // A new round is a new board, so no tenancy carries over.
            tenancy.Clear();
            tenantCard.Clear();
            goldThisRound = 0;
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
                    continue; // only a PLAIN cube can be a tenant
                }
                seen.Add(cell);
                int cardId = cube.Value.SourceCardId;
                int heldBy;
                int turns;
                if (tenantCard.TryGetValue(cell, out heldBy) && heldBy == cardId
                    && tenancy.TryGetValue(cell, out turns))
                {
                    turns++;
                }
                else
                {
                    // A fresh tenant, or the cell changed hands: the clock starts over.
                    turns = 1;
                    tenantCard[cell] = cardId;
                }
                tenancy[cell] = turns;
                if (turns >= TurnsToRipen)
                {
                    ripened.Add(cell);
                }
            }

            // Drop the cells that no longer hold a plain cube - exploded, lifted, or retyped.
            foreach (KeyValuePair<GridPos, int> entry in tenancy)
            {
                if (!seen.Contains(entry.Key))
                {
                    stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                tenancy.Remove(stale[i]);
                tenantCard.Remove(stale[i]);
            }

            // Rent day. A ripened cube stops being a tenant because it stops being plain.
            for (int i = 0; i < ripened.Count; i++)
            {
                if (board.SetCubeKind(ripened[i], CubeKind.Gold))
                {
                    goldThisRound++;
                }
                tenancy.Remove(ripened[i]);
                tenantCard.Remove(ripened[i]);
            }
        }
    }
}
