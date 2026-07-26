// PURPOSE: RoundEngine save/load (partial) - the round half of a mid-run save.
//
// WHAT IS SAVED is everything that outlives a turn: the board, the piles, the hand and bonus
// hand, the score and status, and the per-round memories the rules depend on (dynamite blocks,
// fox reshapes, rotations, frozen cards, the rewind history "Kum saati" reaches into, and the
// erosion clock).
//
// WHAT IS NOT SAVED is the per-TURN scratch state - the destruction snapshot, the current
// report, the sweep flags. A save is taken between turns, when all of it is at rest, and the
// snapshot is re-derived on load (ResyncSnapshot) rather than stored. Storing it would just be
// another thing that could disagree with the board.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        internal void Save(SaveWriter w, string key, CardTable cards)
        {
            CoreSerializers.WriteRoundConfig(w, key + ".config", Config);
            Board.Save(w, key + ".board");
            Deck.Save(w, key + ".deck", cards);
            cards.WriteRefs(w, key + ".hand", Hand.Cards);

            w.Write(key + ".bonus.count", bonusHand.Count);
            for (int i = 0; i < bonusHand.Count; i++)
            {
                w.Write(key + ".bonus." + i + ".card", bonusHand[i].Card.Id);
                w.Write(key + ".bonus." + i + ".outcome", (int)bonusHand[i].OutcomeOnPlay);
            }

            w.Write(key + ".turn", TurnNumber);
            w.Write(key + ".score", RoundScore);
            w.Write(key + ".thresholdPassed", ThresholdPassed);
            w.Write(key + ".sweeps", CleanSweepCount);
            w.Write(key + ".continues", ContinueCount);
            w.Write(key + ".status", (int)Status);
            w.Write(key + ".hasLoss", Loss.HasValue);
            w.Write(key + ".loss", Loss.HasValue ? (int)Loss.Value : 0);
            w.Write(key + ".pendingOffer", pendingAdvanceOffer);
            w.Write(key + ".combo", comboCount);
            w.Write(key + ".comboBlank", comboBlankTurns);
            w.Write(key + ".powersUsed", PowersUsedThisTurn);
            w.Write(key + ".recycles", DeckRecycleCount);
            w.Write(key + ".erosions", BoardErosionCount);
            w.Write(key + ".suppressSweep", SuppressNaturalSweep);
            w.Write(key + ".drawEmptyReported", drawPileReportedEmpty);
            w.Write(key + ".cleanSampleLocked", cleanSampleLocked);

            WriteDynamite(w, key + ".dynamite");
            WriteIntMap(w, key + ".rotations", rotations);
            WriteIntMap(w, key + ".placedSize", cardPlacedSize);
            WriteIntMap(w, key + ".frozen", frozenCards);

            w.Write(key + ".fox.count", foxShapes.Count);
            int foxIndex = 0;
            foreach (KeyValuePair<int, BlockShape> fox in foxShapes)
            {
                w.Write(key + ".fox." + foxIndex + ".card", fox.Key);
                CoreSerializers.WriteShape(w, key + ".fox." + foxIndex + ".shape", fox.Value);
                foxIndex++;
            }

            // The rewind history: "Kum saati" reaches two turns back, so losing it would
            // silently disarm the power after a load.
            w.Write(key + ".history.count", boardHistory.Count);
            for (int i = 0; i < boardHistory.Count; i++)
            {
                WriteCubeMap(w, key + ".history." + i, boardHistory[i]);
            }

            w.Write(key + ".boss", Boss != null ? Boss.DefId : null);
            if (Boss != null)
            {
                ContentStateSerializer.Save(w, key + ".bossState", Boss);
            }
        }

        internal static RoundEngine Load(SaveReader r, string key, CardTable cards,
            RoundRules rules, IRandomSource rng, IScoreCalculator scorer, GameSession session,
            ITurnHooks hooks)
        {
            RoundConfig config = CoreSerializers.ReadRoundConfig(r, key + ".config");
            GameBoard board = GameBoard.Load(r, key + ".board");
            var deck = new RoundDeck(rng);
            deck.Load(r, key + ".deck", cards);

            var round = new RoundEngine(config, rules, rng, scorer, session, hooks, board, deck);
            foreach (BlockCard card in cards.ReadRefs(r, key + ".hand"))
            {
                round.Hand.Add(card);
            }

            int bonusCount = r.ReadInt(key + ".bonus.count");
            for (int i = 0; i < bonusCount; i++)
            {
                int cardId = r.ReadInt(key + ".bonus." + i + ".card");
                var outcome = (BonusPlayOutcome)r.ReadInt(key + ".bonus." + i + ".outcome");
                BlockCard card = cards.Get(cardId);
                if (card == null)
                {
                    throw new SaveFormatException("Bonus card " + cardId + " is not in the table.");
                }
                round.bonusHand.Add(new BonusSlot(card, outcome));
            }

            round.TurnNumber = r.ReadInt(key + ".turn");
            round.RoundScore = r.ReadInt(key + ".score");
            round.ThresholdPassed = r.ReadBool(key + ".thresholdPassed");
            round.CleanSweepCount = r.ReadInt(key + ".sweeps");
            round.ContinueCount = r.ReadInt(key + ".continues");
            var status = (RoundStatus)r.ReadInt(key + ".status");
            bool hasLoss = r.ReadBool(key + ".hasLoss");
            var lossReason = (LossReason)r.ReadInt(key + ".loss");
            round.Loss = hasLoss ? lossReason : (LossReason?)null;
            round.pendingAdvanceOffer = r.ReadBool(key + ".pendingOffer");
            round.comboCount = r.ReadInt(key + ".combo");
            round.comboBlankTurns = r.ReadInt(key + ".comboBlank");
            round.PowersUsedThisTurn = r.ReadInt(key + ".powersUsed");
            round.DeckRecycleCount = r.ReadInt(key + ".recycles");
            round.BoardErosionCount = r.ReadInt(key + ".erosions");
            round.SuppressNaturalSweep = r.ReadBool(key + ".suppressSweep");
            round.drawPileReportedEmpty = r.ReadBool(key + ".drawEmptyReported");
            round.cleanSampleLocked = r.ReadBool(key + ".cleanSampleLocked");

            round.ReadDynamite(r, key + ".dynamite");
            ReadIntMap(r, key + ".rotations", round.rotations);
            ReadIntMap(r, key + ".placedSize", round.cardPlacedSize);
            ReadIntMap(r, key + ".frozen", round.frozenCards);

            int foxCount = r.ReadInt(key + ".fox.count");
            for (int i = 0; i < foxCount; i++)
            {
                int cardId = r.ReadInt(key + ".fox." + i + ".card");
                round.foxShapes[cardId] = CoreSerializers.ReadShape(r, key + ".fox." + i + ".shape");
            }

            int historyCount = r.ReadInt(key + ".history.count");
            for (int i = 0; i < historyCount; i++)
            {
                round.boardHistory.Add(ReadCubeMap(r, key + ".history." + i));
            }

            string bossId = r.ReadString(key + ".boss");
            if (bossId != null)
            {
                BossRound boss = BossRegistry.Create(bossId);
                if (boss == null)
                {
                    throw new SaveFormatException("Unknown boss '" + bossId + "' in the save.");
                }
                ContentStateSerializer.Load(r, key + ".bossState", boss);
                // Assigned directly rather than through SetBoss: the board came back with its
                // own IgnoreElements already set, and SetBoss would overwrite it.
                round.Boss = boss;
            }

            // The per-turn destruction bookkeeping is re-derived from the board that just
            // loaded, rather than being carried in the file (see the file header).
            round.ResyncSnapshot();
            round.CaptureTurnStartCardCounts();
            // Set last so no earlier assignment can fire StatusChanged into a half-built round.
            round.Status = status;
            return round;
        }

        /// <summary>Every card the round is holding that the owned deck does not - bonus cards
        /// are round-scoped and would otherwise be missing from the save's card table.</summary>
        internal IEnumerable<BlockCard> AllRoundCards()
        {
            foreach (BlockCard card in Deck.AllCards())
            {
                yield return card;
            }
            for (int i = 0; i < Hand.Count; i++)
            {
                yield return Hand[i];
            }
            for (int i = 0; i < bonusHand.Count; i++)
            {
                yield return bonusHand[i].Card;
            }
        }

        private void WriteDynamite(SaveWriter w, string key)
        {
            w.Write(key + ".count", dynamiteBlocks.Count);
            int index = 0;
            foreach (KeyValuePair<int, DynamiteState> entry in dynamiteBlocks)
            {
                w.Write(key + "." + index + ".card", entry.Key);
                w.Write(key + "." + index + ".full", entry.Value.FullSize);
                w.Write(key + "." + index + ".remaining", entry.Value.RemainingAtTurnStart);
                w.Write(key + "." + index + ".placedTurn", entry.Value.PlacementTurn);
                index++;
            }
        }

        private void ReadDynamite(SaveReader r, string key)
        {
            int count = r.ReadInt(key + ".count");
            for (int i = 0; i < count; i++)
            {
                int cardId = r.ReadInt(key + "." + i + ".card");
                var state = new DynamiteState();
                state.FullSize = r.ReadInt(key + "." + i + ".full");
                state.RemainingAtTurnStart = r.ReadInt(key + "." + i + ".remaining");
                state.PlacementTurn = r.ReadInt(key + "." + i + ".placedTurn");
                dynamiteBlocks[cardId] = state;
            }
        }

        private static void WriteIntMap(SaveWriter w, string key, Dictionary<int, int> map)
        {
            w.Write(key + ".count", map.Count);
            int index = 0;
            foreach (KeyValuePair<int, int> entry in map)
            {
                w.Write(key + "." + index + ".k", entry.Key);
                w.Write(key + "." + index + ".v", entry.Value);
                index++;
            }
        }

        private static void ReadIntMap(SaveReader r, string key, Dictionary<int, int> map)
        {
            map.Clear();
            int count = r.ReadInt(key + ".count");
            for (int i = 0; i < count; i++)
            {
                int k = r.ReadInt(key + "." + i + ".k");
                map[k] = r.ReadInt(key + "." + i + ".v");
            }
        }

        private static void WriteCubeMap(SaveWriter w, string key, Dictionary<GridPos, Cube> map)
        {
            w.Write(key + ".count", map.Count);
            int index = 0;
            foreach (KeyValuePair<GridPos, Cube> entry in map)
            {
                CoreSerializers.WritePos(w, key + "." + index, entry.Key);
                CoreSerializers.WriteCube(w, key + "." + index, entry.Value);
                index++;
            }
        }

        private static Dictionary<GridPos, Cube> ReadCubeMap(SaveReader r, string key)
        {
            var map = new Dictionary<GridPos, Cube>();
            int count = r.ReadInt(key + ".count");
            for (int i = 0; i < count; i++)
            {
                GridPos at = CoreSerializers.ReadPos(r, key + "." + i);
                map[at] = CoreSerializers.ReadCube(r, key + "." + i);
            }
            return map;
        }
    }
}
