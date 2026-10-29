namespace B64Sud.Tests;

public sealed class SolverTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    private static Grid LoadFixture(string filename) =>
        GridParser.Parse(File.ReadAllText(Path.Combine(RepoRoot, filename)));

    // ── Symbols ────────────────────────────────────────────────────────────

    [Fact]
    public void Symbols_RoundTrip_AllIndices()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        for (int i = 0; i < Grid.Size; i++)
        {
            char ch = alphabet[i];
            Assert.Equal(i, Symbols.ToIndex(ch));
            Assert.Equal(ch, Symbols.ToChar(i));
        }
    }

    // ── Solve: already-complete grid ───────────────────────────────────────

    [Fact]
    public void Solve_CompleteSolution_SolvedWithoutTechniqueCalls()
    {
        var result = Solver.Solve(LoadFixture("solution.txt"));

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.NotNull(result.Solution);
        Assert.Equal(0, result.TierCalls[0]); // all cells given; no technique needed
    }

    [Fact]
    public void Solve_CompleteSolution_OutputMatchesInput()
    {
        var solution = LoadFixture("solution.txt");
        var result = Solver.Solve(solution);

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.Equal(solution[r, c], result.Solution![r, c]);
    }

    // ── Solve: one cell removed (naked single) ─────────────────────────────

    [Fact]
    public void Solve_OneCellMissing_SolvesViaNakedSingle()
    {
        var solution = LoadFixture("solution.txt");
        char expected = solution[0, 0];

        var puzzle = solution.Clone();
        puzzle[0, 0] = Grid.Empty;

        var result = Solver.Solve(puzzle);

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(expected, result.Solution![0, 0]);
        Assert.True(result.TierCalls[0] >= 1);
    }

    // ── Solve: hard puzzle exceeds Tier 1 ─────────────────────────────────

    [Fact]
    public void Solve_PoorExample_RequiresHigherTier()
    {
        // Cap backtracking at 0 so the solver returns quickly rather than running indefinitely.
        var result = Solver.Solve(LoadFixture("poor_example1.txt"), [int.MaxValue, int.MaxValue, int.MaxValue, 0]);

        Assert.Equal(SolveOutcome.RequiresHigherTier, result.Outcome);
        Assert.Null(result.Solution);
    }

    // ── Solve: invalid puzzle ──────────────────────────────────────────────

    [Fact]
    public void Solve_DuplicateValueInRow_ReturnsInvalid()
    {
        // Place the same symbol in two cells of the same row
        var grid = new Grid();
        grid[0, 0] = 'A';
        grid[0, 1] = 'A'; // duplicate in row 0

        var result = Solver.Solve(grid);

        Assert.Equal(SolveOutcome.Invalid, result.Outcome);
    }

    // ── TierCaps ──────────────────────────────────────────────────────────

    [Fact]
    public void Solve_TierCapAtZero_RequiresHigherTierForUnsolvedPuzzle()
    {
        var solution = LoadFixture("solution.txt");
        var puzzle = solution.Clone();
        puzzle[0, 0] = Grid.Empty; // needs 1 Tier-1 call to solve

        int[] caps = [0, 0, 0, 0];
        var result = Solver.Solve(puzzle, caps);

        Assert.Equal(SolveOutcome.RequiresHigherTier, result.Outcome);
    }

    // ── Tier 2 ────────────────────────────────────────────────────────────

    [Fact]
    public void Solve_TierTwoCapAtZero_BlocksTierTwoOnHardPuzzle()
    {
        // poor_example1 needs more than Tier 1; Tier-2 cap at 0 must still block it.
        var puzzle = LoadFixture("poor_example1.txt");
        var result = Solver.Solve(puzzle, [int.MaxValue, 0, 0, 0]);

        Assert.Equal(SolveOutcome.RequiresHigherTier, result.Outcome);
        Assert.Equal(0, result.TierCalls[1]);
    }

    [Fact]
    public void Solve_HardPuzzle_WithTierTwo_MakesProgressBeyondTierOne()
    {
        // With Tier-2 available, the solver should reach further than Tier-1 alone.
        var puzzle = LoadFixture("poor_example1.txt");
        var tier1Only = Solver.Solve(puzzle, [int.MaxValue, 0, 0, 0]);
        var withTier2 = Solver.Solve(puzzle, [int.MaxValue, int.MaxValue, 0, 0]);

        // Tier-2 must either solve it or make at least one Tier-2 call before giving up.
        Assert.True(withTier2.Outcome == SolveOutcome.Solved || withTier2.TierCalls[1] > 0,
            "Tier-2 techniques made no progress on a puzzle that Tier-1 could not solve.");
        // Tier-1 call count should be at least as high — same or more work before escalating.
        Assert.True(withTier2.TierCalls[0] >= tier1Only.TierCalls[0]);
    }

    [Fact]
    public void Solve_DigggedTierOnePuzzle_TierTwoCallCountIsZero()
    {
        // A puzzle produced under TierOneOnly caps must not need Tier-2.
        // Use a small target so the Digger finishes in milliseconds.
        var solution = LoadFixture("solution.txt");
        var puzzle = Digger.Dig(solution,
            new DiggerProfile { TargetEmptyCells = 100, TierCaps = DiggerProfile.TierOneOnly.TierCaps },
            new Random(42)).Puzzle;
        var result = Solver.Solve(puzzle);

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(0, result.TierCalls[1]);
    }

    // ── Tier 3 ────────────────────────────────────────────────────────────

    [Fact]
    public void Solve_TierThreeCapAtZero_BlocksTierThreeOnHardPuzzle()
    {
        var puzzle = LoadFixture("poor_example1.txt");
        var result = Solver.Solve(puzzle, [int.MaxValue, int.MaxValue, 0, 0]);

        Assert.Equal(SolveOutcome.RequiresHigherTier, result.Outcome);
        Assert.Equal(0, result.TierCalls[2]);
    }

    // Note: a "WithTierThree_MakesProgress" test (analogous to the Tier-2 version) requires a
    // fixture that is solvable by Tier-3 but not by Tier-1+2. poor_example1.txt needs Tier-4.
    // That fixture will be added once the first --dig run with --tier3cap produces a puzzle.

    [Fact]
    public void Solve_DiggedTierTwoPuzzle_TierThreeCallCountIsZero()
    {
        var solution = LoadFixture("solution.txt");
        var puzzle = Digger.Dig(solution,
            new DiggerProfile { TargetEmptyCells = 100, TierCaps = DiggerProfile.TierTwoOnly.TierCaps },
            new Random(42)).Puzzle;
        var result = Solver.Solve(puzzle);

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(0, result.TierCalls[2]);
    }

    // ── Tier 4 ────────────────────────────────────────────────────────────

    [Fact]
    public void Solve_TierFourDepthCapAtZero_BlocksBacktracking()
    {
        var puzzle = LoadFixture("poor_example1.txt");
        var result = Solver.Solve(puzzle, [int.MaxValue, int.MaxValue, int.MaxValue, 0]);

        Assert.Equal(SolveOutcome.RequiresHigherTier, result.Outcome);
        Assert.Equal(0, result.TierCalls[3]);
    }

    [Fact]
    public void Solve_DiggedTierThreePuzzle_BacktrackDepthIsZero()
    {
        var solution = LoadFixture("solution.txt");
        var puzzle = Digger.Dig(solution,
            new DiggerProfile { TargetEmptyCells = 100, TierCaps = DiggerProfile.TierThreeOnly.TierCaps },
            new Random(42)).Puzzle;
        var result = Solver.Solve(puzzle);

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(0, result.TierCalls[3]);
    }

    [Fact]
    public void Solve_WithBacktrackerEnabled_SolutionMatchesOriginal()
    {
        // Verifies the backtracker does not produce wrong answers when it fires.
        // Dig with a shallow depth cap so the digger can accept T4-requiring removals,
        // then solve and assert every cell matches the known solution.
        var solution = LoadFixture("solution.txt");
        var puzzle = Digger.Dig(solution,
            new DiggerProfile { TargetEmptyCells = 50, TierCaps = [int.MaxValue, int.MaxValue, int.MaxValue, 1] },
            new Random(42)).Puzzle;
        var result = Solver.Solve(puzzle);

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.Equal(solution[r, c], result.Solution![r, c]);
    }

    // Note: a "WithTierFour_MakesProgress" test (analogous to the Tier-2 version) requires a
    // fixture solvable by backtracking but not by Tier-1+2+3. That fixture will be added once
    // the first --dig run with --tier4cap produces a puzzle.

    // ── Solution validity ─────────────────────────────────────────────────

    [Fact]
    public void Solve_Solution_EachRowHasAllSymbols()
    {
        var result = Solver.Solve(LoadFixture("solution.txt"));
        var grid = result.Solution!;

        for (int r = 0; r < Grid.Size; r++)
        {
            var seen = new HashSet<char>();
            for (int c = 0; c < Grid.Size; c++)
                Assert.True(seen.Add(grid[r, c]), $"Duplicate in row {r}");
        }
    }

    [Fact]
    public void Solve_Solution_EachColHasAllSymbols()
    {
        var result = Solver.Solve(LoadFixture("solution.txt"));
        var grid = result.Solution!;

        for (int c = 0; c < Grid.Size; c++)
        {
            var seen = new HashSet<char>();
            for (int r = 0; r < Grid.Size; r++)
                Assert.True(seen.Add(grid[r, c]), $"Duplicate in col {c}");
        }
    }

    [Fact]
    public void Solve_Solution_EachBoxHasAllSymbols()
    {
        var result = Solver.Solve(LoadFixture("solution.txt"));
        var grid = result.Solution!;

        for (int boxR = 0; boxR < Grid.Size; boxR += Grid.BlockSize)
        for (int boxC = 0; boxC < Grid.Size; boxC += Grid.BlockSize)
        {
            var seen = new HashSet<char>();
            for (int r = boxR; r < boxR + Grid.BlockSize; r++)
            for (int c = boxC; c < boxC + Grid.BlockSize; c++)
                Assert.True(seen.Add(grid[r, c]), $"Duplicate in box ({boxR},{boxC})");
        }
    }
}
