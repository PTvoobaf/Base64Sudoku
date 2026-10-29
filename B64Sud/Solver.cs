namespace B64Sud;

public static class Solver
{
    // tierCaps[i] caps work for tier (i+1).
    // tierCaps[3] is the maximum backtracking depth (0 = no backtracking allowed).
    // calls[3] reports the maximum backtrack depth actually reached (0 = no backtracking used).
    public static SolverResult Solve(Grid puzzle, int[]? tierCaps = null)
    {
        var state = CandidateState.Initialize(puzzle);
        var calls = new int[4];

        if (!state.PropagateGivens())
            return new SolverResult(SolveOutcome.Invalid, null, calls);

        return SolveState(state, tierCaps, calls, depth: 0);
    }

    private static SolverResult SolveState(CandidateState state, int[]? tierCaps, int[] calls, int depth)
    {
        while (true)
        {
            if (!state.IsValid())
                return new SolverResult(SolveOutcome.Invalid, null, calls);

            if (state.IsComplete())
                return new SolverResult(SolveOutcome.Solved, state.ToGrid(), calls);

            if (tierCaps != null && calls[0] >= tierCaps[0])
                return new SolverResult(SolveOutcome.RequiresHigherTier, null, calls);

            int ns = state.FindNakedSingles();
            if (ns > 0) { calls[0]++; continue; }

            int hs = state.FindHiddenSingles();
            if (hs > 0) { calls[0]++; continue; }

            if (tierCaps != null && tierCaps.Length > 1 && calls[1] >= tierCaps[1])
                return new SolverResult(SolveOutcome.RequiresHigherTier, null, calls);

            int inter = state.FindIntersections();
            if (inter > 0) { calls[1]++; continue; }

            int np = state.FindNakedPairs();
            if (np > 0) { calls[1]++; continue; }

            if (tierCaps != null && tierCaps.Length > 2 && calls[2] >= tierCaps[2])
                return new SolverResult(SolveOutcome.RequiresHigherTier, null, calls);

            int xw = state.FindXWings();
            if (xw > 0) { calls[2]++; continue; }

            int sf = state.FindSwordfish();
            if (sf > 0) { calls[2]++; continue; }

            int xyw = state.FindXYWings();
            if (xyw > 0) { calls[2]++; continue; }

            // Tier 4: depth-limited human-like backtracker.
            // Branches only when all logic tiers are exhausted; recurses with depth+1.
            if (tierCaps != null && tierCaps.Length > 3 && depth >= tierCaps[3])
                return new SolverResult(SolveOutcome.RequiresHigherTier, null, calls);

            if (depth + 1 > calls[3])
                calls[3] = depth + 1;

            int cellIdx = state.FindMRVCell();
            if (cellIdx < 0)
                return new SolverResult(SolveOutcome.Invalid, null, calls);

            int cr = cellIdx / Grid.Size, cc = cellIdx % Grid.Size;
            ulong cands = state.GetCandidates(cellIdx);

            SolverResult? found = null;
            while (cands != 0)
            {
                ulong bit = cands & (ulong)(-(long)cands); // lowest set bit
                cands &= ~bit;

                var branch = state.Clone();
                branch.AssumeCell(cr, cc, bit);

                var sub = SolveState(branch, tierCaps, calls, depth + 1);
                if (sub.Outcome == SolveOutcome.Solved)
                {
                    if (found != null)
                        // Puzzle is non-unique: second solution found. Preserve the first so callers
                        // can still emit a valid (if ambiguous) completion.
                        return new SolverResult(SolveOutcome.RequiresHigherTier, found.Solution, calls);
                    found = sub;
                }
                else if (sub.Outcome == SolveOutcome.RequiresHigherTier && tierCaps == null)
                {
                    // Uncapped mode only: RequiresHigherTier from a sub-branch means that branch
                    // itself contains 2+ solutions, so the overall puzzle is already non-unique.
                    // In capped mode we intentionally ignore this (cap-hit branches are inconclusive;
                    // we continue looking for any one solution for difficulty classification).
                    return new SolverResult(SolveOutcome.RequiresHigherTier,
                        found?.Solution ?? sub.Solution, calls);
                }
            }

            return found ?? new SolverResult(SolveOutcome.RequiresHigherTier, null, calls);
        }
    }
}
