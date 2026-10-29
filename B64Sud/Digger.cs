namespace B64Sud;

public static class Digger
{
    // Attempt to remove up to profile.TargetEmptyCells cells from solution,
    // keeping only removals where Solver still returns Solved under profile.TierCaps.
    // Pass a seeded Random for reproducibility; defaults to Random.Shared.
    public static DiggerResult Dig(Grid solution, DiggerProfile profile, Random? rng = null)
    {
        rng ??= Random.Shared;

        var puzzle = solution.Clone();
        var indices = BuildShuffledIndices(rng);

        int removed = 0;
        var tierCellCounts = new int[4];

        foreach (int idx in indices)
        {
            if (removed >= profile.TargetEmptyCells) break;

            int r = idx / Grid.Size;
            int c = idx % Grid.Size;
            char backup = puzzle[r, c];

            puzzle[r, c] = Grid.Empty;
            // progress animation commented away
            // string annunciate = $"r {r,2} c {c,2}  ";
            // string rewind = new String('\b', annunciate.Length);
            // Console.Error.Write(annunciate + rewind);

            var result = Solver.Solve(puzzle, profile.TierCaps);
            if (result.Outcome == SolveOutcome.Solved
                && NeedsUniquenessCheck(profile.TierCaps)
                && Solver.Solve(puzzle).Outcome != SolveOutcome.Solved)
            {
                puzzle[r, c] = backup;
                continue;
            }
            if (result.Outcome == SolveOutcome.Solved)
            {
                removed++;
                string annunciate = $"Removed: {removed}";
                string rewind = new String('\b', annunciate.Length);
                Console.Error.Write(annunciate + rewind);
                for (int i = 0; i < tierCellCounts.Length; i++)
                    if (result.TierCalls[i] > 0) tierCellCounts[i]++;
            }
            else
            {
                puzzle[r, c] = backup;
            }
        }

        return new DiggerResult(puzzle, removed, tierCellCounts);
    }

    // Uniqueness is self-evident for logic-only solves; only backtracking can produce non-unique
    // results because the capped solver silently ignores branches it couldn't fully explore.
    private static bool NeedsUniquenessCheck(int[]? tierCaps) =>
        tierCaps != null && tierCaps.Length > 3 && tierCaps[3] > 0;

    private static int[] BuildShuffledIndices(Random rng)
    {
        var indices = new int[Grid.Size * Grid.Size];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices;
    }
}
