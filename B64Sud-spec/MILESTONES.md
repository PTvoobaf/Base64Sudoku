# Milestone Roadmap

## 0: Utility

- C# / .NET 10 console app project structure
- xUnit test project (driven by MTP via `dotnet test`)
- Base64 I/O and parse 71x71 ASCII into 64x64 matrix (4096-cells)
- Tests: round-trip parse/serialize of `solution.txt`; verify `poor_example1.txt` loads without error
- Baseline `Measure-Command` timing established

## 1: "Tier 1" digging and solving

- Bitmask initialization, UpdateConstraints()
- Technique: FindNakedSingles()
- Technique: FindHiddenSingles()
- Solver returns per-tier call counts alongside result
- Digger/Solver reliably produces a 64x64 puzzle solvable with just these two techniques
- Tests: known Tier-1-solvable puzzle states; verify Solver returns correct call counts
- `Measure-Command` timing recorded

## 1: Observations

- Eight dig trials (seeds vary; targets 2048–3840) all saturated at **1863–1897 empty cells (~46% of 4096)**, regardless of target. This appears to be the genuine Tier-1 structural ceiling for `solution.txt`.
- Multi-pass digging (same shuffle order, no reshuffle) always yields **0 additional removals on pass 2**. Monotonicity explains why: removing cells only increases puzzle difficulty; a cell rejected in pass 1 cannot become removable in pass 2 because the state has only become harder. The single-pass greedy already reaches the fixed point for a given seed.
- `Measure-Command` timing: dig ~2–3 min (scales with target up to saturation); solve ~2.5 s.

## 2: "Tier 2" digging and solving

- Technique: FindIntersections()
- Technique: FindNakedPairs()
- Digger enforces Target_Technique_Profile Tier-2 call count cap using Solver's returned counts
- Digger/Solver extended to produce puzzles solvable with Tier 1 and a bounded number of Tier-2 applications
- **Fix `DiggerProfile.TierOneOnly`**: change `TierCaps` from `null` to `[int.MaxValue, 0, 0, 0]` so the profile actually restricts Tier 2–4 now that those tiers exist in the Solver
- Tests: regression on all Milestone 1 tests; Tier-2 technique unit tests
- `Measure-Command` timing recorded

## 3: "Tier 3" digging and solving

- Techniques: FindXWings() and related tactics
- Digger enforces Tier-3 call count cap
- Digger/Solver extended to produce puzzles solvable with Tier 1 and bounded Tier-2 and Tier-3 counts
- Tests: regression suite; Tier-3 technique unit tests
- `Measure-Command` timing recorded

## 4: "Tier 4" digging and solving

- Human-like Backtracker: branches only when no logic technique fires; MRV cell selection; reports max search depth to Digger
- Digger uses depth as an additional cap in the Target_Technique_Profile (`tierCaps[3]`)
- **Two-pass uniqueness verification** required when backtracking is enabled: the capped solve (difficulty classification) and an uncapped solve (uniqueness check) must be separate passes. A single capped solve cannot reliably detect non-uniqueness because cap-hit branches are inconclusive and silently ignored — allowing a non-unique puzzle to be falsely accepted. Fix: `Digger.cs` runs a second uncapped `Solver.Solve()` after every accepted removal when `tierCaps[3] > 0`.
- Root cause in `Solver.cs`: `RequiresHigherTier` from a sub-branch has two incompatible meanings — "depth cap hit, inconclusive" in capped mode vs. "2+ solutions found, non-unique" in uncapped mode. Fixed by guarding on `tierCaps == null` to propagate non-uniqueness immediately in uncapped mode only.
- AIC deferred; backtracker alone proved sufficient for the puzzle generation depth explored.
- Tests: regression suite; verify depth cap rejects computationally expensive paths
- `Measure-Command` timing recorded

## 4: Observations

- Dig timing across depth caps 0–5 shows **sub-linear growth (~3–4× total from cap=0 to cap=5)**, not exponential. The two-pass uniqueness check dominates overhead between cap=0 and cap=1; each additional depth level adds roughly 5 minutes.
- Empty cell counts cluster at **1880–1902 regardless of depth cap**, suggesting a hard structural ceiling for `solution.txt` under the greedy single-pass algorithm. The remaining cells are genuinely non-uniquely removable at any backtrack depth.
- Solve times on dug puzzles are 1–3 seconds across all depth caps; the puzzle difficulty is well-bounded.

| Tier-4 depth cap | Empty cells dug (avg) | Dig time | n |
|-----------------|----------------------|----------|---|
| 0 (no backtrack) | ~1886               | 10–13 min | 2 |
| 1               | ~1887                | 15–17 min | 2 |
| 2               | ~1892                | 18–20 min | 4 |
| 3               | ~1873                | 19–29 min | 3 |
| 4               | ~1890                | ~34 min   | 1 |
| 5               | ~1897                | ~40 min   | 1 |
