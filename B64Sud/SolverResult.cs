namespace B64Sud;

public enum SolveOutcome { Solved, Invalid, RequiresHigherTier }

public sealed class SolverResult
{
    public SolveOutcome Outcome { get; }
    public Grid? Solution { get; }

    // Index 0 = Tier 1, 1 = Tier 2, 2 = Tier 3, 3 = Tier 4.
    // Each entry counts productive technique invocations (calls that resolved ≥1 cell).
    public IReadOnlyList<int> TierCalls { get; }

    internal SolverResult(SolveOutcome outcome, Grid? solution, int[] tierCalls)
    {
        Outcome = outcome;
        Solution = solution;
        TierCalls = tierCalls;
    }
}
