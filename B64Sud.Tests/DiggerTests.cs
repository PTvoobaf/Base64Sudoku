namespace B64Sud.Tests;

public sealed class DiggerTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    private static Grid LoadSolution() =>
        GridParser.Parse(File.ReadAllText(Path.Combine(RepoRoot, "solution.txt")));

    private static int CountEmpty(Grid g)
    {
        int n = 0;
        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            if (g[r, c] == Grid.Empty) n++;
        return n;
    }

    // ── Basic output shape ─────────────────────────────────────────────────

    [Fact]
    public void Dig_OutputHasEmptyCells()
    {
        var puzzle = Digger.Dig(LoadSolution(),
            new DiggerProfile { TargetEmptyCells = 100 }, new Random(42)).Puzzle;

        Assert.True(CountEmpty(puzzle) > 0);
    }

    [Fact]
    public void Dig_EmptyCellCountDoesNotExceedTarget()
    {
        const int target = 100;
        var puzzle = Digger.Dig(LoadSolution(),
            new DiggerProfile { TargetEmptyCells = target }, new Random(42)).Puzzle;

        Assert.True(CountEmpty(puzzle) <= target);
    }

    // ── Correctness ────────────────────────────────────────────────────────

    [Fact]
    public void Dig_Output_IsTierOneSolvable()
    {
        var puzzle = Digger.Dig(LoadSolution(),
            new DiggerProfile { TargetEmptyCells = 100 }, new Random(42)).Puzzle;

        var result = Solver.Solve(puzzle);
        Assert.Equal(SolveOutcome.Solved, result.Outcome);
    }

    [Fact]
    public void Dig_SolvedOutput_MatchesOriginalSolution()
    {
        var solution = LoadSolution();
        var puzzle = Digger.Dig(solution,
            new DiggerProfile { TargetEmptyCells = 100 }, new Random(42)).Puzzle;

        var result = Solver.Solve(puzzle);

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.Equal(solution[r, c], result.Solution![r, c]);
    }

    // ── Determinism ────────────────────────────────────────────────────────

    [Fact]
    public void Dig_SameSeed_ProducesSameOutput()
    {
        var solution = LoadSolution();
        var profile = new DiggerProfile { TargetEmptyCells = 50 };

        var p1 = Digger.Dig(solution, profile, new Random(7)).Puzzle;
        var p2 = Digger.Dig(solution, profile, new Random(7)).Puzzle;

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.Equal(p1[r, c], p2[r, c]);
    }

    // ── Round-trip serialization ───────────────────────────────────────────

    [Fact]
    public void Dig_Output_SerializesAndReparses()
    {
        var puzzle = Digger.Dig(LoadSolution(),
            new DiggerProfile { TargetEmptyCells = 100 }, new Random(42)).Puzzle;

        var text = GridParser.Serialize(puzzle);
        var reparsed = GridParser.Parse(text);

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.Equal(puzzle[r, c], reparsed[r, c]);
    }
}
