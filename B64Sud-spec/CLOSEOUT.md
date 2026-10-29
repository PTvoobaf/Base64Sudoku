# B64Sud Project Closeout

## What Was Built

B64Sud is a complete 64×64 Sudoku solver and puzzle digger implemented in C# / .NET 10. It uses a 64-bit bitmask per cell (`uint64`) — an exact structural fit for 64 Base64 symbols — and a four-tier solving pipeline that progresses from trivial pencil-mark techniques to depth-limited backtracking.

Two console programs:

- **Solver** (`--solve`) — takes a puzzle file with empty cells (`.`), returns the solved grid or a failure reason, always with per-tier technique call counts on stderr.
- **Digger** (`--dig`) — takes a fully-solved grid and a difficulty profile (cell target + per-tier caps), removes cells greedily while enforcing uniqueness and technique-count bounds, emits the resulting puzzle.

## Accomplishments by Milestone

### Milestone 0: Scaffolding
- C# / .NET 10 console app and xUnit test project (MTP runner)
- Base64 I/O: parser and serializer for the 71×71 ASCII grid format
- Round-trip and negative-fixture tests established

### Milestone 1: Tier-1 Solver + Digger
- `uint64` bitmask init and constraint propagation (`UpdateConstraints`)
- Techniques: Naked Singles, Hidden Singles
- Single-pass greedy Digger with Tier-1 difficulty enforcement
- Observation: **structural ceiling at ~1863–1897 empty cells (~46% of 4096)** for `solution.txt`; single-pass greedy always reaches the fixed point for a given shuffle seed

### Milestone 2: Tier-2 Techniques
- Techniques: Intersections (pointing/claiming), Naked Pairs
- `DiggerProfile.TierOneOnly` bug fix: `null` caps corrected to `[int.MaxValue, 0, 0, 0]` so lower tiers are properly restricted once higher-tier techniques exist

### Milestone 3: Tier-3 Techniques
- Techniques: X-Wings, Swordfish, XY-Wings
- Tier-3-capped digging validated

### Milestone 4: Tier-4 Backtracker
- Depth-limited backtracker: branches only when all logic tiers are exhausted; MRV (minimum remaining values) cell selection; lowest-candidate-bit branch ordering
- `calls[3]` reports max depth reached; `tierCaps[3]` caps max allowed depth
- AIC deferred — backtracker alone proved sufficient for the puzzle generation depth explored

## Key Technical Finding: Two-Pass Uniqueness Verification

When Tier-4 backtracking is enabled, uniqueness verification requires a separate uncapped solve. A single capped solve is not sufficient.

**Why:** The capped solver silently ignores sub-branches that hit the depth cap (they are inconclusive, not failures). If a branch that would expose a second solution gets capped, the solver falsely reports the puzzle as unique. A puzzle that passes the capped check can still have two valid solutions.

**Fix:** `Digger.cs` runs a second, uncapped `Solver.Solve(puzzle)` after every accepted cell removal when `tierCaps[3] > 0`. Only if the uncapped pass returns `Solved` (exactly one solution) is the removal committed.

**Root cause in `Solver.cs`:** `RequiresHigherTier` from a sub-branch means two different things depending on context:
- **Capped mode:** "depth cap hit — branch inconclusive; keep looking for any one solution"
- **Uncapped mode:** "this branch itself contains 2+ solutions — puzzle is already non-unique"

The fix guards on `tierCaps == null` to propagate non-uniqueness immediately in uncapped mode, while leaving capped behavior (ignore inconclusive branches) unchanged.

## Timing Summary (Digger on `solution.txt`)

| Tier-4 depth cap | Empty cells dug | Dig time | n |
|-----------------|-----------------|----------|---|
| 0 (no backtracking) | ~1886       | 10–13 min | 2 |
| 1               | ~1887           | 15–17 min | 2 |
| 2               | ~1892           | 18–20 min | 4 |
| 3               | ~1873           | 19–29 min | 3 |
| 4               | ~1890           | ~34 min   | 1 |
| 5               | ~1897           | ~40 min   | 1 |

Growth from cap=0 to cap=5 is approximately 3–4× — sub-linear in the depth cap. The two-pass uniqueness check dominates the overhead gap between cap=0 and cap=1; each additional depth level costs roughly 5 minutes. Solve times on dug puzzles are 1–3 seconds regardless of cap.

Cell counts cluster at **1880–1902 across all depth caps**, suggesting a hard structural ceiling for this grid and algorithm. The remaining cells are genuinely non-uniquely removable: a second valid solution simply exists, and no depth of backtracking can change that.

## Reference Artifacts

| Path | Purpose |
|------|---------|
| `solution.txt` | Canonical fully-solved grid; Digger input and test fixture |
| `poor_example1.txt` | Negative fixture: puzzle requiring effectively infinite solver time |
| `B64Sud.Trial-Artifacts/` | Trial puzzles and solution copies from development experiments |
| `B64Sud-spec/` | Specification documents (FORMAT.md, SOLVER.md, DIGGER.md, MILESTONES.md) |

## Not Implemented

- **AIC (Alternating Inference Chains)** — specified in Milestone 4, deferred. The backtracker alone proved sufficient.
- **Multiple starting grids** — only one canonical `solution.txt` was used as Digger input. Generating diverse complete grids (e.g., via Dancing Links) was identified as a future direction.
- **Parallel digger** — the greedy single-pass loop is sequential by design. Parallelism across shuffle seeds was not explored.
