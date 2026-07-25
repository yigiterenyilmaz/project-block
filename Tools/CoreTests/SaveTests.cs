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
