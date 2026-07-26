// PURPOSE: GameSession save/load (partial) - the run half of a mid-run save.
//
// ORDER MATTERS: the card table is written BEFORE anything that references a card, because
// the reader is positional and rebuilds the shared BlockCard instances from it (see
// CoreSerializers). The table collects the owned deck AND the round-scoped cards that never
// joined it - a bonus "Kara delik" void block lives only in the round.
//
// JOKERS AND POWERS ARE NOT RE-ACQUIRED on load: they are created from the registry, given
// their saved state, and put straight into the inventory. Running OnAcquired again would
// re-apply permanent rule changes ("Seri tetik" grants +2 hand size) on top of the saved
// RoundRules, which already has them - the hand would grow every time the game was loaded.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class GameSession
    {
        /// <summary>Load-only constructor: no starting deck is generated and no round is
        /// started - Load fills all of it in. The rng arrives already wound to the saved
        /// position (see SeededRandom).</summary>
        private GameSession(GameConfig config, SeededRandom restoredRng)
        {
            Config = config;
            resolvedSeed = restoredRng.Seed;
            rng = restoredRng;
            scorer = new DefaultScoreCalculator(config.Scoring);
            Market = new Market();
            Jokers = new JokerInventory(this, rng);
            Powers = new PowerInventory(this, rng);
        }

        internal void Save(SaveWriter w)
        {
            var seeded = rng as SeededRandom;
            if (seeded == null)
            {
                throw new SaveFormatException(
                    "This run's random source cannot be saved (not a SeededRandom).");
            }
            seeded.Save(w, "rng");
            CoreSerializers.WriteRules(w, "rules", Config.Rules);
            CoreSerializers.WriteScoring(w, "scoring", Config.Scoring);

            w.Write("phase", (int)Phase);
            w.Write("round", RoundNumber);
            w.Write("total", TotalScore);
            w.Write("nextCardId", nextCardId);
            w.Write("purchased", purchasedThisMarket);
            w.Write("discount", PendingMarketDiscount);
            w.Write("rerolls", rerollCount);

            w.Write("openingHand.has", pendingOpeningHand != null);
            if (pendingOpeningHand != null)
            {
                w.Write("openingHand.count", pendingOpeningHand.Count);
                for (int i = 0; i < pendingOpeningHand.Count; i++)
                {
                    w.Write("openingHand." + i, pendingOpeningHand[i]);
                }
            }

            w.Write("bosses.count", bossesFought.Count);
            for (int i = 0; i < bossesFought.Count; i++)
            {
                w.Write("bosses." + i, bossesFought[i]);
            }

            // The card table first - everything below refers into it.
            CardTable cards = CollectCards();
            cards.Write(w, "cards");
            cards.WriteRefs(w, "owned", ownedCards);

            WriteMarket(w, "market");
            WriteJokers(w, "jokers");
            WritePowers(w, "powers");

            w.Write("hasRound", CurrentRound != null);
            if (CurrentRound != null)
            {
                CurrentRound.Save(w, "roundState", cards);
            }
        }

        internal static GameSession Load(SaveReader r, GameConfig config)
        {
            SeededRandom rng = SeededRandom.Load(r, "rng");
            // The live rules and scoring are restored INTO the config's own instances, because
            // the engine and the calculator hold references to exactly those objects.
            CoreSerializers.ReadRulesInto(r, "rules", config.Rules);
            CoreSerializers.ReadScoringInto(r, "scoring", config.Scoring);

            var session = new GameSession(config, rng);
            session.Phase = (GamePhase)r.ReadInt("phase");
            session.RoundNumber = r.ReadInt("round");
            session.TotalScore = r.ReadLong("total");
            session.nextCardId = r.ReadInt("nextCardId");
            session.purchasedThisMarket = r.ReadBool("purchased");
            session.PendingMarketDiscount = r.ReadDouble("discount");
            session.rerollCount = r.ReadInt("rerolls");

            if (r.ReadBool("openingHand.has"))
            {
                int count = r.ReadInt("openingHand.count");
                session.pendingOpeningHand = new List<int>(count);
                for (int i = 0; i < count; i++)
                {
                    session.pendingOpeningHand.Add(r.ReadInt("openingHand." + i));
                }
            }

            int bossCount = r.ReadInt("bosses.count");
            for (int i = 0; i < bossCount; i++)
            {
                session.bossesFought.Add(r.ReadString("bosses." + i));
            }

            CardTable cards = CardTable.Read(r, "cards");
            session.ownedCards.AddRange(cards.ReadRefs(r, "owned"));

            session.ReadMarket(r, "market", cards);
            session.ReadJokers(r, "jokers");
            session.ReadPowers(r, "powers");

            if (r.ReadBool("hasRound"))
            {
                session.CurrentRound = RoundEngine.Load(r, "roundState", cards, config.Rules,
                    rng, session.scorer, session, session.Jokers);
                session.CurrentRound.TurnResolved += session.OnTurnResolved;
                session.CurrentRound.StatusChanged += session.OnRoundStatusChanged;
            }
            return session;
        }

        /// <summary>Every card the run knows about: the owned deck plus the round-scoped ones
        /// that never joined it, plus the blocks still sitting on the market shelf.</summary>
        private CardTable CollectCards()
        {
            var cards = new CardTable();
            cards.AddRange(ownedCards);
            if (CurrentRound != null)
            {
                cards.AddRange(CurrentRound.AllRoundCards());
            }
            for (int i = 0; i < Market.Offers.Count; i++)
            {
                cards.Add(Market.Offers[i].Card); // null for joker/power offers, ignored
            }
            return cards;
        }

        private void WriteMarket(SaveWriter w, string key)
        {
            IReadOnlyList<MarketOffer> offers = Market.Offers;
            w.Write(key + ".count", offers.Count);
            for (int i = 0; i < offers.Count; i++)
            {
                MarketOffer offer = offers[i];
                w.Write(key + "." + i + ".kind", (int)offer.Kind);
                w.Write(key + "." + i + ".price", offer.Price);
                w.Write(key + "." + i + ".sold", offer.Sold);
                w.Write(key + "." + i + ".card", offer.Card != null ? offer.Card.Id : 0);
                w.Write(key + "." + i + ".joker", offer.Joker != null ? offer.Joker.DefId : null);
                w.Write(key + "." + i + ".power", offer.Power != null ? offer.Power.DefId : null);
            }
        }

        private void ReadMarket(SaveReader r, string key, CardTable cards)
        {
            int count = r.ReadInt(key + ".count");
            var offers = new List<MarketOffer>(count);
            for (int i = 0; i < count; i++)
            {
                var kind = (MarketOfferKind)r.ReadInt(key + "." + i + ".kind");
                int price = r.ReadInt(key + "." + i + ".price");
                bool sold = r.ReadBool(key + "." + i + ".sold");
                int cardId = r.ReadInt(key + "." + i + ".card");
                string jokerId = r.ReadString(key + "." + i + ".joker");
                string powerId = r.ReadString(key + "." + i + ".power");
                MarketOffer offer;
                if (kind == MarketOfferKind.Joker)
                {
                    offer = new MarketOffer(RequireJoker(jokerId), price);
                }
                else if (kind == MarketOfferKind.Power)
                {
                    offer = new MarketOffer(RequirePower(powerId), price);
                }
                else
                {
                    BlockCard card = cards.Get(cardId);
                    if (card == null)
                    {
                        throw new SaveFormatException("Market block " + cardId + " is missing.");
                    }
                    offer = new MarketOffer(card, price);
                }
                offer.Sold = sold;
                offers.Add(offer);
            }
            Market.SetOffers(offers);
        }

        private static JokerDefinition RequireJoker(string defId)
        {
            JokerDefinition definition = JokerRegistry.Get(defId);
            if (definition == null)
            {
                throw new SaveFormatException("Unknown joker '" + defId + "' in the save.");
            }
            return definition;
        }

        private static PowerDefinition RequirePower(string defId)
        {
            PowerDefinition definition = PowerRegistry.Get(defId);
            if (definition == null)
            {
                throw new SaveFormatException("Unknown power '" + defId + "' in the save.");
            }
            return definition;
        }

        private void WriteJokers(SaveWriter w, string key)
        {
            IReadOnlyList<Joker> held = Jokers.Jokers;
            w.Write(key + ".slots", Jokers.MaxSlots);
            w.Write(key + ".nextId", Jokers.NextInstanceId);
            w.Write(key + ".count", held.Count);
            for (int i = 0; i < held.Count; i++)
            {
                w.Write(key + "." + i + ".def", held[i].DefId);
                ContentStateSerializer.Save(w, key + "." + i + ".state", held[i]);
            }
        }

        private void ReadJokers(SaveReader r, string key)
        {
            Jokers.MaxSlots = r.ReadInt(key + ".slots");
            Jokers.NextInstanceId = r.ReadInt(key + ".nextId");
            int count = r.ReadInt(key + ".count");
            for (int i = 0; i < count; i++)
            {
                string defId = r.ReadString(key + "." + i + ".def");
                Joker joker = JokerRegistry.Create(defId);
                if (joker == null)
                {
                    throw new SaveFormatException("Unknown joker '" + defId + "' in the save.");
                }
                ContentStateSerializer.Load(r, key + "." + i + ".state", joker);
                Jokers.AddRestored(joker);
            }
        }

        private void WritePowers(SaveWriter w, string key)
        {
            IReadOnlyList<Power> held = Powers.Powers;
            w.Write(key + ".slots", Powers.MaxSlots);
            w.Write(key + ".nextId", Powers.NextInstanceId);
            w.Write(key + ".count", held.Count);
            for (int i = 0; i < held.Count; i++)
            {
                w.Write(key + "." + i + ".def", held[i].DefId);
                ContentStateSerializer.Save(w, key + "." + i + ".state", held[i]);
            }
        }

        private void ReadPowers(SaveReader r, string key)
        {
            Powers.MaxSlots = r.ReadInt(key + ".slots");
            Powers.NextInstanceId = r.ReadInt(key + ".nextId");
            int count = r.ReadInt(key + ".count");
            for (int i = 0; i < count; i++)
            {
                string defId = r.ReadString(key + "." + i + ".def");
                Power power = PowerRegistry.Create(defId);
                if (power == null)
                {
                    throw new SaveFormatException("Unknown power '" + defId + "' in the save.");
                }
                ContentStateSerializer.Load(r, key + "." + i + ".state", power);
                Powers.AddRestored(power);
            }
        }
    }
}
