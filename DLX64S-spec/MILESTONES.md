# Milestone Roadmap

## 0: Utility

- `dotnet new console -o DLX64S` and `dotnet new xunit -o DLX64S.Tests` project structure
- Copy `Symbols.cs` and `Grid.cs` (parser + serializer) from B64Sud; adapt as needed — no project reference to B64Sud
- xUnit test project (driven by MTP via `dotnet test`)
- Tests: round-trip parse/serialize of `solution.txt`; verify `poor_example1.txt` loads without error
- `Measure-Command` baseline: parse + serialize round-trip of `solution.txt` (establishes I/O overhead to subtract from later generation timings)

## 0: Observations

- Grid, GridParser, Symbols copied from B64Sud with namespace changed to `DLX64S`; no project reference to B64Sud
- Exhibit files (`solution.txt`, `poor_example1.txt`) copied to `DLX64S.Tests/exhibits/`; test path resolves 3 levels up from `bin/Debug/net10.0/` to the project directory
- All 5 tests pass (parse, round-trip serialize, poor-example load, empty grid, clone independence)
- `Measure-Command` baseline (`--no-build`): ~1.77 s total; xUnit reports ~28 ms for the 5 tests themselves; remainder is test-runner startup overhead

## 1: Node Infrastructure

- Define a flat node-pool struct: a single pre-allocated array of fixed-size node structs; nodes reference each other by `int` index (no heap-allocated objects, no GC pressure)
- Each node struct carries: `Left`, `Right`, `Up`, `Down` (int indices); `Column` (int index of column header); `RowId` (int, encodes candidate as `r*N*N + c*N + v`; `-1` for header nodes)
- Column header nodes extend the base layout with `Size` (count of uncovered ones in the column) and occupy a reserved prefix of the pool (indices `1` through `16384`); index `0` is the root sentinel
- Implement `Cover(col)` and `Uncover(col)` operating directly on pool indices; `Uncover` must be the exact reverse of `Cover` — no separate undo stack
- Tests: hand-crafted small matrix (e.g., 4 columns, 6 rows); verify cover/uncover restores the list to its original state; verify `Size` counts update correctly

## 1: Observations

- `Node` struct (7 int fields: Left, Right, Up, Down, Column, RowId, Size) in `Node.cs`; `NodePool` class in `NodePool.cs`
- Pool uses a flat `Node[]` with root sentinel at index 0; column headers at indices 1–N; data nodes beyond
- `ref Node this[int i]` indexer returns a managed reference into the array — no struct-copy overhead on access
- `AddRow` allocates each data node, appends it to the bottom of its column's vertical list, then splices it into the horizontal row list via circular-insert-right
- `Uncover` traverses UP and LEFT (exact reverse of `Cover`'s DOWN and RIGHT), restoring nodes and sizes with no undo stack
- 7 tests pass covering: header link order (forward and backward), column Size counts after AddRow, Cover removes column and decreases peer sizes, Uncover restores header list and sizes, nested Cover/Uncover pair restores full state

## 2: Algorithm X / Dancing Links

- Implement `AlgorithmX` (recursive search) on top of the node pool from Milestone 1
- Column selection: minimum-`Size` column (MCV heuristic); ties broken by lowest index
- Solution callback: invoked with the current partial-solution row list when all columns are covered; supports early exit after a specified solution count
- No Sudoku knowledge at this layer — the algorithm operates on the abstract node pool
- Tests (four cases, covering all major code paths):
  - **Knuth's 7×6** — 7-column, 6-row instance from the original DLX paper (2000); unique solution; hand-verifiable without Sudoku context
  - **3-rooks on 3×3** — universe {R₀,R₁,R₂,C₀,C₁,C₂}, 9 candidates each covering one Rᵢ and one Cⱼ; assert `--count 6` returns all 3! = 6 distinct valid solutions and `--count 7` still returns exactly 6 (exhaustion)
  - **8-rooks on 8×8** — 16-column, 64-candidate instance; with a fixed seed assert the single returned solution has exactly one rook per row and one per column; assert `--count 2` returns two distinct valid solutions
  - **Infeasibility** — universe {1,2,3}, rows [{1,2},{2,3}]; assert zero solutions returned (validates full backtrack-to-empty path)

## 2: Observations

- `AlgorithmX.Solve` implemented in 34 lines using two local functions (`Search`, `ChooseColumn`) closing over shared `found`, `partial`, pool, and callback — no per-recursion parameter threading required
- Early exit: `Search` returns `bool`; `true` propagates up the call stack immediately; pool is left in a modified state on early exit (acceptable — pool is single-use per solve call)
- MCV ties broken by lowest column index (iteration order from root's right link)
- `NodePool.AddRow` upgraded to `params ReadOnlySpan<int>` (C# 13 / .NET 10) — avoids heap allocation at each call site; existing tests unchanged
- 7 tests across 5 cases: Knuth 7×6 (unique solution), 3-rooks 3×3 (exhaustion at exactly 6), 8-rooks 8×8 (valid single solution), 64-rooks 64×64 (valid single and two distinct solutions — exercises 128-column header list at Sudoku scale), infeasibility (zero solutions)
- 64-rooks 64×64: NodePool holds 8,321 nodes (128 headers + 8,192 data); both tests complete in under 1 ms — MCV finds the main-diagonal solution with no backtracking
- **M3 timing result**: naive full-matrix DLX on 64×64 Sudoku (1,064,961-node pool, seed 42) ran for **~5 hours** without producing a solution. Pure random-shuffle DLX from an empty grid is not tractable at this scale. M3 requires a different approach — see Milestone 3 for the revised strategy.

## 3a: Generator — Full-Matrix DLX (BLOCKED)

- Build the 64×64 Sudoku constraint matrix (262,144 candidates × 16,384 constraints) into the node pool; see `ALGORITHM.md` for column index layout
- Randomized row iteration: before iterating candidates in a chosen column, shuffle the live row list using Fisher-Yates with a seeded PRNG; this is the sole source of output diversity
- CLI:
  - `--seed <N>` — 64-bit integer seed (default: nanosecond clock); always printed to stderr for reproducibility
  - `--count <N>` — number of solutions to emit (default: 1); search terminates after the Nth solution
  - `--out <file>` — write output to file instead of stdout
- Output: one complete 71×71 ASCII grid per solution (FORMAT.md layout), separated by blank lines for `--count > 1`
- Tests: generated grid round-trips through the parser; piped to `B64Sud --solve` returns `Solved` with `T4=0`
- `Measure-Command` timing: single-solution generation baseline; also time `--count 5` to observe per-solution marginal cost

## 3a: Observations

Naive full-matrix DLX (262,144 × 16,384, shuffled, seed 42) ran for ~5 hours without producing a solution.

Two pre-fill strategies were implemented and evaluated:

**Row-0 pre-fill:** shuffle 64 symbols into row 0, cover the 4×64 = 256 resulting constraint columns, leave DLX to fill the remaining 63 rows (depth 4032). Reached peak depth ~3845/4032 (95%) in ~19 minutes before stagnating. Promising improvement but not tractable.

**First-band pre-fill (rows 0–7):** construct a valid 8×64 Latin rectangle via a shuffled cyclic 8×8 Latin square, cover the 4×512 = 2048 constraint columns, leave DLX to fill rows 8–63 (depth 3584). Counterintuitively slower than row-0 pre-fill. Root cause: covering the full first band removes the size-7 box constraint columns that row-0 pre-fill left behind — those small columns were MCV's best guidance for the search. After full-band pre-fill, every remaining column has size 56 (uniform), so MCV has no high-quality anchor and backtracking explodes. Live telemetry (`--seed`, node count, current depth, peak depth) was added to `AlgorithmX.Solve` and `Generator` to diagnose this behaviour.

**20-seed survey:** after adding the live annunciator, 20 seeds with manual timeouts of 1–7 minutes all produced peaks in the range 3343–3394 (93–94% of 3584) with no solutions. Peak values clustered within a ~50-position band regardless of seed, confirming a structural bottleneck in the band construction rather than seed variance. Random restarts will not resolve it.

**Conclusion:** the full-matrix DLX approach is not tractable for 64×64 Sudoku generation at any seed. The minimum column size after any reasonable pre-fill is too large to give MCV effective guidance. A fundamentally different decomposition is required — see Milestone 3b.

## 3b: Generator — Symbol-by-Symbol Rook Cover

**Core insight:** a solved 64×64 Sudoku is equivalent to 64 *disjoint* box-constrained rook covers — one per symbol. Each cover places that symbol exactly once in every row, every column, and every 8×8 box. The 64 covers partition all 4,096 cells. Building them one symbol at a time replaces one intractable 262,144×16,384 problem with 64 tractable subproblems.

**Per-symbol exact cover:**
- Candidates: all cells not yet claimed by a previous symbol (4,096 at symbol 0; 4,032 at symbol 1; … 64 at symbol 63)
- Constraints: 64 row + 64 col + 64 box = **192 columns** (fixed count throughout)
- Each candidate covers exactly 3 constraints (its row, column, and box)
- Column sizes start at 64−k at iteration k and decrease monotonically — MCV quality *improves* as iterations proceed; the final iteration is trivially forced (all column sizes = 1)
- Pool size: ~12,500 nodes (vs 1,065,000 for full-matrix DLX) — fully cache-resident

**Algorithm:**
1. Maintain `claimed[0..v-1]` — the set of cells chosen for all placed symbols
2. For symbol `v`, build a fresh rook-cover pool (4,096 − 64v available candidates, 192 constraints), shuffled with the seeded PRNG
3. Call `AlgorithmX.Solve` for 1 solution; if found, record it, add its 64 cells to `claimed`, advance to `v+1`
4. If no solution (dead end): backtrack — remove symbol `v−1`'s claimed cells, rebuild its pool instructing DLX to skip the solution already tried, and retry
5. Repeat until all 64 symbols are placed

**Backtracking strategy (first implementation):** chronological — always retry the immediately preceding symbol. Track a per-symbol solution-skip counter; rebuild the pool and skip that many solutions before accepting the next one. Because each pool is tiny, rebuilding is cheap.

**New code:**
- `RookMatrix.cs` — builds the 192-column rook-cover pool for a given set of excluded cells and a seeded RNG; analogous to `SudokuMatrix.cs` but ~85× smaller
- `Generator.cs` — replace the single `AlgorithmX.Solve` call with the 64-iteration loop above; reuse existing `Decode` skeleton to assemble the final grid from 64 per-symbol solution arrays
- Telemetry: extend the existing annunciator to report current symbol index `v`, backtrack count, and nodes per symbol

**Tests:** same test plan as Milestone 3 (no-empty-cells, row/col/box validity, round-trip, different seeds differ); existing `GeneratorTests.cs` requires no structural changes

**Expected performance:** the 64-rooks 64×64 test (128-column, 4,096-candidate pool, no box constraint) ran in under 1 ms. The rook-cover pool adds 64 box constraint columns but is otherwise comparable. Even assuming 10 ms per iteration and 500 total backtracks, worst-case generation time is on the order of seconds.

## 3b: Observations (PRESUMED BLOCKED)

The symbol-by-symbol rook-cover plan above was not implemented. Two related approaches were tried instead and both reached the same structural dead end.

**Row-by-row DLX (`RowMatrix`):** each row solved as an independent 64-column × 64-symbol bipartite matching via DLX, with `colForbidden` and `boxForbidden` bitmasks tracking prior placements. Single-level backtracking (retry the current row with an advanced RNG state) and periodic full restarts (threshold 50 backtracks/pass, cap 200,000 total). After 14 s and 289 restarts the telemetry froze — DLX was stuck inside a single row's solve, exhaustively proving unsatisfiability of a row that earlier greedy choices had made infeasible. The underlying problem: committing to full-row placements without cell-level lookahead creates dead ends that single-level backtracking cannot escape.

**Full-matrix DLX with band pre-fill (`SudokuMatrix`):** band 0 (rows 0–7, 512 cells) constructed via a shuffled cyclic Latin square and pre-covered; band 1 (rows 8–15, 512 cells) constructed with a derangement-shifted offset permutation (guaranteeing no column conflicts with band 0) and also pre-covered. AlgorithmX with MCV searched the remaining cells. Extended telemetry runs:

| Pre-fill | Cells for DLX | Depth floor | Remaining at floor |
|---|---|---|---|
| Band 0 only | 3,584 | 3,259 | 325 |
| Bands 0 + 1 | 3,072 | 2,741 | 331 |

The remaining-cell floor is structurally invariant at ~325–331 regardless of pre-fill depth. Pre-filling band 1 relabeled the depths but did not move the floor: the last ~325 cells (concentrated in the maximally-constrained last band) form a subsystem that DLX exhausts without completing. This is intrinsic to the 64×64 constraint density, not seed-specific. Abort-and-retry would encounter the same wall; adding further pre-filled bands would shift the labels again without changing the cell-count at the floor.

**Conclusion:** all DLX-based generation approaches — full-matrix, pre-filled, and row-by-row — are blocked by the same root cause: exponential search in the final band's constraint cluster. A non-backtracking algorithm is required.

---

## 3c: Generator — Simulated Annealing

**Core insight:** rather than building the grid top-down with backtracking, start from a grid that already satisfies one constraint class (rows) and iteratively reduce violations in the other two (columns, boxes) via random perturbation with probabilistic acceptance. Simulated annealing (SA) navigates non-convex cost landscapes by accepting uphill moves with probability `exp(−ΔCost/T)`, allowing escape from the local minima that trap pure hill-climbing and DLX alike.

**State**

A 64×64 int array `grid[r, c]` ∈ [0, 63].  Each row is initialised as a Fisher-Yates shuffle of 0–63 and remains a permutation for the entire run — the row constraint is satisfied by construction and never touched.

**Cost function**

Column cost: for each of the 64 columns, count how many symbols appear more than once; sum the excess counts.  Box cost: same for each of the 64 boxes.  Total cost = 0 if and only if the grid is a valid Sudoku.

Maintain auxiliary arrays `colCount[64, 64]` (column × symbol → occupancy) and `boxCount[64, 64]` (box × symbol → occupancy) so the cost of any proposed swap can be evaluated in O(1) from the four affected column/box entries.

**Move operator**

Pick a uniformly random row r, then two distinct uniformly random column positions c1 and c2 in that row.  Swap `grid[r, c1]` and `grid[r, c2]`.  This is the sole move type; row uniqueness is preserved by construction.  Compute ΔCost from the change in occupancy at columns c1, c2 and boxes `(r/8, c1/8)`, `(r/8, c2/8)`.

**Acceptance criterion**

- ΔCost ≤ 0: always accept (includes neutral moves).
- ΔCost > 0: accept with probability `exp(−ΔCost / T)`.

**Cooling schedule**

Start temperature `T₀ ≈ 2.0` (calibrated so a +1-cost swap is accepted ≈ 60% of the time initially). Multiply T by factor α ≈ 0.99999 after every step. One *epoch* = N² = 4,096 steps.

**Reheat**

If cost has not improved after R epochs (e.g. R = 500), multiply T by a reheat factor (e.g. 5×) to escape the local basin, then resume cooling from the reheated temperature.

**Termination**

Success: cost = 0 — emit the grid.  Failure cap: if total steps exceed a configurable limit without reaching cost 0, report failure and let the caller retry with a fresh random initialisation.

**New code**

- `Annealer.cs` — encapsulates state (`grid`, `colCount`, `boxCount`, current cost, T), `Step()` for a single SA move, `Run()` for the full loop.  No Sudoku-specific I/O — returns a `Grid?`.
- `Generator.cs` — replace the `SudokuMatrix` / `AlgorithmX` path with `new Annealer(rng).Run()`; keep the existing `--seed`, `--count`, `--out` CLI surface unchanged.

**Tests**

- Emitted grid: no duplicate in any row, column, or box (the same property assertions used in M3a's planned test).
- Round-trip: `GridParser.Serialize → GridParser.Parse` preserves every cell.
- Validity: grid piped to `B64Sud --solve` returns `Solved` with `T4 = 0` (no backtracking needed on a fully solved grid).
- Diversity: grids from seeds 1 and 2 differ in at least one cell.
- Timing target: single grid generation completes in under 60 s on the development machine; record actual median and p95 over 10 seeds.

**Expected performance**

SA on 9×9 Sudoku converges in microseconds; for 64×64 the cost landscape is larger but still well-connected (any two valid grids are reachable via row-swaps). Literature on SA applied to Latin square completion suggests convergence in seconds to low minutes at this scale with geometric cooling. If convergence proves slow, candidate tuning levers are: T₀, α, and the reheat trigger threshold.

## 3c: Observations

`Annealer.cs` implemented with T₀ = 2.0, α = 0.9999995, MaxSteps = 50,000,000.  `Generator.cs` replaced entirely — it now constructs an `Annealer` and calls `Run()`, retrying with a fresh random initialisation on the rare case of non-convergence.

First empirical result (seed 42, debug build):

```
step 0.0M  cost 2919   ← initial random state (~predicted value of ~3000)
step 5.1M  cost  161   ← most violations resolved in ~0.5 s
step 12.5M cost    6   ← final approach
Done: 1 grid(s), 1 attempt(s), 00:01 elapsed.
```

Output validated: `B64Sud --solve` returned `Solved. Tier calls: T1=0 T2=0 T3=0 T4=0`.

The local-minimum concern did not materialise for seed 42: the algorithm descended from 2,919 to 0 in a single attempt with no restarts.  Temperature at the last progress tick was T ≈ 0.00383, confirming the search was effectively in greedy-descent mode for the final few thousand violations.  `Program.cs` updated to accept `0x`-prefixed hex seeds via `unchecked((long)Convert.ToUInt64(..., 16))`.

---

## 4: Polish

- Placeholder — scope defined after Milestone 3 reveals kinks
- Candidates: performance tuning if single-solution generation exceeds acceptable time; output diversity evaluation; CLI edge-case hardening; search instrumentation (nodes visited, backtracks) for diagnostic output

## 4: Observations — M4 Timing Baseline (10 seeds, Release build)

`DLX64S.Tests/m4-timing.ps1` — builds Release binaries, generates one grid per seed, validates each with `B64Sud --solve`, prints per-seed elapsed time and attempt count.

| Seed | Label | Sec | Attempts | Valid |
|---|---|---|---|---|
| 42 | the answer | 1.4 | 1 | OK |
| 1337 | leet | 1.6 | 1 | OK |
| 0xc0ffee | coffee | 1.7 | 1 | OK |
| 0xdeadbeef8badf00d | dead beef / bad food | 1.8 | 1 | OK |
| 0xbadc0ffee0ddf00d | bad coffee / odd food | 1.6 | 1 | OK |
| 0xfeedface | Mach-O magic | 1.7 | 1 | OK |
| 314159265358979323 | pi digits | 1.7 | 1 | OK |
| 2718281828459045235 | e digits | 1.8 | 1 | OK |
| 1000000007 | beloved CP prime | 1.5 | 1 | OK |
| 0 | zero | 1.7 | 1 | OK |

**Summary:** Median **1.7 s** | p95 **1.8 s** | Max **1.8 s** | Failures **0/10**

All 10 seeds converged in a single attempt.  The timing target of < 60 s was met with 33× headroom.  No local-minimum stalls observed across any seed, including edge cases (zero, full uint64 values).  Generation time variance is tight (±0.2 s), reflecting deterministic cooling schedule rather than search luck.

## 4: Observations — SA-Artifacts Dig (10 seeds × 3 tiers)

Solutions saved to `DLX64S.SA-Artifacts/` as `<label>-sol.txt`.  Each dug with B64Sud `--dig 4095` at three tier ceilings (tier-4 always capped at 0, so no two-pass uniqueness check):

| Label | T1 empty | T2 empty | T3 empty |
|---|---|---|---|
| the-answer | 1874 | 1883 | 1887 |
| leet | 1894 | 1888 | 1894 |
| coffee | 1866 | 1885 | 1884 |
| deadbeef | 1878 | 1886 | 1892 |
| badcoffee | 1888 | **1922** | 1889 |
| feedface | 1875 | 1883 | 1890 |
| pi | 1867 | 1887 | 1897 |
| e | 1896 | 1887 | 1895 |
| cp-prime | 1869 | 1897 | 1877 |
| zero | 1885 | 1897 | 1893 |
| **Max** | **1896** | **1922** | **1897** |

**Key finding:** `badcoffee` T2 reached **1922 empty cells**, verified uniquely solvable by `B64Sud --solve` (T1=431, T2=90, T3=0, T4=0).  This breaks the 1902-cell ceiling observed across all B64Sud trials on `solution.txt`, confirming that ceiling was grid-specific rather than a fundamental limit of the 64×64 constraint system.

The greedy single-pass digger is order-dependent (random cell selection per run), so T2 counts are not monotone with T1 counts — e.g. `leet` T2 (1888) < T1 (1894).  Dig times: T1 ≈ 50 s, T2 ≈ 150 s, T3 ≈ 220 s per grid.

## 4: Observations — SA-Artifacts T4 Dig (10 seeds × cap 1/2/4)

Puzzles saved as `<label>-t4-N.txt` in `DLX64S.SA-Artifacts/`.  Parameters: `--tier2cap 4002 --tier3cap 4001 --tier4cap N` (tier-2/3 de facto uncapped; tier-4 depth limited to N).  Tier-4cap > 0 triggers the two-pass uniqueness check, which dominates dig time.  Total wall-clock: ~4 hours (16:40–20:40).

| Label | cap=1 (s) | cap=1 empty | cap=2 (s) | cap=2 empty | cap=4 (s) | cap=4 empty |
|---|---|---|---|---|---|---|
| the-answer | 368 | 1889 | 332 | 1892 | 791 | 1901 |
| leet | 325 | 1895 | 490 | 1896 | 805 | 1889 |
| coffee | 511 | 1888 | 513 | 1885 | 1021 | 1857 |
| deadbeef | 378 | 1895 | 465 | 1898 | 647 | 1883 |
| **badcoffee** | 351 | **1907** | 336 | 1895 | 544 | 1879 |
| feedface | 287 | 1898 | 355 | 1882 | 599 | 1882 |
| pi | 316 | 1892 | 460 | 1882 | 644 | 1888 |
| **e** | 241 | **1907** | 352 | 1886 | 628 | **1903** |
| cp-prime | 308 | 1882 | 338 | 1886 | 680 | 1878 |
| zero | 258 | 1897 | 325 | 1878 | 708 | 1895 |
| **Max** | — | **1907** | — | **1898** | — | **1903** |

**Median dig times:** cap=1 ≈ 327 s (5.5 min) | cap=2 ≈ 393 s (6.6 min) | cap=4 ≈ 708 s (11.8 min).  The two-pass uniqueness check roughly doubles cap=1 time vs T3 (220 s); each additional depth level adds ~1.5–4 min depending on the grid.

**Ceiling findings:** The previous B64Sud ceiling of ~1902 (on `solution.txt`) is breached by multiple SA-generated grids.  `badcoffee` and `e` both reached **1907** at cap=1; `e` reached **1903** at cap=4; `the-answer` reached **1901** at cap=4.  The ceiling is not a property of the 64×64 constraint system — it is grid-specific.  Empty-cell counts are not monotone with cap depth due to random cell-selection order in the greedy single-pass digger.
