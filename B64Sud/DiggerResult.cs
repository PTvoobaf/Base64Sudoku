namespace B64Sud;

public sealed class DiggerResult
{
    public Grid Puzzle { get; init; }
    public int RemovedCells { get; init; }

    // TierCellCounts[i] = number of committed removals that required ≥1 tier-(i+1) call.
    public IReadOnlyList<int> TierCellCounts { get; init; }

    internal DiggerResult(Grid puzzle, int removedCells, int[] tierCellCounts)
    {
        Puzzle = puzzle;
        RemovedCells = removedCells;
        TierCellCounts = tierCellCounts;
    }
}
