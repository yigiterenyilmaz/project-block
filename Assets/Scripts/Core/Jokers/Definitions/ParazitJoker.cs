// PURPOSE: "Parazit" - in the market, bind ONE other joker to a single cube of a block in
// your deck. The bound joker keeps working but stops taking up an inventory slot; if that
// cube is ever destroyed on the board, the joker is destroyed with it.
//
// DESIGN CALLS MADE HERE (all reversible, none of them were pinned down by the design doc):
//  1. A bound joker does NOT occupy a slot (JokerInventory.OccupiedSlots skips it). That is
//     the whole payoff, and it only became meaningful once the market grew a slot cap.
//     Parazit itself keeps its own slot, per "parazitin kendisi envanterde kalmak zorunda".
//  2. ONE binding at a time. Re-binding requires the current host to die or Parazit to go.
//  3. The bound joker works wherever its block is - deck, hand or board. Restricting it to
//     "only while the cube is on the board" would leave it dormant almost all the time,
//     since a card spends most of a round in the piles.
//  4. If Parazit itself leaves the inventory, the bound joker simply comes home: it starts
//     occupying a slot again rather than being destroyed.
//
// HOW THE HOST CUBE IS TRACKED: cubes on the board only carry their SOURCE CARD, not which
// cube of the block they are. So the binding stores a cell INDEX, and the moment the host
// card is played the joker reads TurnReport.PlacedCells[index] to learn where that cube
// actually landed - which is also correct for rotated (Mechanical) and reshaped (Fox) blocks,
// because PlacedCells is the post-transform layout.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Parazit" - rides a joker on a block instead of an inventory slot.</summary>
    public sealed class ParazitJoker : Joker
    {
        /// <summary>Instance id of the joker currently riding a block, or null.</summary>
        public int? BoundJokerInstanceId { get; private set; }

        /// <summary>Card the bound joker rides on.</summary>
        public int HostCardId { get; private set; }

        /// <summary>Which cube of that block carries it (index into the placed cells).</summary>
        public int HostCellIndex { get; private set; }

        /// <summary>Where the host cube currently sits, once the block has been placed.</summary>
        private GridPos? hostPos;

        public ParazitJoker()
            : base("parazit", "Parazit")
        {
            SetDescription(
                "In the market, attach a joker to a cube of a block in your deck. That joker "
                    + "takes no slot. Its host cube is sweep-exempt and unbreakable by other "
                    + "jokers and powers - only a line you complete destroys it (and the joker).",
                "Markette bir jokeri destendeki bir bloğun küpüne takarsın. O joker slot "
                    + "işgal etmez. Konak küp temizliğe girmez ve başka joker/güçlerle kırılmaz "
                    + "- sadece senin tamamladığın bir satır onu (ve jokeri) yok eder.");
            BaseSellValue = 70;
        }

        public bool HasBinding
        {
            get { return BoundJokerInstanceId.HasValue; }
        }

        public override string StatusText
        {
            get
            {
                return HasBinding
                    ? Loc.Pick("bound to block #" + HostCardId, "blok #" + HostCardId + "'e bağlı")
                    : Loc.Pick("idle", "boşta");
            }
        }

        /// <summary>Binds a joker to one cube of one owned card. Market-phase action; the
        /// caller (GameSession.TryAttachJokerToCard) has already validated the phase.</summary>
        public bool TryBind(SessionContext ctx, Joker target, BlockCard card, int cellIndex)
        {
            if (HasBinding || target == null || card == null)
            {
                return false;
            }
            if (ReferenceEquals(target, this) || target.Attachment.HasValue)
            {
                return false; // Parazit cannot ride itself, and a joker rides only one block
            }
            if (cellIndex < 0 || cellIndex >= card.Shape.Size)
            {
                return false;
            }
            target.Attachment = new CubeAttachment(card.Id, cellIndex);
            BoundJokerInstanceId = target.InstanceId;
            HostCardId = card.Id;
            HostCellIndex = cellIndex;
            hostPos = null;
            return true;
        }

        /// <summary>Parazit left the inventory: the passenger comes home to a normal slot.</summary>
        public override void OnRemoved(SessionContext ctx)
        {
            Joker bound = FindBound(ctx.Session);
            if (bound != null)
            {
                bound.Attachment = null;
            }
            ClearBinding();
        }

        /// <summary>The bound joker was sold or destroyed by something else.</summary>
        public override void OnJokerRemoved(SessionContext ctx, Joker other)
        {
            if (BoundJokerInstanceId.HasValue && other.InstanceId == BoundJokerInstanceId.Value)
            {
                ClearBinding();
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            hostPos = null; // the board is fresh; the host cube is back in the piles
        }

        // Destruction can happen at either point in a turn, so both hooks run the same check.
        public override void AfterLineExplosion(TurnContext turn)
        {
            Track(turn);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Track(turn);
        }

        private void Track(TurnContext turn)
        {
            if (!HasBinding)
            {
                return;
            }
            LearnHostPosition(turn);
            if (!hostPos.HasValue)
            {
                return;
            }
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Cube.SourceCardId != HostCardId
                    || !destroyed[i].Pos.Equals(hostPos.Value))
                {
                    continue;
                }
                KillPassenger(turn.Session);
                return;
            }
            // Host cube survived this turn: (re)assert its protection so external jokers and
            // powers cannot break it and it stays out of the clean-sweep check. Only the cube
            // that actually belongs to the host card is protected.
            Cube? here = turn.Round.Board.GetCube(hostPos.Value);
            if (here.HasValue && here.Value.SourceCardId == HostCardId)
            {
                turn.Round.Board.SetCubeProtected(hostPos.Value);
            }
        }

        /// <summary>The turn the host card is played is the only moment its cube's board
        /// position is knowable, so it is captured here.</summary>
        private void LearnHostPosition(TurnContext turn)
        {
            BlockCard played = turn.Report.Card;
            if (played == null || played.Id != HostCardId)
            {
                return;
            }
            IReadOnlyList<GridPos> cells = turn.Report.PlacedCells;
            if (HostCellIndex < cells.Count)
            {
                hostPos = cells[HostCellIndex];
            }
            else if (cells.Count > 0)
            {
                // A Fox reshape can leave the block with fewer cubes than the binding index
                // expects; fall back to the last cube rather than losing track of the host.
                hostPos = cells[cells.Count - 1];
            }
        }

        private void KillPassenger(GameSession session)
        {
            Joker bound = FindBound(session);
            ClearBinding();
            if (bound != null)
            {
                bound.Attachment = null; // so it does not free a slot on the way out
                session.Jokers.Remove(bound);
            }
        }

        private Joker FindBound(GameSession session)
        {
            return BoundJokerInstanceId.HasValue && session != null
                ? session.Jokers.Find(BoundJokerInstanceId.Value)
                : null;
        }

        private void ClearBinding()
        {
            BoundJokerInstanceId = null;
            HostCardId = 0;
            HostCellIndex = 0;
            hostPos = null;
        }
    }
}
