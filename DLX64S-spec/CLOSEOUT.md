# DLX64S Project Closeout

## What Was Built

DLX64S is a CLI tool that generates complete, valid 64×64 Base64 Sudoku grids in the B64Sud plaintext format.  Its output is a drop-in input for the B64Sud Digger, breaking B64Sud's dependence on a single canonical `solution.txt`.

The name is a historical artifact: the project was originally scoped around Knuth's **Dancing Links** (DLX) implementation of Algorithm X.  DLX proved intractable for 64×64 generation (see Milestones 3a/3b), and the final generator uses **Simulated Annealing**.  The DLX infrastructure (Milestones 0–2) was built, fully tested, and remains correct — it just isn't called at runtime.

One console program:

- **Generator** (`--seed`, `--count`, `--out`) — emits one or more fully-solved grids; seed is printed to stderr for reproducibility.

## Milestone Summary

| Milestone | Status | What was built |
|---|---|---|
| 0: Scaffolding | Done | Console + xUnit project; Grid/GridParser/Symbols copied from B64Sud; 5 tests; exhibit files |
| 1: Node pool | Done | Flat `Node[]` pool, Cover/Uncover; 7 tests |
| 2: Algorithm X | Done | `AlgorithmX.Solve` with MCV, early-exit, onProgress telemetry; 8 tests including 64-rooks |
| 3a: Full-matrix DLX | Blocked | 262,144 × 16,384 matrix; ran ~5 h without solution |
| 3b: Band pre-fill / row-by-row DLX | Blocked | Depth floor invariant at ~325 remaining cells regardless of pre-fill depth |
| 3c: Simulated Annealing | Done | `Annealer.cs`; converges in 1.4–1.8 s; 33× better than 60 s target |
| 4: Polish / artifacts | Done | M4 timing baseline (`m4-timing.ps1`); `DLX64S.SA-Artifacts/` with 70 validated files |

## Why DLX Failed

All DLX-based approaches hit the same root cause: exponential backtracking in the final band of the 64×64 constraint matrix.

- **Full-matrix DLX (3a):** MCV heuristic had no useful guidance — all remaining column sizes were large and uniform.
- **Row-by-row DLX (3b early):** committed full rows greedily; DLX proved a row infeasible after 289 restarts and froze.
- **Band pre-fill (3b late):** pre-filling 1 or 2 bands shifted the depth numbers but the remaining-cell floor stayed at ~325, confirming a structural bottleneck intrinsic to the 64×64 constraint density.

## How SA Works (and Why It Succeeded)

**State:** a 64×64 int array where every row is a Fisher-Yates shuffle of 0–63.  Row uniqueness is guaranteed by construction and never violated by a move.

**Move:** pick a random row and two distinct column positions; swap the two symbols.  O(1) delta cost via auxiliary `colCount[col, symbol]` and `boxCount[box, symbol]` arrays.

**Cost:** `Σ max(0, count−1)` over all column and box occupancies.  Zero iff the grid is a valid Sudoku.  `Inc`/`Dec` helpers evaluate the delta from a single swap in four to eight array reads.

**Acceptance:** `ΔCost ≤ 0` always accepted; `ΔCost > 0` accepted with probability `exp(−ΔCost / T)`.

**Cooling:** `T₀ = 2.0`, `α = 0.9999995`, `MaxSteps = 50,000,000`.  At α over 50 M steps, T effectively reaches zero — the search ends in deterministic greedy descent.  No reheat was needed in practice: none of the 10 tested seeds stalled.

**Box-skip optimization:** when a swap keeps both cells in the same box-column, box occupancies are unchanged; the box delta is skipped entirely.

## Key Findings

### Generation timing (Release build, 10 seeds)

Median **1.7 s** | p95 **1.8 s** | Max **1.8 s** | Failures **0/10**.  All seeds converged in a single SA attempt.

### The 1902-cell ceiling is not fundamental

B64Sud's prior trials on `solution.txt` found a structural ceiling of ~1902 removable cells regardless of solver depth.  SA-generated grids break this:

| Grid | T2 empty | T4-cap=1 empty | T4-cap=4 empty |
|---|---|---|---|
| badcoffee (0xbadc0ffee0ddf00d) | **1922** | **1907** | 1879 |
| e (2718281828459045235) | 1887 | **1907** | **1903** |
| the-answer (42) | 1883 | 1889 | **1901** |

The ceiling is a property of the specific grid's constraint structure, not of the 64×64 system.  Diverse generator output directly expands the achievable puzzle space.

### Digger tier classification is a conservative upper bound

When a puzzle is dug at tier-4 depth cap N and then solved fresh by the B64Sud solver, the solver's actual T4 call count is sometimes lower than N — in one case (`cp-prime-t4-1.txt`) zero, despite being dug with cap=1.

**Why:** the digger classifies each cell removal by running the capped solver on the *current partially-dug grid*, which still has many clues and a different MRV ordering than the final sparse puzzle.  The batch solver starts from the final puzzle and picks cells in global MRV order, sometimes finding chains of deductions that were not visible during the incremental dig.  The digger's label is a valid *solvability guarantee* (the puzzle can always be solved within the cap), not a tight difficulty label.

### Dig timing (Release build, no-uniqueness-check tier ≤ 3)

| Tier ceiling | Approx time per grid |
|---|---|
| T1 (tier2cap=0) | ~50 s |
| T2 (tier3cap=0) | ~150 s |
| T3 (tier4cap=0) | ~220 s |
| T4-cap=1 | ~330 s |
| T4-cap=2 | ~400 s |
| T4-cap=4 | ~700 s |

Tier-4 digs are more expensive because each committed cell removal requires a second uncapped solve for uniqueness verification.  This cost is absent for tier ≤ 3 (uniqueness is only required when `tierCaps[3] > 0`).

## Reference Artifacts

| Path | Contents |
|---|---|
| `DLX64S-spec/` | Specification documents (OVERVIEW, FORMAT, ALGORITHM, GENERATOR, MILESTONES, CLOSEOUT) |
| `DLX64S.Tests/m4-timing.ps1` | 10-seed timing baseline script; builds Release, generates, validates |
| `DLX64S.SA-Artifacts/<label>-sol.txt` | 10 fully-solved grids (one per nerdy seed) |
| `DLX64S.SA-Artifacts/<label>-t1.txt` | Tier-1-only puzzles (~1866–1898 empty cells) |
| `DLX64S.SA-Artifacts/<label>-t2.txt` | Tier-1+2 puzzles (~1878–1922 empty cells) |
| `DLX64S.SA-Artifacts/<label>-t3.txt` | Tier-1+2+3 puzzles (~1877–1897 empty cells) |
| `DLX64S.SA-Artifacts/<label>-t4-N.txt` | Tier-1+2+3+4 puzzles, depth cap N ∈ {1, 2, 4} |

All 70 artifact files pass `B64Sud --solve` with `Solved` and tier calls consistent with their difficulty labels.

## Future Directions

**Smaller puzzle sizes.**  The SA approach is not specific to 64×64.  Parameterising N would enable 9×9 (standard Sudoku), 16×16, and other sizes — useful for validating the solver and digger at scales where hand-verification is practical.

**Other generator algorithms.**  Alternatives include random Latin-square construction (build a random Latin square and add box constraints by column-permutation), genetic algorithms, or a hybrid SA + DLX where SA gets within a few violations and DLX finishes.  The DLX infrastructure in Milestones 0–2 is available for reuse.

**Atypical Base64 alphabets.**  RFC 4648 defines URL-safe Base64 (`-_` instead of `+/`) and several other variants.  The `Symbols.cs` alphabet mapping is the only change required; the solver and generator are alphabet-agnostic.

**Ports to other languages.**  The SA generator is a self-contained algorithm (no external dependencies, O(1) inner loop) — a natural candidate for ports to Python, Rust, TypeScript, or Go.  The B64Sud FORMAT.md and this generator's approach together constitute a complete spec.

**PEM-style puzzle format.**  A novelty converter that wraps grids in RFC 1421-style encapsulation would be amusing and vaguely self-describing:

```
-----BEGIN BASE64 SUDOKU PUZZLE-----
Difficulty: T3
Seed: 0xbadc0ffee0ddf00d
Empty-Cells: 1884

RQAL.... .H.Grh.. ...
...
-----END BASE64 SUDOKU PUZZLE-----
```

The irony is deliberate: the encapsulation format is named after Base64 (PEM content is Base64-encoded), but here the *content itself* uses Base64 characters as cell values and is transmitted as plain ASCII — no encoding needed.  Metadata headers (difficulty tier, seed, empty-cell count) would make puzzle files self-describing without requiring a sidecar file.
