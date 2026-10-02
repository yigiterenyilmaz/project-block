// PURPOSE: "Blackjack" (2026-10-02): the boss stage played against the house, for the purse. The
// deck is cut in two each hand (the house leaning to the elemental cards), the player bets from
// the purse, both halves are played out, and the higher score takes twice the bet. Doubling the
// purse beats the stage; going broke ends the run. The house's play is DuelPlanner's - these
// tests pin what it must never do (read the order of its pile, keep an alchemical choice, miss a
// fox form that clears a line) and that it plays far better than greedy or random play.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    private static void RunBlackjackTests()
    {
        Blackjack_AnEmptyPurseIsStakedAndTheTargetIsDouble();
        Blackjack_TheTargetIsTwiceThePurseItSitsDownWith();
        Blackjack_NothingIsPlacedBeforeTheBet();
        Blackjack_TheBetDealsTwoEqualHalves();
        Blackjack_TheHouseIsDealtMostOfTheElements();
        Blackjack_LinesAreNotMoney();
        Blackjack_TheHousePlaysAfterEveryTurn();
        Blackjack_AWonHandPaysDoubleAndDoublingThePurseWins();
        Blackjack_ADrawnHandReturnsTheBet();
        Blackjack_GoingBrokeLosesTheRun();
        Blackjack_AWholeHandIsPlayedOutAndSettled();
        Blackjack_TheTableSurvivesASaveMidHand();
        Blackjack_TableBreakingPowersAreSwitchedOff();
        Duel_ACopyOfARoundIsThatRound();
        DuelPlanner_NeverReadsTheOrderOfItsPile();
        DuelPlanner_GivesTheFoxTheFormThatClears();
        DuelPlanner_PutsAnAlchemicalChoiceBack();
        DuelPlanner_PlaysFarBetterThanGreedyOrRandom();
    }

    // ---------------------------------------------------------------- helpers

    private static BlockShape HorizontalBar(int length)
    {
        var cells = new List<GridPos>();
        for (int i = 0; i < length; i++)
        {
            cells.Add(new GridPos(i, 0));
        }
        return BlockShape.FromCells(cells);
    }

    /// <summary>A session whose every stage is a Blackjack stage (or, with
    /// <paramref name="bossFirst"/> false, whose first stage is ordinary).</summary>
    private static GameSession NewDuelSession(int seed, IReadOnlyList<BlockShape> deck, bool bossFirst)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        config.ForcedBossDefId = "blackjack";
        config.Deck = new DeckDefinition("duel", deck, new SizedShapeGenerator(1));
        config.Progression = new FixedProgression(7, 1000, ShuffleErosion.None, bossFirst);
        return new GameSession(config);
    }

    /// <summary>Twelve 1x7 bars: every one fills a whole row of a 7x7 arena on its own, so every
    /// placement scores and the arena is empty again after it.</summary>
    private static List<BlockShape> BarDeck(int count)
    {
        var deck = new List<BlockShape>();
        for (int i = 0; i < count; i++)
        {
            deck.Add(HorizontalBar(7));
        }
        return deck;
    }

    private static TurnReport PlayFirstLegal(RoundEngine round)
    {
        for (int h = 0; h < round.Hand.Count; h++)
        {
            BlockCard card = round.Hand[h];
            List<GridPos> origins = round.GetValidOrigins(round.EffectiveShape(card));
            for (int i = 0; i < origins.Count; i++)
            {
                if (round.CanPlaceCard(card, origins[i]))
                {
                    return round.PlayFromHand(h, origins[i]);
                }
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- the stage

    private static void Blackjack_AnEmptyPurseIsStakedAndTheTargetIsDouble()
    {
        Section("blackjack / an empty purse is staked 500 by the house, and the target is 1000");
        GameSession session = NewDuelSession(9101, DeckLibrary.Classic.FixedShapes, true);
        BlackjackBoss duel = session.DuelBoss;
        Check(duel != null, "the first stage is the duel");
        Check(session.TotalScore == BlackjackBoss.StarterStake && duel.StarterGranted,
            "the house staked the empty purse", "purse " + session.TotalScore);
        Check(duel.Target == 2 * BlackjackBoss.StarterStake, "and the target is twice it",
            "target " + duel.Target);
        Check(duel.AwaitingBet, "the table waits for a bet");
    }

    private static void Blackjack_TheTargetIsTwiceThePurseItSitsDownWith()
    {
        Section("blackjack / the target is twice the purse the stage begins with");
        GameSession session = NewDuelSession(9102, DeckLibrary.Classic.FixedShapes, false);
        Check(session.DuelBoss == null, "an ordinary first stage");
        session.AddCurrency(830);
        Check(session.DebugStartBossStage("blackjack"), "the duel starts");
        BlackjackBoss duel = session.DuelBoss;
        Check(!duel.StarterGranted && session.TotalScore == 830, "a purse with money is not staked",
            "purse " + session.TotalScore);
        Check(duel.Target == 1660, "target 1660", "target " + duel.Target);
    }

    private static void Blackjack_NothingIsPlacedBeforeTheBet()
    {
        Section("blackjack / nothing is held and nothing goes down before the bet");
        GameSession session = NewDuelSession(9103, DeckLibrary.Classic.FixedShapes, true);
        RoundEngine round = session.CurrentRound;
        Check(round.Hand.Count == 0 && round.Deck.DrawCount == 0, "the table holds every card");
        Check(round.PlayIsHeld, "play is held");
        Check(round.Status == RoundStatus.InProgress, "and that is not a dead end");
        Check(!session.PlaceDuelBet(0) && !session.PlaceDuelBet(session.TotalScore + 1),
            "a stake of nothing or more than the purse is refused");
    }

    private static void Blackjack_TheBetDealsTwoEqualHalves()
    {
        Section("blackjack / the bet comes off the purse and deals two equal halves of the deck");
        GameSession session = NewDuelSession(9104, DeckLibrary.Classic.FixedShapes, true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        Check(session.PlaceDuelBet(200), "a bet of 200");
        Check(session.TotalScore == 300, "it came off the purse", "purse " + session.TotalScore);
        Check(!round.PlayIsHeld && round.Hand.Count == 3, "the player holds a hand and may play");
        RoundEngine house = duel.HouseRound;
        int playerCards = round.Hand.Count + round.Deck.DrawCount;
        int houseCards = house.Hand.Count + house.Deck.DrawCount;
        Check(playerCards == 12 && houseCards == 12, "twelve cards each from a deck of 24",
            playerCards + " / " + houseCards);
        var seen = new HashSet<int>();
        foreach (BlockCard card in round.Hand.Cards) { seen.Add(card.Id); }
        foreach (BlockCard card in round.Deck.DrawPile) { seen.Add(card.Id); }
        bool disjoint = true;
        foreach (BlockCard card in house.Hand.Cards) { disjoint &= seen.Add(card.Id); }
        foreach (BlockCard card in house.Deck.DrawPile) { disjoint &= seen.Add(card.Id); }
        Check(disjoint && seen.Count == 24, "no card is on both sides, none is missing");
    }

    private static void Blackjack_TheHouseIsDealtMostOfTheElements()
    {
        Section("blackjack / the house is dealt about seven in ten of the elemental cards");
        GameSession session = NewDuelSession(9105, DeckLibrary.Classic.FixedShapes, false);
        BlockElement[] elements = { BlockElement.Fire, BlockElement.Water, BlockElement.Obsidian,
            BlockElement.Gold, BlockElement.Dynamite, BlockElement.Fox, BlockElement.Mechanical,
            BlockElement.Ghost, BlockElement.Transparent, BlockElement.Fire };
        for (int i = 0; i < elements.Length; i++)
        {
            session.AddOwnedCardForTests(session.CreateCard(HorizontalBar(2), new[] { elements[i] }));
        }
        session.AddCurrency(100000);
        session.DebugStartBossStage("blackjack");
        BlackjackBoss duel = session.DuelBoss;
        RoundEngine round = session.CurrentRound;
        var elemental = new HashSet<int>();
        foreach (BlockCard card in session.OwnedCards)
        {
            if (card.Elements.Count > 0) { elemental.Add(card.Id); }
        }
        int toHouse = 0;
        int dealt = 0;
        for (int hand = 0; hand < 60; hand++)
        {
            if (!session.PlaceDuelBet(1))
            {
                Check(false, "a bet was refused on hand " + hand);
                return;
            }
            foreach (int id in duel.AiDealtIds)
            {
                if (elemental.Contains(id)) { toHouse++; }
            }
            dealt += elemental.Count; // 34 cards: every one of them is dealt each hand
            // end the hand at once: neither side has a card left
            duel.HouseRound.SetAsideForDuel();
            round.SetAsideForDuel();
            round.DebugCheckForDeadEnd();
            Check(duel.AwaitingBet, "an empty hand is settled as a draw");
        }
        double share = toHouse / (double)dealt;
        Check(share > 0.62 && share < 0.78, "the house got about 70% of them",
            share.ToString("0.000"));
    }

    private static void Blackjack_LinesAreNotMoney()
    {
        Section("blackjack / a cleared line scores the hand but never pays the purse");
        GameSession session = NewDuelSession(9106, BarDeck(12), true);
        RoundEngine round = session.CurrentRound;
        session.PlaceDuelBet(100);
        long purse = session.TotalScore;
        TurnReport report = PlayFirstLegal(round);
        Check(report != null && report.ExplodedRows.Count == 1, "a bar fills and clears a row");
        Check(session.DuelBoss.PlayerHandScore(round) > 0, "the hand has points",
            "" + session.DuelBoss.PlayerHandScore(round));
        Check(session.TotalScore == purse, "and the purse did not move",
            purse + " -> " + session.TotalScore);
    }

    private static void Blackjack_TheHousePlaysAfterEveryTurn()
    {
        Section("blackjack / the house lays a card on its own arena after every turn of the player's");
        GameSession session = NewDuelSession(9107, DeckLibrary.Classic.FixedShapes, true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        session.PlaceDuelBet(100);
        PlayFirstLegal(round);
        Check(duel.LastAiTurns != null && duel.LastAiTurns.Plays.Count == 1, "one house play");
        Check(duel.HouseRound.TurnNumber == 1, "on its own round");
        Check(duel.LastAiTurns.Plays[0].PlacedCells.Count > 0, "with cubes laid on its arena");
        DuelAiTurns first = duel.LastAiTurns;
        PlayFirstLegal(round);
        Check(!ReferenceEquals(first, duel.LastAiTurns) && duel.HouseRound.TurnNumber == 2,
            "a new report for the next turn");
    }

    private static void Blackjack_AWonHandPaysDoubleAndDoublingThePurseWins()
    {
        Section("blackjack / outscoring the house pays twice the bet; doubling the purse beats the stage");
        GameSession session = NewDuelSession(9108, BarDeck(12), true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        session.PlaceDuelBet(500); // all of it
        Check(session.TotalScore == 0, "the whole purse is on the table");
        duel.HouseRound.SetAsideForDuel(); // the house has nothing to play: it scores 0
        int guard = 0;
        while (duel.Phase == BlackjackBoss.DuelPhase.Playing && guard++ < 20)
        {
            PlayFirstLegal(round);
        }
        DuelHandResult hand = duel.LastHand;
        Check(hand != null && hand.Outcome == DuelHandOutcome.Win, "the player won the hand");
        Check(hand != null && hand.Payout == 1000 && session.TotalScore == 1000,
            "and was paid twice the bet", "purse " + session.TotalScore);
        Check(hand != null && hand.StageWon && duel.Phase == BlackjackBoss.DuelPhase.Won,
            "1000 is the target: the stage is beaten");
        Check(session.Phase == GamePhase.Market || session.Phase == GamePhase.RunWon,
            "and the run goes on to the market", session.Phase.ToString());
    }

    private static void Blackjack_ADrawnHandReturnsTheBet()
    {
        Section("blackjack / a drawn hand gives the bet back");
        GameSession session = NewDuelSession(9109, BarDeck(12), true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        session.PlaceDuelBet(200);
        duel.HouseRound.SetAsideForDuel();
        round.SetAsideForDuel();
        round.DebugCheckForDeadEnd();
        Check(duel.LastHand != null && duel.LastHand.Outcome == DuelHandOutcome.Push, "nothing to nothing");
        Check(session.TotalScore == 500, "the stake came back", "purse " + session.TotalScore);
        Check(duel.AwaitingBet && duel.HandNumber == 1, "and the next hand waits for a bet");
    }

    private static void Blackjack_GoingBrokeLosesTheRun()
    {
        Section("blackjack / losing the whole purse loses the run");
        GameSession session = NewDuelSession(9110, BarDeck(12), true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        session.PlaceDuelBet(500);
        round.SetAsideForDuel(); // the player has nothing to play; the house plays its bars out
        round.DebugCheckForDeadEnd();
        DuelHandResult hand = duel.LastHand;
        Check(hand != null && hand.Outcome == DuelHandOutcome.Lose && hand.AiScore > 0,
            "the house outscored an empty hand", hand != null ? hand.AiScore.ToString() : "-");
        Check(session.TotalScore == 0 && hand.Bankrupt, "the purse is empty");
        Check(round.Status == RoundStatus.Lost && round.Loss == LossReason.DuelBankrupt,
            "the round is lost for it", round.Loss.HasValue ? round.Loss.Value.ToString() : "-");
        Check(session.Phase == GamePhase.GameOver, "and the run is over");
    }

    private static void Blackjack_AWholeHandIsPlayedOutAndSettled()
    {
        Section("blackjack / a real hand is played out on both sides and settled by the scores");
        GameSession session = NewDuelSession(9111, DeckLibrary.Classic.FixedShapes, true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        session.PlaceDuelBet(250);
        int turns = 0;
        int lastPlayer = 0;
        int lastHouse = 0;
        while (duel.Phase == BlackjackBoss.DuelPhase.Playing && turns < 30)
        {
            lastPlayer = duel.PlayerHandScore(round);
            if (PlayFirstLegal(round) == null)
            {
                break;
            }
            turns++;
            if (duel.Phase == BlackjackBoss.DuelPhase.Playing)
            {
                lastPlayer = duel.PlayerHandScore(round);
            }
            lastHouse = duel.HouseHandScore;
        }
        DuelHandResult hand = duel.LastHand;
        Check(hand != null, "the hand was settled", "after " + turns + " turns");
        if (hand == null)
        {
            return;
        }
        Check(turns <= 12, "the player played no more than the twelve cards dealt", turns + " turns");
        Check(duel.HouseRound.Hand.Count == 0 || !duel.HouseRound.HasMoveLeft,
            "the house played its half out");
        DuelHandOutcome expected = hand.PlayerScore > hand.AiScore ? DuelHandOutcome.Win
            : hand.PlayerScore == hand.AiScore ? DuelHandOutcome.Push : DuelHandOutcome.Lose;
        Check(hand.Outcome == expected, "the outcome follows the scores",
            hand.PlayerScore + " vs " + hand.AiScore + " -> " + hand.Outcome);
        long expectedPurse = 250 + (expected == DuelHandOutcome.Win ? 500 : expected == DuelHandOutcome.Push ? 250 : 0);
        Check(session.TotalScore == expectedPurse, "and the purse was paid accordingly",
            "purse " + session.TotalScore);
        Check(round.Hand.Count == 0 && round.PlayIsHeld, "the table is cleared for the next bet");
    }

    private static void Blackjack_TheTableSurvivesASaveMidHand()
    {
        Section("blackjack / a save in the middle of a hand brings back both arenas and the stakes");
        GameSession session = NewDuelSession(9112, DeckLibrary.Classic.FixedShapes, true);
        RoundEngine round = session.CurrentRound;
        BlackjackBoss duel = session.DuelBoss;
        session.PlaceDuelBet(150);
        PlayFirstLegal(round);
        PlayFirstLegal(round);
        string text = SaveGame.Save(session);
        GameSession back = SaveGame.Load(text, new GameConfig());
        BlackjackBoss duelBack = back.DuelBoss;
        Check(duelBack != null && duelBack.Phase == BlackjackBoss.DuelPhase.Playing, "the duel came back mid-hand");
        if (duelBack == null || duelBack.HouseRound == null)
        {
            Check(false, "the house's round came back");
            return;
        }
        Check(duelBack.Bet == 150 && duelBack.Target == duel.Target && back.TotalScore == session.TotalScore,
            "with its stake, its target and the purse");
        Check(duelBack.HouseRound.Board.ToAscii() == duel.HouseRound.Board.ToAscii(),
            "the house's arena is the same");
        Check(duelBack.HouseRound.RoundScore == duel.HouseRound.RoundScore
            && duelBack.HouseRound.Hand.Count == duel.HouseRound.Hand.Count
            && duelBack.HouseRound.Deck.DrawCount == duel.HouseRound.Deck.DrawCount,
            "and its score, hand and pile");
        Check(duelBack.HouseRound.PlaysOutItsCards, "and it is still seated at the table");
        PlayFirstLegal(round);
        PlayFirstLegal(back.CurrentRound);
        Check(duelBack.HouseRound.Board.ToAscii() == duel.HouseRound.Board.ToAscii(),
            "the house answers the next turn the same way after the load");
    }

    private static void Blackjack_TableBreakingPowersAreSwitchedOff()
    {
        Section("blackjack / the powers that would break the table are switched off for the stage");
        var duel = new BlackjackBoss();
        Check(duel.DisablesPower(PowerRegistry.Create("oteki_dunya")), "the second world");
        Check(duel.DisablesPower(PowerRegistry.Create("hiper_enflasyon")), "the inflations");
        Check(duel.DisablesPower(PowerRegistry.Create("totem")), "totem");
        Check(!duel.DisablesPower(PowerRegistry.Create("buldozer")), "but not the rest");
    }

    // ---------------------------------------------------------------- the house's player

    private static RoundEngine NewHouseSide(IReadOnlyList<BlockCard> cards)
    {
        return RoundEngine.CreateDuelSide(new RoundConfig(1, 7, 7, 135), cards, DuelBench.Scorer());
    }

    private static void Duel_ACopyOfARoundIsThatRound()
    {
        Section("duel / a copy of a round saves exactly like the round it copies");
        List<BlockCard> cards = DuelBench.Deal(7);
        RoundEngine side = NewHouseSide(cards);
        var planner = new DuelPlanner();
        for (int i = 0; i < 4; i++)
        {
            DuelMove move = planner.Choose(side);
            if (move != null) { move.ApplyTo(side); }
        }
        RoundEngine copy = side.CloneForPlanning();
        var table = new CardTable();
        table.AddRange(cards);
        var a = new SaveWriter();
        var b = new SaveWriter();
        side.Save(a, "r", table);
        copy.Save(b, "r", table);
        Check(a.ToText() == b.ToText(), "every saved field is the same");
        DuelMove next = planner.Choose(side);
        Check(next != null && next.ApplyTo(copy) != null, "a move plays on the copy");
        var after = new SaveWriter();
        side.Save(after, "r", table);
        Check(copy.TurnNumber == side.TurnNumber + 1 && after.ToText() == a.ToText(),
            "and leaves the round it was copied from untouched");
    }

    private static void DuelPlanner_NeverReadsTheOrderOfItsPile()
    {
        Section("duel planner / the same hand and the same pile in another order: the same move");
        List<BlockCard> cards = DuelBench.Deal(11);
        // CreateDuelSide draws from the END: the last three are the hand, the rest the pile.
        var reordered = new List<BlockCard>(cards);
        reordered.Reverse(0, cards.Count - 3);
        RoundEngine a = NewHouseSide(cards);
        RoundEngine b = NewHouseSide(reordered);
        var planner = new DuelPlanner();
        DuelMove ma = planner.Choose(a);
        DuelMove mb = planner.Choose(b);
        Check(ma != null && mb != null && ma.CardId == mb.CardId && ma.Origin.Equals(mb.Origin)
            && ma.Shape.CanonicalKey == mb.Shape.CanonicalKey && ma.Value == mb.Value,
            "the planner's choice does not depend on what lies on top of the pile");
    }

    private static void DuelPlanner_GivesTheFoxTheFormThatClears()
    {
        Section("duel planner / a fox block is given the form that finishes a row, and put there");
        var bar = new BlockCard(501, HorizontalBar(3));
        var single = new BlockCard(502, HorizontalBar(1));
        var single2 = new BlockCard(503, HorizontalBar(1));
        var square = BlockShape.FromCells(new[] { new GridPos(0, 0), new GridPos(1, 0),
            new GridPos(0, 1), new GridPos(1, 1) });
        var fox = new BlockCard(504, square, new[] { BlockElement.Fox });
        // the pile holds the bar; the hand is the fox and two single cubes
        RoundEngine side = NewHouseSide(new List<BlockCard> { bar, single, single2, fox });
        int[] filled = { 0, 1, 5, 6 };
        foreach (int x in filled)
        {
            side.Board.SetCubeAt(new GridPos(x, 0), new Cube(CubeKind.Normal, 900));
        }
        DuelMove move = new DuelPlanner().Choose(side);
        Check(move != null && move.CardId == 504, "the fox is the card it plays",
            move != null ? move.ToString() : "no move");
        Check(move != null && move.FoxShape != null && move.FoxShape.CanonicalKey == bar.Shape.CanonicalKey,
            "worn as the bar from its pile");
        Check(move != null && move.Origin.Equals(new GridPos(2, 0)), "laid in the row's gap",
            move != null ? move.Origin.ToString() : "-");
        TurnReport report = move != null ? move.ApplyTo(side) : null;
        Check(report != null && report.ExplodedRows.Count == 1, "and the row goes off");
    }

    private static void DuelPlanner_PutsAnAlchemicalChoiceBack()
    {
        Section("duel planner / the house's choice of an alchemical element does not stick to the card");
        var alchemical = new BlockCard(601, HorizontalBar(2),
            new[] { BlockElement.Fire, BlockElement.Obsidian });
        int before = alchemical.ActiveChoice;
        RoundEngine side = NewHouseSide(new List<BlockCard> { alchemical });
        DuelMove move = new DuelPlanner().Choose(side);
        Check(move != null && move.Element.HasValue, "the planner picked an element for it");
        move.ApplyTo(side);
        Check(alchemical.ActiveChoice == before, "and the card is back to the player's choice");
    }

    private static void DuelPlanner_PlaysFarBetterThanGreedyOrRandom()
    {
        Section("duel planner / over whole hands it outscores greedy play by far, and random play by more");
        const int deals = 10;
        DuelBench.Result random = DuelBench.Run(DuelBench.Policy.Random, deals, null);
        DuelBench.Result greedy = DuelBench.Run(DuelBench.Policy.Greedy, deals, null);
        DuelBench.Result planner = DuelBench.Run(DuelBench.Policy.Planner, deals, null);
        Check(greedy.Mean > random.Mean, "greedy beats random",
            greedy.Mean.ToString("0") + " vs " + random.Mean.ToString("0"));
        Check(planner.Mean > greedy.Mean * 1.5, "the planner scores half as much again as greedy",
            planner.Mean.ToString("0") + " vs " + greedy.Mean.ToString("0"));
        Check(planner.MeanTurnMs < 250, "and decides in well under a quarter of a second",
            planner.MeanTurnMs.ToString("0.0") + " ms");
    }
}
