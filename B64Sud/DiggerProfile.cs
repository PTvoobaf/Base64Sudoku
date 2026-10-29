namespace B64Sud;

public sealed class DiggerProfile
{
    // How many cells to attempt to remove from the solved grid.
    public int TargetEmptyCells { get; init; } = 2048;

    // Per-tier caps forwarded to Solver on each candidate removal.
    // null = no caps (rely on which tiers are implemented to limit difficulty).
    public int[]? TierCaps { get; init; } = null;

    // Tier 1 only: caps Tier 2–4 at zero so the Solver never escalates past naked/hidden singles.
    public static readonly DiggerProfile TierOneOnly = new()
    {
        TargetEmptyCells = 2048,
        TierCaps = [int.MaxValue, 0, 0, 0]
    };

    // Tier 1–2 only: caps Tier 3–4 at zero.
    public static readonly DiggerProfile TierTwoOnly = new()
    {
        TargetEmptyCells = 2048,
        TierCaps = [int.MaxValue, int.MaxValue, 0, 0]
    };

    // Tier 1–3 only: caps Tier 4 (backtrack depth) at zero.
    public static readonly DiggerProfile TierThreeOnly = new()
    {
        TargetEmptyCells = 2048,
        TierCaps = [int.MaxValue, int.MaxValue, int.MaxValue, 0]
    };
}
