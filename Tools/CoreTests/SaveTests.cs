// Round-trip tests for the save format and the structural serializers.
// Compiled INTO the Core assembly, so internal members (GameBoard.Save/Load) are reachable.

using System;
using System.Globalization;
using System.Threading;
using ProjectBlock.Core;

public static class SaveTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Console.WriteLine("== save tests ==");
        PrimitiveRoundTrip();
        StringEscaping();
        InvariantCultureUnderTurkishLocale();
        KeyDriftThrows();
        TruncatedFileThrows();
        CardRoundTrip();
        DesignedCardRoundTrip();
        RulesAndScoringRoundTrip();
        BoardRoundTrip();
        BoardErosionAndSealsRoundTrip();
        CardTableSharesInstances();
        CardTableRejectsUnknownId();
        RngRestoresToTheSamePosition();
        RngRestoreSurvivesMixedDraws();
        RngLogIsRunLengthEncoded();
        EveryJokerRoundTrips();
        EveryPowerRoundTrips();
        EveryBossRoundTrips();
        ContentStateCarriesRealValues();
        Console.WriteLine(failures == 0
            ? "save tests: all passed"
            : "save tests: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            failures++;
            Console.WriteLine("  FAIL " + what);
        }
    }

    private static void CheckEqual(object expected, object actual, string what)
    {
        Check(Equals(expected, actual), what + " (expected " + expected + ", got " + actual + ")");
    }

    private static void PrimitiveRoundTrip()
    {
        var w = new SaveWriter();
        w.Write("i", -42);
        w.Write("l", 9000000000L);
        w.Write("b", true);
        w.Write("b2", false);
        w.Write("d", 0.1);
        var r = new SaveReader(w.ToText());
        CheckEqual(-42, r.ReadInt("i"), "int round-trip");
        CheckEqual(9000000000L, r.ReadLong("l"), "long round-trip");
        CheckEqual(true, r.ReadBool("b"), "true round-trip");
        CheckEqual(false, r.ReadBool("b2"), "false round-trip");
        CheckEqual(0.1, r.ReadDouble("d"), "double round-trip");
        Check(r.AtEnd, "reader consumed every entry");
    }

    private static void StringEscaping()
    {
        var w = new SaveWriter();
        w.Write("plain", "hello");
        w.Write("newline", "two\nlines");
        w.Write("slash", "back\\slash");
        w.Write("equals", "a=b=c");
        w.Write("null", (string)null);
        w.Write("empty", string.Empty);
        w.Write("turkish", "Tükenmişlik ÇĞİÖŞÜ");
        var r = new SaveReader(w.ToText());
        CheckEqual("hello", r.ReadString("plain"), "plain string");
        CheckEqual("two\nlines", r.ReadString("newline"), "newline survives");
        CheckEqual("back\\slash", r.ReadString("slash"), "backslash survives");
        CheckEqual("a=b=c", r.ReadString("equals"), "'=' in a value survives");
        CheckEqual(null, r.ReadString("null"), "null string survives");
        CheckEqual(string.Empty, r.ReadString("empty"), "empty string survives");
        CheckEqual("Tükenmişlik ÇĞİÖŞÜ", r.ReadString("turkish"), "turkish characters survive");
    }

    /// <summary>The team runs Turkish locales, where "0,1" is the default double format. A save
    /// written there must still load anywhere, so every number goes through InvariantCulture.</summary>
    private static void InvariantCultureUnderTurkishLocale()
    {
        CultureInfo original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            var w = new SaveWriter();
            w.Write("factor", 0.1);
            w.Write("fraction", 0.25);
            string text = w.ToText();
            Check(text.Contains("0.1"), "double is written with a '.' under a Turkish locale");
            Check(!text.Contains("0,1"), "double is NOT written with a ','");
            var r = new SaveReader(text);
            CheckEqual(0.1, r.ReadDouble("factor"), "double reads back under a Turkish locale");
            CheckEqual(0.25, r.ReadDouble("fraction"), "second double reads back");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    /// <summary>A save from a build whose fields moved must fail loudly, not load half a run.</summary>
    private static void KeyDriftThrows()
    {
        var w = new SaveWriter();
        w.Write("alpha", 1);
        w.Write("beta", 2);
        var r = new SaveReader(w.ToText());
        r.ReadInt("alpha");
        bool threw = false;
        try
        {
            r.ReadInt("gamma"); // the field a newer build expects here
        }
        catch (SaveFormatException)
        {
            threw = true;
        }
        Check(threw, "a drifted key throws SaveFormatException");
    }

    private static void TruncatedFileThrows()
    {
        var w = new SaveWriter();
        w.Write("only", 1);
        var r = new SaveReader(w.ToText());
        r.ReadInt("only");
        bool threw = false;
        try
        {
            r.ReadInt("missing");
        }
        catch (SaveFormatException)
        {
            threw = true;
        }
        Check(threw, "reading past the end throws SaveFormatException");
    }

    private static void CardRoundTrip()
    {
        BlockShape shape = BlockShape.FromCells(new[]
        {
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1)
        });
        var card = new BlockCard(7, shape, new[] { BlockElement.Fire, BlockElement.Gold });
        var w = new SaveWriter();
        CoreSerializers.WriteCard(w, "card", card);
        BlockCard back = CoreSerializers.ReadCard(new SaveReader(w.ToText()), "card");
        CheckEqual(7, back.Id, "card id");
        CheckEqual(shape.CanonicalKey, back.Shape.CanonicalKey, "card shape");
        Check(back.Has(BlockElement.Fire), "card keeps fire");
        Check(back.Has(BlockElement.Gold), "card keeps gold");
        Check(!back.IsCustom, "card is not custom");
        Check(!back.HasPerCubeElements, "card is not per-cube");
    }

    private static void DesignedCardRoundTrip()
    {
        BlockShape shape = BlockShape.FromCells(new[]
        {
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
        });
        BlockCard card = BlockCard.Designed(11, shape,
            new BlockElement?[] { BlockElement.Fire, null, BlockElement.Obsidian });
        var w = new SaveWriter();
        CoreSerializers.WriteCard(w, "c", card);
        BlockCard back = CoreSerializers.ReadCard(new SaveReader(w.ToText()), "c");
        Check(back.IsCustom, "designed card stays custom");
        Check(back.HasPerCubeElements, "designed card stays per-cube");
        CheckEqual(card.CellElement(0), back.CellElement(0), "per-cube element 0");
        CheckEqual(card.CellElement(1), back.CellElement(1), "per-cube element 1 (plain)");
        CheckEqual(card.CellElement(2), back.CellElement(2), "per-cube element 2");
    }

    private static void RulesAndScoringRoundTrip()
    {
        var rules = new RoundRules();
        rules.HandSize = 8;              // "Seri tetik" grew it
        rules.RetroMode = true;
        rules.DeadZoneRows = 4;
        rules.CountExternalSweeps = true;
        rules.RevealedDrawCount = 2;
        var scoring = new ScoringConfig();
        scoring.CleanSweepBonus = 999;   // "bereket" raised it
        scoring.OvertimeRegularScoreFactor = 0.33;

        var w = new SaveWriter();
        CoreSerializers.WriteRules(w, "rules", rules);
        CoreSerializers.WriteScoring(w, "scoring", scoring);
        var r = new SaveReader(w.ToText());
        var loadedRules = new RoundRules();
        var loadedScoring = new ScoringConfig();
        CoreSerializers.ReadRulesInto(r, "rules", loadedRules);
        CoreSerializers.ReadScoringInto(r, "scoring", loadedScoring);

        CheckEqual(8, loadedRules.HandSize, "mutated hand size survives");
        CheckEqual(true, loadedRules.RetroMode, "retro mode survives");
        CheckEqual(4, loadedRules.DeadZoneRows, "dead zone rows survive");
        CheckEqual(true, loadedRules.CountExternalSweeps, "external sweeps flag survives");
        CheckEqual(2, loadedRules.RevealedDrawCount, "revealed draw count survives");
        CheckEqual(999, loadedScoring.CleanSweepBonus, "mutated sweep bonus survives");
        CheckEqual(0.33, loadedScoring.OvertimeRegularScoreFactor, "overtime factor survives");
    }

    private static void BoardRoundTrip()
    {
        var board = new GameBoard(5, 5);
        board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Fire, 3));
        board.SetCubeAt(new GridPos(4, 4), new Cube(CubeKind.Gold, 9));
        board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Normal, 5, true)); // Parazit host

        var w = new SaveWriter();
        board.Save(w, "board");
        GameBoard back = GameBoard.Load(new SaveReader(w.ToText()), "board");

        CheckEqual(board.Width, back.Width, "board width");
        CheckEqual(board.Height, back.Height, "board height");
        CheckEqual(board.OccupiedCount, back.OccupiedCount, "occupied count recomputed");
        CheckEqual(board.PlayableCellCount, back.PlayableCellCount, "playable count recomputed");
        CheckEqual(CubeKind.Fire, back.GetCube(new GridPos(0, 0)).Value.Kind, "fire cube kind");
        CheckEqual(3, back.GetCube(new GridPos(0, 0)).Value.SourceCardId, "cube source card");
        CheckEqual(CubeKind.Gold, back.GetCube(new GridPos(4, 4)).Value.Kind, "gold cube kind");
        Check(back.GetCube(new GridPos(2, 1)).Value.Protected, "protected flag survives");
        Check(!back.GetCube(new GridPos(1, 1)).HasValue, "empty cell stays empty");
    }

    /// <summary>An eaten cell kills its row and column, a plain hole does not - so the two masks
    /// must come back distinct, and a line through an eaten cell must still be uncompletable.</summary>
    private static void BoardErosionAndSealsRoundTrip()
    {
        var board = new GameBoard(5, 5);
        board.MarkDead(new[] { new GridPos(2, 2) });
        board.SealCell(new GridPos(0, 3));

        var w = new SaveWriter();
        board.Save(w, "b");
        GameBoard back = GameBoard.Load(new SaveReader(w.ToText()), "b");

        Check(back.IsDead(new GridPos(2, 2)), "eaten cell is still dead");
        Check(!back.IsInside(new GridPos(2, 2)), "eaten cell is not playable");
        Check(back.IsInside(new GridPos(2, 3)), "a live cell is still playable");
        CheckEqual(board.DeadCellCount, back.DeadCellCount, "dead cell count recomputed");
        CheckEqual(board.PlayableCellCount, back.PlayableCellCount,
            "playable count drops with the eaten cell");
        Check(back.IsSealed(new GridPos(0, 3)), "placement seal survives");
        Check(!back.IsSealed(new GridPos(1, 3)), "an unsealed cell stays unsealed");
    }

    private static void CardTableSharesInstances()
    {
        BlockShape shape = BlockShape.FromCells(new[] { new GridPos(0, 0), new GridPos(1, 0) });
        var a = new BlockCard(1, shape);
        var b = new BlockCard(2, shape);
        var table = new CardTable();
        table.AddRange(new[] { a, b });

        var w = new SaveWriter();
        table.Write(w, "cards");
        table.WriteRefs(w, "draw", new[] { b, a, b });

        var r = new SaveReader(w.ToText());
        CardTable loaded = CardTable.Read(r, "cards");
        System.Collections.Generic.List<BlockCard> pile = loaded.ReadRefs(r, "draw");

        CheckEqual(3, pile.Count, "pile size");
        Check(ReferenceEquals(pile[0], pile[2]), "the same card id yields the SAME instance");
        Check(ReferenceEquals(pile[0], loaded.Get(2)), "pile shares the table's instance");
        CheckEqual(1, pile[1].Id, "pile order preserved");
    }

    /// <summary>The heart of a mid-run save: a restored source must continue the SAME stream,
    /// so a reloaded run shuffles and draws exactly as the original would have.</summary>
    private static void RngRestoresToTheSamePosition()
    {
        var original = new SeededRandom(12345);
        for (int i = 0; i < 500; i++)
        {
            original.NextInt(0, 100);
        }
        var w = new SaveWriter();
        original.Save(w, "rng");
        SeededRandom restored = SeededRandom.Load(new SaveReader(w.ToText()), "rng");

        CheckEqual(original.DrawCount, restored.DrawCount, "restored draw count matches");
        bool same = true;
        for (int i = 0; i < 200; i++)
        {
            if (original.NextInt(0, 1000) != restored.NextInt(0, 1000))
            {
                same = false;
                break;
            }
        }
        Check(same, "a restored rng continues the identical stream");
    }

    /// <summary>Ints and doubles interleaved - the log has to keep their order, not just a count.</summary>
    private static void RngRestoreSurvivesMixedDraws()
    {
        var original = new SeededRandom(-99);
        for (int i = 0; i < 120; i++)
        {
            if (i % 3 == 0)
            {
                original.NextDouble();
            }
            else
            {
                original.NextInt(0, 7);
            }
        }
        var w = new SaveWriter();
        original.Save(w, "r");
        SeededRandom restored = SeededRandom.Load(new SaveReader(w.ToText()), "r");

        bool same = true;
        for (int i = 0; i < 100; i++)
        {
            if (original.NextDouble() != restored.NextDouble()
                || original.NextInt(0, 500) != restored.NextInt(0, 500))
            {
                same = false;
                break;
            }
        }
        Check(same, "a restored rng continues an interleaved int/double stream");
    }

    /// <summary>Shuffles make thousands of consecutive NextInt calls; the log must collapse
    /// them, or a long run's save would be enormous.</summary>
    private static void RngLogIsRunLengthEncoded()
    {
        var rng = new SeededRandom(1);
        for (int i = 0; i < 5000; i++)
        {
            rng.NextInt(0, 10);
        }
        var w = new SaveWriter();
        rng.Save(w, "rng");
        string text = w.ToText();
        CheckEqual(5000, rng.DrawCount, "5000 draws were counted");
        Check(text.Length < 200, "5000 consecutive draws compress to a tiny log (got "
            + text.Length + " chars)");
    }

    /// <summary>Saves an object's whole field state to text.</summary>
    private static string StateOf(object instance)
    {
        var w = new SaveWriter();
        ContentStateSerializer.Save(w, "s", instance);
        return w.ToText();
    }

    /// <summary>Save -> load into a fresh instance -> save again. If the two texts match, every
    /// field the walker writes it also reads back, for that exact type.</summary>
    private static void RoundTripContent(object original, object fresh, string what)
    {
        string first = StateOf(original);
        try
        {
            ContentStateSerializer.Load(new SaveReader(first), "s", fresh);
        }
        catch (Exception e)
        {
            failures++;
            Console.WriteLine("  FAIL " + what + " threw on load: " + e.Message);
            return;
        }
        CheckEqual(first, StateOf(fresh), what + " round-trips");
    }

    /// <summary>Every registered joker, so a new joker that holds an unsupported field shape
    /// fails HERE rather than silently losing its state in a player's save.</summary>
    private static void EveryJokerRoundTrips()
    {
        int checkedCount = 0;
        foreach (JokerDefinition definition in JokerRegistry.All)
        {
            RoundTripContent(definition.Create(), definition.Create(), "joker " + definition.DefId);
            checkedCount++;
        }
        Check(checkedCount > 30, "all jokers were round-tripped (" + checkedCount + ")");
    }

    private static void EveryPowerRoundTrips()
    {
        int checkedCount = 0;
        foreach (PowerDefinition definition in PowerRegistry.All)
        {
            RoundTripContent(definition.Create(), definition.Create(), "power " + definition.DefId);
            checkedCount++;
        }
        Check(checkedCount > 25, "all powers were round-tripped (" + checkedCount + ")");
    }

    private static void EveryBossRoundTrips()
    {
        int checkedCount = 0;
        foreach (BossDefinition definition in BossRegistry.All)
        {
            RoundTripContent(definition.Create(), definition.Create(), "boss " + definition.DefId);
            checkedCount++;
        }
        Check(checkedCount > 8, "all bosses were round-tripped (" + checkedCount + ")");
    }

    /// <summary>The round-trip above would still pass if every field were skipped, since two
    /// fresh instances look alike. So poke real values in first and prove they travel.</summary>
    private static void ContentStateCarriesRealValues()
    {
        foreach (JokerDefinition definition in JokerRegistry.All)
        {
            Joker original = definition.Create();
            int poked = PokeFields(original);
            if (poked == 0)
            {
                continue; // a stateless joker - nothing to prove here
            }
            Joker fresh = definition.Create();
            string before = StateOf(original);
            Check(before != StateOf(fresh), "poking changed " + definition.DefId + "'s state");
            ContentStateSerializer.Load(new SaveReader(before), "s", fresh);
            CheckEqual(before, StateOf(fresh), definition.DefId + " carries poked values");
        }
    }

    /// <summary>Writes recognisable values into the simple fields of an object. Returns how many
    /// were changed. The object is never RUN afterwards, only serialized, so the values do not
    /// have to be legal game states.</summary>
    private static int PokeFields(object instance)
    {
        int poked = 0;
        System.Reflection.FieldInfo[] fields = instance.GetType().GetFields(
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            Type type = fields[i].FieldType;
            // Values are chosen to differ from ANY plausible default - a poke that happened to
            // write the value already there would make the "state changed" check meaningless.
            if (type == typeof(int))
            {
                fields[i].SetValue(instance, 10007 + i);
                poked++;
            }
            else if (type == typeof(bool))
            {
                fields[i].SetValue(instance, !(bool)fields[i].GetValue(instance));
                poked++;
            }
            else if (type == typeof(double))
            {
                fields[i].SetValue(instance, 1234.5 + i);
                poked++;
            }
        }
        return poked;
    }

    private static void CardTableRejectsUnknownId()
    {
        var w = new SaveWriter();
        w.Write("cards.count", 0);
        w.Write("draw.count", 1);
        w.Write("draw.0", 77);
        var r = new SaveReader(w.ToText());
        CardTable loaded = CardTable.Read(r, "cards");
        bool threw = false;
        try
        {
            loaded.ReadRefs(r, "draw");
        }
        catch (SaveFormatException)
        {
            threw = true;
        }
        Check(threw, "a card id missing from the table throws instead of shrinking the pile");
    }
}
