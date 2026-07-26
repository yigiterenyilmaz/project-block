// PURPOSE: RoundEngine scoring & round-flow helpers - card disposal, between-turn and
// overtime score, dead-zone-aware line scoring, retro/Tetris collapse, top-out, and
// the loss/force-advance/force-sweep entry points.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>THE way a card leaves play into a pile. Normally the discard; with
        /// "Oryantasyon" it is buried at a random depth in the draw pile instead, which is
        /// why every disposal in this class goes through here.</summary>
        private void DisposeCard(BlockCard card)
        {
            // "Gen nakli" is a LOAN: reaching a pile is where it ends, so the card goes plain
            // again and the cube it borrowed from gets its element back. Here, because this is
            // THE way a card leaves play - every disposal in the engine comes through it.
            ReturnBorrowedGene(card.Id);
            if (Rules.PlayedCardsReturnToDrawPile)
            {
                Deck.InsertRandomIntoDraw(card);
                return;
            }
            Deck.Discard(card);
        }

        /// <summary>Takes a hand card out of the ROUND entirely - not to the discard, where it
        /// would come back this round ("Neşter" cutting it up, "Lehimleme" welding it into
        /// something else). It is still in the deck the player owns, so next round it is back.
        /// </summary>
        internal bool TakeCardOutOfRound(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                return false;
            }
            BlockCard card = Hand[handIndex];
            Hand.RemoveAt(handIndex);
            Deck.RemoveFromRound(card);
            return true;
        }

        /// <summary>
        /// "Gen nakli": moves an element off a cube and onto a card, and remembers how to undo it.
        /// The cube goes plain; the card carries the element until it reaches the discard, at
        /// which point BOTH go back to what they were (see ReturnBorrowedGenes).
        /// </summary>
        internal bool TransplantElement(GridPos cell, BlockCard card, BlockElement gene)
        {
            Cube? cube = Board.GetCube(cell);
            if (card == null || !cube.HasValue || borrowedGenes.ContainsKey(card.Id))
            {
                return false;
            }
            borrowedGenes[card.Id] = new BorrowedGene(cell, cube.Value.Kind, gene);
            Board.SetCubeAt(cell, new Cube(CubeKind.Normal, cube.Value.SourceCardId));
            cardElements[card.Id] = gene;
            return true;
        }

        /// <summary>The element a card is carrying on loan, or null. Read by CardHasElement, so
        /// every rule that asks what a block is made of sees the borrowed gene too.</summary>
        internal BlockElement? BorrowedElementOf(int cardId)
        {
            BlockElement gene;
            return cardElements.TryGetValue(cardId, out gene) ? gene : (BlockElement?)null;
        }

        /// <summary>Gives a borrowed gene back when its card hits the discard: the card is plain
        /// again, and the cube it came from gets its element back IF it is still standing.</summary>
        internal void ReturnBorrowedGene(int cardId)
        {
            BorrowedGene loan;
            if (!borrowedGenes.TryGetValue(cardId, out loan))
            {
                return;
            }
            borrowedGenes.Remove(cardId);
            cardElements.Remove(cardId);
            Cube? cube = Board.GetCube(loan.Cell);
            if (cube.HasValue && cube.Value.Kind == CubeKind.Normal)
            {
                Board.SetCubeAt(loan.Cell, new Cube(loan.Kind, cube.Value.SourceCardId));
            }
        }

        /// <summary>"Hidrolik pres" letting go. Reports the cells it changed as LIFTED - nothing
        /// was destroyed in the scoring sense - and re-checks the lines, because an expansion can
        /// complete one exactly as a board-reshaping power can.</summary>
        internal PressExpansion ReleasePress(GridPos anchor, Cube?[] swallowed)
        {
            PressExpansion result = MainBoard.Expand(anchor, swallowed);
            if (result == null)
            {
                return null;
            }
            ResyncSnapshot();
            if (currentReport != null && result.DetonatedCells.Count > 0)
            {
                currentReport.AddLiftedCells(result.DetonatedCells);
            }
            // An expansion can fill a line. Same path a deflating inflation power uses.
            LineExplosionResult lines = LineExplosionsSuppressed
                ? LineExplosionResult.None
                : MainBoard.ResolveFullLines(Rules.RetroMode);
            if (lines.LineCount > 0)
            {
                AddScoreOutsideTurn(PriceLines(BuildLineScore(lines, lines.ExplodedCells.Count,
                    false)));
                if (currentReport != null)
                {
                    currentReport.AddExtraExplodedCells(lines.ExplodedCells);
                }
                ResyncSnapshot();
                TryResolveCleanSweep();
            }
            return result;
        }

        /// <summary>True while that card id is sitting in the bonus hand ("Antimadde" checking
        /// whether the antimatter it minted is still there to rot).</summary>
        internal bool BonusHandHolds(int cardId)
        {
            for (int i = 0; i < bonusHand.Count; i++)
            {
                if (bonusHand[i].Card.Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Takes a bonus card off the table entirely - not to the discard, not to the
        /// deck, gone ("Antimadde" decaying to nothing). Returns false when it was not there.
        /// </summary>
        internal bool RemoveBonusCard(int cardId)
        {
            for (int i = 0; i < bonusHand.Count; i++)
            {
                if (bonusHand[i].Card.Id == cardId)
                {
                    bonusHand.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>"Hologram": moves a bonus-hand card into the discard, folding it back
        /// into the round's pile economy instead of letting it expire unused.</summary>
        internal bool MoveBonusCardToDiscard(int bonusIndex)
        {
            if (bonusIndex < 0 || bonusIndex >= bonusHand.Count)
            {
                return false;
            }
            BlockCard card = bonusHand[bonusIndex].Card;
            bonusHand.RemoveAt(bonusIndex);
            Deck.Discard(card);
            return true;
        }

        /// <summary>
        /// Bills the player a penalty from a boss, whether or not a turn is resolving ("Dört kutup"
        /// charging for a turn the live quarter had no room for - which can happen mid-turn or at
        /// round start, before there is any report to attach to).
        ///
        /// Inside a turn it goes through AddLateTurnScore, so the central round-score clamp covers
        /// it and a turn can be emptied but never pushed below where it started. Outside one it is
        /// floored at zero directly, because the round score and the run currency never go negative.
        /// </summary>
        internal void ChargeScore(int amount, string source)
        {
            if (amount <= 0)
            {
                return;
            }
            if (currentReport != null)
            {
                AddLateTurnScore(-amount, source);
                return;
            }
            int scaled = amount * scorer.ScoreScale;
            if (scaled > RoundScore)
            {
                scaled = RoundScore;
            }
            if (scaled <= 0)
            {
                return;
            }
            RoundScore -= scaled;
            if (session != null)
            {
                session.AddCurrency(-scaled);
            }
        }

        /// <summary>Banks score from something that happened BETWEEN turns - a power, which
        /// never costs a turn and therefore has no TurnReport to attach to. Crossing the
        /// threshold here raises the advance offer exactly as it would mid-turn.</summary>
        internal void AddScoreOutsideTurn(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            if (currentReport != null)
            {
                AddLateTurnScore(amount, "power");
                return;
            }
            // Between turns there is no breakdown to scale, so lift the logical amount into the
            // scaled economy here before it touches RoundScore and the run currency.
            int scaled = amount * scorer.ScoreScale;
            RoundScore += scaled;
            if (session != null)
            {
                session.AddCurrency(scaled);
            }
            if (!ThresholdPassed && RoundScore >= ScaledThreshold)
            {
                if (RoundOutcomeInverted)
                {
                    DeclareLoss(LossReason.ForbiddenThreshold); // "Çıkmaz", between turns
                    return;
                }
                CapScoreAtThresholdOnCrossing();
                ThresholdPassed = true;
                Deck.ShuffleDiscardIntoDraw();
                EnterOvertime();
                SetStatus(RoundStatus.AwaitingAdvanceDecision);
            }
        }

        /// <summary>
        /// CONFIRMED RULE: a round's score is CAPPED at its own threshold. Reaching the bar is
        /// the whole goal of normal play, so the turn that crosses it takes you TO it and no
        /// further - a sweep that would have carried 600 past a 650 bar all the way to 1200
        /// banks 650. The only way past the threshold is to decline the advance and play
        /// OVERTIME for it.
        ///
        /// The excess is dropped from the run currency too, through the turn report the session
        /// reads afterwards, so the money can never outrun the meter that earned it.
        ///
        /// Call this the moment the threshold is first reached, before ThresholdPassed is set.
        /// </summary>
        private void CapScoreAtThresholdOnCrossing()
        {
            int excess = RoundScore - ScaledThreshold;
            if (excess <= 0)
            {
                return;
            }
            RoundScore = ScaledThreshold;
            if (currentReport != null)
            {
                // The session banks report.ScoreGained after the turn resolves, so trimming it
                // here is what keeps TotalScore honest - no separate refund needed.
                currentReport.ScoreGained -= excess;
                currentReport.RoundScoreAfter = RoundScore;
            }
            else if (session != null)
            {
                // Between turns there is no report to trim, so the currency is given back.
                session.AddCurrency(-excess);
            }
        }

        /// <summary>The one place overtime "begins". Wipes the board-rewind history so "Kum
        /// saati" cannot reach back across the threshold, and lets jokers react to the
        /// transition (Seri Tetik takes its hand bonus back). Also invoked when the player
        /// continues an overtime, so the history restarts each continue.</summary>
        private void EnterOvertime()
        {
            boardHistory.Clear();
            if (session != null)
            {
                session.Jokers.DispatchOvertimeStarted(this);
            }
        }

        /// <summary>Cubes destroyed this turn by sources that COUNT (line explosions, fire
        /// chains, dynamite, joker effects that opted in). A scoreless wipe that opted OUT
        /// (Deprem's collapse) is excluded, which is what keeps it from feeding "Kayıt defteri".</summary>
        internal int CubesDestroyedThisTurn
        {
            get { return cubesDestroyedThisTurn; }
        }

        /// <summary>Fetches a played card back out of the draw/discard piles, or null if it
        /// is not in either. Used by "Kazı çalışması" to hand a block back to the player.</summary>
        internal BlockCard TakeCardFromPiles(int cardId)
        {
            return Deck.TakeCard(cardId);
        }

        /// <summary>Pulls a card the run deck just lost out of this round too, so a tax bites
        /// now instead of next round (GameSession.TaxOwnedCards). A card the player is HOLDING
        /// is left alone: it vanishes at round end anyway, and yanking it out of the hand could
        /// dead-end the round through no fault of the player's.</summary>
        internal void TaxCardOutOfRound(BlockCard card)
        {
            if (card == null)
            {
                return;
            }
            BlockCard pulled = Deck.TakeCard(card.Id);
            if (pulled != null)
            {
                Deck.RemoveFromRound(pulled);
            }
        }

        /// <summary>"Pull the earned score to the threshold": the round's contribution to the
        /// run-wide TotalScore is capped at the threshold and the local meter drops to it.
        /// Used by "İkinci şans" and "Totem" in overtime, where the score is above threshold.</summary>
        internal void CapRoundScoreAtThreshold()
        {
            int excess = RoundScore - ScaledThreshold;
            if (excess <= 0)
            {
                return;
            }
            RoundScore = ScaledThreshold;
            if (session != null)
            {
                // excess is already in scaled units (RoundScore is scaled), so no extra scale.
                session.AddCurrency(-excess); // remove the overtime-farmed excess from the run
            }
        }

        /// <summary>Lowest y that is part of the retro dead zone (the top DeadZoneRows rows), or
        /// int.MaxValue when there is no dead zone. Rows at or above this are the overflow zone.</summary>
        private int DeadZoneFloor
        {
            get
            {
                return Rules.DeadZoneRows > 0
                    ? Board.MinY + Board.Height - Rules.DeadZoneRows
                    : int.MaxValue;
            }
        }

        private bool IsDeadRow(int y)
        {
            return y >= DeadZoneFloor;
        }

        /// <summary>Score for a line explosion, with the retro dead zone excluded: rows and cubes
        /// in the top overflow zone earn nothing (a clear up there is survival, not points). Plain
        /// scoring when there is no dead zone.</summary>
        private int ScoreLineExplosionScored(LineExplosionResult explosion, int cubesExploded)
        {
            return PriceLines(BuildLineScore(explosion, cubesExploded, true));
        }

        /// <summary>Splits an explosion into the counts a boss round prices ("Ufuk"/"Kule" pay
        /// for one axis only). <paramref name="honourDeadZone"/> drops retro dead-zone rows and
        /// cubes; the between-turn path passes false, keeping its long-standing behaviour.</summary>
        private LineExplosionScore BuildLineScore(LineExplosionResult explosion, int cubesExploded,
            bool honourDeadZone)
        {
            bool deadZone = honourDeadZone && Rules.DeadZoneRows > 0;
            int scoredRows = 0;
            for (int i = 0; i < explosion.Rows.Count; i++)
            {
                if (!deadZone || !IsDeadRow(explosion.Rows[i]))
                {
                    scoredRows++;
                }
            }
            int deadCubes = 0;
            int rowCubes = 0;
            int columnCubes = 0;
            for (int i = 0; i < explosion.ExplodedCells.Count; i++)
            {
                GridPos cell = explosion.ExplodedCells[i];
                if (deadZone && IsDeadRow(cell.Y))
                {
                    deadCubes++;
                    continue; // scores for nothing, on either axis
                }
                if (Contains(explosion.Rows, cell.Y))
                {
                    rowCubes++;
                }
                if (Contains(explosion.Columns, cell.X))
                {
                    columnCubes++;
                }
            }
            int scoredCubes = System.Math.Max(0, cubesExploded - deadCubes);
            return new LineExplosionScore(scoredRows, explosion.Columns.Count, scoredCubes,
                rowCubes, columnCubes);
        }

        /// <summary>What those counts pay. The boss round gets the final say; with no boss this
        /// is exactly the plain rule, so an ordinary round scores byte-identically to before.</summary>
        private int PriceLines(LineExplosionScore lines)
        {
            if (Boss != null)
            {
                return Boss.ScoreLineExplosion(scorer, lines);
            }
            return scorer.ScoreLineExplosion(lines.Rows + lines.Columns, lines.Cubes);
        }

        /// <summary>What a boss adds to or takes off an explosion for the SPECIFIC cells it
        /// touched ("Karantina"). 0 with no boss, so an ordinary round is untouched.</summary>
        private int AdjustExplosionScore(IReadOnlyList<GridPos> cells)
        {
            return Boss != null ? Boss.AdjustExplosionScore(scorer, cells) : 0;
        }

        /// <summary>What a clean sweep pays. Same deal as PriceLines: the boss gets the final
        /// say ("Titizlik" pays more for the one thing it leaves standing), and with no boss it
        /// is exactly the plain rule.</summary>
        private int PriceCleanSweep()
        {
            return Boss != null ? Boss.ScoreCleanSweep(scorer) : scorer.ScoreCleanSweep();
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Retro/Tetris gravity, run after a placement's full rows have cleared: the rows
        /// ABOVE each cleared row drop straight down (classic Tetris - a locked block never falls
        /// into a gap, only whole rows collapse and everything resting above rides down together).
        /// Re-bases the destruction snapshot so the moved cubes are not mistaken for destroyed ones,
        /// exactly like a water settle. No cascade: a row-collapse cannot complete a new line, so
        /// nothing re-explodes and no extra score is granted. Only called in retro mode.</summary>
        private void CollapseRetroLines(IReadOnlyList<int> clearedRows)
        {
            if (Board.CollapseClearedRows(clearedRows))
            {
                ResyncSnapshot(); // cubes moved, nothing died - re-baseline the destruction diff
            }
        }

        /// <summary>True if any cube sits in the retro dead zone. The retro toggle reads this - it
        /// refuses to turn off while the dead zone still holds cubes.</summary>
        public bool DeadZoneOccupied
        {
            get
            {
                if (Rules.DeadZoneRows <= 0)
                {
                    return false;
                }
                int top = Board.MinY + Board.Height - 1;
                for (int y = DeadZoneFloor; y <= top; y++)
                {
                    for (int x = Board.MinX; x <= Board.MinX + Board.Width - 1; x++)
                    {
                        if (Board.GetCube(new GridPos(x, y)).HasValue)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>Retro top-out: any cube reached the very top row, so a new piece can no longer
        /// drop in from above (even if lower rows still have gaps) - the Tetris loss. False when
        /// there is no dead zone (i.e. not in retro).</summary>
        private bool IsToppedOut()
        {
            if (Rules.DeadZoneRows <= 0)
            {
                return false;
            }
            int topY = Board.MinY + Board.Height - 1;
            for (int x = Board.MinX; x <= Board.MinX + Board.Width - 1; x++)
            {
                if (Board.GetCube(new GridPos(x, topY)).HasValue)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Clears every destructible cube WITHOUT scoring or triggering a sweep
        /// ("İkinci şans"). Indestructible cubes (obsidian/gold/Parazit host) stay.</summary>
        internal void ClearBoardScoreless()
        {
            var cells = new List<GridPos>(Board.GetOccupiedCells());
            DestroyCubes(cells, false);
        }

        /// <summary>Ends the round straight to the market ("Totem"), with no advance offer.</summary>
        internal void ForceAdvanceToMarket()
        {
            SetStatus(RoundStatus.Advanced);
        }

        /// <summary>
        /// "Alzheimer": lifts a card's cubes off EVERY world without any of the bookkeeping a
        /// destruction carries - no score, no sweep, nothing for "Kayıt defteri" to count. The
        /// snapshot is re-baselined straight afterwards, and THAT is what makes it not a
        /// destruction: the turn's diff never sees these cubes leave.
        ///
        /// Indestructible cubes go too. The board is not breaking them, it is forgetting they
        /// were ever laid, and nothing survives being forgotten.
        /// </summary>
        internal IReadOnlyList<GridPos> ForgetCard(int cardId)
        {
            var cleared = new List<GridPos>(MainBoard.ForgetCard(cardId));
            if (MirrorBoard != null)
            {
                cleared.AddRange(MirrorBoard.ForgetCard(cardId));
            }
            if (cleared.Count > 0)
            {
                ResyncSnapshot();
                if (currentReport != null)
                {
                    currentReport.AddLiftedCells(cleared);
                }
            }
            return cleared;
        }

        /// <summary>
        /// "Yürüyen merdiven": runs the escalator on EVERY world - each row rides up one and the
        /// top row is carried off. Same terms as forgetting: no score, no sweep, no tally, and
        /// the snapshot is re-baselined so the turn's diff never reads it as a destruction.
        /// </summary>
        internal IReadOnlyList<GridPos> EscalateBoards()
        {
            var lost = new List<GridPos>(MainBoard.ShiftRowsUp());
            if (MirrorBoard != null)
            {
                lost.AddRange(MirrorBoard.ShiftRowsUp());
            }
            // ALWAYS re-baseline: the escalator moves cubes even when it carries none off, and a
            // moved cube would otherwise read as a destroyed one.
            ResyncSnapshot();
            if (lost.Count > 0 && currentReport != null)
            {
                currentReport.AddLiftedCells(lost);
            }
            return lost;
        }

        /// <summary>
        /// "Merkezkaç kuvveti": flings every cube one cell further from the middle, on BOTH worlds,
        /// and reports what went over the edge. Same contract as EscalateBoards - the cubes are
        /// LIFTED, not destroyed, so nothing scores, nothing counts toward a sweep and the
        /// destruction diff is re-baselined so moved cubes do not read as dead ones.
        /// </summary>
        internal IReadOnlyList<GridPos> FlingBoardsOutward()
        {
            var lost = new List<GridPos>(MainBoard.FlingCubesOutward());
            if (MirrorBoard != null)
            {
                lost.AddRange(MirrorBoard.FlingCubesOutward());
            }
            ResyncSnapshot();
            if (lost.Count > 0 && currentReport != null)
            {
                currentReport.AddLiftedCells(lost);
            }
            return lost;
        }

        /// <summary>
        /// "Kangren": spreads the infection one cell and lets it kill whatever line it has taken
        /// whole, cascading to the nearest edge. Returns the cell it spread to, or null when there
        /// was nowhere left.
        ///
        /// MAIN BOARD ONLY, like the other board bosses: "Öteki dünya" opening a second arena must
        /// not double an infection that was balanced against one.
        ///
        /// The converted cells are reported as LIFTED - not because anything left the board, but
        /// because that is the report's channel for "these cells changed without being destroyed",
        /// and the View needs to know where to redraw. Nothing here scores or counts for a sweep.
        /// </summary>
        internal GridPos? SpreadGangrene()
        {
            GridPos? spread = MainBoard.SpreadGangrene(rng);
            List<GridPos> converted = MainBoard.InfectFullLines();
            // The infection changes cubes in place; a converted cube is not a destroyed one, so the
            // destruction diff has to be re-baselined or it would read as a killing.
            ResyncSnapshot();
            if (converted.Count > 0 && currentReport != null)
            {
                currentReport.AddLiftedCells(converted);
            }
            return spread;
        }

        /// <summary>
        /// "Kütleçekim merkezi": turns the arena's gravity, and makes the water already standing
        /// on it obey the new pull AT ONCE - the flow is the whole point of the power, so it must
        /// be visible the moment the charge is spent rather than waiting for the next placement.
        ///
        /// Written to the world the activation was aimed at, so a power pointed at the mirror
        /// ("Öteki dünya") turns the mirror's gravity and not the main world's. Any line the flow
        /// happens to complete explodes under the ordinary between-turn rules, which is also what
        /// gives "Genel temizlik" its say.
        /// </summary>
        internal void SetWaterFlow(GridPos direction)
        {
            Board.SetWaterFlow(direction);
            externalWaterFrames.Clear();
            Board.SettleWaterAndReact(externalWaterFrames);
            // Water MOVED; nothing died. Re-baseline or the diff reads a flow as a killing.
            ResyncSnapshot();
            ResolveFullLinesOutsideTurn();
        }

        /// <summary>Water moves the last power caused, for the View to animate. Empty otherwise;
        /// cleared by BeginExternalCapture like the destruction log it sits beside.</summary>
        public IReadOnlyList<IReadOnlyList<WaterMove>> ExternalWaterFrames
        {
            get { return externalWaterFrames; }
        }

        /// <summary>
        /// A boss REARRANGED the board rather than destroying anything ("Snake" sliding its body
        /// along, one cell in at the head and one out at the tail). Re-baselines the destruction
        /// diff so the cubes that MOVED are never mistaken for cubes that died - the same
        /// treatment the water settle and "Alzheimer"'s forgetting already get.
        ///
        /// Call it after the rearrangement and before anything that destroys for real, or the
        /// next diff will bill this turn for cubes that simply walked away.
        /// </summary>
        internal void NoteBoardRearranged()
        {
            ResyncSnapshot();
        }

        /// <summary>Gangrene cubes standing on the main board right now.</summary>
        internal int CountGangrene()
        {
            return MainBoard.CountGangrene();
        }

        /// <summary>Records why the RUN ended on a round the player actually survived
        /// ("Kredi kartı" hitting its deadline). The status stays Advanced - the round was won -
        /// so nothing re-reads this as a lost round; it only gives the UI a cause to name.</summary>
        internal void NoteRunLoss(LossReason reason)
        {
            if (Loss == null)
            {
                Loss = reason;
            }
        }

        /// <summary>
        /// Ends the round as WON on a boss's OWN terms rather than at the score bar ("Matruşka"
        /// cracking the last doll, "Snake" killing the snake). Three things follow from that, and
        /// each is deliberate:
        ///
        ///  - the round banks EXACTLY its threshold and not a point more. The bar is a ceiling
        ///    for normal play and beating the boss is not overtime, so the payout is the bar.
        ///  - there is NO advance offer. ThresholdPassed is set here, which is also what stops
        ///    the turn resolver's own threshold check from opening overtime behind our back: a
        ///    boss beaten on its own terms hands you the market, not another lap.
        ///  - A LOSS OUTRANKS IT. A boss whose round can be lost by a careless move (exploding a
        ///    doll-less line) settles that first, so the same turn cannot both kill and crown.
        ///
        /// Inside a turn this only ARMS the win - the status is set at step 10 with every other
        /// outcome, so nothing later in the turn can overwrite it.
        /// </summary>
        internal void DeclareRoundWon()
        {
            if (Loss != null || Status == RoundStatus.Advanced || Status == RoundStatus.Lost)
            {
                return;
            }
            int owed = ScaledThreshold - RoundScore;
            if (owed > 0)
            {
                RoundScore += owed;
                if (currentReport != null)
                {
                    // The session banks report.ScoreGained when the turn resolves, so keeping it
                    // in step is what pays the player. (Step 8.6 re-derives the same number.)
                    currentReport.ScoreGained = RoundScore - turnStartRoundScore;
                    currentReport.RoundScoreAfter = RoundScore;
                }
                else if (session != null)
                {
                    session.AddCurrency(owed);
                }
            }
            ThresholdPassed = true;
            bossWonTheRound = true;
            if (currentReport == null)
            {
                SetStatus(RoundStatus.Advanced);
            }
        }

        /// <summary>Ends the round with a joker-defined reason ("Batak" losing its bet).
        /// Obeys the standing rule that a pending advance offer outranks a same-turn loss.</summary>
        internal void DeclareLoss(LossReason reason)
        {
            if (Loss == null)
            {
                Loss = reason;
            }
            if (!pendingAdvanceOffer && currentReport == null)
            {
                SetStatus(RoundStatus.Lost);
            }
        }

        /// <summary>Raises the clean sweep from a joker rule rather than from an empty board
        /// ("Kayıt defteri" hitting its cube count). Still at most one sweep per turn.</summary>
        internal bool ForceCleanSweep()
        {
            // Kayıt defteri's counter sweep. external:false - once it has suppressed the natural
            // sweep, the counter IS the player's sweep, so it counts (pays bonus + recharges).
            return ResolveCleanSweep(true, false);
        }
    }
}
