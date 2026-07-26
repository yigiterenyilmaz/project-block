// PURPOSE: The base type of every boss round ("patron raundu"). A boss is the round's
// ANTAGONIST: unlike a joker it is not owned, not bought and cannot be sold - it is handed
// to one round by GameSession and dies with it.
//
// THE THREE CENTRAL RULES, enforced by RoundEngine / the inventories so no boss has to:
//  1. ONE BOSS PER ROUND, and only on a round the progression flagged
//     (RoundConfig.IsBossRound). Never two at once, so bosses never need to compose.
//  2. ROUND-SCOPED. A boss instance belongs to one RoundEngine. It must NEVER mutate
//     session-level state (RoundRules, ScoringConfig, OwnedCards) to express a rule bend -
//     that state outlives the round and the bend would leak into the next one. Express
//     bends by OVERRIDING THE QUERIES below; the engine asks them live.
//     (The two tax bosses do touch OwnedCards, but that is their effect, not a rule bend.)
//  3. NO RANDOMNESS OF ITS OWN. Everything random comes from the context's IRandomSource.
//
// Hooks are virtual no-ops; a boss overrides only what it needs. Queries are asked every
// time the engine reaches the decision, so a boss may change its answer mid-round.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>A boss round's rules. Subclass, override what you need, register in BossRegistry.</summary>
    public abstract class BossRound
    {
        protected BossRound(string defId, string displayName)
        {
            DefId = defId;
            DisplayName = displayName;
        }

        /// <summary>Stable content id ("ufuk"). The save/replay key - never rename it.</summary>
        public string DefId { get; }

        /// <summary>Human-readable name for the UI (Turkish).</summary>
        public string DisplayName { get; }

        /// <summary>One-line rules text in the ACTIVE language (see Loc). Read live, so a
        /// language switch takes effect on the next UI refresh.</summary>
        public string Description
        {
            get { return Loc.Pick(descriptionEn, descriptionTr); }
        }

        private string descriptionEn = string.Empty;
        private string descriptionTr = string.Empty;

        /// <summary>Sets the rules text in both languages.</summary>
        protected void SetDescription(string english, string turkish)
        {
            descriptionEn = english;
            descriptionTr = turkish;
        }

        /// <summary>Short live state for the UI ("2 kart gitti"), or null when there is
        /// nothing to show.</summary>
        public virtual string StatusText
        {
            get { return null; }
        }

        // ------------------------------------------------------------------- lifecycle

        /// <summary>The round has been built and this boss is now attached to it. The one
        /// place to pick a victim, arm a counter, or take a first bite.</summary>
        /// <summary>
        /// Reshapes the round before it is built - the ONE bend that cannot be a live query,
        /// because the board is made once and the boss has to be able to change its size
        /// ("Dört kutup" rounding the arena up to an even edge so it splits into four).
        ///
        /// Use RoundConfig.WithBoard, never a hand-written new RoundConfig: a field listed by hand
        /// is a field that can be forgotten, and both IsBossRound and Erosion have been dropped
        /// that way once each.
        /// </summary>
        public virtual RoundConfig FilterRoundConfig(RoundConfig config)
        {
            return config;
        }

        /// <summary>
        /// The board is stuck and the round is about to end. A boss whose own rule caused the jam
        /// gets to undo it instead ("Dört kutup" turning to the next region and charging for the
        /// wasted turn). Return true if something changed; the engine then re-checks for a move and
        /// may ask again, up to a bounded number of times.
        ///
        /// This is NOT a rescue in the player's favour - it exists so a boss cannot lock the round
        /// with its own restriction. A boss that jams nothing should never override this.
        /// </summary>
        public virtual bool TryEscapeDeadEnd(RoundContext ctx)
        {
            return false;
        }

        public virtual void OnRoundStarted(RoundContext ctx)
        {
        }

        // ------------------------------------------------- queries (asked live, every time)

        /// <summary>True while every block must behave as a plain block, its element ignored
        /// ("Vanilya"). Read by the engine and the board on every placement.</summary>
        public virtual bool IgnoresBlockElements
        {
            get { return false; }
        }

        /// <summary>True while nothing may put a charge back into a power ("Tükenmişlik") -
        /// clean sweeps and "Powerbank" alike. The round-start recharge already happened.</summary>
        /// <summary>
        /// While true a full line does NOT explode - it just sits there, full ("Bilinmezlik").
        /// Asked live by the turn resolver, on BOTH worlds, because it is a rule about how lines
        /// behave rather than something done to one board.
        ///
        /// The board fills up fast under this, and running out of room is an ordinary dead end
        /// (confirmed design): the boss does not rescue you from its own rule.
        /// </summary>
        public virtual bool SuppressesLineExplosions
        {
            get { return false; }
        }

        /// <summary>
        /// While true NOTHING scores by itself - not a placement, not a line, not a combo, not
        /// gold, not a sweep ("Bürokrasi bataklığı", where the only income is finishing the task
        /// you were handed). A boss that switches this on has to pay the player itself, or the
        /// round is unwinnable.
        ///
        /// Base values only, exactly like the score bosses: a joker's own bonuses still land.
        /// </summary>
        public virtual bool SuppressesAllBaseScore
        {
            get { return false; }
        }

        /// <summary>
        /// True for a boss that may only ever be drawn as a run's FIRST boss ("Bul parayı al
        /// karayı" is a coin flip, and a coin flip is an introduction, not a wall). Read by
        /// GameSession.DrawBoss.
        /// </summary>
        public virtual bool OnlyOnFirstBossRound
        {
            get { return false; }
        }

        public virtual bool BlocksPowerRecharge
        {
            get { return false; }
        }

        /// <summary>True while this joker must be treated as silenced: every hook skipped,
        /// exactly like the overtime gate ("Anarşi", "Oburluk"). It stays in the inventory
        /// and keeps its sell value.</summary>
        public virtual bool DisablesJoker(Joker joker)
        {
            return false;
        }

        /// <summary>As DisablesJoker, for a power: it cannot be used and its hooks are skipped.</summary>
        public virtual bool DisablesPower(Power power)
        {
            return false;
        }

        /// <summary>True while every joker works AGAINST the player ("Terslik"): the points and
        /// the sell value a joker grants itself become losses of the same size. Jokers that grant
        /// neither are untouched and keep doing their job. Powers are never inverted.</summary>
        public virtual bool InvertsJokerScore
        {
            get { return false; }
        }

        /// <summary>True if a block may NOT be placed on this empty cell ("Mapus" sealing one
        /// cell per turn). Only ever asked about cells that are empty and on the board.</summary>
        public virtual bool BlocksPlacementOn(GridPos cell)
        {
            return false;
        }

        /// <summary>Rewrites what a line explosion pays ("Ufuk", "Kule"). The default is the
        /// plain rule. See LineExplosionScore for what each count means - it arrives with the
        /// retro dead zone already taken out, so a boss never has to know about that.</summary>
        public virtual int ScoreLineExplosion(IScoreCalculator scorer, LineExplosionScore lines)
        {
            return scorer.ScoreLineExplosion(lines.Rows + lines.Columns, lines.Cubes);
        }

        /// <summary>
        /// Score a boss adds to - or takes off - an explosion, judged on the CELLS it actually
        /// touched rather than on the counts ("Karantina" charging for the ones inside its
        /// zones). The counts in LineExplosionScore cannot express "which cubes", which is why
        /// this is a separate hook rather than another argument to ScoreLineExplosion.
        ///
        /// Return a negative number to take score away. It is applied on top of the normal
        /// price, so a boss that wants a cube to LOSE what it would have earned returns twice
        /// its value negated. The cells are absolute board coordinates.
        /// </summary>
        public virtual int AdjustExplosionScore(IScoreCalculator scorer,
            IReadOnlyList<GridPos> cells)
        {
            return 0;
        }

        /// <summary>Rewrites what a clean sweep pays ("Titizlik" sweetening the one thing it
        /// leaves standing).</summary>
        public virtual int ScoreCleanSweep(IScoreCalculator scorer)
        {
            return scorer.ScoreCleanSweep();
        }

        /// <summary>True while the ONLY thing that pays is a clean sweep ("Titizlik"): placing
        /// blocks, clearing lines, the combo and the gold upkeep all pay nothing. Deliberately
        /// about the BASE values only - a joker's own bonuses still land, exactly as they do
        /// under "Ufuk" and "Kule", so the boss beats your board rather than your build.</summary>
        public virtual bool OnlyCleanSweepsScore
        {
            get { return false; }
        }

        /// <summary>
        /// True while the arena is in DARKNESS ("Alacakaranlık") and the player must play blind.
        ///
        /// The only boss whose entire effect is what the player can SEE. Not one rule bends -
        /// the board, the placements and the scoring are all exactly as they always were - so
        /// nothing in the engine reads this except to hand it to the View.
        /// </summary>
        public virtual bool HidesTheBoard
        {
            get { return false; }
        }

        /// <summary>Rewrites the round's score threshold ("Taş ve sopa" asking for less because
        /// it took your whole inventory away). Read live, in logical (unscaled) points.</summary>
        public virtual int FilterScoreThreshold(int threshold)
        {
            return threshold;
        }

        /// <summary>
        /// "Çıkmaz": the round's win condition is turned inside out. Running out of room WINS
        /// the round; a clean sweep or reaching the threshold LOSES it.
        ///
        /// The engine reads this at the three places that decide a round, and at a fourth: the
        /// AUTOMATIC dead-end rescues (a joker like "Deprem") are skipped, because they would
        /// take a win the player never chose to give up. A rescue the player is OFFERED (the
        /// "Kentsel Dönüşüm" power) still runs - declining it is the win, so the choice is real.
        /// </summary>
        public virtual bool InvertsRoundOutcome
        {
            get { return false; }
        }

        // ---------------------------------------------------------------------- events

        /// <summary>End of a resolved turn - after the hand refill, BEFORE the threshold and
        /// loss checks, so score added here still counts and a cost here can still be fatal.
        /// This is where the per-turn harassers act ("Alıkoyma", "Mapus").</summary>
        public virtual void AfterTurnScored(TurnContext turn)
        {
        }

        /// <summary>A bonus-hand card was just played ("Feda" makes that cost the hand).
        /// Runs inside the turn, right after the card was disposed of.</summary>
        public virtual void OnBonusCardPlayed(TurnContext turn)
        {
        }

        /// <summary>The draw pile just ran dry ("Harcama vergisi" taxes the deck for it).
        /// Raised once per emptying, whether or not the discard was recycled back in.</summary>
        public virtual void OnDrawPileEmptied(RoundContext ctx)
        {
        }

        /// <summary>A power was used ("Özel tüketim vergisi" taxes the deck for it).</summary>
        public virtual void OnPowerUsed(RoundContext ctx, string powerId)
        {
        }

        public override string ToString()
        {
            return DisplayName + " (" + DefId + ")";
        }
    }
}
